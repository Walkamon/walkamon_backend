SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.items', 'content_code') IS NULL ALTER TABLE dbo.items ADD content_code VARCHAR(80) NULL;
IF COL_LENGTH('dbo.items', 'source_language_code') IS NULL ALTER TABLE dbo.items ADD source_language_code VARCHAR(5) NULL;
IF COL_LENGTH('dbo.items', 'item_name_vi') IS NULL ALTER TABLE dbo.items ADD item_name_vi NVARCHAR(80) NULL;
IF COL_LENGTH('dbo.items', 'item_name_en') IS NULL ALTER TABLE dbo.items ADD item_name_en NVARCHAR(80) NULL;
IF COL_LENGTH('dbo.items', 'description_vi') IS NULL ALTER TABLE dbo.items ADD description_vi NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.items', 'description_en') IS NULL ALTER TABLE dbo.items ADD description_en NVARCHAR(300) NULL;
IF COL_LENGTH('dbo.items', 'translation_status_code') IS NULL ALTER TABLE dbo.items ADD translation_status_code VARCHAR(20) NULL;
IF COL_LENGTH('dbo.items', 'translation_source_hash') IS NULL ALTER TABLE dbo.items ADD translation_source_hash VARCHAR(64) NULL;
IF COL_LENGTH('dbo.items', 'translated_at') IS NULL ALTER TABLE dbo.items ADD translated_at DATETIME2(0) NULL;

IF COL_LENGTH('dbo.notifications', 'content_code') IS NULL ALTER TABLE dbo.notifications ADD content_code VARCHAR(80) NULL;
IF COL_LENGTH('dbo.notifications', 'params_json') IS NULL ALTER TABLE dbo.notifications ADD params_json NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.notifications', 'source_language_code') IS NULL ALTER TABLE dbo.notifications ADD source_language_code VARCHAR(5) NULL;
IF COL_LENGTH('dbo.notifications', 'title_vi') IS NULL ALTER TABLE dbo.notifications ADD title_vi NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.notifications', 'title_en') IS NULL ALTER TABLE dbo.notifications ADD title_en NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.notifications', 'body_vi') IS NULL ALTER TABLE dbo.notifications ADD body_vi NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.notifications', 'body_en') IS NULL ALTER TABLE dbo.notifications ADD body_en NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.notifications', 'translation_status_code') IS NULL ALTER TABLE dbo.notifications ADD translation_status_code VARCHAR(20) NULL;
IF COL_LENGTH('dbo.notifications', 'translation_source_hash') IS NULL ALTER TABLE dbo.notifications ADD translation_source_hash VARCHAR(64) NULL;
IF COL_LENGTH('dbo.notifications', 'translated_at') IS NULL ALTER TABLE dbo.notifications ADD translated_at DATETIME2(0) NULL;

-- Preserve all existing content for old clients and give the new fields a
-- deterministic source/fallback until the translation worker backfills rows.
UPDATE dbo.items
SET item_name_vi = COALESCE(item_name_vi, item_name),
    item_name_en = COALESCE(item_name_en, item_name),
    description_vi = COALESCE(description_vi, description),
    description_en = COALESCE(description_en, description),
    source_language_code = COALESCE(source_language_code, 'vi'),
    translation_status_code = COALESCE(translation_status_code, 'fallback')
WHERE item_name_vi IS NULL OR item_name_en IS NULL OR description_vi IS NULL OR description_en IS NULL;

UPDATE dbo.notifications
SET title_vi = COALESCE(title_vi, title),
    title_en = COALESCE(title_en, title),
    body_vi = COALESCE(body_vi, body),
    body_en = COALESCE(body_en, body),
    source_language_code = COALESCE(source_language_code, 'vi'),
    translation_status_code = COALESCE(translation_status_code, 'fallback')
WHERE title_vi IS NULL OR title_en IS NULL OR body_vi IS NULL OR body_en IS NULL;

COMMIT TRANSACTION;
