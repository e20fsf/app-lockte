# app-lockte

## Branching workflow

- `main` — production-ready code. Only updated by merging `develop` (releases) or `hotfix/*`.
- `develop` — integration branch. Default target for pull requests.
- `feature/<name>` — new work, branched from `develop`, merged back via PR.
- `fix/<name>` — non-urgent bug fixes, branched from `develop`.
- `hotfix/<name>` — urgent production fixes, branched from `main`, merged into both `main` and `develop`.

```bash
git switch develop
git pull
git switch -c feature/my-change
# ...commit...
git push -u origin feature/my-change
# open a PR into develop
```

## Development

Needs the .NET 10 SDK and SQL Server LocalDB (`SqlLocalDB.msi` from SQL Server Express, or a full SQL Server instance).

```bash
dotnet tool restore
dotnet build Lockte.slnx
dotnet test Lockte.slnx                       # tests run against LocalDB: each run creates and drops its own database
dotnet run --project src/Lockte.Web           # migrates the database on start, then http://localhost:5175
dotnet run --project src/Lockte.Web -- reset-admin   # create the admin account / reset its password (temporary)
```

- Tests use another SQL Server when the env var `LOCKTE_TEST_SQL` is set to a connection string (without `Database=`),
  e.g. the shared dev server: `Server=<host>;User Id=<login>;Password=<password>;TrustServerCertificate=true`.
  The login needs to create databases; each run makes its own `LockteTests_*` database and drops it. Keep credentials out of git
  (env var for tests, `dotnet user-secrets` for `ConnectionStrings:Lockte` when running the app).
- Connection string: `ConnectionStrings:Lockte`; idle logout: `Auth:IdleTimeoutMinutes` (default 30).
- New migration: `dotnet ef migrations add <Name> -p src/Lockte.Web -c AccountsDbContext -o Accounts/Migrations` (or `-c AuditDbContext -o Audit/Migrations`);
  module องค์กร: `dotnet ef migrations add <Name> -p src/Lockte.Org -s src/Lockte.Org -c OrgDbContext -o Data/Migrations`.
  module พนักงานและค่าแรง: `dotnet ef migrations add <Name> -p src/Lockte.Hr -s src/Lockte.Hr -c HrDbContext -o Data/Migrations`.

### Solution layout (ADR 0002)

| Project | What |
|---|---|
| `src/Lockte.Web` | Blazor Server host: login, user management (`Accounts/`), change history (`Audit/`, schema `audit`), legacy menu (`Menus/`), pages |
| `src/Lockte.SharedKernel` | Non-business shared code: audit columns + rowversion convention, concurrency conflict (`Persistence/`) |
| `src/Lockte.Org` | module องค์กร, schema `org`: ฝ่าย, แผนก, กะ, หัวหน้ากะ — others read via `OrgDirectory`, SCR-05 writes via `OrgRegistry`; migrations in `Data/Migrations` (`-p src/Lockte.Org -c OrgDbContext`) |
| `src/Lockte.Hr` | module พนักงานและค่าแรง, schema `hr` |
| `src/Lockte.Sales` | module ขายและส่งของ, schema `sales`: so far only the minimal ทะเบียนลูกค้า (รหัส + ชื่อ) the employee program needs |
| `src/Lockte.Migration` | เครื่องมือย้ายข้อมูล — console run on a PC, reads a copy of the `.accdb` (see below) |
| `tests/Lockte.Web.Tests` | Tests through the services, on LocalDB |

### เครื่องมือย้ายข้อมูล (data migration, #47)

Runs on a Windows PC with Microsoft 365 / Access Runtime (64-bit ACE OLE DB 16.0), never on the server (ADR 0001).
The database must exist and be migrated first (start the web app on it once). Each run **deletes** the master data
of `org`, `hr` and `sales` and loads it again from a copy of the file (the file itself is not changed), through the
modules' registries, with no change history:

```
dotnet run --project src/Lockte.Migration -- --source "<copy of พนักงาน.accdb>" --connection "<connection string>"
    --confirm <database name> --out <report folder> [--map code-map.csv]
```

`--confirm` must repeat the database name of the connection string (env `LOCKTE_MIGRATION_SQL` can hold the connection
instead). The report folder gets `dq-exceptions.csv` (rows not loaded as they are, by DQ-x / NF-06), `row-counts.csv`
(legacy rows = not migrated by design + DQ + loaded, and loaded = rows in the database) and `code-map-used.csv`
(what every department / customer / shift value became). Edit that file and pass it back as `--map` to give
departments, customers or shifts other codes or names (columns `kind,legacy,code,name,prefix`). The reports hold real
names — keep them out of git. Tests build a fake `.accdb` (`FakeLegacyFile`) with fictional data.

Every table follows the convention in `SharedKernel/Persistence`:

- Module DbContexts derive from `ModuleDbContext` (own schema, audit conventions, rowversion mismatch → `ConcurrencyConflictException`,
  whose message is shown to the user). Entities implement `IAudited` (CreatedBy/At, UpdatedBy/At, RowVersion).
- `AuditInterceptor` stamps those columns and writes ประวัติการแก้ไข (`audit.History`) column by column, in the same save.
  In the model, give tables/columns their Thai name with `HasAuditName(...)`, give each entity its `BusinessKey` (required by `IAudited`)
  so history shows รหัสรายการ (e.g. รหัสพนักงาน), mark system-written columns `IsBookkeeping()` and sensitive ones `IsSecret()`.
- Batch jobs / calculations: do the row writes inside `AuditScope.SuppressRowChanges()` and record one
  `context.AddHistoryEvent(action, parameters, rowCount)` instead. The data-migration tool uses the same scope with no event.
- The `audit` schema is migrated first (`LockteDatabase.MigrateAsync`); add each module's context there once it has migrations.
