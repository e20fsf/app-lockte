using Lockte.SharedKernel.Persistence;
using Lockte.Web.Accounts;
using Lockte.Web.Audit;
using Microsoft.EntityFrameworkCore;

namespace Lockte.Web.Tests;

/// <summary>ประวัติการแก้ไข (#43): change something through a service, then find it on the history search.</summary>
[Collection(LocalDbCollection.Name)]
public sealed class HistoryTests(LocalDbFixture db)
{
    private async Task<IReadOnlyList<HistoryRow>> SearchAsync(HistoryFilter filter)
    {
        await using var scope = db.Scope();
        return await scope.Get<HistoryService>().SearchAsync(filter);
    }

    private static string NewCode() => "W" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task Adding_a_user_records_each_value_with_who_did_it()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
            await admin.Get<UserAdminService>().CreateAsync(userName, "สมชาย ใจดี", "temp1234");

        var rows = await SearchAsync(new(Table: "ผู้ใช้", RowKey: userName));

        Assert.All(rows, r => Assert.Equal(("เพิ่ม", AppUser.AdminUserName, HistoryKind.Change), (r.Action, r.UserName, r.Kind)));
        var displayName = Assert.Single(rows, r => r.Column == "ชื่อที่แสดง");
        Assert.Null(displayName.OldValue);
        Assert.Equal("สมชาย ใจดี", displayName.NewValue);
        Assert.Equal("ใช่", Assert.Single(rows, r => r.Column == "ต้องเปลี่ยนรหัส").NewValue);
        Assert.DoesNotContain(rows, r => r.Column is "CreatedBy" or "RowVersion" or "SecurityStamp" or "NormalizedUserName");
    }

    [Fact]
    public async Task Deactivating_a_user_records_old_and_new_value()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
        {
            var service = admin.Get<UserAdminService>();
            await service.CreateAsync(userName, "สมชาย", "temp1234");
            var user = await Users.FindAsync(service, userName);
            await service.SetActiveAsync(user.Id, false, user.RowVersion);
        }

        var edits = (await SearchAsync(new(RowKey: userName))).Where(r => r.Action == "แก้").ToList();

        var change = Assert.Single(edits);
        Assert.Equal(("ผู้ใช้", "ใช้งาน", "ใช่", "ไม่ใช่", AppUser.AdminUserName),
            (change.Table, change.Column, change.OldValue, change.NewValue, change.UserName));
    }

    [Fact]
    public async Task Password_changes_are_recorded_without_the_hash()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
            await admin.Get<UserAdminService>().CreateAsync(userName, "สมชาย", "temp1234");
        await using (var self = db.Scope(userName))
        {
            var accounts = self.Get<AccountService>();
            var login = await accounts.CheckLoginAsync(userName, "temp1234");
            await accounts.ChangePasswordAsync(login.UserId!, "temp1234", "my own pass");
        }

        var rows = await SearchAsync(new(UserName: userName, Table: "ผู้ใช้"));

        var password = Assert.Single(rows, r => r.Column == "รหัสผ่าน");
        Assert.Equal(("***", "***"), (password.OldValue, password.NewValue));
        var mustChange = Assert.Single(rows, r => r.Column == "ต้องเปลี่ยนรหัส");
        Assert.Equal(("ใช่", "ไม่ใช่"), (mustChange.OldValue, mustChange.NewValue));
    }

    [Fact]
    public async Task Successful_and_failed_logins_are_recorded_but_are_not_edits()
    {
        var userName = Users.NewName();
        await using (var admin = db.Scope(AppUser.AdminUserName))
            await admin.Get<UserAdminService>().CreateAsync(userName, "สมชาย", "temp1234");
        await using (var anonymous = db.Scope(CurrentUser.Anonymous))
        {
            var accounts = anonymous.Get<AccountService>();
            await accounts.CheckLoginAsync(userName, "wrong password");
            await accounts.CheckLoginAsync(userName, "temp1234");
            await accounts.CheckLoginAsync(Users.NewName(), "temp1234");
        }

        var logins = await SearchAsync(new(Table: HistoryService.LoginTable, RowKey: userName));
        var edits = (await SearchAsync(new(Table: "ผู้ใช้", RowKey: userName))).Where(r => r.Action != "เพิ่ม");

        Assert.Equal(
            [(HistoryService.LoginSucceeded, "รหัสชั่วคราว ต้องเปลี่ยนรหัส"), (HistoryService.LoginFailed, "รหัสผ่านผิด")],
            logins.Select(l => (l.Action, l.Detail)));
        Assert.All(logins, l => Assert.Equal((HistoryKind.Login, userName), (l.Kind, l.UserName)));
        Assert.Empty(edits); // failed-login counter is bookkeeping
    }

    [Fact]
    public async Task Every_audited_table_records_add_edit_and_delete_by_business_key()
    {
        var code = NewCode();
        await using (var scope = db.Scope("somchai"))
        {
            var widgets = scope.Get<WidgetDbContext>();
            var widget = new Widget { Code = code, Name = "ผ้าเบรก", Price = 10.5m };
            widgets.Add(widget);
            await widgets.SaveChangesAsync();
            widget.Price = 12m; // plain property set, no Update() call
            await widgets.SaveChangesAsync();
            widgets.Remove(widget);
            await widgets.SaveChangesAsync();
        }

        var rows = (await SearchAsync(new(Table: WidgetDbContext.AuditName, RowKey: code))).Reverse().ToList(); // oldest first

        // Columns without an audit name show the property name; values are culture-independent text.
        Assert.Equal(
            [("เพิ่ม", "รหัส", null, code), ("เพิ่ม", "ชื่อ", null, "ผ้าเบรก"), ("เพิ่ม", "Price", null, "10.5"),
             ("แก้", "Price", "10.5", "12"),
             ("ลบ", "รหัส", code, null), ("ลบ", "ชื่อ", "ผ้าเบรก", null), ("ลบ", "Price", "12", null)],
            rows.Select(r => (r.Action, r.Column, r.OldValue, r.NewValue)));
        Assert.All(rows, r => Assert.Equal("somchai", r.UserName));
    }

    [Fact]
    public async Task A_batch_job_is_one_history_row_not_one_per_row()
    {
        var codes = Enumerable.Range(0, 3).Select(_ => NewCode()).ToList();
        await using (var scope = db.Scope("somchai"))
        {
            var widgets = scope.Get<WidgetDbContext>();
            using (scope.Get<AuditScope>().SuppressRowChanges())
            {
                widgets.AddRange(codes.Select(c => new Widget { Code = c, Name = "ชุด", Price = 1 }));
                widgets.AddHistoryEvent("สร้างชิ้นทดสอบทั้งชุด", new { รหัสแรก = codes[0], จำนวน = 3 }, rowCount: 3,
                    table: WidgetDbContext.AuditName);
                await widgets.SaveChangesAsync();
            }

            // Stamps are still written for rows saved by the job.
            Assert.All(await widgets.Widgets.Where(w => codes.Contains(w.Code)).ToListAsync(),
                w => Assert.Equal("somchai", w.CreatedBy));
        }

        var rows = await SearchAsync(new(Table: WidgetDbContext.AuditName, UserName: "somchai"));

        Assert.DoesNotContain(rows, r => codes.Contains(r.RowKey!));
        var job = Assert.Single(rows, r => r.Detail?.Contains(codes[0]) == true);
        Assert.Equal((HistoryKind.Event, "สร้างชิ้นทดสอบทั้งชุด", 3), (job.Kind, job.Action, job.RowCount));
        Assert.Contains("\"จำนวน\":3", job.Detail);
    }

    [Fact]
    public async Task Data_migration_load_can_skip_history_entirely()
    {
        var code = NewCode();
        await using (var tool = db.Scope(SystemUser.Name))
        using (tool.Get<AuditScope>().SuppressRowChanges())
        {
            var widgets = tool.Get<WidgetDbContext>();
            widgets.Add(new Widget { Code = code, Name = "ย้ายมา", Price = 1 });
            await widgets.SaveChangesAsync();
        }

        Assert.Empty(await SearchAsync(new(RowKey: code)));
    }

    [Fact]
    public async Task Search_filters_by_time_window()
    {
        var code = NewCode();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await using (var scope = db.Scope("somchai"))
        {
            var widgets = scope.Get<WidgetDbContext>();
            widgets.Add(new Widget { Code = code, Name = "เวลา", Price = 1 });
            await widgets.SaveChangesAsync();
        }
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.NotEmpty(await SearchAsync(new(From: before, To: after, RowKey: code)));
        Assert.Empty(await SearchAsync(new(From: after, RowKey: code)));
        Assert.Empty(await SearchAsync(new(To: before, RowKey: code)));
    }
}
