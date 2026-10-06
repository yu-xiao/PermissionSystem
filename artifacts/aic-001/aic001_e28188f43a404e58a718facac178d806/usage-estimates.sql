BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924032728_AddAiUsageEstimates'
)
BEGIN
    ALTER TABLE [ai_usage_log] ADD [EstimatedInputTokens] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924032728_AddAiUsageEstimates'
)
BEGIN
    ALTER TABLE [ai_usage_log] ADD [EstimatedOutputTokens] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260924032728_AddAiUsageEstimates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260924032728_AddAiUsageEstimates', N'10.0.10');
END;

COMMIT;
GO

