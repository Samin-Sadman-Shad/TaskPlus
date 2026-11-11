# Database

`SmartTask.sql` is **generated** from the EF Core migrations in
[`src/SmartTask.Persistence/Migrations`](../src/SmartTask.Persistence/Migrations). Don't edit it by hand:
change the model, add a migration, then regenerate the script.

It is **idempotent**: every migration is wrapped in a check against `__EFMigrationsHistory`, so it can run
on an empty database or on one that already has some of the migrations.

| Migration | Adds |
|---|---|
| `InitialCreate` | `Users`, `RefreshTokens`, `Projects`, `ProjectMembers`, `TaskItems`, indexes and constraints |
| `AddUserMustChangePassword` | `Users.MustChangePassword` (`bit NOT NULL DEFAULT 0`) |

## Apply the schema

Use **one** of these. Both produce the same schema.

**EF Core migrations** (from the repo root; the `dotnet-ef` tool version is pinned in `dotnet-tools.json`):

```bash
dotnet tool restore
dotnet ef database update -p src/SmartTask.Persistence -s src/SmartTask.Api
```

**SQL script** (create the database first; the script doesn't create it):

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "IF DB_ID('SmartTask') IS NULL CREATE DATABASE [SmartTask]"
sqlcmd -S "(localdb)\MSSQLLocalDB" -d SmartTask -i database/SmartTask.sql
```

**SQL script in SQL Server Management Studio (SSMS):**

1. Connect to the server and run `IF DB_ID(N'SmartTask') IS NULL CREATE DATABASE [SmartTask];` in a new query.
2. Open `database/SmartTask.sql` (**File → Open → File**).
3. Choose `SmartTask` in the database drop-down on the toolbar (not `master`) and press **Execute** (F5).

The full walkthrough, including an application login and the connection string, is in the root
[README](../README.md#use-sql-server-and-ssms-without-docker).

The API can also apply migrations itself at start-up: set `Database:ApplyMigrationsOnStartup=true`. This is
on by default inside Docker.

## Regenerate the script

```bash
dotnet ef migrations script --idempotent -p src/SmartTask.Persistence -s src/SmartTask.Api -o database/SmartTask.sql
```
