# Database migrations (API)

Since V5 M9a (C9a.0) the API's schema is managed with **EF Core migrations** (EF Core 10, .NET 10 LTS), applied on
startup. `EnsureCreated` is gone.

## How it is set up

- `GameDb` (`RoguePirates.Api/Data/GameDb.cs`) holds the model and is abstract. Two subclasses pick the provider:
  `PostgresGameDb` (Railway, `DATABASE_URL` set) and `SqliteGameDb` (local runs and the tests). Services still ask for
  `GameDb`; `Program.cs` resolves it to the active one.
- Each subclass has its **own migrations folder**, because migration SQL is provider specific (uuid vs TEXT, identity
  columns vs AUTOINCREMENT, ...):
  - `RoguePirates.Api/Data/Migrations/Postgres/` (what runs live)
  - `RoguePirates.Api/Data/Migrations/Sqlite/` (local and tests)
- Both folders start with a `*_Baseline` migration generated from the same model. Its SQL is identical to what EF Core 8
  `EnsureCreated` built (checked by `MigrationTests.BaselineMigration_CreatesExactlyTheEnsureCreatedSchema` against the
  EF Core 8 scripts in `RoguePirates.Api.Tests/Fixtures/`).
- On startup `DatabaseMigrator.Migrate` (`RoguePirates.Api/Data/DatabaseMigrator.cs`) runs before the API serves:
  1. migration history present: apply pending migrations (none on a plain restart);
  2. no history and **none** of the baseline tables: empty database, apply every migration (the baseline creates all);
  3. no history and **all** baseline tables with all their columns (a database the old `EnsureCreated` built, like the
     live Railway Postgres): create `__EFMigrationsHistory`, record the baseline as applied **without running it**
     (one transaction; tables and rows untouched), then apply the later migrations;
  4. anything else (some baseline tables missing, or a column missing): startup stops with an error naming what is
     missing, rather than guessing. The health check then fails and Railway keeps the previous deployment.

  Every step is logged (`Database Npgsql.EntityFrameworkCore.PostgreSQL: ...`). Re-running is safe: once the history
  exists, step 1 applies only what is pending. EF Core takes a migration lock while it applies.

## Add a migration

Change the entities or `GameDb.OnModelCreating`, then add the migration **for both providers, with the same name**
(from `server/`):

```powershell
dotnet tool restore   # installs the pinned dotnet-ef (dotnet-tools.json)
dotnet ef migrations add AddRunVerdicts --project RoguePirates.Api --context PostgresGameDb --output-dir Data/Migrations/Postgres --namespace RoguePirates.Api.Data.Migrations.Postgres
dotnet ef migrations add AddRunVerdicts --project RoguePirates.Api --context SqliteGameDb --output-dir Data/Migrations/Sqlite --namespace RoguePirates.Api.Data.Migrations.Sqlite
```

Review the Postgres SQL before it ships (no database needed):

```powershell
dotnet ef migrations script --idempotent --project RoguePirates.Api --context PostgresGameDb -o pg-migrate.sql
```

Rules:

- Never edit, rename or delete a migration that has been deployed (the Baseline above all); add a new one instead.
- Data-destroying changes (dropping or narrowing a column) need the owner's OK and a backup first.
- `MigrationTests.Model_HasNoChangesMissingAMigration` fails if either provider is missing a migration for the current
  model; the app would also refuse to migrate in that state (EF Core's pending-model-changes check).
- Run `dotnet test RoguePirates.Api.Tests`: the migration tests cover an empty database, the old EnsureCreated
  database and restarts on SQLite.

## Deploy a schema change (and the first deploy of C9a.0)

1. **Back up the Railway Postgres first.** Either:
   - Railway UI: project, **Postgres** service, **Backups** tab, **Create backup** (if the plan shows the tab); or
   - `pg_dump` from this PC (PostgreSQL client tools of the server's major version or newer, free:
     `winget install PostgreSQL.PostgreSQL.17`). Copy `DATABASE_PUBLIC_URL` from the Postgres service's
     **Variables** tab (do not paste it into chat or commit it), then:

     ```powershell
     $env:PGURL = Read-Host 'DATABASE_PUBLIC_URL'
     & 'C:\Program Files\PostgreSQL\17\bin\pg_dump.exe' --format=custom --no-owner --file "roguepirates-$(Get-Date -Format yyyyMMdd-HHmm).dump" $env:PGURL
     Remove-Item Env:PGURL
     ```

     Keep the `.dump` file outside the repository. Restore, if ever needed:
     `pg_restore --clean --if-exists --no-owner --dbname <url> <file>.dump`.
2. Deploy: `powershell -NoProfile -ExecutionPolicy Bypass -File server/deploy.ps1` (it ends by calling `/health`).
3. Check `https://api-production-dad8.up.railway.app/health` returns `{"ok":true,"db":"postgres"}`.
4. Check the deploy logs (Railway, api service, **Deployments**, the new deployment, **Logs**). The first C9a.0 deploy
   shows `pre-migration schema found (built by EnsureCreated, no migration history); recording 20261007204742_Baseline as
   applied without running it`, then `schema up to date`. Later deploys show `applying N migration(s): ...`.
5. Check the history (Railway UI, Postgres service, **Data** tab, `__EFMigrationsHistory`, or
   `psql $env:PGURL -c 'SELECT * FROM "__EFMigrationsHistory";'`): after C9a.0 exactly one row,
   `20261007204742_Baseline` / `10.0.12`; every later deploy adds its migrations. Spot-check that the leaderboard
   still shows last week's runs.

If startup fails, Railway keeps serving the previous deployment (the health check never passes). Read the error in the
logs; the baseline step changes nothing unless it completes, so the database is as before. Right after C9a.0 (only the
Baseline applied), rolling back to the .NET 8 build is safe too: its `EnsureCreated` ignores a database that already
has tables, and the extra history table is harmless. Once later migrations have changed the schema, roll forward with a
fix instead, or restore the backup.
