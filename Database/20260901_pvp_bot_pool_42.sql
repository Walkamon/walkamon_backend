SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Idempotent PvP bot pool expansion. Fixed IDs make this safe to run repeatedly.
-- New bots have no loadout slots by design; they use the server's calibrated pace.
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000001')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000001', N'Easy Scout 01', NULL, 600, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000002')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000002', N'Easy Scout 02', NULL, 800, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000003')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000003', N'Easy Scout 03', NULL, 1000, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000004')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000004', N'Easy Scout 04', NULL, 1200, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000005')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000005', N'Easy Scout 05', NULL, 1400, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000006')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000006', N'Easy Scout 06', NULL, 1600, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000007')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000007', N'Easy Scout 07', NULL, 1800, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000008')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000008', N'Easy Scout 08', NULL, 2000, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000009')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000009', N'Easy Scout 09', NULL, 2200, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000010')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000010', N'Easy Scout 10', NULL, 2400, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000011')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000011', N'Easy Scout 11', NULL, 2600, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000012')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000012', N'Easy Scout 12', NULL, 2800, 1.10, 'easy', 900, 1800, 8000, 9000, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000013')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000013', N'Fair Scout 01', NULL, 600, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000014')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000014', N'Fair Scout 02', NULL, 800, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000015')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000015', N'Fair Scout 03', NULL, 1000, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000016')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000016', N'Fair Scout 04', NULL, 1200, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000017')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000017', N'Fair Scout 05', NULL, 1400, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000018')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000018', N'Fair Scout 06', NULL, 1600, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000019')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000019', N'Fair Scout 07', NULL, 1800, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000020')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000020', N'Fair Scout 08', NULL, 2000, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000021')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000021', N'Fair Scout 09', NULL, 2200, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000022')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000022', N'Fair Scout 10', NULL, 2400, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000023')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000023', N'Fair Scout 11', NULL, 2600, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000024')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000024', N'Fair Scout 12', NULL, 2800, 1.35, 'fair', 1100, 2300, 4500, 5500, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000025')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000025', N'Hard Scout 01', NULL, 600, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000026')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000026', N'Hard Scout 02', NULL, 800, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000027')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000027', N'Hard Scout 03', NULL, 1000, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000028')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000028', N'Hard Scout 04', NULL, 1200, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000029')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000029', N'Hard Scout 05', NULL, 1400, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000030')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000030', N'Hard Scout 06', NULL, 1600, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000031')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000031', N'Hard Scout 07', NULL, 1800, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000032')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000032', N'Hard Scout 08', NULL, 2000, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'moonlight', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000033')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000033', N'Hard Scout 09', NULL, 2200, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'sprout', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000034')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000034', N'Hard Scout 10', NULL, 2400, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'dawn', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000035')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000035', N'Hard Scout 11', NULL, 2600, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'warm_sun', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.pvp_bot_profiles WHERE bot_profile_id = '00000000-0000-4000-8000-000000000036')
    INSERT INTO dbo.pvp_bot_profiles (bot_profile_id, display_name, avatar_url, mmr, steps_per_second, difficulty_code, min_pace_milli, max_pace_milli, target_user_win_min_bps, target_user_win_max_bps, item_power_budget_bps, profile_version, spirit_affinity_code, pet_stage_no, is_active)
    VALUES ('00000000-0000-4000-8000-000000000036', N'Hard Scout 12', NULL, 2800, 1.60, 'hard', 1300, 2800, 2500, 3500, 1000, 1, 'moonlight', 1, 1);

COMMIT TRANSACTION;
