-- Idempotent bootstrap for the SmartTask database, run once per `docker compose up` by the
-- sqlserver-init service as `sa`. Values come from sqlcmd variables (-v), never from this file.
-- CHECK_POLICY = OFF allows the short development password; never use it outside local development.
SET NOCOUNT ON;

IF DB_ID(N'$(APP_DB_NAME)') IS NULL
BEGIN
    PRINT 'Creating database $(APP_DB_NAME)';
    CREATE DATABASE [$(APP_DB_NAME)];
END;
GO

IF SUSER_ID(N'$(APP_DB_USER)') IS NULL
BEGIN
    PRINT 'Creating login $(APP_DB_USER)';
    CREATE LOGIN [$(APP_DB_USER)] WITH PASSWORD = N'$(APP_DB_PASSWORD)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
END;
GO

USE [$(APP_DB_NAME)];
GO

IF USER_ID(N'$(APP_DB_USER)') IS NULL
BEGIN
    CREATE USER [$(APP_DB_USER)] FOR LOGIN [$(APP_DB_USER)];
END;

-- db_owner is needed so EF Core can apply migrations at API start-up.
IF IS_ROLEMEMBER(N'db_owner', N'$(APP_DB_USER)') = 0
BEGIN
    ALTER ROLE db_owner ADD MEMBER [$(APP_DB_USER)];
END;
GO

PRINT 'SmartTask database bootstrap complete';
