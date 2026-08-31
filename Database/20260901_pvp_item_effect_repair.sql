SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Repair installations where the four catalogue items exist but their
-- realtime effect definitions were not seeded (or were left inactive).  The
-- item table remains the source of the item id; all values are deterministic
-- gameplay configuration and this script is safe to rerun.
IF OBJECT_ID('dbo.items', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.pvp_item_effect_definitions', 'U') IS NOT NULL
BEGIN
    DECLARE @effectCode VARCHAR(30);
    DECLARE @targetCode VARCHAR(20);
    DECLARE @magnitudeBps INT;
    DECLARE @durationMs INT;
    DECLARE @cooldownMs INT;
    DECLARE @assetKey NVARCHAR(300);
    DECLARE @itemId UNIQUEIDENTIFIER;
    DECLARE @definitionItemId UNIQUEIDENTIFIER;

    DECLARE effect_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT effect_code, target_code, magnitude_bps, duration_ms, cooldown_ms, asset_key
        FROM (VALUES
            ('pvp_speed_up',   'self',     1500, 5000,  5000, N'Assets/Mobile/PVP/Items/pvp_speed_up.png'),
            ('pvp_speed_down', 'opponent', 1500, 5000, 10000, N'Assets/Mobile/PVP/Items/pvp_speed_down.png'),
            ('pvp_cleanse',    'self',        0,    0,  5000, N'Assets/Mobile/PVP/Items/pvp_cleanse.png'),
            ('pvp_shield',     'self',        0, 8000, 15000, N'Assets/Mobile/PVP/Items/pvp_shield.png')
        ) AS values_table(effect_code, target_code, magnitude_bps, duration_ms, cooldown_ms, asset_key);

    OPEN effect_cursor;
    FETCH NEXT FROM effect_cursor INTO @effectCode, @targetCode, @magnitudeBps, @durationMs, @cooldownMs, @assetKey;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT TOP (1) @itemId = item_id
        FROM dbo.items
        WHERE effect_type_code = @effectCode
          AND is_active = 1
        ORDER BY item_id;

        IF @itemId IS NOT NULL
        BEGIN
            SELECT @definitionItemId = item_id
            FROM dbo.pvp_item_effect_definitions
            WHERE effect_code = @effectCode;

            IF @definitionItemId IS NULL
            BEGIN
                -- UQ(item_id) means only insert if this item is not already
                -- attached to another definition.
                IF NOT EXISTS (SELECT 1 FROM dbo.pvp_item_effect_definitions WHERE item_id = @itemId)
                    INSERT INTO dbo.pvp_item_effect_definitions
                        (item_id, effect_code, target_code, magnitude_bps, duration_ms, cooldown_ms, asset_key, is_active)
                    VALUES
                        (@itemId, @effectCode, @targetCode, @magnitudeBps, @durationMs, @cooldownMs, @assetKey, 1);
            END
            ELSE
            BEGIN
                UPDATE dbo.pvp_item_effect_definitions
                SET item_id = CASE
                        WHEN @definitionItemId <> @itemId
                         AND NOT EXISTS (
                             SELECT 1
                             FROM dbo.pvp_item_effect_definitions
                             WHERE item_id = @itemId
                               AND effect_code <> @effectCode
                         )
                        THEN @itemId
                        ELSE item_id
                    END,
                    target_code = @targetCode,
                    magnitude_bps = @magnitudeBps,
                    duration_ms = @durationMs,
                    cooldown_ms = @cooldownMs,
                    asset_key = @assetKey,
                    is_active = 1,
                    updated_at = SYSUTCDATETIME()
                WHERE effect_code = @effectCode;
            END;
        END;

        SET @itemId = NULL;
        SET @definitionItemId = NULL;
        FETCH NEXT FROM effect_cursor INTO @effectCode, @targetCode, @magnitudeBps, @durationMs, @cooldownMs, @assetKey;
    END;
    CLOSE effect_cursor;
    DEALLOCATE effect_cursor;
END;

COMMIT TRANSACTION;
