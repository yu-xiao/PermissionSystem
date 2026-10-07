BEGIN TRANSACTION;
ALTER TABLE [Notifications] ADD [DeliveryKey] nvarchar(64) NULL;

ALTER TABLE [ScheduledTasks] ADD CONSTRAINT [AK_ScheduledTasks_TenantId_Id] UNIQUE ([TenantId], [Id]);

CREATE TABLE [ai_anomaly_rule] (
    [Id] uniqueidentifier NOT NULL,
    [OwnerUserId] uniqueidentifier NOT NULL,
    [ScheduledTaskId] uniqueidentifier NOT NULL,
    [RuleType] nvarchar(64) NOT NULL,
    [ContractVersion] int NOT NULL,
    [IsEnabled] bit NOT NULL,
    [IsAnomalous] bit NOT NULL,
    [EpisodeSequence] bigint NOT NULL,
    [LastNotifiedAt] datetimeoffset NULL,
    [ReservedEventId] uniqueidentifier NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_anomaly_rule] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_ai_anomaly_rule_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [CK_ai_anomaly_rule_Contract] CHECK ([ContractVersion] = 1 AND [RuleType] = N'DemoPendingCount' AND [EpisodeSequence] >= 0),
    CONSTRAINT [FK_ai_anomaly_rule_ScheduledTasks_TenantId_ScheduledTaskId] FOREIGN KEY ([TenantId], [ScheduledTaskId]) REFERENCES [ScheduledTasks] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE TABLE [ai_anomaly_event] (
    [Id] uniqueidentifier NOT NULL,
    [RuleId] uniqueidentifier NOT NULL,
    [RecipientUserId] uniqueidentifier NOT NULL,
    [EpisodeSequence] bigint NOT NULL,
    [ObservedCount] bigint NOT NULL,
    [ScopeFingerprint] nvarchar(64) NOT NULL,
    [ObservedAt] datetimeoffset NOT NULL,
    [ClosedAt] datetimeoffset NULL,
    [CloseReason] nvarchar(64) NULL,
    [DeliveryStatus] nvarchar(32) NOT NULL,
    [AttemptCount] int NOT NULL,
    [NextAttemptAt] datetimeoffset NULL,
    [ErrorCode] nvarchar(64) NULL,
    [DeliveryKey] nvarchar(64) NOT NULL,
    [NotificationId] uniqueidentifier NULL,
    [MessageId] nvarchar(64) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] uniqueidentifier NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] uniqueidentifier NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_ai_anomaly_event] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ai_anomaly_event_Observation] CHECK ([ObservedCount] >= 1 AND [EpisodeSequence] >= 1 AND [AttemptCount] BETWEEN 0 AND 3),
    CONSTRAINT [FK_ai_anomaly_event_ai_anomaly_rule_TenantId_RuleId] FOREIGN KEY ([TenantId], [RuleId]) REFERENCES [ai_anomaly_rule] ([TenantId], [Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_Notifications_TenantId_DeliveryKey] ON [Notifications] ([TenantId], [DeliveryKey]) WHERE [DeliveryKey] IS NOT NULL;

CREATE INDEX [IX_ai_anomaly_event_IsDeleted] ON [ai_anomaly_event] ([IsDeleted]);

CREATE INDEX [IX_ai_anomaly_event_TenantId] ON [ai_anomaly_event] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_anomaly_event_TenantId_DeliveryKey] ON [ai_anomaly_event] ([TenantId], [DeliveryKey]);

CREATE INDEX [IX_ai_anomaly_event_TenantId_RecipientUserId_ObservedAt] ON [ai_anomaly_event] ([TenantId], [RecipientUserId], [ObservedAt]);

CREATE UNIQUE INDEX [IX_ai_anomaly_event_TenantId_RuleId_EpisodeSequence] ON [ai_anomaly_event] ([TenantId], [RuleId], [EpisodeSequence]);

CREATE INDEX [IX_ai_anomaly_rule_IsDeleted] ON [ai_anomaly_rule] ([IsDeleted]);

CREATE INDEX [IX_ai_anomaly_rule_TenantId] ON [ai_anomaly_rule] ([TenantId]);

CREATE UNIQUE INDEX [IX_ai_anomaly_rule_TenantId_OwnerUserId] ON [ai_anomaly_rule] ([TenantId], [OwnerUserId]);

CREATE UNIQUE INDEX [IX_ai_anomaly_rule_TenantId_ScheduledTaskId] ON [ai_anomaly_rule] ([TenantId], [ScheduledTaskId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261007043054_AddAiAnomalyReminders', N'10.0.10');

COMMIT;
GO
