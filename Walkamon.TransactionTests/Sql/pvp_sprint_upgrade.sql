/*
Manual SQL Server upgrade for the Lumina Sprint PvP feature.
Run against an existing Walkamon database after taking a backup.
This is not an EF migration and does not create a database.
*/
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

/* Daily eligible step aggregate. */
IF COL_LENGTH (
    'dbo.daily_steps',
    'eligible_step_count'
) IS NULL BEGIN EXEC (
    N'ALTER TABLE dbo.daily_steps ADD eligible_step_count INT NULL;'
);

EXEC (
    N'UPDATE dbo.daily_steps SET eligible_step_count = step_count WHERE eligible_step_count IS NULL;'
);

EXEC (
    N'ALTER TABLE dbo.daily_steps ALTER COLUMN eligible_step_count INT NOT NULL;'
);

EXEC (
    N'ALTER TABLE dbo.daily_steps ADD CONSTRAINT DF_daily_steps_eligible_step_count DEFAULT 0 FOR eligible_step_count;'
);

EXEC (
    N'ALTER TABLE dbo.daily_steps ADD CONSTRAINT CK_daily_steps_eligible_step_count CHECK (eligible_step_count >= 0 AND eligible_step_count <= step_count);'
);

END;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.system_settings
    WHERE
        setting_key = 'pvp_daily_step_limit'
)
INSERT INTO
    dbo.system_settings (setting_key, setting_value)
VALUES (
        'pvp_daily_step_limit',
        '100000'
    );

/* PvP profile and single-active-activity lock. */
IF OBJECT_ID (
    'dbo.pvp_player_profiles',
    'U'
) IS NULL
CREATE TABLE dbo.pvp_player_profiles (
    user_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    mmr INT NOT NULL CONSTRAINT DF_pvp_player_profiles_mmr DEFAULT 1000,
    updated_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_player_profiles_updated_at DEFAULT SYSUTCDATETIME (),
    CONSTRAINT FK_pvp_player_profiles_user FOREIGN KEY (user_id) REFERENCES dbo.users (user_id)
);

INSERT INTO
    dbo.pvp_player_profiles (user_id, mmr)
SELECT u.user_id, 1000
FROM dbo.users u
WHERE
    NOT EXISTS (
        SELECT 1
        FROM dbo.pvp_player_profiles p
        WHERE
            p.user_id = u.user_id
    );

IF OBJECT_ID (
    'dbo.pvp_player_activities',
    'U'
) IS NULL
CREATE TABLE dbo.pvp_player_activities (
    user_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    activity_type VARCHAR(30) NOT NULL,
    activity_id UNIQUEIDENTIFIER NOT NULL,
    due_at DATETIME2 (0) NULL,
    row_version ROWVERSION NOT NULL,
    created_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_player_activities_created_at DEFAULT SYSUTCDATETIME (),
    updated_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_player_activities_updated_at DEFAULT SYSUTCDATETIME (),
    CONSTRAINT CK_pvp_player_activities_type CHECK (
        activity_type IN (
            'invite_pending',
            'queue_waiting',
            'match_countdown',
            'match_running',
            'match_settling'
        )
    ),
    CONSTRAINT FK_pvp_player_activities_user FOREIGN KEY (user_id) REFERENCES dbo.users (user_id)
);

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        name = 'IX_pvp_player_activities_type_due'
        AND object_id = OBJECT_ID ('dbo.pvp_player_activities')
)
CREATE INDEX IX_pvp_player_activities_type_due ON dbo.pvp_player_activities (activity_type, due_at);

IF OBJECT_ID ('dbo.pvp_bot_profiles', 'U') IS NULL
CREATE TABLE dbo.pvp_bot_profiles (
    bot_profile_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_bot_profiles_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    display_name NVARCHAR (80) NOT NULL,
    avatar_url NVARCHAR (500) NULL,
    mmr INT NOT NULL,
    steps_per_second DECIMAL(5, 2) NOT NULL,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_bot_profiles_active DEFAULT 1,
    created_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_bot_profiles_created_at DEFAULT SYSUTCDATETIME (),
    updated_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_bot_profiles_updated_at DEFAULT SYSUTCDATETIME (),
    CONSTRAINT CK_pvp_bot_profiles_pace CHECK (steps_per_second > 0)
);

/* Extend existing matches. Existing created/started values are mapped to new states. */
UPDATE dbo.pvp_matches
SET
    status_code = CASE status_code
        WHEN 'created' THEN 'countdown'
        WHEN 'started' THEN 'running'
        ELSE status_code
    END
WHERE
    status_code IN ('created', 'started');

DECLARE @MatchStatusCheck SYSNAME = (SELECT TOP 1 kc.name FROM sys.check_constraints kc WHERE kc.parent_object_id = OBJECT_ID('dbo.pvp_matches') AND kc.definition LIKE '%status_code%');
DECLARE @Sql NVARCHAR(MAX);
IF @MatchStatusCheck IS NOT NULL
BEGIN
    SET @Sql = N'ALTER TABLE dbo.pvp_matches DROP CONSTRAINT ' + QUOTENAME(@MatchStatusCheck) + N';';
    EXEC(@Sql);
END;

DECLARE @MatchStatusDefault SYSNAME = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id WHERE dc.parent_object_id = OBJECT_ID('dbo.pvp_matches') AND c.name = 'status_code');
IF @MatchStatusDefault IS NOT NULL
BEGIN
    SET @Sql = N'ALTER TABLE dbo.pvp_matches DROP CONSTRAINT ' + QUOTENAME(@MatchStatusDefault) + N';';
    EXEC(@Sql);
END;

ALTER TABLE dbo.pvp_matches
ADD CONSTRAINT DF_pvp_matches_status_code DEFAULT 'countdown' FOR status_code;

IF COL_LENGTH (
    'dbo.pvp_matches',
    'source_code'
) IS NULL
ALTER TABLE dbo.pvp_matches
ADD source_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_matches_source_code DEFAULT 'matchmaking';

IF COL_LENGTH (
    'dbo.pvp_matches',
    'countdown_ends_at'
) IS NULL
ALTER TABLE dbo.pvp_matches
ADD countdown_ends_at DATETIME2 (0) NULL;

IF COL_LENGTH (
    'dbo.pvp_matches',
    'settlement_ends_at'
) IS NULL
ALTER TABLE dbo.pvp_matches
ADD settlement_ends_at DATETIME2 (0) NULL;

IF COL_LENGTH (
    'dbo.pvp_matches',
    'resolved_at'
) IS NULL
ALTER TABLE dbo.pvp_matches
ADD resolved_at DATETIME2 (0) NULL;

IF COL_LENGTH ('dbo.pvp_matches', 'rating_k') IS NULL
ALTER TABLE dbo.pvp_matches
ADD rating_k INT NOT NULL CONSTRAINT DF_pvp_matches_rating_k DEFAULT 32;

IF COL_LENGTH (
    'dbo.pvp_matches',
    'rating_divisor'
) IS NULL
ALTER TABLE dbo.pvp_matches
ADD rating_divisor INT NOT NULL CONSTRAINT DF_pvp_matches_rating_divisor DEFAULT 400;

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE
        name = 'CK_pvp_matches_status_code'
)
ALTER TABLE dbo.pvp_matches
ADD CONSTRAINT CK_pvp_matches_status_code CHECK (
    status_code IN (
        'countdown',
        'running',
        'settling',
        'finished',
        'cancelled'
    )
);

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE
        name = 'CK_pvp_matches_source_code'
) EXEC (
    N'ALTER TABLE dbo.pvp_matches ADD CONSTRAINT CK_pvp_matches_source_code CHECK (source_code IN (''invite'', ''matchmaking'', ''bot''));'
);

/* Convert the old composite participant key to a surrogate key, preserving old user participants. */
IF COL_LENGTH (
    'dbo.pvp_match_players',
    'match_player_id'
) IS NULL BEGIN EXEC (
    N'ALTER TABLE dbo.pvp_match_players ADD match_player_id UNIQUEIDENTIFIER NULL;'
);

EXEC (
    N'UPDATE dbo.pvp_match_players SET match_player_id = NEWID() WHERE match_player_id IS NULL;'
);

EXEC (
    N'ALTER TABLE dbo.pvp_match_players ALTER COLUMN match_player_id UNIQUEIDENTIFIER NOT NULL;'
);

DECLARE @OldPlayerPk SYSNAME = (SELECT kc.name FROM sys.key_constraints kc WHERE kc.parent_object_id = OBJECT_ID('dbo.pvp_match_players') AND kc.type = 'PK');
    IF @OldPlayerPk IS NOT NULL
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.pvp_match_players DROP CONSTRAINT ' + QUOTENAME(@OldPlayerPk) + N';';
        EXEC(@Sql);
    END;

EXEC (
    N'ALTER TABLE dbo.pvp_match_players ADD CONSTRAINT PK_pvp_match_players PRIMARY KEY (match_player_id);'
);

END;

IF COL_LENGTH (
    'dbo.pvp_match_players',
    'bot_profile_id'
) IS NULL
ALTER TABLE dbo.pvp_match_players
ADD bot_profile_id UNIQUEIDENTIFIER NULL;

IF COL_LENGTH (
    'dbo.pvp_match_players',
    'participant_type_code'
) IS NULL BEGIN EXEC (
    N'ALTER TABLE dbo.pvp_match_players ADD participant_type_code VARCHAR(10) NULL;'
);

EXEC (
    N'UPDATE dbo.pvp_match_players SET participant_type_code = ''user'' WHERE participant_type_code IS NULL;'
);

EXEC (
    N'ALTER TABLE dbo.pvp_match_players ALTER COLUMN participant_type_code VARCHAR(10) NOT NULL;'
);

END;

IF COL_LENGTH (
    'dbo.pvp_match_players',
    'mmr_before'
) IS NULL BEGIN EXEC (
    N'ALTER TABLE dbo.pvp_match_players ADD mmr_before INT NULL;'
);

EXEC (
    N'UPDATE mp SET mmr_before = p.mmr FROM dbo.pvp_match_players mp JOIN dbo.pvp_player_profiles p ON p.user_id = mp.user_id WHERE mp.mmr_before IS NULL;'
);

EXEC (
    N'ALTER TABLE dbo.pvp_match_players ALTER COLUMN mmr_before INT NOT NULL;'
);

END;

IF COL_LENGTH (
    'dbo.pvp_match_players',
    'mmr_delta'
) IS NULL
ALTER TABLE dbo.pvp_match_players
ADD mmr_delta INT NOT NULL CONSTRAINT DF_pvp_match_players_mmr_delta DEFAULT 0;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.pvp_match_players')
      AND name = 'user_id'
      AND is_nullable = 0
)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_pvp_match_players_match_user' AND object_id = OBJECT_ID('dbo.pvp_match_players'))
        EXEC(N'DROP INDEX UX_pvp_match_players_match_user ON dbo.pvp_match_players;');
    EXEC(N'ALTER TABLE dbo.pvp_match_players ALTER COLUMN user_id UNIQUEIDENTIFIER NULL;');
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE
        name = 'FK_pvp_match_players_bot'
) EXEC (
    N'ALTER TABLE dbo.pvp_match_players ADD CONSTRAINT FK_pvp_match_players_bot FOREIGN KEY (bot_profile_id) REFERENCES dbo.pvp_bot_profiles(bot_profile_id);'
);

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE
        name = 'CK_pvp_match_players_participant'
) EXEC (
    N'ALTER TABLE dbo.pvp_match_players ADD CONSTRAINT CK_pvp_match_players_participant CHECK ((participant_type_code = ''user'' AND user_id IS NOT NULL AND bot_profile_id IS NULL) OR (participant_type_code = ''bot'' AND bot_profile_id IS NOT NULL AND user_id IS NULL));'
);

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        name = 'UX_pvp_match_players_match_user'
        AND object_id = OBJECT_ID ('dbo.pvp_match_players')
) CREATE UNIQUE INDEX UX_pvp_match_players_match_user ON dbo.pvp_match_players (match_id, user_id)
WHERE
    user_id IS NOT NULL;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        name = 'UX_pvp_match_players_match_bot'
        AND object_id = OBJECT_ID ('dbo.pvp_match_players')
) EXEC (
    N'CREATE UNIQUE INDEX UX_pvp_match_players_match_bot ON dbo.pvp_match_players(match_id, bot_profile_id) WHERE bot_profile_id IS NOT NULL;'
);

IF OBJECT_ID ('dbo.pvp_sprint_invites', 'U') IS NULL
CREATE TABLE dbo.pvp_sprint_invites (
    invite_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_sprint_invites_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    inviter_user_id UNIQUEIDENTIFIER NOT NULL,
    invitee_user_id UNIQUEIDENTIFIER NOT NULL,
    user_low_id UNIQUEIDENTIFIER NOT NULL,
    user_high_id UNIQUEIDENTIFIER NOT NULL,
    status_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_sprint_invites_status DEFAULT 'pending',
    expires_at DATETIME2 (0) NOT NULL,
    responded_at DATETIME2 (0) NULL,
    match_id UNIQUEIDENTIFIER NULL,
    created_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_sprint_invites_created_at DEFAULT SYSUTCDATETIME (),
    CONSTRAINT CK_pvp_sprint_invites_users CHECK (
        inviter_user_id <> invitee_user_id
        AND user_low_id < user_high_id
    ),
    CONSTRAINT CK_pvp_sprint_invites_status CHECK (
        status_code IN (
            'pending',
            'accepted',
            'declined',
            'expired',
            'cancelled'
        )
    ),
    FOREIGN KEY (inviter_user_id) REFERENCES dbo.users (user_id),
    FOREIGN KEY (invitee_user_id) REFERENCES dbo.users (user_id),
    FOREIGN KEY (match_id) REFERENCES dbo.pvp_matches (match_id)
);

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        name = 'UX_pvp_sprint_invites_pending_pair'
        AND object_id = OBJECT_ID ('dbo.pvp_sprint_invites')
) CREATE UNIQUE INDEX UX_pvp_sprint_invites_pending_pair ON dbo.pvp_sprint_invites (user_low_id, user_high_id)
WHERE
    status_code = 'pending';

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        name = 'IX_pvp_sprint_invites_incoming'
        AND object_id = OBJECT_ID ('dbo.pvp_sprint_invites')
)
CREATE INDEX IX_pvp_sprint_invites_incoming ON dbo.pvp_sprint_invites (
    invitee_user_id,
    status_code,
    expires_at DESC
);

IF OBJECT_ID ('dbo.pvp_step_sessions', 'U') IS NULL
CREATE TABLE dbo.pvp_step_sessions (
    step_session_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_step_sessions_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    user_id UNIQUEIDENTIFIER NOT NULL,
    platform_code VARCHAR(20) NOT NULL,
    nonce NVARCHAR (128) NOT NULL,
    status_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_step_sessions_status DEFAULT 'active',
    expires_at DATETIME2 (0) NOT NULL,
    created_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_step_sessions_created_at DEFAULT SYSUTCDATETIME (),
    last_submitted_at DATETIME2 (0) NULL,
    last_sequence INT NOT NULL CONSTRAINT DF_pvp_step_sessions_last_sequence DEFAULT 0,
    CONSTRAINT CK_pvp_step_sessions_status CHECK (
        status_code IN ('active', 'expired', 'closed')
    ),
    FOREIGN KEY (match_id) REFERENCES dbo.pvp_matches (match_id),
    FOREIGN KEY (user_id) REFERENCES dbo.users (user_id),
    CONSTRAINT UX_pvp_step_sessions_match_user UNIQUE (match_id, user_id)
);

IF OBJECT_ID (
    'dbo.validated_step_records',
    'U'
) IS NULL
CREATE TABLE dbo.validated_step_records (
    validated_step_record_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_validated_step_records_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    step_session_id UNIQUEIDENTIFIER NULL,
    platform_code VARCHAR(20) NOT NULL,
    source_code VARCHAR(30) NOT NULL,
    recorded_at DATETIME2 (3) NOT NULL,
    step_count INT NOT NULL,
    eligible_step_count INT NOT NULL,
    sequence_number INT NOT NULL,
    payload_hash CHAR(64) NOT NULL,
    validation_status VARCHAR(20) NOT NULL,
    rejection_reason NVARCHAR (200) NULL,
    received_at DATETIME2 (3) NOT NULL CONSTRAINT DF_validated_step_records_received_at DEFAULT SYSUTCDATETIME (),
    CONSTRAINT CK_validated_step_records_counts CHECK (
        step_count >= 0
        AND eligible_step_count >= 0
        AND eligible_step_count <= step_count
    ),
    CONSTRAINT CK_validated_step_records_status CHECK (
        validation_status IN (
            'accepted',
            'rejected',
            'suspicious'
        )
    ),
    FOREIGN KEY (user_id) REFERENCES dbo.users (user_id),
    FOREIGN KEY (step_session_id) REFERENCES dbo.pvp_step_sessions (step_session_id),
    CONSTRAINT UX_validated_step_records_user_hash UNIQUE (user_id, payload_hash)
);

IF OBJECT_ID (
    'dbo.pvp_match_step_ledgers',
    'U'
) IS NULL
CREATE TABLE dbo.pvp_match_step_ledgers (
    match_step_ledger_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_step_ledgers_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    match_player_id UNIQUEIDENTIFIER NOT NULL,
    validated_step_record_id UNIQUEIDENTIFIER NOT NULL,
    counted_steps INT NOT NULL,
    created_at DATETIME2 (3) NOT NULL CONSTRAINT DF_pvp_match_step_ledgers_created_at DEFAULT SYSUTCDATETIME (),
    CONSTRAINT CK_pvp_match_step_ledgers_count CHECK (counted_steps > 0),
    FOREIGN KEY (match_id) REFERENCES dbo.pvp_matches (match_id),
    FOREIGN KEY (match_player_id) REFERENCES dbo.pvp_match_players (match_player_id),
    FOREIGN KEY (validated_step_record_id) REFERENCES dbo.validated_step_records (validated_step_record_id),
    CONSTRAINT UX_pvp_match_step_ledgers_record UNIQUE (validated_step_record_id)
);

IF OBJECT_ID ('dbo.pvp_reward_rules', 'U') IS NULL
CREATE TABLE dbo.pvp_reward_rules (
    pvp_reward_rule_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_reward_rules_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    match_type_code VARCHAR(20) NOT NULL,
    result_code VARCHAR(20) NOT NULL,
    reward_package_id UNIQUEIDENTIFIER NOT NULL,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_reward_rules_active DEFAULT 1,
    updated_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_reward_rules_updated_at DEFAULT SYSUTCDATETIME (),
    CONSTRAINT CK_pvp_reward_rules_type CHECK (
        match_type_code IN ('ranked', 'friendly', 'event')
    ),
    CONSTRAINT CK_pvp_reward_rules_result CHECK (
        result_code IN ('win', 'lose', 'draw')
    ),
    FOREIGN KEY (reward_package_id) REFERENCES dbo.reward_packages (reward_package_id),
    CONSTRAINT UX_pvp_reward_rules_type_result UNIQUE (match_type_code, result_code)
);

IF OBJECT_ID (
    'dbo.pvp_match_reward_entitlements',
    'U'
) IS NULL
CREATE TABLE dbo.pvp_match_reward_entitlements (
    match_reward_entitlement_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_reward_entitlements_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    user_id UNIQUEIDENTIFIER NOT NULL,
    result_code VARCHAR(20) NOT NULL,
    wallet_amount INT NOT NULL,
    created_at DATETIME2 (0) NOT NULL CONSTRAINT DF_pvp_match_reward_entitlements_created_at DEFAULT SYSUTCDATETIME (),
    claimed_at DATETIME2 (0) NULL,
    CONSTRAINT CK_pvp_match_reward_entitlements_amount CHECK (wallet_amount >= 0),
    FOREIGN KEY (match_id) REFERENCES dbo.pvp_matches (match_id),
    FOREIGN KEY (user_id) REFERENCES dbo.users (user_id),
    CONSTRAINT UX_pvp_match_reward_entitlements_match_user UNIQUE (match_id, user_id)
);

IF OBJECT_ID (
    'dbo.pvp_match_reward_items',
    'U'
) IS NULL
CREATE TABLE dbo.pvp_match_reward_items (
    match_reward_entitlement_id UNIQUEIDENTIFIER NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    quantity INT NOT NULL,
    PRIMARY KEY (
        match_reward_entitlement_id,
        item_id
    ),
    CONSTRAINT CK_pvp_match_reward_items_quantity CHECK (quantity > 0),
    FOREIGN KEY (match_reward_entitlement_id) REFERENCES dbo.pvp_match_reward_entitlements (match_reward_entitlement_id),
    FOREIGN KEY (item_id) REFERENCES dbo.items (item_id)
);

IF OBJECT_ID ('dbo.pvp_match_events', 'U') IS NULL
CREATE TABLE dbo.pvp_match_events (
    pvp_match_event_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_events_id DEFAULT NEWSEQUENTIALID () PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    sequence BIGINT NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload_json NVARCHAR (MAX) NOT NULL,
    created_at DATETIME2 (3) NOT NULL CONSTRAINT DF_pvp_match_events_created_at DEFAULT SYSUTCDATETIME (),
    FOREIGN KEY (match_id) REFERENCES dbo.pvp_matches (match_id),
    CONSTRAINT UX_pvp_match_events_match_sequence UNIQUE (match_id, sequence)
);

IF OBJECT_ID ('dbo.outbox_events', 'U') IS NULL
CREATE TABLE dbo.outbox_events (
    event_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    aggregate_type VARCHAR(50) NOT NULL,
    aggregate_id UNIQUEIDENTIFIER NOT NULL,
    destination VARCHAR(30) NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload_json NVARCHAR (MAX) NOT NULL,
    attempts INT NOT NULL CONSTRAINT DF_outbox_events_attempts DEFAULT 0,
    lease_until DATETIME2 (3) NULL,
    lease_owner NVARCHAR (100) NULL,
    published_at DATETIME2 (3) NULL,
    created_at DATETIME2 (3) NOT NULL CONSTRAINT DF_outbox_events_created_at DEFAULT SYSUTCDATETIME ()
);

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE
        name = 'IX_outbox_events_dispatch'
        AND object_id = OBJECT_ID ('dbo.outbox_events')
)
CREATE INDEX IX_outbox_events_dispatch ON dbo.outbox_events (published_at, lease_until);

IF COL_LENGTH('dbo.pets', 'pvp_affinity_code') IS NULL
    EXEC(N'ALTER TABLE dbo.pets ADD pvp_affinity_code VARCHAR(30) NULL;');
IF COL_LENGTH('dbo.pvp_bot_profiles', 'spirit_affinity_code') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_bot_profiles ADD spirit_affinity_code VARCHAR(30) NULL;');
IF COL_LENGTH('dbo.pvp_matches', 'speed_min_bps') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_matches ADD speed_min_bps INT NOT NULL CONSTRAINT DF_pvp_matches_speed_min DEFAULT 7500;');
IF COL_LENGTH('dbo.pvp_matches', 'speed_max_bps') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_matches ADD speed_max_bps INT NOT NULL CONSTRAINT DF_pvp_matches_speed_max DEFAULT 12500;');
IF COL_LENGTH('dbo.pvp_matches', 'item_slot_limit') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_matches ADD item_slot_limit TINYINT NOT NULL CONSTRAINT DF_pvp_matches_item_slots DEFAULT 2;');
IF COL_LENGTH('dbo.pvp_matches', 'rule_version') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_matches ADD rule_version INT NOT NULL CONSTRAINT DF_pvp_matches_rule_version DEFAULT 1;');
IF COL_LENGTH('dbo.pvp_matches', 'row_version') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_matches ADD row_version ROWVERSION NOT NULL;');
IF COL_LENGTH('dbo.pvp_matches', 'last_event_sequence') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_matches ADD last_event_sequence BIGINT NOT NULL CONSTRAINT DF_pvp_matches_last_event_sequence DEFAULT 0;');
EXEC(N'UPDATE m
SET last_event_sequence = event_state.max_sequence
FROM dbo.pvp_matches m
CROSS APPLY (
    SELECT ISNULL(MAX(e.sequence), 0) AS max_sequence
    FROM dbo.pvp_match_events e
    WHERE e.match_id = m.match_id
) event_state
WHERE m.last_event_sequence < event_state.max_sequence;');
IF COL_LENGTH('dbo.pvp_match_players', 'pet_id_snapshot') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_players ADD pet_id_snapshot UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH('dbo.pvp_match_players', 'spirit_affinity_code') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_players ADD spirit_affinity_code VARCHAR(30) NULL;');
IF COL_LENGTH('dbo.pvp_match_players', 'passive_speed_bps') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_players ADD passive_speed_bps INT NOT NULL CONSTRAINT DF_pvp_match_players_passive DEFAULT 0;');
IF COL_LENGTH('dbo.pvp_match_players', 'validated_steps') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_players ADD validated_steps INT NOT NULL CONSTRAINT DF_pvp_match_players_validated_steps DEFAULT 0;');
IF COL_LENGTH('dbo.pvp_match_players', 'distance_units') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_players ADD distance_units BIGINT NOT NULL CONSTRAINT DF_pvp_match_players_distance DEFAULT 0;');
EXEC(N'UPDATE dbo.pvp_match_players SET validated_steps = score, distance_units = CONVERT(BIGINT, score) * 10000 WHERE distance_units = 0 AND score > 0;');
IF COL_LENGTH('dbo.pvp_match_players', 'row_version') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_players ADD row_version ROWVERSION NOT NULL;');
IF COL_LENGTH('dbo.pvp_step_sessions', 'row_version') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD row_version ROWVERSION NOT NULL;');
IF COL_LENGTH('dbo.pvp_match_step_ledgers', 'multiplier_bps') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_step_ledgers ADD multiplier_bps INT NOT NULL CONSTRAINT DF_pvp_match_step_ledgers_multiplier DEFAULT 10000;');
IF COL_LENGTH('dbo.pvp_match_step_ledgers', 'distance_units') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_step_ledgers ADD distance_units BIGINT NOT NULL CONSTRAINT DF_pvp_match_step_ledgers_distance DEFAULT 0;');
IF COL_LENGTH('dbo.pvp_match_step_ledgers', 'effect_snapshot_json') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_match_step_ledgers ADD effect_snapshot_json NVARCHAR(MAX) NOT NULL CONSTRAINT DF_pvp_match_step_ledgers_effects DEFAULT N''[]'';');
EXEC(N'UPDATE dbo.pvp_match_step_ledgers SET distance_units = CONVERT(BIGINT, counted_steps) * multiplier_bps WHERE distance_units = 0 AND counted_steps > 0;');

IF OBJECT_ID('dbo.pvp_item_effect_definitions', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_item_effect_definitions (
    pvp_item_effect_definition_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_item_effect_definitions_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    item_id UNIQUEIDENTIFIER NOT NULL, effect_code VARCHAR(30) NOT NULL, target_code VARCHAR(20) NOT NULL,
    magnitude_bps INT NOT NULL, duration_ms INT NOT NULL, cooldown_ms INT NOT NULL, asset_key NVARCHAR(300) NOT NULL,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_item_effect_definitions_active DEFAULT 1,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_item_effect_definitions_updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_item_effect_definitions_target CHECK (target_code IN (''self'', ''opponent'')),
    CONSTRAINT CK_pvp_item_effect_definitions_values CHECK (magnitude_bps >= 0 AND duration_ms >= 0 AND cooldown_ms >= 0),
    CONSTRAINT UQ_pvp_item_effect_definitions_item UNIQUE(item_id), CONSTRAINT UQ_pvp_item_effect_definitions_code UNIQUE(effect_code),
    FOREIGN KEY(item_id) REFERENCES dbo.items(item_id));');

IF OBJECT_ID('dbo.pvp_player_loadout_slots', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_player_loadout_slots (
    user_id UNIQUEIDENTIFIER NOT NULL, slot_no TINYINT NOT NULL, item_id UNIQUEIDENTIFIER NOT NULL,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_player_loadout_slots_updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_pvp_player_loadout_slots PRIMARY KEY(user_id, slot_no),
    CONSTRAINT CK_pvp_player_loadout_slots_slot CHECK(slot_no BETWEEN 1 AND 2),
    CONSTRAINT UQ_pvp_player_loadout_slots_item UNIQUE(user_id, item_id),
    FOREIGN KEY(user_id) REFERENCES dbo.users(user_id), FOREIGN KEY(item_id) REFERENCES dbo.items(item_id));');

IF OBJECT_ID('dbo.pvp_bot_loadout_slots', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_bot_loadout_slots (
    bot_profile_id UNIQUEIDENTIFIER NOT NULL, slot_no TINYINT NOT NULL, item_id UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_pvp_bot_loadout_slots PRIMARY KEY(bot_profile_id, slot_no),
    CONSTRAINT CK_pvp_bot_loadout_slots_slot CHECK(slot_no BETWEEN 1 AND 2),
    CONSTRAINT UQ_pvp_bot_loadout_slots_item UNIQUE(bot_profile_id, item_id),
    FOREIGN KEY(bot_profile_id) REFERENCES dbo.pvp_bot_profiles(bot_profile_id), FOREIGN KEY(item_id) REFERENCES dbo.items(item_id));');

IF OBJECT_ID('dbo.pvp_match_loadout_slots', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_match_loadout_slots (
    pvp_match_loadout_slot_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_loadout_slots_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL, match_player_id UNIQUEIDENTIFIER NOT NULL, slot_no TINYINT NOT NULL, item_id UNIQUEIDENTIFIER NOT NULL,
    effect_code VARCHAR(30) NOT NULL, target_code VARCHAR(20) NOT NULL, magnitude_bps INT NOT NULL, duration_ms INT NOT NULL,
    cooldown_ms INT NOT NULL, asset_key NVARCHAR(300) NOT NULL, used_at DATETIME2(3) NULL,
    CONSTRAINT CK_pvp_match_loadout_slots_slot CHECK(slot_no BETWEEN 1 AND 2),
    CONSTRAINT CK_pvp_match_loadout_slots_target CHECK(target_code IN (''self'', ''opponent'')),
    CONSTRAINT UQ_pvp_match_loadout_slots_player_slot UNIQUE(match_player_id, slot_no),
    FOREIGN KEY(match_id) REFERENCES dbo.pvp_matches(match_id), FOREIGN KEY(match_player_id) REFERENCES dbo.pvp_match_players(match_player_id),
    FOREIGN KEY(item_id) REFERENCES dbo.items(item_id));');

IF OBJECT_ID('dbo.pvp_match_item_actions', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_match_item_actions (
    pvp_match_item_action_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_item_actions_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL, actor_match_player_id UNIQUEIDENTIFIER NOT NULL, target_match_player_id UNIQUEIDENTIFIER NULL,
    match_loadout_slot_id UNIQUEIDENTIFIER NOT NULL, client_action_id UNIQUEIDENTIFIER NOT NULL, result_code VARCHAR(20) NOT NULL,
    effect_code VARCHAR(30) NOT NULL, created_at DATETIME2(3) NOT NULL CONSTRAINT DF_pvp_match_item_actions_created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_match_item_actions_result CHECK(result_code IN (''applied'', ''blocked'', ''cleansed'')),
    CONSTRAINT UQ_pvp_match_item_actions_idempotency UNIQUE(actor_match_player_id, client_action_id),
    FOREIGN KEY(match_id) REFERENCES dbo.pvp_matches(match_id), FOREIGN KEY(actor_match_player_id) REFERENCES dbo.pvp_match_players(match_player_id),
    FOREIGN KEY(target_match_player_id) REFERENCES dbo.pvp_match_players(match_player_id), FOREIGN KEY(match_loadout_slot_id) REFERENCES dbo.pvp_match_loadout_slots(pvp_match_loadout_slot_id));');

IF OBJECT_ID('dbo.pvp_match_effects', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_match_effects (
    pvp_match_effect_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_effects_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL, target_match_player_id UNIQUEIDENTIFIER NOT NULL, source_match_player_id UNIQUEIDENTIFIER NULL,
    source_item_action_id UNIQUEIDENTIFIER NULL, effect_code VARCHAR(30) NOT NULL, effect_kind_code VARCHAR(20) NOT NULL,
    magnitude_bps INT NOT NULL, status_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_match_effects_status DEFAULT ''active'',
    starts_at DATETIME2(3) NOT NULL, ends_at DATETIME2(3) NOT NULL, consumed_at DATETIME2(3) NULL,
    created_at DATETIME2(3) NOT NULL CONSTRAINT DF_pvp_match_effects_created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_match_effects_kind CHECK(effect_kind_code IN (''buff'', ''debuff'', ''shield'', ''passive'')),
    CONSTRAINT CK_pvp_match_effects_status CHECK(status_code IN (''active'', ''expired'', ''consumed'', ''cleansed'')),
    CONSTRAINT CK_pvp_match_effects_values CHECK(magnitude_bps >= 0 AND ends_at >= starts_at),
    FOREIGN KEY(match_id) REFERENCES dbo.pvp_matches(match_id), FOREIGN KEY(target_match_player_id) REFERENCES dbo.pvp_match_players(match_player_id),
    FOREIGN KEY(source_match_player_id) REFERENCES dbo.pvp_match_players(match_player_id), FOREIGN KEY(source_item_action_id) REFERENCES dbo.pvp_match_item_actions(pvp_match_item_action_id));');

IF OBJECT_ID('dbo.pvp_spirit_speed_rules', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_spirit_speed_rules (
    affinity_code VARCHAR(30) NOT NULL PRIMARY KEY, start_minute INT NOT NULL, end_minute INT NOT NULL, bonus_bps INT NOT NULL,
    time_zone_code VARCHAR(50) NOT NULL CONSTRAINT DF_pvp_spirit_speed_rules_timezone DEFAULT ''Asia/Ho_Chi_Minh'',
    is_active BIT NOT NULL CONSTRAINT DF_pvp_spirit_speed_rules_active DEFAULT 1,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_spirit_speed_rules_updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_spirit_speed_rules_values CHECK(start_minute BETWEEN 0 AND 1439 AND end_minute BETWEEN 0 AND 1439 AND bonus_bps >= 0));');

IF OBJECT_ID('dbo.pvp_rank_tiers', 'U') IS NULL
EXEC(N'CREATE TABLE dbo.pvp_rank_tiers (
    tier_code VARCHAR(30) NOT NULL PRIMARY KEY, display_name NVARCHAR(80) NOT NULL, min_mmr INT NOT NULL UNIQUE,
    sort_order SMALLINT NOT NULL UNIQUE, asset_key NVARCHAR(300) NOT NULL, color_hex CHAR(7) NOT NULL,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_rank_tiers_active DEFAULT 1);');

/* Multiple pet definitions may share one PvP affinity. Remove the old
   incorrectly unique index before backfilling affinity codes. */
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_pets_pvp_affinity_code' AND object_id = OBJECT_ID('dbo.pets'))
    EXEC(N'DROP INDEX UX_pets_pvp_affinity_code ON dbo.pets;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pets_pvp_affinity_code' AND object_id = OBJECT_ID('dbo.pets'))
    EXEC(N'CREATE INDEX IX_pets_pvp_affinity_code ON dbo.pets(pvp_affinity_code) WHERE pvp_affinity_code IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pvp_match_effects_active_due' AND object_id = OBJECT_ID('dbo.pvp_match_effects'))
    EXEC(N'CREATE INDEX IX_pvp_match_effects_active_due ON dbo.pvp_match_effects(match_id, target_match_player_id, status_code, ends_at);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_pvp_match_item_actions_match_created' AND object_id = OBJECT_ID('dbo.pvp_match_item_actions'))
    EXEC(N'CREATE INDEX IX_pvp_match_item_actions_match_created ON dbo.pvp_match_item_actions(match_id, created_at);');

EXEC(N'
UPDATE dbo.pets SET pvp_affinity_code = ''sprout'' WHERE pvp_affinity_code IS NULL AND LOWER(pet_name) IN (''stater'', ''starter'', N''mầm non'');
UPDATE dbo.pets SET pvp_affinity_code = ''warm_sun'' WHERE pvp_affinity_code IS NULL AND pet_name LIKE N''%Nắng Ấm%'';
UPDATE dbo.pets SET pvp_affinity_code = ''dawn'' WHERE pvp_affinity_code IS NULL AND pet_name LIKE N''%Bình Minh%'';
UPDATE dbo.pets SET pvp_affinity_code = ''moonlight'' WHERE pvp_affinity_code IS NULL AND pet_name LIKE N''%Ánh Trăng%'';
IF NOT EXISTS (SELECT 1 FROM dbo.item_types WHERE item_type_name=N''Pet Consumable'') INSERT dbo.item_types(item_type_name, IsActive) VALUES(N''Pet Consumable'',1);
IF NOT EXISTS (SELECT 1 FROM dbo.item_types WHERE item_type_name=N''PvP Buff'') INSERT dbo.item_types(item_type_name, IsActive) VALUES(N''PvP Buff'',1);
IF NOT EXISTS (SELECT 1 FROM dbo.item_types WHERE item_type_name=N''PvP Debuff'') INSERT dbo.item_types(item_type_name, IsActive) VALUES(N''PvP Debuff'',1);
IF NOT EXISTS (SELECT 1 FROM dbo.item_types WHERE item_type_name=N''PvP Utility'') INSERT dbo.item_types(item_type_name, IsActive) VALUES(N''PvP Utility'',1);
DECLARE @b UNIQUEIDENTIFIER=(SELECT item_type_id FROM dbo.item_types WHERE item_type_name=N''PvP Buff'');
DECLARE @d UNIQUEIDENTIFIER=(SELECT item_type_id FROM dbo.item_types WHERE item_type_name=N''PvP Debuff'');
DECLARE @u UNIQUEIDENTIFIER=(SELECT item_type_id FROM dbo.item_types WHERE item_type_name=N''PvP Utility'');
IF NOT EXISTS(SELECT 1 FROM dbo.items WHERE effect_type_code=''pvp_speed_up'') INSERT dbo.items(item_name,img_url,item_type_id,effect_type_code,effect_value,description,is_active) VALUES(N''Bùa Gió Nhanh'',N''Assets/Mobile/PVP/Items/pvp_speed_up.png'',@b,''pvp_speed_up'',1500,N''Tăng 15% tốc độ trong 5 giây.'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.items WHERE effect_type_code=''pvp_speed_down'') INSERT dbo.items(item_name,img_url,item_type_id,effect_type_code,effect_value,description,is_active) VALUES(N''Bẫy Sương Chậm'',N''Assets/Mobile/PVP/Items/pvp_speed_down.png'',@d,''pvp_speed_down'',1500,N''Giảm 15% tốc độ đối thủ trong 5 giây.'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.items WHERE effect_type_code=''pvp_cleanse'') INSERT dbo.items(item_name,img_url,item_type_id,effect_type_code,effect_value,description,is_active) VALUES(N''Giọt Sương Thanh Tẩy'',N''Assets/Mobile/PVP/Items/pvp_cleanse.png'',@u,''pvp_cleanse'',0,N''Xóa debuff tốc độ.'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.items WHERE effect_type_code=''pvp_shield'') INSERT dbo.items(item_name,img_url,item_type_id,effect_type_code,effect_value,description,is_active) VALUES(N''Khiên Lá Lumina'',N''Assets/Mobile/PVP/Items/pvp_shield.png'',@u,''pvp_shield'',0,N''Chặn một debuff trong 8 giây.'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_item_effect_definitions WHERE effect_code=''pvp_speed_up'') INSERT dbo.pvp_item_effect_definitions(item_id,effect_code,target_code,magnitude_bps,duration_ms,cooldown_ms,asset_key) SELECT item_id,''pvp_speed_up'',''self'',1500,5000,5000,N''Assets/Mobile/PVP/Items/pvp_speed_up.png'' FROM dbo.items WHERE effect_type_code=''pvp_speed_up'';
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_item_effect_definitions WHERE effect_code=''pvp_speed_down'') INSERT dbo.pvp_item_effect_definitions(item_id,effect_code,target_code,magnitude_bps,duration_ms,cooldown_ms,asset_key) SELECT item_id,''pvp_speed_down'',''opponent'',1500,5000,10000,N''Assets/Mobile/PVP/Items/pvp_speed_down.png'' FROM dbo.items WHERE effect_type_code=''pvp_speed_down'';
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_item_effect_definitions WHERE effect_code=''pvp_cleanse'') INSERT dbo.pvp_item_effect_definitions(item_id,effect_code,target_code,magnitude_bps,duration_ms,cooldown_ms,asset_key) SELECT item_id,''pvp_cleanse'',''self'',0,0,5000,N''Assets/Mobile/PVP/Items/pvp_cleanse.png'' FROM dbo.items WHERE effect_type_code=''pvp_cleanse'';
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_item_effect_definitions WHERE effect_code=''pvp_shield'') INSERT dbo.pvp_item_effect_definitions(item_id,effect_code,target_code,magnitude_bps,duration_ms,cooldown_ms,asset_key) SELECT item_id,''pvp_shield'',''self'',0,8000,15000,N''Assets/Mobile/PVP/Items/pvp_shield.png'' FROM dbo.items WHERE effect_type_code=''pvp_shield'';
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_spirit_speed_rules WHERE affinity_code=''sprout'') INSERT dbo.pvp_spirit_speed_rules VALUES(''sprout'',0,1439,0,''Asia/Ho_Chi_Minh'',1,SYSUTCDATETIME());
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_spirit_speed_rules WHERE affinity_code=''dawn'') INSERT dbo.pvp_spirit_speed_rules VALUES(''dawn'',360,719,1000,''Asia/Ho_Chi_Minh'',1,SYSUTCDATETIME());
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_spirit_speed_rules WHERE affinity_code=''warm_sun'') INSERT dbo.pvp_spirit_speed_rules VALUES(''warm_sun'',720,1079,1000,''Asia/Ho_Chi_Minh'',1,SYSUTCDATETIME());
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_spirit_speed_rules WHERE affinity_code=''moonlight'') INSERT dbo.pvp_spirit_speed_rules VALUES(''moonlight'',1080,359,1000,''Asia/Ho_Chi_Minh'',1,SYSUTCDATETIME());
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_rank_tiers WHERE tier_code=''mam_sang'') INSERT dbo.pvp_rank_tiers VALUES(''mam_sang'',N''Mầm Sáng'',-2147483648,1,N''Assets/Mobile/PVP/Rank/mam_sang.png'',''#91B95A'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_rank_tiers WHERE tier_code=''choi_sang'') INSERT dbo.pvp_rank_tiers VALUES(''choi_sang'',N''Chồi Sáng'',1100,2,N''Assets/Mobile/PVP/Rank/choi_sang.png'',''#70C987'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_rank_tiers WHERE tier_code=''tan_sang'') INSERT dbo.pvp_rank_tiers VALUES(''tan_sang'',N''Tán Sáng'',1300,3,N''Assets/Mobile/PVP/Rank/tan_sang.png'',''#43B9A9'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_rank_tiers WHERE tier_code=''linh_quang'') INSERT dbo.pvp_rank_tiers VALUES(''linh_quang'',N''Linh Quang'',1500,4,N''Assets/Mobile/PVP/Rank/linh_quang.png'',''#5C8DE8'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_rank_tiers WHERE tier_code=''tinh_tu'') INSERT dbo.pvp_rank_tiers VALUES(''tinh_tu'',N''Tinh Tú'',1700,5,N''Assets/Mobile/PVP/Rank/tinh_tu.png'',''#9A6BE8'',1);
IF NOT EXISTS(SELECT 1 FROM dbo.pvp_rank_tiers WHERE tier_code=''lumina'') INSERT dbo.pvp_rank_tiers VALUES(''lumina'',N''Lumina'',1900,6,N''Assets/Mobile/PVP/Rank/lumina.png'',''#F3C969'',1);
');

-- Shared daily/PvP sensor pipeline. Every column/index operation is compiled
-- only after its prerequisite exists, so this block is safe on older schemas.
IF COL_LENGTH('dbo.pvp_step_sessions', 'purpose_code') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD purpose_code VARCHAR(10) NOT NULL CONSTRAINT DF_pvp_step_sessions_purpose DEFAULT ''pvp'';');
IF COL_LENGTH('dbo.pvp_step_sessions', 'sensor_mode_code') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD sensor_mode_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_step_sessions_sensor_mode DEFAULT ''detector'';');
IF COL_LENGTH('dbo.pvp_step_sessions', 'last_sensor_total') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD last_sensor_total BIGINT NULL;');
IF COL_LENGTH('dbo.pvp_step_sessions', 'last_recorded_at') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD last_recorded_at DATETIME2(3) NULL;');
IF COL_LENGTH('dbo.pvp_step_sessions', 'closed_reason') IS NULL
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD closed_reason NVARCHAR(100) NULL;');
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.pvp_step_sessions')
      AND name = 'match_id' AND is_nullable = 0
)
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ALTER COLUMN match_id UNIQUEIDENTIFIER NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_pvp_step_sessions_purpose')
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD CONSTRAINT CK_pvp_step_sessions_purpose CHECK (purpose_code IN (''daily'', ''pvp''));');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_pvp_step_sessions_sensor_mode')
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD CONSTRAINT CK_pvp_step_sessions_sensor_mode CHECK (sensor_mode_code IN (''detector'', ''counter''));');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_pvp_step_sessions_match_purpose')
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions ADD CONSTRAINT CK_pvp_step_sessions_match_purpose CHECK ((purpose_code = ''daily'' AND match_id IS NULL) OR (purpose_code = ''pvp'' AND match_id IS NOT NULL));');

IF OBJECT_ID('dbo.step_sensor_batches', 'U') IS NULL
    EXEC(N'CREATE TABLE dbo.step_sensor_batches (
        step_sensor_batch_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_step_sensor_batches_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        step_session_id UNIQUEIDENTIFIER NOT NULL,
        sequence INT NOT NULL,
        payload_hash CHAR(64) NOT NULL,
        attestation_status VARCHAR(30) NOT NULL,
        package_name NVARCHAR(200) NULL,
        verdict_timestamp DATETIME2(3) NULL,
        verdict_json NVARCHAR(MAX) NULL,
        accepted_steps INT NOT NULL CONSTRAINT DF_step_sensor_batches_accepted DEFAULT 0,
        rejected_steps INT NOT NULL CONSTRAINT DF_step_sensor_batches_rejected DEFAULT 0,
        suspicious_steps INT NOT NULL CONSTRAINT DF_step_sensor_batches_suspicious DEFAULT 0,
        received_at DATETIME2(3) NOT NULL CONSTRAINT DF_step_sensor_batches_received DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_step_sensor_batches_counts CHECK (accepted_steps >= 0 AND rejected_steps >= 0 AND suspicious_steps >= 0),
        CONSTRAINT FK_step_sensor_batches_session FOREIGN KEY(step_session_id) REFERENCES dbo.pvp_step_sessions(step_session_id)
    );');

IF COL_LENGTH('dbo.validated_step_records', 'batch_id') IS NULL
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD batch_id UNIQUEIDENTIFIER NULL;');
IF COL_LENGTH('dbo.validated_step_records', 'event_index') IS NULL
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD event_index INT NULL;');
IF COL_LENGTH('dbo.validated_step_records', 'sensor_mode_code') IS NULL
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD sensor_mode_code VARCHAR(20) NOT NULL CONSTRAINT DF_validated_step_records_sensor_mode DEFAULT ''detector'';');
IF COL_LENGTH('dbo.validated_step_records', 'interval_started_at') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD interval_started_at DATETIME2(3) NULL;');
    EXEC(N'UPDATE dbo.validated_step_records SET interval_started_at = recorded_at WHERE interval_started_at IS NULL;');
    EXEC(N'ALTER TABLE dbo.validated_step_records ALTER COLUMN interval_started_at DATETIME2(3) NOT NULL;');
END;
IF COL_LENGTH('dbo.validated_step_records', 'sensor_start_total') IS NULL
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD sensor_start_total BIGINT NULL;');
IF COL_LENGTH('dbo.validated_step_records', 'sensor_end_total') IS NULL
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD sensor_end_total BIGINT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_validated_step_records_batch')
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD CONSTRAINT FK_validated_step_records_batch FOREIGN KEY(batch_id) REFERENCES dbo.step_sensor_batches(step_sensor_batch_id);');

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UX_pvp_step_sessions_match_user' AND parent_object_id = OBJECT_ID('dbo.pvp_step_sessions'))
    EXEC(N'ALTER TABLE dbo.pvp_step_sessions DROP CONSTRAINT UX_pvp_step_sessions_match_user;');
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_pvp_step_sessions_match_user' AND object_id = OBJECT_ID('dbo.pvp_step_sessions'))
    EXEC(N'DROP INDEX UX_pvp_step_sessions_match_user ON dbo.pvp_step_sessions;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_pvp_step_sessions_match_user' AND object_id = OBJECT_ID('dbo.pvp_step_sessions'))
    EXEC(N'CREATE UNIQUE INDEX UX_pvp_step_sessions_match_user ON dbo.pvp_step_sessions(match_id, user_id) WHERE match_id IS NOT NULL;');

EXEC(N';WITH active_sessions AS (
    SELECT step_session_id, ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY created_at DESC, step_session_id DESC) AS rn
    FROM dbo.pvp_step_sessions WHERE status_code = ''active''
) UPDATE s SET status_code = ''closed'', closed_reason = ''upgrade_deduplicated''
FROM dbo.pvp_step_sessions s INNER JOIN active_sessions a ON a.step_session_id = s.step_session_id
WHERE a.rn > 1;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_pvp_step_sessions_active_user' AND object_id = OBJECT_ID('dbo.pvp_step_sessions'))
    EXEC(N'CREATE UNIQUE INDEX UX_pvp_step_sessions_active_user ON dbo.pvp_step_sessions(user_id) WHERE status_code = ''active'';');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_step_sensor_batches_session_sequence' AND object_id = OBJECT_ID('dbo.step_sensor_batches'))
    EXEC(N'CREATE UNIQUE INDEX UX_step_sensor_batches_session_sequence ON dbo.step_sensor_batches(step_session_id, sequence);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_step_sensor_batches_session_hash' AND object_id = OBJECT_ID('dbo.step_sensor_batches'))
    EXEC(N'CREATE UNIQUE INDEX UX_step_sensor_batches_session_hash ON dbo.step_sensor_batches(step_session_id, payload_hash);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_validated_step_records_batch_event' AND object_id = OBJECT_ID('dbo.validated_step_records'))
    EXEC(N'CREATE UNIQUE INDEX UX_validated_step_records_batch_event ON dbo.validated_step_records(batch_id, event_index) WHERE batch_id IS NOT NULL;');

IF COL_LENGTH('dbo.step_sensor_batches', 'evidence_version') IS NULL
    EXEC(N'ALTER TABLE dbo.step_sensor_batches ADD evidence_version INT NOT NULL CONSTRAINT DF_step_sensor_batches_evidence_version DEFAULT 1;');
IF COL_LENGTH('dbo.step_sensor_batches', 'motion_score') IS NULL
    EXEC(N'ALTER TABLE dbo.step_sensor_batches ADD motion_score INT NOT NULL CONSTRAINT DF_step_sensor_batches_motion_score DEFAULT 0;');
IF COL_LENGTH('dbo.step_sensor_batches', 'motion_status') IS NULL
    EXEC(N'ALTER TABLE dbo.step_sensor_batches ADD motion_status VARCHAR(20) NOT NULL CONSTRAINT DF_step_sensor_batches_motion_status DEFAULT ''unavailable'';');
IF COL_LENGTH('dbo.step_sensor_batches', 'motion_reasons_json') IS NULL
    EXEC(N'ALTER TABLE dbo.step_sensor_batches ADD motion_reasons_json NVARCHAR(MAX) NOT NULL CONSTRAINT DF_step_sensor_batches_motion_reasons DEFAULT N''[]'';');
IF COL_LENGTH('dbo.step_sensor_batches', 'degraded_evidence') IS NULL
    EXEC(N'ALTER TABLE dbo.step_sensor_batches ADD degraded_evidence BIT NOT NULL CONSTRAINT DF_step_sensor_batches_degraded DEFAULT 0;');

IF COL_LENGTH('dbo.validated_step_records', 'motion_score') IS NULL
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD motion_score INT NOT NULL CONSTRAINT DF_validated_step_records_motion_score DEFAULT 0;');
IF COL_LENGTH('dbo.validated_step_records', 'motion_status') IS NULL
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD motion_status VARCHAR(20) NOT NULL CONSTRAINT DF_validated_step_records_motion_status DEFAULT ''unavailable'';');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_step_sensor_batches_motion_score')
    EXEC(N'ALTER TABLE dbo.step_sensor_batches ADD CONSTRAINT CK_step_sensor_batches_motion_score CHECK (motion_score BETWEEN 0 AND 100);');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_step_sensor_batches_motion_status')
    EXEC(N'ALTER TABLE dbo.step_sensor_batches ADD CONSTRAINT CK_step_sensor_batches_motion_status CHECK (motion_status IN (''accepted'', ''suspicious'', ''rejected'', ''unavailable''));');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_validated_step_records_motion_score')
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD CONSTRAINT CK_validated_step_records_motion_score CHECK (motion_score BETWEEN 0 AND 100);');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_validated_step_records_motion_status')
    EXEC(N'ALTER TABLE dbo.validated_step_records ADD CONSTRAINT CK_validated_step_records_motion_status CHECK (motion_status IN (''accepted'', ''suspicious'', ''rejected'', ''unavailable''));');

IF OBJECT_ID('dbo.step_motion_evidence_windows', 'U') IS NULL
    EXEC(N'CREATE TABLE dbo.step_motion_evidence_windows (
        step_motion_evidence_window_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_step_motion_windows_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
        batch_id UNIQUEIDENTIFIER NOT NULL,
        window_index SMALLINT NOT NULL,
        window_started_at DATETIME2(3) NOT NULL,
        window_ended_at DATETIME2(3) NOT NULL,
        sample_count SMALLINT NOT NULL,
        accelerometer_source VARCHAR(20) NOT NULL,
        gyroscope_available BIT NOT NULL,
        activity_available BIT NOT NULL,
        acceleration_rms_milli INT NOT NULL,
        acceleration_peak_milli INT NOT NULL,
        jerk_rms_milli INT NOT NULL,
        gyroscope_rms_milli INT NULL,
        gyroscope_peak_milli INT NULL,
        orientation_delta_millidegrees INT NULL,
        dominant_frequency_millihz INT NOT NULL,
        periodicity_bps INT NOT NULL,
        gait_cycle_count SMALLINT NOT NULL,
        activity_code VARCHAR(20) NOT NULL,
        activity_confidence TINYINT NOT NULL,
        motion_score TINYINT NOT NULL,
        classification VARCHAR(20) NOT NULL,
        reason_codes NVARCHAR(500) NOT NULL CONSTRAINT DF_step_motion_windows_reasons DEFAULT N''[]'',
        CONSTRAINT CK_step_motion_windows_time CHECK (window_ended_at > window_started_at),
        CONSTRAINT CK_step_motion_windows_samples CHECK (sample_count >= 0),
        CONSTRAINT CK_step_motion_windows_periodicity CHECK (periodicity_bps BETWEEN 0 AND 10000),
        CONSTRAINT CK_step_motion_windows_activity_confidence CHECK (activity_confidence BETWEEN 0 AND 100),
        CONSTRAINT CK_step_motion_windows_motion_score CHECK (motion_score BETWEEN 0 AND 100),
        CONSTRAINT CK_step_motion_windows_classification CHECK (classification IN (''accepted'', ''suspicious'', ''rejected'')),
        CONSTRAINT FK_step_motion_windows_batch FOREIGN KEY(batch_id)
            REFERENCES dbo.step_sensor_batches(step_sensor_batch_id) ON DELETE CASCADE
    );');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_step_motion_windows_batch_index' AND object_id = OBJECT_ID('dbo.step_motion_evidence_windows'))
    EXEC(N'CREATE UNIQUE INDEX UX_step_motion_windows_batch_index ON dbo.step_motion_evidence_windows(batch_id, window_index);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_step_motion_windows_classification_started' AND object_id = OBJECT_ID('dbo.step_motion_evidence_windows'))
    EXEC(N'CREATE INDEX IX_step_motion_windows_classification_started ON dbo.step_motion_evidence_windows(classification, window_started_at);');

COMMIT TRANSACTION;
GO
