/*
Additive production schema upgrade for Walkamon Step Tracking contract v3.

Safety properties:
- idempotent object/column/index checks;
- one transaction with XACT_ABORT;
- no row deletion or step/reward mutation;
- existing v2 rows remain valid through nullable/defaulted additions.
*/
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.pvp_step_sessions', 'U') IS NULL
    THROW 51000, 'Required table dbo.pvp_step_sessions is missing.', 1;
IF OBJECT_ID('dbo.step_sensor_batches', 'U') IS NULL
    THROW 51000, 'Required table dbo.step_sensor_batches is missing.', 1;
IF OBJECT_ID('dbo.step_motion_evidence_windows', 'U') IS NULL
    THROW 51000, 'Required table dbo.step_motion_evidence_windows is missing.', 1;
IF OBJECT_ID('dbo.validated_step_records', 'U') IS NULL
    THROW 51000, 'Required table dbo.validated_step_records is missing.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.pvp_step_sessions', 'contract_version') IS NULL
        EXEC(N'ALTER TABLE dbo.pvp_step_sessions
            ADD contract_version INT NOT NULL
                CONSTRAINT DF_pvp_step_sessions_contract_version DEFAULT 2;');

    IF COL_LENGTH('dbo.pvp_step_sessions', 'capture_metadata_json') IS NULL
        EXEC(N'ALTER TABLE dbo.pvp_step_sessions
            ADD capture_metadata_json NVARCHAR(MAX) NULL;');

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_pvp_step_sessions_sensor_mode'
          AND parent_object_id = OBJECT_ID('dbo.pvp_step_sessions')
    )
        EXEC(N'ALTER TABLE dbo.pvp_step_sessions
            DROP CONSTRAINT CK_pvp_step_sessions_sensor_mode;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_pvp_step_sessions_sensor_mode'
          AND parent_object_id = OBJECT_ID('dbo.pvp_step_sessions')
    )
        EXEC(N'ALTER TABLE dbo.pvp_step_sessions
            ADD CONSTRAINT CK_pvp_step_sessions_sensor_mode
            CHECK (sensor_mode_code IN (''detector'', ''counter'', ''dual''));');

    IF COL_LENGTH('dbo.step_sensor_batches', 'reconciliation_status') IS NULL
        EXEC(N'ALTER TABLE dbo.step_sensor_batches
            ADD reconciliation_status VARCHAR(30) NOT NULL
                CONSTRAINT DF_step_sensor_batches_reconciliation_status DEFAULT ''unavailable'';');

    IF COL_LENGTH('dbo.step_sensor_batches', 'reconciliation_reason') IS NULL
        EXEC(N'ALTER TABLE dbo.step_sensor_batches
            ADD reconciliation_reason NVARCHAR(200) NULL;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_step_sensor_batches_reconciliation_status'
          AND parent_object_id = OBJECT_ID('dbo.step_sensor_batches')
    )
        EXEC(N'ALTER TABLE dbo.step_sensor_batches
            ADD CONSTRAINT CK_step_sensor_batches_reconciliation_status
            CHECK (reconciliation_status IN (
                ''unavailable'',
                ''pending_reconciliation'',
                ''accepted'',
                ''suspicious'',
                ''rejected''
            ));');

    IF COL_LENGTH('dbo.step_motion_evidence_windows', 'boot_session_id') IS NULL
        EXEC(N'ALTER TABLE dbo.step_motion_evidence_windows
            ADD boot_session_id UNIQUEIDENTIFIER NULL;');

    IF COL_LENGTH('dbo.step_motion_evidence_windows', 'window_start_elapsed_realtime_ns') IS NULL
        EXEC(N'ALTER TABLE dbo.step_motion_evidence_windows
            ADD window_start_elapsed_realtime_ns BIGINT NULL;');

    IF COL_LENGTH('dbo.step_motion_evidence_windows', 'window_end_elapsed_realtime_ns') IS NULL
        EXEC(N'ALTER TABLE dbo.step_motion_evidence_windows
            ADD window_end_elapsed_realtime_ns BIGINT NULL;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_step_motion_windows_elapsed'
          AND parent_object_id = OBJECT_ID('dbo.step_motion_evidence_windows')
    )
        EXEC(N'ALTER TABLE dbo.step_motion_evidence_windows
            ADD CONSTRAINT CK_step_motion_windows_elapsed CHECK (
                (window_start_elapsed_realtime_ns IS NULL
                    AND window_end_elapsed_realtime_ns IS NULL)
                OR
                (window_start_elapsed_realtime_ns > 0
                    AND window_end_elapsed_realtime_ns > window_start_elapsed_realtime_ns)
            );');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_step_motion_windows_boot_elapsed'
          AND object_id = OBJECT_ID('dbo.step_motion_evidence_windows')
    )
        EXEC(N'CREATE INDEX IX_step_motion_windows_boot_elapsed
            ON dbo.step_motion_evidence_windows(
                boot_session_id,
                window_start_elapsed_realtime_ns,
                window_end_elapsed_realtime_ns
            );');

    IF OBJECT_ID('dbo.step_counter_evidence_samples', 'U') IS NULL
        EXEC(N'CREATE TABLE dbo.step_counter_evidence_samples (
            counter_sample_id UNIQUEIDENTIFIER NOT NULL
                CONSTRAINT DF_step_counter_samples_id DEFAULT NEWSEQUENTIALID()
                PRIMARY KEY,
            batch_id UNIQUEIDENTIFIER NOT NULL,
            sample_index SMALLINT NOT NULL,
            client_sample_id UNIQUEIDENTIFIER NOT NULL,
            boot_session_id UNIQUEIDENTIFIER NOT NULL,
            sensor_elapsed_realtime_ns BIGINT NOT NULL,
            observed_at DATETIME2(3) NOT NULL,
            counter_total BIGINT NOT NULL,
            CONSTRAINT CK_step_counter_samples_index CHECK (sample_index >= 0),
            CONSTRAINT CK_step_counter_samples_elapsed CHECK (sensor_elapsed_realtime_ns >= 0),
            CONSTRAINT CK_step_counter_samples_total CHECK (counter_total >= 0),
            CONSTRAINT FK_step_counter_samples_batch FOREIGN KEY(batch_id)
                REFERENCES dbo.step_sensor_batches(step_sensor_batch_id)
                ON DELETE CASCADE
        );');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_step_counter_samples_batch_index'
          AND object_id = OBJECT_ID('dbo.step_counter_evidence_samples')
    )
        EXEC(N'CREATE UNIQUE INDEX UX_step_counter_samples_batch_index
            ON dbo.step_counter_evidence_samples(batch_id, sample_index);');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_step_counter_samples_client_id'
          AND object_id = OBJECT_ID('dbo.step_counter_evidence_samples')
    )
        EXEC(N'CREATE UNIQUE INDEX UX_step_counter_samples_client_id
            ON dbo.step_counter_evidence_samples(client_sample_id);');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'IX_step_counter_samples_boot_elapsed'
          AND object_id = OBJECT_ID('dbo.step_counter_evidence_samples')
    )
        EXEC(N'CREATE INDEX IX_step_counter_samples_boot_elapsed
            ON dbo.step_counter_evidence_samples(
                boot_session_id,
                sensor_elapsed_realtime_ns
            );');

    IF COL_LENGTH('dbo.validated_step_records', 'client_event_id') IS NULL
        EXEC(N'ALTER TABLE dbo.validated_step_records
            ADD client_event_id UNIQUEIDENTIFIER NULL;');

    IF COL_LENGTH('dbo.validated_step_records', 'boot_session_id') IS NULL
        EXEC(N'ALTER TABLE dbo.validated_step_records
            ADD boot_session_id UNIQUEIDENTIFIER NULL;');

    IF COL_LENGTH('dbo.validated_step_records', 'sensor_elapsed_realtime_ns') IS NULL
        EXEC(N'ALTER TABLE dbo.validated_step_records
            ADD sensor_elapsed_realtime_ns BIGINT NULL;');

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_validated_step_records_status'
          AND parent_object_id = OBJECT_ID('dbo.validated_step_records')
    )
        EXEC(N'ALTER TABLE dbo.validated_step_records
            DROP CONSTRAINT CK_validated_step_records_status;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE name = 'CK_validated_step_records_status'
          AND parent_object_id = OBJECT_ID('dbo.validated_step_records')
    )
        EXEC(N'ALTER TABLE dbo.validated_step_records
            ADD CONSTRAINT CK_validated_step_records_status
            CHECK (validation_status IN (
                ''accepted'',
                ''pending'',
                ''rejected'',
                ''suspicious''
            ));');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = 'UX_validated_step_records_session_client_event'
          AND object_id = OBJECT_ID('dbo.validated_step_records')
    )
        EXEC(N'CREATE UNIQUE INDEX UX_validated_step_records_session_client_event
            ON dbo.validated_step_records(step_session_id, client_event_id)
            WHERE client_event_id IS NOT NULL;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
