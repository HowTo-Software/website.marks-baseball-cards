-- READ ONLY. Run as the DBA/deployment account after configuring encryption.
-- This does not create keys, change server settings or grant privileges.
SET NOCOUNT ON;
IF DB_NAME() <> N'Website_Application_MarksBaseballCardsDb'
    THROW 51000, 'Select Website_Application_MarksBaseballCardsDb explicitly.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_connections WHERE session_id = @@SPID AND encrypt_option = N'TRUE')
    THROW 51000, 'This SQL connection is not encrypted. Use validated TLS.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID() AND encryption_state = 3 AND key_algorithm = N'AES' AND key_length = 256)
    THROW 51000, 'AES-256 TDE is not fully enabled. Configure it and back up its certificate/private key before release.', 1;
PRINT 'SQL connection encryption and AES-256 database-at-rest encryption verified.';
-- Photo volumes and backups require separate host/storage encryption verification.
