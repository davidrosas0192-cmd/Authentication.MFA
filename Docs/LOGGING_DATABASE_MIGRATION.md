# Logging Database Migration Runbook

## Purpose
This project uses a secondary SQL Server database dedicated to application logs.

- Application database: `AuthenticationFido2`
- Logging database: `AuthenticationFido2Logs`

## How logging DB creation works
The API startup config includes `ConfigureSqlServerLogging()`.
At startup, it:

1. Reads `ConnectionStrings:LoggingConnection`.
2. Ensures the logging database exists.
3. Configures Serilog MSSql sink.
4. Auto-creates table `dbo.ApplicationLogs` when missing.

## Environment behavior
- Development: minimum level `Verbose`.
- Production: minimum level `Error`.

## Commands to execute
From repository root:

```bash
dotnet build
dotnet run
```

Starting the API once is enough to bootstrap the logging database and `ApplicationLogs` table.

## Verification SQL
Run against SQL Server:

```sql
SELECT name FROM sys.databases WHERE name = 'AuthenticationFido2Logs';
GO
USE AuthenticationFido2Logs;
GO
SELECT TABLE_SCHEMA, TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME = 'ApplicationLogs';
```

## Notes
- This logging database is intentionally separate from business/auth data.
- No EF migration is required for log table creation because Serilog sink handles it automatically.

## Execution Status
Executed on 2026-07-09:

- Created database if missing: `AuthenticationFido2Logs`
- Created table if missing: `dbo.ApplicationLogs`
- Created indexes:
	- `IX_ApplicationLogs_TimeStamp`
	- `IX_ApplicationLogs_Level_TimeStamp`
- Verified table exists in `INFORMATION_SCHEMA.TABLES`
