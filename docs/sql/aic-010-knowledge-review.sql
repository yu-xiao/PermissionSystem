BEGIN TRANSACTION;
ALTER TABLE [Roles] ADD CONSTRAINT [AK_Roles_TenantId_Id] UNIQUE ([TenantId], [Id]);

ALTER TABLE [FileResources] ADD CONSTRAINT [AK_FileResources_TenantId_Id] UNIQUE ([TenantId], [Id]);

ALTER TABLE [ai_run] ADD CONSTRAINT [AK_ai_run_TenantId_Id] UNIQUE ([TenantId], [Id]);

CREATE TABLE [ai_knowledge_chunk] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [VersionId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    [StartLine] int NOT NULL,
    [EndLine] int NOT NULL,
    [Content] nvarchar(2000) NOT NULL,
    [ContentHash] nvarchar(64) NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_knowledge_chunk] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_ai_knowledge_chunk_TenantId_DocumentId_VersionId_Id] UNIQUE ([TenantId], [DocumentId], [VersionId], [Id])
);

CREATE TABLE [ai_knowledge_run_reference] (
    [Id] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [InvocationId] nvarchar(100) NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [VersionId] uniqueidentifier NOT NULL,
    [ChunkId] uniqueidentifier NOT NULL,
    [ContentHash] nvarchar(64) NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_knowledge_run_reference] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_knowledge_run_reference_ai_knowledge_chunk_TenantId_DocumentId_VersionId_ChunkId] FOREIGN KEY ([TenantId], [DocumentId], [VersionId], [ChunkId]) REFERENCES [ai_knowledge_chunk] ([TenantId], [DocumentId], [VersionId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_knowledge_run_reference_ai_run_TenantId_RunId] FOREIGN KEY ([TenantId], [RunId]) REFERENCES [ai_run] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_knowledge_document] (
    [Id] uniqueidentifier NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Owner] nvarchar(200) NOT NULL,
    [License] nvarchar(500) NOT NULL,
    [Classification] nvarchar(32) NOT NULL,
    [CurrentVersionId] uniqueidentifier NULL,
    [AccessVersion] int NOT NULL,
    [LastVersionNumber] int NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_knowledge_document] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_ai_knowledge_document_TenantId_Id] UNIQUE ([TenantId], [Id])
);

CREATE TABLE [ai_knowledge_document_role] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_knowledge_document_role] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ai_knowledge_document_role_Roles_TenantId_RoleId] FOREIGN KEY ([TenantId], [RoleId]) REFERENCES [Roles] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_knowledge_document_role_ai_knowledge_document_TenantId_DocumentId] FOREIGN KEY ([TenantId], [DocumentId]) REFERENCES [ai_knowledge_document] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_knowledge_document_version] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [VersionNumber] int NOT NULL,
    [FileResourceId] uniqueidentifier NULL,
    [ContentHash] nvarchar(64) NOT NULL,
    [ParserVersion] nvarchar(64) NOT NULL,
    [ParseStatus] nvarchar(32) NOT NULL,
    [ErrorCode] nvarchar(100) NULL,
    [ValidFrom] datetimeoffset NOT NULL,
    [ValidUntil] datetimeoffset NOT NULL,
    [PublishedAt] datetimeoffset NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_knowledge_document_version] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_ai_knowledge_document_version_TenantId_DocumentId_Id] UNIQUE ([TenantId], [DocumentId], [Id]),
    CONSTRAINT [CK_ai_knowledge_version_Validity] CHECK ([ValidUntil] > [ValidFrom]),
    CONSTRAINT [FK_ai_knowledge_document_version_FileResources_TenantId_FileResourceId] FOREIGN KEY ([TenantId], [FileResourceId]) REFERENCES [FileResources] ([TenantId], [Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ai_knowledge_document_version_ai_knowledge_document_TenantId_DocumentId] FOREIGN KEY ([TenantId], [DocumentId]) REFERENCES [ai_knowledge_document] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ai_knowledge_chunk_IsDeleted] ON [ai_knowledge_chunk] ([IsDeleted]);

CREATE INDEX [IX_ai_knowledge_chunk_TenantId] ON [ai_knowledge_chunk] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_knowledge_chunk_TenantId_VersionId_Sequence] ON [ai_knowledge_chunk] ([TenantId], [VersionId], [Sequence]);

CREATE INDEX [IX_ai_knowledge_document_IsDeleted] ON [ai_knowledge_document] ([IsDeleted]);

CREATE INDEX [IX_ai_knowledge_document_TenantId] ON [ai_knowledge_document] ([TenantId]);

CREATE INDEX [IX_ai_knowledge_document_TenantId_Id_CurrentVersionId] ON [ai_knowledge_document] ([TenantId], [Id], [CurrentVersionId]);

CREATE INDEX [IX_ai_knowledge_document_TenantId_IsDeleted_CreatedAt] ON [ai_knowledge_document] ([TenantId], [IsDeleted], [CreatedAt]);

CREATE INDEX [IX_ai_knowledge_document_role_IsDeleted] ON [ai_knowledge_document_role] ([IsDeleted]);

CREATE INDEX [IX_ai_knowledge_document_role_TenantId] ON [ai_knowledge_document_role] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_knowledge_document_role_TenantId_DocumentId_RoleId] ON [ai_knowledge_document_role] ([TenantId], [DocumentId], [RoleId]) WHERE [IsDeleted] = 0;

CREATE INDEX [IX_ai_knowledge_document_role_TenantId_RoleId] ON [ai_knowledge_document_role] ([TenantId], [RoleId]);

CREATE INDEX [IX_ai_knowledge_document_version_IsDeleted] ON [ai_knowledge_document_version] ([IsDeleted]);

CREATE INDEX [IX_ai_knowledge_document_version_TenantId] ON [ai_knowledge_document_version] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_knowledge_document_version_TenantId_DocumentId_ContentHash] ON [ai_knowledge_document_version] ([TenantId], [DocumentId], [ContentHash]);

CREATE UNIQUE INDEX [IX_ai_knowledge_document_version_TenantId_DocumentId_VersionNumber] ON [ai_knowledge_document_version] ([TenantId], [DocumentId], [VersionNumber]);

CREATE INDEX [IX_ai_knowledge_document_version_TenantId_FileResourceId] ON [ai_knowledge_document_version] ([TenantId], [FileResourceId]);

CREATE INDEX [IX_ai_knowledge_run_reference_IsDeleted] ON [ai_knowledge_run_reference] ([IsDeleted]);

CREATE INDEX [IX_ai_knowledge_run_reference_TenantId] ON [ai_knowledge_run_reference] ([TenantId]);

CREATE INDEX [IX_ai_knowledge_run_reference_TenantId_DocumentId_RunId] ON [ai_knowledge_run_reference] ([TenantId], [DocumentId], [RunId]);

CREATE INDEX [IX_ai_knowledge_run_reference_TenantId_DocumentId_VersionId_ChunkId] ON [ai_knowledge_run_reference] ([TenantId], [DocumentId], [VersionId], [ChunkId]);

CREATE UNIQUE INDEX [IX_ai_knowledge_run_reference_TenantId_RunId_InvocationId_ChunkId] ON [ai_knowledge_run_reference] ([TenantId], [RunId], [InvocationId], [ChunkId]);

ALTER TABLE [ai_knowledge_chunk] ADD CONSTRAINT [FK_ai_knowledge_chunk_ai_knowledge_document_version_TenantId_DocumentId_VersionId] FOREIGN KEY ([TenantId], [DocumentId], [VersionId]) REFERENCES [ai_knowledge_document_version] ([TenantId], [DocumentId], [Id]) ON DELETE NO ACTION;

ALTER TABLE [ai_knowledge_document] ADD CONSTRAINT [FK_ai_knowledge_document_ai_knowledge_document_version_TenantId_Id_CurrentVersionId] FOREIGN KEY ([TenantId], [Id], [CurrentVersionId]) REFERENCES [ai_knowledge_document_version] ([TenantId], [DocumentId], [Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007033743_AddAiKnowledgeBase', N'10.0.10');

COMMIT;
GO
