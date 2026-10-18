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
- New migration: `dotnet ef migrations add <Name> -p src/Lockte.Web -c AccountsDbContext -o Accounts/Migrations` (or `-c AuditDbContext -o Audit/Migrations`).

### Solution layout (ADR 0002)

| Project | What |
|---|---|
| `src/Lockte.Web` | Blazor Server host: login, user management (`Accounts/`), change history (`Audit/`, schema `audit`), legacy menu (`Menus/`), pages |
| `src/Lockte.SharedKernel` | Non-business shared code: audit columns + rowversion convention, concurrency conflict (`Persistence/`) |
| `src/Lockte.Org` | module องค์กร, schema `org` |
| `src/Lockte.Hr` | module พนักงานและค่าแรง, schema `hr` |
| `tests/Lockte.Web.Tests` | Tests through the services, on LocalDB |

Every table follows the convention in `SharedKernel/Persistence`:

- Module DbContexts derive from `ModuleDbContext` (own schema, audit conventions, rowversion mismatch → `ConcurrencyConflictException`,
  whose message is shown to the user). Entities implement `IAudited` (CreatedBy/At, UpdatedBy/At, RowVersion).
- `AuditInterceptor` stamps those columns and writes ประวัติการแก้ไข (`audit.History`) column by column, in the same save.
  In the model, give tables/columns their Thai name with `HasAuditName(...)`, give each entity its `BusinessKey` (required by `IAudited`)
  so history shows รหัสรายการ (e.g. รหัสพนักงาน), mark system-written columns `IsBookkeeping()` and sensitive ones `IsSecret()`.
- Batch jobs / calculations: do the row writes inside `AuditScope.SuppressRowChanges()` and record one
  `context.AddHistoryEvent(action, parameters, rowCount)` instead. The data-migration tool uses the same scope with no event.
- The `audit` schema is migrated first (`LockteDatabase.MigrateAsync`); add each module's context there once it has migrations.
