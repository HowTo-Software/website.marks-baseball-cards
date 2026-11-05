-- Run manually in the intended database using a DBA/deployment account, after migrations.
-- Create the SQL login/user marks_app separately with a strong secret and secure TLS.
-- The application account must not be sa, db_owner or an account with schema/role privileges.
IF DB_NAME() <> N'Website_Application_MarksBaseballCardsDb'
    THROW 51000, 'Connect explicitly to Website_Application_MarksBaseballCardsDb before applying permissions.', 1;
IF DATABASE_PRINCIPAL_ID(N'marks_app') IS NULL
    THROW 51000, 'Create the marks_app database user for the application login first.', 1;
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.Cards TO marks_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CardPhotos TO marks_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.Users TO marks_app;
GRANT SELECT, INSERT, UPDATE ON OBJECT::dbo.History TO marks_app;
-- No database creation, ALTER, DROP, GRANT, migration-history write or server-wide permissions.
