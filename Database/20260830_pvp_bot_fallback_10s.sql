SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.pvp_matchmaking_policies', 'U') IS NOT NULL
BEGIN
    UPDATE dbo.pvp_matchmaking_policies
    SET bot_fallback_seconds = 10
    WHERE is_active = 1
      AND bot_fallback_seconds = 15;

    DECLARE @defaultConstraint sysname;
    SELECT @defaultConstraint = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.pvp_matchmaking_policies')
      AND c.name = 'bot_fallback_seconds';

    IF @defaultConstraint IS NOT NULL
        EXEC(N'ALTER TABLE dbo.pvp_matchmaking_policies DROP CONSTRAINT [' + @defaultConstraint + N'];');

    ALTER TABLE dbo.pvp_matchmaking_policies
        ADD CONSTRAINT DF_pvp_policy_bot_fallback DEFAULT 10 FOR bot_fallback_seconds;
END;

IF OBJECT_ID('dbo.matchmaking_queue', 'U') IS NOT NULL
BEGIN
    UPDATE dbo.matchmaking_queue
    SET bot_fallback_at = DATEADD(SECOND, 10, queued_at)
    WHERE status_code = 'waiting'
      AND bot_fallback_at = DATEADD(SECOND, 15, queued_at);
END;

COMMIT TRANSACTION;
