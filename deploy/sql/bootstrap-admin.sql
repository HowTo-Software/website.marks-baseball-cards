-- TEMPLATE ONLY. Generate a personalized copy with tools/MarksBaseballCards.AdminBootstrap.
-- A real password must NEVER be written here, passed to sqlcmd or committed.
-- The generated script contains an email and a salted ASP.NET Identity password hash.
-- Run with a separate deployment account after applying card-photo-onboarding.sql.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'Website_Application_MarksBaseballCardsDb'
    THROW 51000, 'Select Website_Application_MarksBaseballCardsDb explicitly.', 1;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 51000, 'Apply the reviewed schema migrations before creating staff.', 1;

DECLARE @AdminEmail nvarchar(64) = N'REPLACE_WITH_ADMIN_EMAIL';
DECLARE @AdminPasswordHash nvarchar(256) = N'REPLACE_WITH_IDENTITY_HASH';
IF @AdminEmail = N'REPLACE_WITH_ADMIN_EMAIL' OR @AdminEmail NOT LIKE N'%_@_%._%'
    THROW 51000, 'Generate this script with a valid admin email.', 1;
IF @AdminPasswordHash = N'REPLACE_WITH_IDENTITY_HASH'
    OR LEN(@AdminPasswordHash) < 84
    OR LEFT(@AdminPasswordHash, 2) <> N'AQ'
    THROW 51000, 'A generated ASP.NET IdentityV3 hash is required; never use plaintext or SQL HASHBYTES.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    -- Serialize creation by the existing unique username index. Never reset a password silently.
    IF EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Username = @AdminEmail)
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.Users WHERE Username = @AdminEmail AND Role <> N'Admin')
            THROW 51000, 'This email already belongs to a different role. Review it manually.', 1;
        COMMIT TRANSACTION;
        PRINT 'Admin already exists. Password and permissions were not changed.';
        RETURN;
    END;
    INSERT dbo.Users (Username, PasswordHash, Role, FailedLoginCount, LockoutEndUtc, CreatedAtUtc, LastLoginUtc)
    VALUES (@AdminEmail, @AdminPasswordHash, N'Admin', 0, NULL, SYSUTCDATETIME(), NULL);
    COMMIT TRANSACTION;
    PRINT 'Admin created. Sign in using the configured email and the original password.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
