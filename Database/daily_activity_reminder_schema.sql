/*
Additive settings required by Daily Activity Reminder.

This script is idempotent and does not enable the feature on an existing
installation unless an operator explicitly changes the enabled setting.
*/
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.system_settings', 'U') IS NULL
    THROW 51030, 'Required table dbo.system_settings is missing.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.system_settings WITH (UPDLOCK, HOLDLOCK)
        WHERE setting_key = 'daily_activity_reminder_enabled')
        INSERT dbo.system_settings(setting_key, setting_value)
        VALUES ('daily_activity_reminder_enabled', 'false');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.system_settings WITH (UPDLOCK, HOLDLOCK)
        WHERE setting_key = 'daily_activity_reminder_default_goal')
        INSERT dbo.system_settings(setting_key, setting_value)
        VALUES ('daily_activity_reminder_default_goal', '7000');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.system_settings WITH (UPDLOCK, HOLDLOCK)
        WHERE setting_key = 'daily_activity_reminder_local_time')
        INSERT dbo.system_settings(setting_key, setting_value)
        VALUES ('daily_activity_reminder_local_time', '18:00');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.system_settings WITH (UPDLOCK, HOLDLOCK)
        WHERE setting_key = 'daily_activity_reminder_grace_minutes')
        INSERT dbo.system_settings(setting_key, setting_value)
        VALUES ('daily_activity_reminder_grace_minutes', '120');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
