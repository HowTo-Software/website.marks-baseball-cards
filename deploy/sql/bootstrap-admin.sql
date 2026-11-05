-- PARAMETERIZED SCRIPT. Use tools/MarksBaseballCards.AdminBootstrap create/apply.
-- A real password must NEVER be written here, passed to sqlcmd or committed.
-- The checked-in SQL contains no credentials. @AdminEmail and @AdminPasswordHash
-- are provided as typed SQL parameters after decrypting the operator's package in memory.
-- Run with a separate deployment account after applying card-photo-onboarding.sql.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'Website_Application_MarksBaseballCardsDb'
    THROW 51000, 'Select Website_Application_MarksBaseballCardsDb explicitly.', 1;
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 51000, 'Apply the reviewed schema migrations before creating staff.', 1;

IF @AdminEmail IS NULL OR @AdminEmail NOT LIKE N'%_@_%._%'
    THROW 51000, 'Supply a valid admin email parameter.', 1;
IF @AdminPasswordHash IS NULL OR LEN(@AdminPasswordHash) < 84
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
