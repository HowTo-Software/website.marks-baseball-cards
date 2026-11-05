IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE TABLE [Cards] (
        [Id] int NOT NULL IDENTITY,
        [CardNumber] nvarchar(20) NOT NULL,
        [PlayerName] nvarchar(120) NOT NULL,
        [InPlastic] bit NOT NULL,
        [InSet1] bit NOT NULL,
        [InSet2] bit NOT NULL,
        [InSet3] bit NOT NULL,
        [InSet4] bit NOT NULL,
        [InSet5] bit NOT NULL,
        [InSet6] bit NOT NULL,
        [Doubles] int NOT NULL,
        [IsRookie] bit NOT NULL,
        [IsRoyals] bit NOT NULL,
        [IsForSale] bit NOT NULL,
        [Price] decimal(18,2) NULL,
        [Condition] nvarchar(60) NULL,
        [ListingNotes] nvarchar(500) NULL,
        [IsSold] bit NOT NULL,
        [SoldAtUtc] datetime2 NULL,
        [SoldPrice] decimal(18,2) NULL,
        [StripeCheckoutSessionId] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Cards] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(64) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [Role] nvarchar(32) NOT NULL,
        [FailedLoginCount] int NOT NULL,
        [LockoutEndUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [LastLoginUtc] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE TABLE [History] (
        [Id] int NOT NULL IDENTITY,
        [CardId] int NULL,
        [CardNumber] nvarchar(20) NOT NULL,
        [PlayerName] nvarchar(120) NOT NULL,
        [Action] nvarchar(32) NOT NULL,
        [Details] nvarchar(1000) NULL,
        [ChangedBy] nvarchar(64) NOT NULL,
        [ChangedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_History] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_History_Cards_CardId] FOREIGN KEY ([CardId]) REFERENCES [Cards] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Cards_CardNumber] ON [Cards] ([CardNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Cards_IsForSale] ON [Cards] ([IsForSale]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_History_CardId] ON [History] ([CardId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_History_ChangedAtUtc] ON [History] ([ChangedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260625231754_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260625231754_InitialCreate', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105005857_CardPhotoOnboarding'
)
BEGIN
    ALTER TABLE [Cards] ADD [Description] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105005857_CardPhotoOnboarding'
)
BEGIN
    ALTER TABLE [Cards] ADD [IsPublished] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105005857_CardPhotoOnboarding'
)
BEGIN
    CREATE TABLE [CardPhotos] (
        [Id] uniqueidentifier NOT NULL,
        [CardId] int NOT NULL,
        [Caption] nvarchar(200) NOT NULL,
        [Position] int NOT NULL,
        [Width] int NOT NULL,
        [Height] int NOT NULL,
        [Bytes] bigint NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_CardPhotos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CardPhotos_Cards_CardId] FOREIGN KEY ([CardId]) REFERENCES [Cards] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105005857_CardPhotoOnboarding'
)
BEGIN
    CREATE INDEX [IX_Cards_IsPublished_Id] ON [Cards] ([IsPublished], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105005857_CardPhotoOnboarding'
)
BEGIN
    CREATE INDEX [IX_Cards_IsPublished_IsForSale_IsSold_Id] ON [Cards] ([IsPublished], [IsForSale], [IsSold], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105005857_CardPhotoOnboarding'
)
BEGIN
    CREATE INDEX [IX_CardPhotos_CardId_Position_Id] ON [CardPhotos] ([CardId], [Position], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261105005857_CardPhotoOnboarding'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261105005857_CardPhotoOnboarding', N'10.0.14');
END;

COMMIT;
GO
