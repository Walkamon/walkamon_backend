CREATE DATABASE Walkamon;
GO

USE Walkamon;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE roles (
    role_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    role_code VARCHAR(30) NOT NULL UNIQUE,
    role_name NVARCHAR(100) NOT NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_roles_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_roles_updated_at DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE users (
    user_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_users_user_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    role_id INT NOT NULL,
    email NVARCHAR(320) NOT NULL,
    normalized_email NVARCHAR(320) NOT NULL,
    password_hash NVARCHAR(255) NULL,
    email_confirmed BIT NOT NULL
        CONSTRAINT DF_users_email_confirmed DEFAULT 0,
    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_users_status_code DEFAULT 'active',
    access_failed_count INT NOT NULL
        CONSTRAINT DF_users_access_failed_count DEFAULT 0,
    lockout_end_at DATETIME2(0) NULL,
    last_login_at DATETIME2(0) NULL,
	last_active_at DATETIME2(0) NULL,
    password_changed_at DATETIME2(0) NULL,
	last_logout_at DATETIME2(0) NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_users_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_users_updated_at DEFAULT SYSUTCDATETIME(),
    deleted_at DATETIME2(0) NULL,
    CONSTRAINT CK_users_status_code
        CHECK (status_code IN ('active', 'disabled')),
    CONSTRAINT CK_users_access_failed_count
        CHECK (access_failed_count >= 0),
    FOREIGN KEY (role_id) REFERENCES roles(role_id)
);
GO

CREATE UNIQUE INDEX UX_users_normalized_email_active
    ON users(normalized_email)
    WHERE deleted_at IS NULL;
GO

CREATE INDEX IX_users_role_id
    ON users(role_id);
GO

CREATE INDEX IX_users_status_created
    ON users(status_code, created_at DESC);
GO

CREATE TABLE external_logins (
    external_login_id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    provider_name VARCHAR(20) NOT NULL,
    provider_subject NVARCHAR(200) NOT NULL,
    provider_email NVARCHAR(320) NULL,
    provider_display_name NVARCHAR(200) NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_external_logins_created_at DEFAULT SYSUTCDATETIME(),
    last_login_at DATETIME2(0) NULL,
    UNIQUE (provider_name, provider_subject),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO


CREATE TABLE otp_requests (
    otp_request_id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    purpose_code VARCHAR(20) NOT NULL,
    target_value NVARCHAR(320) NOT NULL,
    otp_hash BINARY(32) NOT NULL,
    request_code UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_otp_requests_request_code DEFAULT NEWSEQUENTIALID(),
    expires_at DATETIME2(0) NOT NULL,
    used_at DATETIME2(0) NULL,
    attempt_count SMALLINT NOT NULL
        CONSTRAINT DF_otp_requests_attempt_count DEFAULT 0,
    max_attempts SMALLINT NOT NULL
        CONSTRAINT DF_otp_requests_max_attempts DEFAULT 5,
    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_otp_requests_status_code DEFAULT 'pending',
    requested_ip VARCHAR(45) NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_otp_requests_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_otp_requests_updated_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_otp_requests_request_code UNIQUE (request_code),
    CONSTRAINT CK_otp_requests_purpose_code
        CHECK (purpose_code IN ('forgot_password', 'verify_email')),
    CONSTRAINT CK_otp_requests_status_code
        CHECK (status_code IN ('pending', 'verified', 'expired', 'cancelled')),
    CONSTRAINT CK_otp_requests_attempt_count
        CHECK (attempt_count >= 0 AND max_attempts > 0 AND attempt_count <= max_attempts),
    CONSTRAINT CK_otp_requests_dates
        CHECK (
            expires_at > created_at
            AND (used_at IS NULL OR used_at >= created_at)
        ),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE user_profiles (
    user_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    username NVARCHAR(30) NULL,
    bio NVARCHAR(280) NULL,
    gender NVARCHAR(15) NULL,
    dob DATE NULL,
    avatar_url NVARCHAR(500) NULL,
	has_seen_story BIT NOT NULL
    CONSTRAINT DF_user_profiles_has_seen_story DEFAULT 0,
    language_code VARCHAR(10) NOT NULL
        CONSTRAINT DF_user_profiles_language_code DEFAULT 'vi-VN',
    theme_code VARCHAR(10) NOT NULL
        CONSTRAINT DF_user_profiles_theme_code DEFAULT 'light',
    time_zone_id NVARCHAR(64) NOT NULL
        CONSTRAINT DF_user_profiles_time_zone_id DEFAULT 'Asia/Ho_Chi_Minh',
    show_activity_stats BIT NOT NULL
        CONSTRAINT DF_user_profiles_show_activity_stats DEFAULT 1,
    notifications_enabled BIT NOT NULL
        CONSTRAINT DF_user_profiles_notifications_enabled DEFAULT 1,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_user_profiles_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_user_profiles_updated_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_user_profiles_theme_code
        CHECK (theme_code IN ('light', 'dark', 'system')),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE device_tokens (
    device_token_id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    fcm_token NVARCHAR(512) NOT NULL UNIQUE,
    is_active BIT NOT NULL
        CONSTRAINT DF_device_tokens_is_active DEFAULT 1,
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_device_tokens_updated_at DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE wallets (
    user_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    balance INT NOT NULL
        CONSTRAINT DF_wallets_balance DEFAULT 0,
    CONSTRAINT CK_wallets_balance CHECK (balance >= 0),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE item_types (
    item_type_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_item_types_item_type_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    item_type_name NVARCHAR(80) NOT NULL UNIQUE,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_item_types_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_item_types_updated_at DEFAULT SYSUTCDATETIME(),
		IsActive BIT NOT NULL DEFAULT 1
);
GO

CREATE TABLE items (
    item_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_items_item_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    item_name NVARCHAR(80) NOT NULL UNIQUE,
	 img_url NVARCHAR(300) NULL,
    item_type_id UNIQUEIDENTIFIER NOT NULL,
    effect_type_code VARCHAR(30) NULL,
    effect_value INT NULL,
    description NVARCHAR(300) NULL,
    is_active BIT NOT NULL
        CONSTRAINT DF_items_is_active DEFAULT 1,
    CONSTRAINT CK_items_effect
        CHECK (
            (effect_type_code IS NULL AND effect_value IS NULL)
            OR
            (effect_type_code IS NOT NULL AND effect_value IS NOT NULL AND effect_value >= 0)
        ),
    FOREIGN KEY (item_type_id) REFERENCES item_types(item_type_id)
);
GO

CREATE TABLE reward_packages (
    reward_package_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_reward_packages_reward_package_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    package_name NVARCHAR(100) NOT NULL UNIQUE,
    wallet_amount INT NOT NULL
        CONSTRAINT DF_reward_packages_wallet_amount DEFAULT 0,
    CONSTRAINT CK_reward_packages_wallet_amount CHECK (wallet_amount >= 0)
);
GO

CREATE TABLE reward_package_items (
    reward_package_id UNIQUEIDENTIFIER NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    quantity INT NOT NULL,
    PRIMARY KEY (reward_package_id, item_id),
    CONSTRAINT CK_reward_package_items_quantity CHECK (quantity > 0),
    FOREIGN KEY (reward_package_id) REFERENCES reward_packages(reward_package_id),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE pets(
 pet_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
 pet_name NVARCHAR(50) NOT NULL,
 pvp_affinity_code VARCHAR(30) NULL,
 life_force_rate FLOAT NOT NULL DEFAULT 0,
 energy_rate FLOAT NOT NULL DEFAULT 0,
 bond_rate FLOAT NOT NULL DEFAULT 0,
 exp_rate FLOAT NOT NULL DEFAULT 0,
 life_force INT NOT NULL DEFAULT 0,
 energy INT NOT NULL DEFAULT 0,
 bond INT NOT NULL DEFAULT 0,
 exp INT NOT NULL DEFAULT 0,
 created_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 updated_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE TABLE pet_stages(
 stage_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
 pet_id UNIQUEIDENTIFIER NOT NULL REFERENCES pets(pet_id),
 state_url NVARCHAR(500),
 stage_no INT NOT NULL,
 stage_name NVARCHAR(50) NOT NULL,
 required_level INT NOT NULL,
 is_active BIT NOT NULL DEFAULT 1,
 created_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 updated_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE UNIQUE INDEX UX_pet_stages_pet_stage_no ON pet_stages(pet_id,stage_no);
GO
CREATE TABLE pet_animations(
 pet_animation_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
 pet_id UNIQUEIDENTIFIER NOT NULL REFERENCES pets(pet_id),
 animation_url NVARCHAR(500),
 type_animation VARCHAR(30) NOT NULL,
 pet_stage_use INT NOT NULL DEFAULT 0,
 is_active BIT NOT NULL DEFAULT 1,
 created_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 updated_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE TABLE user_pets(
 user_id UNIQUEIDENTIFIER PRIMARY KEY REFERENCES users(user_id),
 pet_id UNIQUEIDENTIFIER NOT NULL REFERENCES pets(pet_id),
 level INT NOT NULL DEFAULT 1,
 pet_name NVARCHAR(50) NOT NULL,
 pet_exp INT NOT NULL DEFAULT 0,
 pet_energy INT NOT NULL DEFAULT 0,
 pet_bond INT NOT NULL DEFAULT 0,
 pet_life_force INT NOT NULL DEFAULT 0,
 current_pet_exp INT NOT NULL DEFAULT 0,
 current_pet_energy INT NOT NULL DEFAULT 0,
 current_pet_bond INT NOT NULL DEFAULT 0,
 current_pet_life_force INT NOT NULL DEFAULT 0,
 energy_updated_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 bond_updated_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 life_force_updated_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
 exp_updated_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE TABLE PetInteraction
(
    InteractionId UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_PetInteraction PRIMARY KEY,

    UserId UNIQUEIDENTIFIER NOT NULL,

    InteractionType NVARCHAR(20) NOT NULL,

    InteractionDate DATE NOT NULL,

    Count INT NOT NULL DEFAULT 0,

    CONSTRAINT FK_PetInteraction_User
        FOREIGN KEY (UserId)
        REFERENCES users(user_id)
);
GO
CREATE TABLE pet_evolution_history(
 evolution_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
 user_id UNIQUEIDENTIFIER NOT NULL REFERENCES users(user_id),
 stage_id UNIQUEIDENTIFIER NOT NULL REFERENCES pet_stages(stage_id),
 level INT NOT NULL,
 evolved_at DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE TABLE daily_steps (
    user_id UNIQUEIDENTIFIER NOT NULL,
    step_date DATE NOT NULL,
    step_count INT NOT NULL,
    eligible_step_count INT NOT NULL
        CONSTRAINT DF_daily_steps_eligible_step_count DEFAULT 0,
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_daily_steps_updated_at DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (user_id, step_date),
    CONSTRAINT CK_daily_steps_step_count CHECK (step_count >= 0),
    CONSTRAINT CK_daily_steps_eligible_step_count CHECK (eligible_step_count >= 0 AND eligible_step_count <= step_count),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE step_goals (
    user_id UNIQUEIDENTIFIER NOT NULL,
    effective_from DATE NOT NULL,
    target_steps INT NOT NULL,
    PRIMARY KEY (user_id, effective_from),
    CONSTRAINT CK_step_goals_target_steps CHECK (target_steps > 0),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE missions(
    mission_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_missions_mission_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    mission_type_code VARCHAR(20) NOT NULL,
    title NVARCHAR(100) NOT NULL,
    description NVARCHAR(500) NULL,
    metric_code VARCHAR(30) NOT NULL,
    target_value INT NOT NULL,
    reward_package_id UNIQUEIDENTIFIER NOT NULL,
    is_cancelable BIT NOT NULL
        CONSTRAINT DF_missions_is_cancelable DEFAULT 0,
    is_active BIT NOT NULL
        CONSTRAINT DF_missions_is_active DEFAULT 1,
    start_at DATETIME2(0) NULL,
    end_at DATETIME2(0) NULL,
    CONSTRAINT CK_missions_mission_type_code
        CHECK (mission_type_code IN ('daily', 'overall', 'challenge')),
    CONSTRAINT CK_missions_metric_code
        CHECK (metric_code IN ('steps', 'feed_pet', 'mission_completed', 'wallet_earned', 'pet_level')),
    CONSTRAINT CK_missions_target_value CHECK (target_value > 0),
    CONSTRAINT CK_missions_dates CHECK (start_at IS NULL OR end_at IS NULL OR start_at <= end_at),
    FOREIGN KEY (reward_package_id) REFERENCES reward_packages(reward_package_id)
);
GO

CREATE TABLE mission_conditions (
    mission_condition_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_mission_conditions_mission_condition_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    mission_id UNIQUEIDENTIFIER NOT NULL,
    condition_group VARCHAR(20) NOT NULL,
    condition_code VARCHAR(30) NOT NULL,
    target_value INT NOT NULL,
    reference_mission_id UNIQUEIDENTIFIER NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_mission_conditions_created_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_mission_conditions_group
        CHECK (condition_group IN ('completion', 'assignment')),
    CONSTRAINT CK_mission_conditions_code
        CHECK (condition_code IN ('steps', 'feed_pet', 'mission_completed', 'wallet_earned', 'pet_level')),
    CONSTRAINT CK_mission_conditions_target
        CHECK (target_value > 0),
    FOREIGN KEY (mission_id) REFERENCES missions(mission_id) ON DELETE CASCADE,
    FOREIGN KEY (reference_mission_id) REFERENCES missions(mission_id)
);
GO

CREATE TABLE user_missions (
    user_mission_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_user_missions_user_mission_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    mission_id UNIQUEIDENTIFIER NOT NULL,
    cycle_date DATE NOT NULL,
    assigned_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_user_missions_assigned_at DEFAULT SYSUTCDATETIME(),
    progress_value INT NOT NULL
        CONSTRAINT DF_user_missions_progress_value DEFAULT 0,
    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_user_mission_status_code DEFAULT 'active',
    claimed_at DATETIME2(0) NULL,
    UNIQUE (user_id, mission_id, cycle_date),
    CONSTRAINT CK_user_missions_progress_value CHECK (progress_value >= 0),
    CONSTRAINT CK_user_missions_status_code
        CHECK (status_code IN ('active', 'completed', 'claimed', 'cancelled')),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (mission_id) REFERENCES missions(mission_id)
);
GO

CREATE TABLE achievements (
    achievement_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_achievements_achievement_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    title NVARCHAR(100) NOT NULL UNIQUE,
	[description] NVARCHAR(500) null,
    metric_code VARCHAR(30) NOT NULL,
    target_value INT NOT NULL,
    icon_url NVARCHAR(300) NULL,
    reward_package_id UNIQUEIDENTIFIER NOT NULL,
    is_active BIT NOT NULL
        CONSTRAINT DF_achievements_is_active DEFAULT 1,
    CONSTRAINT CK_achievements_target_value CHECK (target_value > 0),
    FOREIGN KEY (reward_package_id) REFERENCES reward_packages(reward_package_id)
);
GO

CREATE TABLE achievement_conditions (
    achievement_condition_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_achievement_conditions_condition_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    achievement_id UNIQUEIDENTIFIER NOT NULL,
    condition_group VARCHAR(20) NOT NULL,
    condition_code VARCHAR(30) NOT NULL,
    target_value INT NOT NULL,
    reference_achievement_id UNIQUEIDENTIFIER NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_achievement_conditions_created_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_achievement_conditions_group
        CHECK (condition_group IN ('completion', 'assignment')),
    CONSTRAINT CK_achievement_conditions_target
        CHECK (target_value > 0),
    FOREIGN KEY (achievement_id) REFERENCES achievements(achievement_id) ON DELETE CASCADE,
    FOREIGN KEY (reference_achievement_id) REFERENCES achievements(achievement_id)
);
GO

CREATE INDEX IX_achievement_conditions_achievement_group
    ON achievement_conditions(achievement_id, condition_group);
GO

CREATE TABLE user_achievements (
    user_id UNIQUEIDENTIFIER NOT NULL,
    achievement_id UNIQUEIDENTIFIER NOT NULL,
    progress_value INT NOT NULL
        CONSTRAINT DF_user_achievements_progress_value DEFAULT 0,
    unlocked_at DATETIME2(0) NULL,
    claimed_at DATETIME2(0) NULL,
    PRIMARY KEY (user_id, achievement_id),
    CONSTRAINT CK_user_achievements_progress_value CHECK (progress_value >= 0),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (achievement_id) REFERENCES achievements(achievement_id)
);
GO

CREATE TABLE inventory_items (
    user_id UNIQUEIDENTIFIER NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    quantity INT NOT NULL
        CONSTRAINT DF_inventory_items_quantity DEFAULT 0,
    PRIMARY KEY (user_id, item_id),
    CONSTRAINT CK_inventory_items_quantity CHECK (quantity >= 0),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO


CREATE TABLE shop_items (
    shop_item_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_shop_items_shop_item_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    item_id UNIQUEIDENTIFIER NOT NULL,
    price_amount INT NOT NULL,
    is_active BIT NOT NULL
        CONSTRAINT DF_shop_items_is_active DEFAULT 1,
    UNIQUE (item_id),
    CONSTRAINT CK_shop_items_price_amount CHECK (price_amount >= 0),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE shop_purchases (
    purchase_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_shop_purchases_purchase_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    shop_item_id UNIQUEIDENTIFIER NOT NULL,
    quantity INT NOT NULL
        CONSTRAINT DF_shop_purchases_quantity DEFAULT 1,
    unit_price_amount INT NOT NULL,
    purchased_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_shop_purchases_purchased_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_shop_purchases_quantity CHECK (quantity > 0),
    CONSTRAINT CK_shop_purchases_unit_price_amount CHECK (unit_price_amount >= 0),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (shop_item_id) REFERENCES shop_items(shop_item_id)
);
GO

CREATE TABLE friend_requests (
    request_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_friend_requests_request_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    sender_user_id UNIQUEIDENTIFIER NOT NULL,
    receiver_user_id UNIQUEIDENTIFIER NOT NULL,
    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_friend_requests_status_code DEFAULT 'pending',
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_friend_requests_created_at DEFAULT SYSUTCDATETIME(),
    responded_at DATETIME2(0) NULL,
    CONSTRAINT CK_friend_requests_users CHECK (sender_user_id <> receiver_user_id),
    CONSTRAINT CK_friend_requests_status_code
        CHECK (status_code IN ('pending', 'accepted', 'rejected', 'cancelled')),
    FOREIGN KEY (sender_user_id) REFERENCES users(user_id),
    FOREIGN KEY (receiver_user_id) REFERENCES users(user_id)
);
GO
CREATE TABLE friendships (
    user_low_id UNIQUEIDENTIFIER NOT NULL,
    user_high_id UNIQUEIDENTIFIER NOT NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_friendships_created_at DEFAULT SYSUTCDATETIME(),
    PRIMARY KEY (user_low_id, user_high_id),
    CONSTRAINT CK_friendships_users CHECK (user_low_id <> user_high_id),
    FOREIGN KEY (user_low_id) REFERENCES users(user_id),
    FOREIGN KEY (user_high_id) REFERENCES users(user_id)
);
GO

CREATE TABLE notifications (
    notification_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_notifications_notification_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    notification_type_code VARCHAR(30) NOT NULL,
    title NVARCHAR(120) NOT NULL,
    body NVARCHAR(500) NOT NULL,
    image_url NVARCHAR(500) NULL,
    target_audience_code VARCHAR(30) NOT NULL
        CONSTRAINT DF_notifications_target_audience_code DEFAULT 'all_users',
    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_notifications_status_code DEFAULT 'sent',
    created_by_user_id UNIQUEIDENTIFIER NULL,
    scheduled_at DATETIME2(0) NULL,
    sent_at DATETIME2(0) NULL,
    recipient_count INT NOT NULL
        CONSTRAINT DF_notifications_recipient_count DEFAULT 0,
    delivery_success_count INT NOT NULL
        CONSTRAINT DF_notifications_delivery_success_count DEFAULT 0,
    delivery_failure_count INT NOT NULL
        CONSTRAINT DF_notifications_delivery_failure_count DEFAULT 0,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_notifications_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_notifications_updated_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_notifications_target_audience_code
        CHECK (target_audience_code IN
            ('all_users', 'new_users', 'level_10_plus', 'inactive_7_days', 'single_user')),
    CONSTRAINT CK_notifications_status_code
        CHECK (status_code IN ('scheduled', 'sent', 'failed')),
    CONSTRAINT CK_notifications_delivery_counts
        CHECK (recipient_count >= 0
            AND delivery_success_count >= 0
            AND delivery_failure_count >= 0),
    FOREIGN KEY (created_by_user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE user_notifications (
    user_id UNIQUEIDENTIFIER NOT NULL,
    notification_id UNIQUEIDENTIFIER NOT NULL,
    read_at DATETIME2(0) NULL,
    deleted_at DATETIME2(0) NULL,
    PRIMARY KEY (user_id, notification_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (notification_id) REFERENCES notifications(notification_id)
);
GO


CREATE TABLE system_settings (
    setting_key VARCHAR(50) NOT NULL PRIMARY KEY,
    setting_value NVARCHAR(200) NOT NULL,
    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_system_settings_updated_at DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE matchmaking_queue (
    user_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    match_type_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_matchmaking_queue_match_type_code DEFAULT 'ranked',
    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_matchmaking_queue_status_code DEFAULT 'waiting',
    pet_level_snapshot TINYINT NULL,
    mmr_snapshot INT NULL,
    daily_steps_snapshot INT NULL,
    base_pace_snapshot INT NULL,
    expected_distance_units BIGINT NULL,
    expected_speed_bps INT NULL,
    policy_version INT NULL,
    requires_relief BIT NOT NULL CONSTRAINT DF_matchmaking_queue_requires_relief DEFAULT 0,
    power_snapshot_at DATETIME2(0) NULL,
    bot_fallback_at DATETIME2(0) NULL,
    row_version ROWVERSION NOT NULL,
    queued_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_matchmaking_queue_queued_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_matchmaking_queue_match_type_code
        CHECK (match_type_code IN ('ranked', 'friendly', 'event')),
    CONSTRAINT CK_matchmaking_queue_status_code
        CHECK (status_code IN ('waiting', 'matched', 'cancelled')),
    CONSTRAINT CK_matchmaking_queue_pet_level_snapshot
        CHECK (pet_level_snapshot IS NULL OR pet_level_snapshot > 0),
    CONSTRAINT CK_matchmaking_queue_power_snapshot CHECK (
        (daily_steps_snapshot IS NULL OR daily_steps_snapshot >= 0)
        AND (base_pace_snapshot IS NULL OR base_pace_snapshot > 0)
        AND (expected_distance_units IS NULL OR expected_distance_units >= 0)
        AND (expected_speed_bps IS NULL OR expected_speed_bps BETWEEN 7500 AND 12500)
    ),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE pvp_player_profiles (
    user_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    mmr INT NOT NULL CONSTRAINT DF_pvp_player_profiles_mmr DEFAULT 1000,
    consecutive_valid_ranked_losses SMALLINT NOT NULL CONSTRAINT DF_pvp_profiles_loss_streak DEFAULT 0,
    completed_ranked_matches_since_relief INT NOT NULL CONSTRAINT DF_pvp_profiles_since_relief DEFAULT 0,
    last_relief_completed_at DATETIME2(0) NULL,
    last_bot_difficulty_code VARCHAR(10) NULL,
    consecutive_hard_bot_count TINYINT NOT NULL CONSTRAINT DF_pvp_profiles_hard_count DEFAULT 0,
    row_version ROWVERSION NOT NULL,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_player_profiles_updated_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_profiles_protection_state CHECK (
        consecutive_valid_ranked_losses >= 0
        AND completed_ranked_matches_since_relief >= 0
        AND (last_bot_difficulty_code IS NULL OR last_bot_difficulty_code IN ('easy', 'fair', 'hard', 'relief'))
    ),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE pvp_player_activities (
    user_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    activity_type VARCHAR(30) NOT NULL,
    activity_id UNIQUEIDENTIFIER NOT NULL,
    due_at DATETIME2(0) NULL,
    row_version ROWVERSION NOT NULL,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_player_activities_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_player_activities_updated_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_player_activities_type CHECK (activity_type IN ('invite_pending', 'queue_waiting', 'match_countdown', 'match_running', 'match_settling')),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE pvp_bot_profiles (
    bot_profile_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_bot_profiles_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    display_name NVARCHAR(80) NOT NULL,
    avatar_url NVARCHAR(500) NULL,
    mmr INT NOT NULL,
    steps_per_second DECIMAL(5,2) NOT NULL,
    difficulty_code VARCHAR(10) NOT NULL CONSTRAINT DF_pvp_bot_profiles_difficulty DEFAULT 'fair',
    min_pace_milli INT NOT NULL CONSTRAINT DF_pvp_bot_profiles_min_pace DEFAULT 1000,
    max_pace_milli INT NOT NULL CONSTRAINT DF_pvp_bot_profiles_max_pace DEFAULT 2500,
    target_user_win_min_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_bot_profiles_target_min DEFAULT 4500,
    target_user_win_max_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_bot_profiles_target_max DEFAULT 5500,
    item_power_budget_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_bot_profiles_item_budget DEFAULT 1000,
    profile_version INT NOT NULL CONSTRAINT DF_pvp_bot_profiles_version DEFAULT 1,
    row_version ROWVERSION NOT NULL,
    spirit_affinity_code VARCHAR(30) NULL,
    pet_stage_no TINYINT NOT NULL CONSTRAINT DF_pvp_bot_profiles_pet_stage DEFAULT 1,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_bot_profiles_active DEFAULT 1,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_bot_profiles_created_at DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_bot_profiles_updated_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_bot_profiles_pace CHECK (
        steps_per_second > 0
        AND min_pace_milli > 0
        AND max_pace_milli >= min_pace_milli
    ),
    CONSTRAINT CK_pvp_bot_profiles_difficulty CHECK (difficulty_code IN ('easy', 'fair', 'hard', 'relief')),
    CONSTRAINT CK_pvp_bot_profiles_targets CHECK (
        target_user_win_min_bps BETWEEN 0 AND 10000
        AND target_user_win_max_bps BETWEEN target_user_win_min_bps AND 10000
        AND item_power_budget_bps BETWEEN 0 AND 10000
    )
);
GO

CREATE TABLE pvp_matchmaking_policies (
    policy_version INT NOT NULL PRIMARY KEY,
    is_active BIT NOT NULL,
    match_duration_seconds TINYINT NOT NULL CONSTRAINT DF_pvp_policy_duration DEFAULT 30,
    bot_fallback_seconds TINYINT NOT NULL CONSTRAINT DF_pvp_policy_bot_fallback DEFAULT 15,
    stage1_mmr_gap SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s1_mmr DEFAULT 75,
    stage1_power_gap_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s1_power DEFAULT 800,
    stage1_pace_ratio_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s1_pace DEFAULT 11000,
    stage2_mmr_gap SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s2_mmr DEFAULT 100,
    stage2_power_gap_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s2_power DEFAULT 1200,
    stage2_pace_ratio_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s2_pace DEFAULT 11500,
    stage3_mmr_gap SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s3_mmr DEFAULT 150,
    stage3_power_gap_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s3_power DEFAULT 1500,
    stage3_pace_ratio_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_s3_pace DEFAULT 12000,
    hard_mmr_gap SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_mmr DEFAULT 250,
    hard_power_gap_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_power DEFAULT 2000,
    hard_pace_ratio_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_pace DEFAULT 12500,
    streak01_easy_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_01_easy DEFAULT 2000,
    streak01_fair_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_01_fair DEFAULT 5000,
    streak01_hard_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_01_hard DEFAULT 3000,
    streak23_easy_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_23_easy DEFAULT 4500,
    streak23_fair_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_23_fair DEFAULT 4500,
    streak23_hard_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_23_hard DEFAULT 1000,
    streak4_easy_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_4_easy DEFAULT 7000,
    streak4_fair_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_4_fair DEFAULT 3000,
    streak4_hard_weight_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_4_hard DEFAULT 0,
    relief_loss_threshold TINYINT NOT NULL CONSTRAINT DF_pvp_policy_relief_losses DEFAULT 5,
    relief_target_user_win_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_relief_target DEFAULT 8200,
    easy_target_user_win_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_easy_target DEFAULT 8200,
    fair_target_user_win_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_fair_target DEFAULT 5000,
    hard_target_user_win_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_target DEFAULT 3000,
    bot_history_window TINYINT NOT NULL CONSTRAINT DF_pvp_policy_bot_window DEFAULT 10,
    max_bot_matches_in_window TINYINT NOT NULL CONSTRAINT DF_pvp_policy_bot_cap DEFAULT 6,
    allow_consecutive_hard BIT NOT NULL CONSTRAINT DF_pvp_policy_hard_repeat DEFAULT 0,
    easy_win_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_easy_win_mmr DEFAULT 0,
    easy_draw_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_easy_draw_mmr DEFAULT 0,
    easy_loss_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_easy_loss_mmr DEFAULT -1,
    fair_win_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_fair_win_mmr DEFAULT 2,
    fair_draw_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_fair_draw_mmr DEFAULT 0,
    fair_loss_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_fair_loss_mmr DEFAULT -2,
    hard_win_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_win_mmr DEFAULT 6,
    hard_draw_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_draw_mmr DEFAULT 0,
    hard_loss_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_loss_mmr DEFAULT -2,
    relief_win_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_relief_win_mmr DEFAULT 0,
    relief_draw_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_relief_draw_mmr DEFAULT 0,
    relief_loss_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_relief_loss_mmr DEFAULT 0,
    bot_rating_window TINYINT NOT NULL CONSTRAINT DF_pvp_policy_rating_window DEFAULT 20,
    max_positive_bot_mmr_in_window SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_rating_cap DEFAULT 8,
    easy_reward_multiplier_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_easy_reward DEFAULT 2500,
    fair_reward_multiplier_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_fair_reward DEFAULT 5000,
    hard_reward_multiplier_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_hard_reward DEFAULT 10000,
    relief_reward_multiplier_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_policy_relief_reward DEFAULT 0,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_policy_created DEFAULT SYSUTCDATETIME(),
    activated_at DATETIME2(0) NULL,
    CONSTRAINT CK_pvp_policy_timing CHECK (match_duration_seconds BETWEEN 10 AND 120 AND bot_fallback_seconds BETWEEN 1 AND 120),
    CONSTRAINT CK_pvp_policy_windows CHECK (
        stage1_mmr_gap >= 0 AND stage1_power_gap_bps >= 0 AND stage1_pace_ratio_bps >= 10000
        AND stage1_mmr_gap <= stage2_mmr_gap AND stage2_mmr_gap <= stage3_mmr_gap AND stage3_mmr_gap <= hard_mmr_gap
        AND stage1_power_gap_bps <= stage2_power_gap_bps AND stage2_power_gap_bps <= stage3_power_gap_bps AND stage3_power_gap_bps <= hard_power_gap_bps
        AND stage1_pace_ratio_bps <= stage2_pace_ratio_bps AND stage2_pace_ratio_bps <= stage3_pace_ratio_bps AND stage3_pace_ratio_bps <= hard_pace_ratio_bps
    ),
    CONSTRAINT CK_pvp_policy_weights CHECK (
        streak01_easy_weight_bps + streak01_fair_weight_bps + streak01_hard_weight_bps = 10000
        AND streak23_easy_weight_bps + streak23_fair_weight_bps + streak23_hard_weight_bps = 10000
        AND streak4_easy_weight_bps + streak4_fair_weight_bps + streak4_hard_weight_bps = 10000
    ),
    CONSTRAINT CK_pvp_policy_caps CHECK (
        relief_loss_threshold > 0
        AND max_bot_matches_in_window <= bot_history_window
        AND bot_rating_window > 0
        AND max_positive_bot_mmr_in_window >= 0
        AND relief_target_user_win_bps BETWEEN 0 AND 10000
        AND easy_target_user_win_bps BETWEEN 0 AND 10000
        AND fair_target_user_win_bps BETWEEN 0 AND 10000
        AND hard_target_user_win_bps BETWEEN 0 AND 10000
        AND easy_reward_multiplier_bps BETWEEN 0 AND 10000
        AND fair_reward_multiplier_bps BETWEEN 0 AND 10000
        AND hard_reward_multiplier_bps BETWEEN 0 AND 10000
        AND relief_reward_multiplier_bps BETWEEN 0 AND 10000
    )
);
GO

INSERT INTO pvp_matchmaking_policies(policy_version, is_active, activated_at)
VALUES (1, 1, SYSUTCDATETIME());
GO

ALTER TABLE matchmaking_queue ADD CONSTRAINT FK_matchmaking_queue_policy
    FOREIGN KEY (policy_version) REFERENCES pvp_matchmaking_policies(policy_version);
GO

CREATE TABLE pvp_matches (
    match_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_pvp_matches_match_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,
    match_type_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_pvp_matches_match_type_code DEFAULT 'ranked',
    source_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_matches_source_code DEFAULT 'matchmaking',
    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_pvp_matches_status_code DEFAULT 'countdown',
    winner_user_id UNIQUEIDENTIFIER NULL,
    cancel_reason NVARCHAR(200) NULL,
    finish_reason_code VARCHAR(30) NULL,
    forfeited_by_user_id UNIQUEIDENTIFIER NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_pvp_matches_created_at DEFAULT SYSUTCDATETIME(),
    started_at DATETIME2(0) NULL,
    ended_at DATETIME2(0) NULL,
    countdown_ends_at DATETIME2(0) NULL,
    settlement_ends_at DATETIME2(0) NULL,
    resolved_at DATETIME2(0) NULL,
    rating_k INT NOT NULL CONSTRAINT DF_pvp_matches_rating_k DEFAULT 32,
    rating_divisor INT NOT NULL CONSTRAINT DF_pvp_matches_rating_divisor DEFAULT 400,
    speed_min_bps INT NOT NULL CONSTRAINT DF_pvp_matches_speed_min DEFAULT 7500,
    speed_max_bps INT NOT NULL CONSTRAINT DF_pvp_matches_speed_max DEFAULT 12500,
    item_slot_limit TINYINT NOT NULL CONSTRAINT DF_pvp_matches_item_slots DEFAULT 2,
    rule_version INT NOT NULL CONSTRAINT DF_pvp_matches_rule_version DEFAULT 2,
    scoring_mode_code VARCHAR(30) NOT NULL CONSTRAINT DF_pvp_matches_scoring_mode DEFAULT 'daily_power_v1',
    daily_step_power_cap INT NOT NULL CONSTRAINT DF_pvp_matches_daily_power_cap DEFAULT 10000,
    base_pace_min_milli_steps_per_second INT NOT NULL CONSTRAINT DF_pvp_matches_min_pace DEFAULT 1000,
    base_pace_max_milli_steps_per_second INT NOT NULL CONSTRAINT DF_pvp_matches_max_pace DEFAULT 2500,
    match_duration_seconds TINYINT NOT NULL CONSTRAINT DF_pvp_matches_duration DEFAULT 30,
    matchmaking_policy_version INT NULL,
    matchmaking_reason_code VARCHAR(30) NULL,
    bot_difficulty_code VARCHAR(10) NULL,
    is_relief_match BIT NOT NULL CONSTRAINT DF_pvp_matches_relief DEFAULT 0,
    rating_policy_code VARCHAR(30) NULL,
    selection_roll_bps SMALLINT NULL,
    expected_first_distance_units BIGINT NULL,
    expected_second_distance_units BIGINT NULL,
    expected_gap_bps SMALLINT NULL,
    bot_reward_multiplier_bps SMALLINT NOT NULL CONSTRAINT DF_pvp_matches_bot_reward DEFAULT 10000,
    bot_win_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_matches_bot_win_mmr DEFAULT 0,
    bot_draw_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_matches_bot_draw_mmr DEFAULT 0,
    bot_loss_mmr_delta SMALLINT NOT NULL CONSTRAINT DF_pvp_matches_bot_loss_mmr DEFAULT 0,
    bot_rating_window TINYINT NOT NULL CONSTRAINT DF_pvp_matches_bot_rating_window DEFAULT 20,
    max_positive_bot_mmr_in_window SMALLINT NOT NULL CONSTRAINT DF_pvp_matches_bot_rating_cap DEFAULT 8,
    profile_state_applied_at DATETIME2(0) NULL,
    last_progress_at DATETIME2(3) NULL,
    last_event_sequence BIGINT NOT NULL CONSTRAINT DF_pvp_matches_last_event_sequence DEFAULT 0,
    row_version ROWVERSION NOT NULL,
    CONSTRAINT CK_pvp_matches_match_type_code
        CHECK (match_type_code IN ('ranked', 'friendly', 'event')),
    CONSTRAINT CK_pvp_matches_status_code
        CHECK (status_code IN ('countdown', 'running', 'settling', 'finished', 'cancelled')),
    CONSTRAINT CK_pvp_matches_source_code CHECK (source_code IN ('invite', 'matchmaking', 'bot')),
    CONSTRAINT CK_pvp_matches_scoring_mode CHECK (scoring_mode_code IN ('legacy_race_steps', 'daily_power_v1')),
    CONSTRAINT CK_pvp_matches_daily_power CHECK (
        daily_step_power_cap > 0
        AND base_pace_min_milli_steps_per_second > 0
        AND base_pace_max_milli_steps_per_second >= base_pace_min_milli_steps_per_second
    ),
    CONSTRAINT CK_pvp_matches_adaptive_snapshots CHECK (
        match_duration_seconds BETWEEN 10 AND 120
        AND (bot_difficulty_code IS NULL OR bot_difficulty_code IN ('easy', 'fair', 'hard', 'relief'))
        AND (selection_roll_bps IS NULL OR selection_roll_bps BETWEEN 0 AND 9999)
        AND (expected_gap_bps IS NULL OR expected_gap_bps BETWEEN 0 AND 10000)
        AND bot_reward_multiplier_bps BETWEEN 0 AND 10000
        AND bot_rating_window > 0
        AND max_positive_bot_mmr_in_window >= 0
    ),
    CONSTRAINT CK_pvp_matches_finish_reason CHECK (finish_reason_code IS NULL OR finish_reason_code IN ('normal_completion', 'user_forfeit')),
    CONSTRAINT CK_pvp_matches_dates
        CHECK (
            (started_at IS NULL OR started_at >= created_at)
            AND
            (ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at)
        ),
    FOREIGN KEY (winner_user_id) REFERENCES users(user_id),
    CONSTRAINT FK_pvp_matches_forfeited_user FOREIGN KEY (forfeited_by_user_id) REFERENCES users(user_id)
    ,CONSTRAINT FK_pvp_matches_matchmaking_policy FOREIGN KEY (matchmaking_policy_version) REFERENCES pvp_matchmaking_policies(policy_version)
);
GO

CREATE TABLE pvp_match_players (
    match_player_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_players_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    user_id UNIQUEIDENTIFIER NULL,
    bot_profile_id UNIQUEIDENTIFIER NULL,
    participant_type_code VARCHAR(10) NOT NULL,
    steps_at_match INT NOT NULL,
    pet_level_at_match TINYINT NOT NULL,
    score INT NOT NULL
        CONSTRAINT DF_pvp_match_players_score DEFAULT 0,
    mmr_before INT NOT NULL,
    mmr_delta INT NOT NULL CONSTRAINT DF_pvp_match_players_mmr_delta DEFAULT 0,
    pet_id_snapshot UNIQUEIDENTIFIER NULL,
    pet_name_snapshot NVARCHAR(100) NULL,
    pet_stage_no_snapshot TINYINT NULL,
    spirit_affinity_code VARCHAR(30) NULL,
    passive_speed_bps INT NOT NULL CONSTRAINT DF_pvp_match_players_passive DEFAULT 0,
    validated_steps INT NOT NULL CONSTRAINT DF_pvp_match_players_validated_steps DEFAULT 0,
    daily_eligible_steps_snapshot INT NOT NULL CONSTRAINT DF_pvp_match_players_daily_snapshot DEFAULT 0,
    base_pace_milli_steps_per_second INT NOT NULL CONSTRAINT DF_pvp_match_players_base_pace DEFAULT 1000,
    distance_units BIGINT NOT NULL CONSTRAINT DF_pvp_match_players_distance DEFAULT 0,
    expected_distance_units BIGINT NULL,
    expected_speed_bps INT NULL,
    expected_passive_bps INT NULL,
    expected_loadout_bps INT NULL,
    passive_rule_bonus_bps_snapshot INT NULL,
    passive_rule_start_minute_snapshot SMALLINT NULL,
    passive_rule_end_minute_snapshot SMALLINT NULL,
    bot_min_pace_snapshot INT NULL,
    bot_max_pace_snapshot INT NULL,
    ready_at DATETIME2(3) NULL,
    realtime_joined_at DATETIME2(3) NULL,
    streak_eligibility_code VARCHAR(30) NULL,
    row_version ROWVERSION NOT NULL,
    is_ready BIT NOT NULL
        CONSTRAINT DF_pvp_match_players_is_ready DEFAULT 0,
    result_code VARCHAR(20) NULL,
    finish_time_ms INT NULL,
    joined_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_pvp_match_players_joined_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_match_players_participant CHECK ((participant_type_code = 'user' AND user_id IS NOT NULL AND bot_profile_id IS NULL) OR (participant_type_code = 'bot' AND bot_profile_id IS NOT NULL AND user_id IS NULL)),
    CONSTRAINT CK_pvp_match_players_steps_at_match CHECK (steps_at_match >= 0),
    CONSTRAINT CK_pvp_match_players_pet_level_at_match CHECK (pet_level_at_match > 0),
    CONSTRAINT CK_pvp_match_players_score CHECK (score >= 0),
    CONSTRAINT CK_pvp_match_players_realtime_score CHECK (
        passive_speed_bps >= 0
        AND validated_steps >= 0
        AND daily_eligible_steps_snapshot >= 0
        AND base_pace_milli_steps_per_second > 0
        AND distance_units >= 0
    ),
    CONSTRAINT CK_pvp_match_players_power_snapshot CHECK (
        (expected_distance_units IS NULL OR expected_distance_units >= 0)
        AND (expected_speed_bps IS NULL OR expected_speed_bps BETWEEN 7500 AND 12500)
        AND (expected_passive_bps IS NULL OR expected_passive_bps >= 0)
        AND (bot_min_pace_snapshot IS NULL OR bot_min_pace_snapshot > 0)
        AND (bot_max_pace_snapshot IS NULL OR bot_min_pace_snapshot IS NOT NULL AND bot_max_pace_snapshot >= bot_min_pace_snapshot)
    ),
    CONSTRAINT CK_pvp_match_players_result_code
        CHECK (result_code IS NULL OR result_code IN ('win', 'lose', 'draw', 'quit')),
    CONSTRAINT CK_pvp_match_players_finish_time_ms CHECK (finish_time_ms IS NULL OR finish_time_ms > 0),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (bot_profile_id) REFERENCES pvp_bot_profiles(bot_profile_id)
);
GO

CREATE TABLE pvp_sprint_invites (
    invite_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_sprint_invites_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    inviter_user_id UNIQUEIDENTIFIER NOT NULL,
    invitee_user_id UNIQUEIDENTIFIER NOT NULL,
    user_low_id UNIQUEIDENTIFIER NOT NULL,
    user_high_id UNIQUEIDENTIFIER NOT NULL,
    status_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_sprint_invites_status DEFAULT 'pending',
    expires_at DATETIME2(0) NOT NULL,
    responded_at DATETIME2(0) NULL,
    match_id UNIQUEIDENTIFIER NULL,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_sprint_invites_created_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_sprint_invites_users CHECK (
        inviter_user_id <> invitee_user_id
        AND user_low_id <> user_high_id
        AND (
            (user_low_id = inviter_user_id AND user_high_id = invitee_user_id)
            OR (user_low_id = invitee_user_id AND user_high_id = inviter_user_id)
        )
    ),
    CONSTRAINT CK_pvp_sprint_invites_status CHECK (status_code IN ('pending', 'accepted', 'declined', 'expired', 'cancelled')),
    FOREIGN KEY (inviter_user_id) REFERENCES users(user_id),
    FOREIGN KEY (invitee_user_id) REFERENCES users(user_id),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id)
);
GO

CREATE TABLE pvp_step_sessions (
    step_session_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_step_sessions_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NULL,
    user_id UNIQUEIDENTIFIER NOT NULL,
    purpose_code VARCHAR(10) NOT NULL,
    platform_code VARCHAR(20) NOT NULL,
    sensor_mode_code VARCHAR(20) NOT NULL,
    nonce NVARCHAR(128) NOT NULL,
    status_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_step_sessions_status DEFAULT 'active',
    expires_at DATETIME2(0) NOT NULL,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_step_sessions_created_at DEFAULT SYSUTCDATETIME(),
    last_submitted_at DATETIME2(0) NULL,
    last_sequence INT NOT NULL CONSTRAINT DF_pvp_step_sessions_last_sequence DEFAULT 0,
    last_sensor_total BIGINT NULL,
    last_recorded_at DATETIME2(3) NULL,
    closed_reason NVARCHAR(100) NULL,
    row_version ROWVERSION NOT NULL,
    CONSTRAINT CK_pvp_step_sessions_purpose CHECK (purpose_code IN ('daily', 'pvp')),
    CONSTRAINT CK_pvp_step_sessions_sensor_mode CHECK (sensor_mode_code IN ('detector', 'counter')),
    CONSTRAINT CK_pvp_step_sessions_match_purpose CHECK ((purpose_code = 'daily' AND match_id IS NULL) OR (purpose_code = 'pvp' AND match_id IS NOT NULL)),
    CONSTRAINT CK_pvp_step_sessions_status CHECK (status_code IN ('active', 'expired', 'closed')),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE step_sensor_batches (
    step_sensor_batch_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_step_sensor_batches_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    step_session_id UNIQUEIDENTIFIER NOT NULL,
    sequence INT NOT NULL,
    payload_hash CHAR(64) NOT NULL,
    attestation_status VARCHAR(30) NOT NULL,
    package_name NVARCHAR(200) NULL,
    verdict_timestamp DATETIME2(3) NULL,
    verdict_json NVARCHAR(MAX) NULL,
    evidence_version INT NOT NULL CONSTRAINT DF_step_sensor_batches_evidence_version DEFAULT 1,
    motion_score INT NOT NULL CONSTRAINT DF_step_sensor_batches_motion_score DEFAULT 0,
    motion_status VARCHAR(20) NOT NULL CONSTRAINT DF_step_sensor_batches_motion_status DEFAULT 'unavailable',
    motion_reasons_json NVARCHAR(MAX) NOT NULL CONSTRAINT DF_step_sensor_batches_motion_reasons DEFAULT N'[]',
    degraded_evidence BIT NOT NULL CONSTRAINT DF_step_sensor_batches_degraded DEFAULT 0,
    accepted_steps INT NOT NULL CONSTRAINT DF_step_sensor_batches_accepted DEFAULT 0,
    rejected_steps INT NOT NULL CONSTRAINT DF_step_sensor_batches_rejected DEFAULT 0,
    suspicious_steps INT NOT NULL CONSTRAINT DF_step_sensor_batches_suspicious DEFAULT 0,
    received_at DATETIME2(3) NOT NULL CONSTRAINT DF_step_sensor_batches_received DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_step_sensor_batches_counts CHECK (accepted_steps >= 0 AND rejected_steps >= 0 AND suspicious_steps >= 0),
    CONSTRAINT CK_step_sensor_batches_motion_score CHECK (motion_score BETWEEN 0 AND 100),
    CONSTRAINT CK_step_sensor_batches_motion_status CHECK (motion_status IN ('accepted', 'suspicious', 'rejected', 'unavailable')),
    FOREIGN KEY (step_session_id) REFERENCES pvp_step_sessions(step_session_id)
);
GO

CREATE TABLE step_motion_evidence_windows (
    step_motion_evidence_window_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_step_motion_windows_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    batch_id UNIQUEIDENTIFIER NOT NULL,
    window_index SMALLINT NOT NULL,
    boot_session_id UNIQUEIDENTIFIER NULL,
    window_start_elapsed_realtime_ns BIGINT NULL,
    window_end_elapsed_realtime_ns BIGINT NULL,
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
    reason_codes NVARCHAR(500) NOT NULL CONSTRAINT DF_step_motion_windows_reasons DEFAULT N'[]',
    CONSTRAINT CK_step_motion_windows_elapsed CHECK ((window_start_elapsed_realtime_ns IS NULL AND window_end_elapsed_realtime_ns IS NULL) OR (window_start_elapsed_realtime_ns > 0 AND window_end_elapsed_realtime_ns > window_start_elapsed_realtime_ns)),
    CONSTRAINT CK_step_motion_windows_time CHECK (window_ended_at > window_started_at),
    CONSTRAINT CK_step_motion_windows_samples CHECK (sample_count >= 0),
    CONSTRAINT CK_step_motion_windows_periodicity CHECK (periodicity_bps BETWEEN 0 AND 10000),
    CONSTRAINT CK_step_motion_windows_activity_confidence CHECK (activity_confidence BETWEEN 0 AND 100),
    CONSTRAINT CK_step_motion_windows_motion_score CHECK (motion_score BETWEEN 0 AND 100),
    CONSTRAINT CK_step_motion_windows_classification CHECK (classification IN ('accepted', 'suspicious', 'rejected')),
    FOREIGN KEY (batch_id) REFERENCES step_sensor_batches(step_sensor_batch_id) ON DELETE CASCADE
);
GO

CREATE TABLE validated_step_records (
    validated_step_record_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_validated_step_records_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    step_session_id UNIQUEIDENTIFIER NULL,
    batch_id UNIQUEIDENTIFIER NULL,
    event_index INT NULL,
    platform_code VARCHAR(20) NOT NULL,
    source_code VARCHAR(30) NOT NULL,
    sensor_mode_code VARCHAR(20) NOT NULL,
    interval_started_at DATETIME2(3) NOT NULL,
    recorded_at DATETIME2(3) NOT NULL,
    sensor_start_total BIGINT NULL,
    sensor_end_total BIGINT NULL,
    step_count INT NOT NULL,
    eligible_step_count INT NOT NULL,
    sequence_number INT NOT NULL,
    payload_hash CHAR(64) NOT NULL,
    validation_status VARCHAR(20) NOT NULL,
    rejection_reason NVARCHAR(200) NULL,
    motion_score INT NOT NULL CONSTRAINT DF_validated_step_records_motion_score DEFAULT 0,
    motion_status VARCHAR(20) NOT NULL CONSTRAINT DF_validated_step_records_motion_status DEFAULT 'unavailable',
    received_at DATETIME2(3) NOT NULL CONSTRAINT DF_validated_step_records_received_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_validated_step_records_counts CHECK (step_count >= 0 AND eligible_step_count >= 0 AND eligible_step_count <= step_count),
    CONSTRAINT CK_validated_step_records_status CHECK (validation_status IN ('accepted', 'rejected', 'suspicious')),
    CONSTRAINT CK_validated_step_records_motion_score CHECK (motion_score BETWEEN 0 AND 100),
    CONSTRAINT CK_validated_step_records_motion_status CHECK (motion_status IN ('accepted', 'suspicious', 'rejected', 'unavailable')),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (step_session_id) REFERENCES pvp_step_sessions(step_session_id),
    FOREIGN KEY (batch_id) REFERENCES step_sensor_batches(step_sensor_batch_id)
);
GO

CREATE TABLE pvp_reward_rules (
    pvp_reward_rule_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_reward_rules_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_type_code VARCHAR(20) NOT NULL,
    result_code VARCHAR(20) NOT NULL,
    reward_package_id UNIQUEIDENTIFIER NOT NULL,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_reward_rules_active DEFAULT 1,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_reward_rules_updated_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_reward_rules_type CHECK (match_type_code IN ('ranked', 'friendly', 'event')),
    CONSTRAINT CK_pvp_reward_rules_result CHECK (result_code IN ('win', 'lose', 'draw')),
    FOREIGN KEY (reward_package_id) REFERENCES reward_packages(reward_package_id)
);
GO

CREATE TABLE pvp_match_reward_snapshots (
    match_reward_snapshot_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_reward_snapshots_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    result_code VARCHAR(20) NOT NULL,
    wallet_amount INT NOT NULL,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_match_reward_snapshots_created_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_match_reward_snapshots_result CHECK (result_code IN ('win', 'lose', 'draw')),
    CONSTRAINT CK_pvp_match_reward_snapshots_amount CHECK (wallet_amount >= 0),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id) ON DELETE CASCADE
);
GO

CREATE TABLE pvp_match_reward_snapshot_items (
    match_reward_snapshot_id UNIQUEIDENTIFIER NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    quantity INT NOT NULL,
    PRIMARY KEY (match_reward_snapshot_id, item_id),
    CONSTRAINT CK_pvp_match_reward_snapshot_items_quantity CHECK (quantity > 0),
    FOREIGN KEY (match_reward_snapshot_id) REFERENCES pvp_match_reward_snapshots(match_reward_snapshot_id) ON DELETE CASCADE,
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE pvp_match_reward_entitlements (
    match_reward_entitlement_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_reward_entitlements_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    user_id UNIQUEIDENTIFIER NOT NULL,
    result_code VARCHAR(20) NOT NULL,
    wallet_amount INT NOT NULL,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_match_reward_entitlements_created_at DEFAULT SYSUTCDATETIME(),
    claimed_at DATETIME2(0) NULL,
    CONSTRAINT CK_pvp_match_reward_entitlements_amount CHECK (wallet_amount >= 0),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE pvp_match_reward_items (
    match_reward_entitlement_id UNIQUEIDENTIFIER NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    quantity INT NOT NULL,
    PRIMARY KEY (match_reward_entitlement_id, item_id),
    CONSTRAINT CK_pvp_match_reward_items_quantity CHECK (quantity > 0),
    FOREIGN KEY (match_reward_entitlement_id) REFERENCES pvp_match_reward_entitlements(match_reward_entitlement_id),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE pvp_match_events (
    pvp_match_event_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_events_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    sequence BIGINT NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload_json NVARCHAR(MAX) NOT NULL,
    created_at DATETIME2(3) NOT NULL CONSTRAINT DF_pvp_match_events_created_at DEFAULT SYSUTCDATETIME(),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id)
);
GO

CREATE TABLE outbox_events (
    event_id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    aggregate_type VARCHAR(50) NOT NULL,
    aggregate_id UNIQUEIDENTIFIER NOT NULL,
    destination VARCHAR(30) NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload_json NVARCHAR(MAX) NOT NULL,
    attempts INT NOT NULL CONSTRAINT DF_outbox_events_attempts DEFAULT 0,
    lease_until DATETIME2(3) NULL,
    lease_owner NVARCHAR(100) NULL,
    published_at DATETIME2(3) NULL,
    created_at DATETIME2(3) NOT NULL CONSTRAINT DF_outbox_events_created_at DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE pvp_item_effect_definitions (
    pvp_item_effect_definition_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_item_effect_definitions_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    item_id UNIQUEIDENTIFIER NOT NULL,
    effect_code VARCHAR(30) NOT NULL,
    target_code VARCHAR(20) NOT NULL,
    magnitude_bps INT NOT NULL,
    duration_ms INT NOT NULL,
    cooldown_ms INT NOT NULL,
    asset_key NVARCHAR(300) NOT NULL,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_item_effect_definitions_active DEFAULT 1,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_item_effect_definitions_updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_item_effect_definitions_target CHECK (target_code IN ('self', 'opponent')),
    CONSTRAINT CK_pvp_item_effect_definitions_values CHECK (magnitude_bps >= 0 AND duration_ms >= 0 AND cooldown_ms >= 0),
    CONSTRAINT UQ_pvp_item_effect_definitions_item UNIQUE (item_id),
    CONSTRAINT UQ_pvp_item_effect_definitions_code UNIQUE (effect_code),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE pvp_player_loadout_slots (
    user_id UNIQUEIDENTIFIER NOT NULL,
    slot_no TINYINT NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_player_loadout_slots_updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_pvp_player_loadout_slots PRIMARY KEY (user_id, slot_no),
    CONSTRAINT CK_pvp_player_loadout_slots_slot CHECK (slot_no BETWEEN 1 AND 2),
    CONSTRAINT UQ_pvp_player_loadout_slots_item UNIQUE (user_id, item_id),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE pvp_bot_loadout_slots (
    bot_profile_id UNIQUEIDENTIFIER NOT NULL,
    slot_no TINYINT NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT PK_pvp_bot_loadout_slots PRIMARY KEY (bot_profile_id, slot_no),
    CONSTRAINT CK_pvp_bot_loadout_slots_slot CHECK (slot_no BETWEEN 1 AND 2),
    CONSTRAINT UQ_pvp_bot_loadout_slots_item UNIQUE (bot_profile_id, item_id),
    FOREIGN KEY (bot_profile_id) REFERENCES pvp_bot_profiles(bot_profile_id),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE pvp_match_loadout_slots (
    pvp_match_loadout_slot_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_loadout_slots_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    match_player_id UNIQUEIDENTIFIER NOT NULL,
    slot_no TINYINT NOT NULL,
    item_id UNIQUEIDENTIFIER NOT NULL,
    effect_code VARCHAR(30) NOT NULL,
    target_code VARCHAR(20) NOT NULL,
    magnitude_bps INT NOT NULL,
    duration_ms INT NOT NULL,
    cooldown_ms INT NOT NULL,
    asset_key NVARCHAR(300) NOT NULL,
    used_at DATETIME2(3) NULL,
    CONSTRAINT CK_pvp_match_loadout_slots_slot CHECK (slot_no BETWEEN 1 AND 2),
    CONSTRAINT CK_pvp_match_loadout_slots_target CHECK (target_code IN ('self', 'opponent')),
    CONSTRAINT UQ_pvp_match_loadout_slots_player_slot UNIQUE (match_player_id, slot_no),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id),
    FOREIGN KEY (match_player_id) REFERENCES pvp_match_players(match_player_id),
    FOREIGN KEY (item_id) REFERENCES items(item_id)
);
GO

CREATE TABLE pvp_match_item_actions (
    pvp_match_item_action_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_item_actions_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    actor_match_player_id UNIQUEIDENTIFIER NOT NULL,
    target_match_player_id UNIQUEIDENTIFIER NULL,
    match_loadout_slot_id UNIQUEIDENTIFIER NOT NULL,
    client_action_id UNIQUEIDENTIFIER NOT NULL,
    result_code VARCHAR(20) NOT NULL,
    effect_code VARCHAR(30) NOT NULL,
    created_at DATETIME2(3) NOT NULL CONSTRAINT DF_pvp_match_item_actions_created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_match_item_actions_result CHECK (result_code IN ('applied', 'blocked', 'cleansed')),
    CONSTRAINT UQ_pvp_match_item_actions_idempotency UNIQUE (actor_match_player_id, client_action_id),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id),
    FOREIGN KEY (actor_match_player_id) REFERENCES pvp_match_players(match_player_id),
    FOREIGN KEY (target_match_player_id) REFERENCES pvp_match_players(match_player_id),
    FOREIGN KEY (match_loadout_slot_id) REFERENCES pvp_match_loadout_slots(pvp_match_loadout_slot_id)
);
GO

CREATE TABLE pvp_match_effects (
    pvp_match_effect_id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pvp_match_effects_id DEFAULT NEWSEQUENTIALID() PRIMARY KEY,
    match_id UNIQUEIDENTIFIER NOT NULL,
    target_match_player_id UNIQUEIDENTIFIER NOT NULL,
    source_match_player_id UNIQUEIDENTIFIER NULL,
    source_item_action_id UNIQUEIDENTIFIER NULL,
    effect_code VARCHAR(30) NOT NULL,
    effect_kind_code VARCHAR(20) NOT NULL,
    magnitude_bps INT NOT NULL,
    status_code VARCHAR(20) NOT NULL CONSTRAINT DF_pvp_match_effects_status DEFAULT 'active',
    starts_at DATETIME2(3) NOT NULL,
    ends_at DATETIME2(3) NOT NULL,
    consumed_at DATETIME2(3) NULL,
    created_at DATETIME2(3) NOT NULL CONSTRAINT DF_pvp_match_effects_created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_match_effects_kind CHECK (effect_kind_code IN ('buff', 'debuff', 'shield', 'passive')),
    CONSTRAINT CK_pvp_match_effects_status CHECK (status_code IN ('active', 'expired', 'consumed', 'cleansed')),
    CONSTRAINT CK_pvp_match_effects_values CHECK (magnitude_bps >= 0 AND ends_at >= starts_at),
    FOREIGN KEY (match_id) REFERENCES pvp_matches(match_id),
    FOREIGN KEY (target_match_player_id) REFERENCES pvp_match_players(match_player_id),
    FOREIGN KEY (source_match_player_id) REFERENCES pvp_match_players(match_player_id),
    FOREIGN KEY (source_item_action_id) REFERENCES pvp_match_item_actions(pvp_match_item_action_id)
);
GO

CREATE TABLE pvp_spirit_speed_rules (
    affinity_code VARCHAR(30) NOT NULL PRIMARY KEY,
    start_minute INT NOT NULL,
    end_minute INT NOT NULL,
    bonus_bps INT NOT NULL,
    time_zone_code VARCHAR(50) NOT NULL CONSTRAINT DF_pvp_spirit_speed_rules_timezone DEFAULT 'Asia/Ho_Chi_Minh',
    is_active BIT NOT NULL CONSTRAINT DF_pvp_spirit_speed_rules_active DEFAULT 1,
    updated_at DATETIME2(0) NOT NULL CONSTRAINT DF_pvp_spirit_speed_rules_updated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_pvp_spirit_speed_rules_values CHECK (start_minute BETWEEN 0 AND 1439 AND end_minute BETWEEN 0 AND 1439 AND bonus_bps >= 0)
);
GO

CREATE TABLE pvp_rank_tiers (
    tier_code VARCHAR(30) NOT NULL PRIMARY KEY,
    display_name NVARCHAR(80) NOT NULL,
    min_mmr INT NOT NULL UNIQUE,
    sort_order SMALLINT NOT NULL UNIQUE,
    asset_key NVARCHAR(300) NOT NULL,
    color_hex CHAR(7) NOT NULL,
    is_active BIT NOT NULL CONSTRAINT DF_pvp_rank_tiers_active DEFAULT 1,
    CONSTRAINT CK_pvp_rank_tiers_color CHECK (color_hex LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]')
);
GO
CREATE TABLE StreakRewardClaim
(
    UserId UNIQUEIDENTIFIER NOT NULL,
    ClaimDate DATE NOT NULL,
    Streak INT NOT NULL,
    Reward INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT PK_StreakRewardClaim
        PRIMARY KEY (UserId, ClaimDate),

    CONSTRAINT FK_StreakRewardClaim_User
        FOREIGN KEY (UserId)
        REFERENCES users(user_id)
);

CREATE TABLE daily_login_reward_claims (
    user_id UNIQUEIDENTIFIER NOT NULL,
    claim_date DATE NOT NULL,
    cycle_day INT NOT NULL,
    reward INT NOT NULL,
    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_daily_login_reward_claims_created_at DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_daily_login_reward_claims PRIMARY KEY (user_id, claim_date),
    CONSTRAINT CK_daily_login_reward_claims_cycle_day
        CHECK (cycle_day BETWEEN 1 AND 7),
    CONSTRAINT CK_daily_login_reward_claims_reward
        CHECK (reward >= 0),
    CONSTRAINT FK_daily_login_reward_claims_users
        FOREIGN KEY (user_id) REFERENCES users(user_id)
);
GO

CREATE TABLE user_feedbacks (
    feedback_id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_user_feedbacks_feedback_id DEFAULT NEWSEQUENTIALID()
        PRIMARY KEY,

    user_id UNIQUEIDENTIFIER NOT NULL,

    feedback_type_code VARCHAR(20) NOT NULL,


    content NVARCHAR(2000) NOT NULL,

    status_code VARCHAR(20) NOT NULL
        CONSTRAINT DF_user_feedbacks_status_code DEFAULT 'pending',

    admin_note NVARCHAR(1000) NULL,

    handled_by_user_id UNIQUEIDENTIFIER NULL,

    handled_at DATETIME2(0) NULL,

    created_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_user_feedbacks_created_at DEFAULT SYSUTCDATETIME(),

    updated_at DATETIME2(0) NOT NULL
        CONSTRAINT DF_user_feedbacks_updated_at DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_user_feedbacks_feedback_type_code
        CHECK (
            feedback_type_code IN (
                'suggestion',
                'bug_report'
            )
        ),

    CONSTRAINT CK_user_feedbacks_status_code
        CHECK (
            status_code IN (
                'pending',
                'in_progress',
                'resolved',
                'rejected'
            )
        ),

    CONSTRAINT CK_user_feedbacks_content_not_empty
        CHECK (LEN(LTRIM(RTRIM(content))) > 0),

    CONSTRAINT CK_user_feedbacks_handled_data
        CHECK (
            (status_code IN ('pending', 'in_progress'))
            OR
            (
                status_code IN ('resolved', 'rejected')
                AND handled_by_user_id IS NOT NULL
                AND handled_at IS NOT NULL
            )
        ),

    FOREIGN KEY (user_id)
        REFERENCES users(user_id),

    FOREIGN KEY (handled_by_user_id)
        REFERENCES users(user_id)
);
CREATE TABLE audit_logs
(
    audit_log_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),

    user_id UNIQUEIDENTIFIER NULL,

    action NVARCHAR(20) NOT NULL,

    table_name NVARCHAR(100) NOT NULL,

    record_id NVARCHAR(100) NULL,

    old_values NVARCHAR(MAX) NULL,

    new_values NVARCHAR(MAX) NULL,

    created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_user_feedbacks_user_created
    ON user_feedbacks(user_id, created_at DESC);
GO

CREATE INDEX IX_user_feedbacks_status_created
    ON user_feedbacks(status_code, created_at DESC);
GO

CREATE INDEX IX_user_feedbacks_type_status
    ON user_feedbacks(feedback_type_code, status_code);
GO
/* Essential indexes */


CREATE INDEX IX_external_logins_user_id
    ON external_logins(user_id);
GO


CREATE INDEX IX_otp_requests_user_purpose_status
    ON otp_requests(user_id, purpose_code, status_code, expires_at);
GO

CREATE INDEX IX_pet_evolution_history_user_time
    ON pet_evolution_history(user_id, evolved_at DESC);
GO

CREATE INDEX IX_daily_steps_step_date_step_count
    ON daily_steps(step_date, step_count DESC);
GO

CREATE INDEX IX_step_goals_user_effective_from_desc
    ON step_goals(user_id, effective_from DESC);
GO

CREATE INDEX IX_missions_type_active_window
    ON missions(mission_type_code, is_active, start_at, end_at);
GO

CREATE INDEX IX_mission_conditions_mission_group
    ON mission_conditions(mission_id, condition_group);
GO

CREATE INDEX IX_user_missions_user_status_cycle
    ON user_missions(user_id, status_code, cycle_date DESC);
GO

CREATE INDEX IX_user_achievements_user_claimed
    ON user_achievements(user_id, claimed_at);
GO

CREATE INDEX IX_shop_items_active_price
    ON shop_items(is_active, price_amount);
GO

CREATE INDEX IX_shop_purchases_user_time
    ON shop_purchases(user_id, purchased_at DESC);
GO

CREATE INDEX IX_friend_requests_receiver_status_created
    ON friend_requests(receiver_user_id, status_code, created_at DESC);
GO

CREATE UNIQUE INDEX UX_friend_requests_pending_sender_receiver
    ON friend_requests(sender_user_id, receiver_user_id)
    WHERE status_code = 'pending';
GO

CREATE INDEX IX_user_notifications_user_unread
    ON user_notifications(user_id, deleted_at, read_at);
GO

CREATE INDEX IX_device_tokens_user_active
    ON device_tokens(user_id, is_active);
GO

CREATE INDEX IX_notifications_schedule
    ON notifications(scheduled_at, created_at DESC);
GO

CREATE INDEX IX_notifications_admin_list
    ON notifications(status_code, target_audience_code, created_at DESC);
GO

CREATE INDEX IX_matchmaking_queue_status_type_time
    ON matchmaking_queue(status_code, match_type_code, queued_at);
GO
CREATE INDEX IX_matchmaking_queue_status_fallback
    ON matchmaking_queue(status_code, bot_fallback_at, queued_at);
GO

CREATE UNIQUE INDEX UX_pvp_matchmaking_policies_active
    ON pvp_matchmaking_policies(is_active)
    WHERE is_active = 1;
GO

CREATE INDEX IX_pvp_bot_profiles_active_difficulty_mmr
    ON pvp_bot_profiles(is_active, difficulty_code, mmr);
GO

CREATE INDEX IX_pvp_matches_status_type_created
    ON pvp_matches(status_code, match_type_code, created_at DESC);
GO

CREATE INDEX IX_pvp_match_players_user_match
    ON pvp_match_players(user_id, match_id);
GO

CREATE UNIQUE INDEX UX_pvp_match_players_match_user
    ON pvp_match_players(match_id, user_id)
    WHERE user_id IS NOT NULL;
GO
CREATE UNIQUE INDEX UX_pvp_match_players_match_bot
    ON pvp_match_players(match_id, bot_profile_id)
    WHERE bot_profile_id IS NOT NULL;
GO

CREATE UNIQUE INDEX UX_pvp_sprint_invites_pending_pair
    ON pvp_sprint_invites(user_low_id, user_high_id)
    WHERE status_code = 'pending';
GO
CREATE INDEX IX_pvp_sprint_invites_incoming
    ON pvp_sprint_invites(invitee_user_id, status_code, expires_at DESC);
GO
CREATE INDEX IX_pvp_player_activities_type_due
    ON pvp_player_activities(activity_type, due_at);
GO
CREATE UNIQUE INDEX UX_pvp_step_sessions_match_user
    ON pvp_step_sessions(match_id, user_id)
    WHERE match_id IS NOT NULL;
GO
CREATE UNIQUE INDEX UX_pvp_step_sessions_active_user
    ON pvp_step_sessions(user_id)
    WHERE status_code = 'active';
GO
CREATE UNIQUE INDEX UX_step_sensor_batches_session_sequence
    ON step_sensor_batches(step_session_id, sequence);
GO
CREATE UNIQUE INDEX UX_step_sensor_batches_session_hash
    ON step_sensor_batches(step_session_id, payload_hash);
GO
CREATE UNIQUE INDEX UX_step_motion_windows_batch_index
    ON step_motion_evidence_windows(batch_id, window_index);
GO
CREATE INDEX IX_step_motion_windows_boot_elapsed
    ON step_motion_evidence_windows(boot_session_id, window_start_elapsed_realtime_ns, window_end_elapsed_realtime_ns);
GO
CREATE INDEX IX_step_motion_windows_classification_started
    ON step_motion_evidence_windows(classification, window_started_at);
GO
CREATE UNIQUE INDEX UX_validated_step_records_user_hash
    ON validated_step_records(user_id, payload_hash);
GO
CREATE UNIQUE INDEX UX_validated_step_records_batch_event
    ON validated_step_records(batch_id, event_index)
    WHERE batch_id IS NOT NULL;
GO
CREATE UNIQUE INDEX UX_pvp_reward_rules_type_result
    ON pvp_reward_rules(match_type_code, result_code);
GO
CREATE UNIQUE INDEX UX_pvp_match_reward_snapshots_match_result
    ON pvp_match_reward_snapshots(match_id, result_code);
GO
CREATE UNIQUE INDEX UX_pvp_match_reward_entitlements_match_user
    ON pvp_match_reward_entitlements(match_id, user_id);
GO
CREATE UNIQUE INDEX UX_pvp_match_events_match_sequence
    ON pvp_match_events(match_id, sequence);
GO
CREATE INDEX IX_outbox_events_dispatch
    ON outbox_events(published_at, lease_until);
GO
CREATE INDEX IX_pets_pvp_affinity_code
    ON pets(pvp_affinity_code)
    WHERE pvp_affinity_code IS NOT NULL;
GO
CREATE INDEX IX_pvp_match_effects_active_due
    ON pvp_match_effects(match_id, target_match_player_id, status_code, ends_at);
GO
CREATE INDEX IX_pvp_match_item_actions_match_created
    ON pvp_match_item_actions(match_id, created_at);
GO

IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'pvp_daily_step_limit')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('pvp_daily_step_limit', '100000');
IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'utc_pet_timestamp_backfill_v1')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('utc_pet_timestamp_backfill_v1', 'fresh_schema_utc');
GO

UPDATE pets SET pvp_affinity_code = 'sprout' WHERE pvp_affinity_code IS NULL AND LOWER(pet_name) IN ('stater', 'starter', N'mầm non');
UPDATE pets SET pvp_affinity_code = 'warm_sun' WHERE pvp_affinity_code IS NULL AND pet_name LIKE N'%Nắng Ấm%';
UPDATE pets SET pvp_affinity_code = 'dawn' WHERE pvp_affinity_code IS NULL AND pet_name LIKE N'%Bình Minh%';
UPDATE pets SET pvp_affinity_code = 'moonlight' WHERE pvp_affinity_code IS NULL AND pet_name LIKE N'%Ánh Trăng%';
GO

IF NOT EXISTS (SELECT 1 FROM item_types WHERE item_type_name = N'Pet Consumable')
    INSERT INTO item_types(item_type_name, IsActive) VALUES (N'Pet Consumable', 1);
IF NOT EXISTS (SELECT 1 FROM item_types WHERE item_type_name = N'PvP Buff')
    INSERT INTO item_types(item_type_name, IsActive) VALUES (N'PvP Buff', 1);
IF NOT EXISTS (SELECT 1 FROM item_types WHERE item_type_name = N'PvP Debuff')
    INSERT INTO item_types(item_type_name, IsActive) VALUES (N'PvP Debuff', 1);
IF NOT EXISTS (SELECT 1 FROM item_types WHERE item_type_name = N'PvP Utility')
    INSERT INTO item_types(item_type_name, IsActive) VALUES (N'PvP Utility', 1);

DECLARE @PvpBuffType UNIQUEIDENTIFIER = (SELECT item_type_id FROM item_types WHERE item_type_name = N'PvP Buff');
DECLARE @PvpDebuffType UNIQUEIDENTIFIER = (SELECT item_type_id FROM item_types WHERE item_type_name = N'PvP Debuff');
DECLARE @PvpUtilityType UNIQUEIDENTIFIER = (SELECT item_type_id FROM item_types WHERE item_type_name = N'PvP Utility');

IF NOT EXISTS (SELECT 1 FROM items WHERE effect_type_code = 'pvp_speed_up')
    INSERT INTO items(item_name, img_url, item_type_id, effect_type_code, effect_value, description, is_active)
    VALUES(N'Bùa Gió Nhanh', N'Assets/Mobile/PVP/Items/pvp_speed_up.png', @PvpBuffType, 'pvp_speed_up', 1500, N'Tăng 15% tốc độ trong Lumina Sprint trong 5 giây.', 1);
IF NOT EXISTS (SELECT 1 FROM items WHERE effect_type_code = 'pvp_speed_down')
    INSERT INTO items(item_name, img_url, item_type_id, effect_type_code, effect_value, description, is_active)
    VALUES(N'Bẫy Sương Chậm', N'Assets/Mobile/PVP/Items/pvp_speed_down.png', @PvpDebuffType, 'pvp_speed_down', 1500, N'Giảm 15% tốc độ đối thủ trong 5 giây.', 1);
IF NOT EXISTS (SELECT 1 FROM items WHERE effect_type_code = 'pvp_cleanse')
    INSERT INTO items(item_name, img_url, item_type_id, effect_type_code, effect_value, description, is_active)
    VALUES(N'Giọt Sương Thanh Tẩy', N'Assets/Mobile/PVP/Items/pvp_cleanse.png', @PvpUtilityType, 'pvp_cleanse', 0, N'Xóa toàn bộ debuff tốc độ đang hoạt động.', 1);
IF NOT EXISTS (SELECT 1 FROM items WHERE effect_type_code = 'pvp_shield')
    INSERT INTO items(item_name, img_url, item_type_id, effect_type_code, effect_value, description, is_active)
    VALUES(N'Khiên Lá Lumina', N'Assets/Mobile/PVP/Items/pvp_shield.png', @PvpUtilityType, 'pvp_shield', 0, N'Chặn một debuff trong 8 giây.', 1);

IF NOT EXISTS (SELECT 1 FROM pvp_item_effect_definitions WHERE effect_code = 'pvp_speed_up')
    INSERT INTO pvp_item_effect_definitions(item_id, effect_code, target_code, magnitude_bps, duration_ms, cooldown_ms, asset_key)
    SELECT item_id, 'pvp_speed_up', 'self', 1500, 5000, 5000, N'Assets/Mobile/PVP/Items/pvp_speed_up.png' FROM items WHERE effect_type_code = 'pvp_speed_up';
IF NOT EXISTS (SELECT 1 FROM pvp_item_effect_definitions WHERE effect_code = 'pvp_speed_down')
    INSERT INTO pvp_item_effect_definitions(item_id, effect_code, target_code, magnitude_bps, duration_ms, cooldown_ms, asset_key)
    SELECT item_id, 'pvp_speed_down', 'opponent', 1500, 5000, 10000, N'Assets/Mobile/PVP/Items/pvp_speed_down.png' FROM items WHERE effect_type_code = 'pvp_speed_down';
IF NOT EXISTS (SELECT 1 FROM pvp_item_effect_definitions WHERE effect_code = 'pvp_cleanse')
    INSERT INTO pvp_item_effect_definitions(item_id, effect_code, target_code, magnitude_bps, duration_ms, cooldown_ms, asset_key)
    SELECT item_id, 'pvp_cleanse', 'self', 0, 0, 5000, N'Assets/Mobile/PVP/Items/pvp_cleanse.png' FROM items WHERE effect_type_code = 'pvp_cleanse';
IF NOT EXISTS (SELECT 1 FROM pvp_item_effect_definitions WHERE effect_code = 'pvp_shield')
    INSERT INTO pvp_item_effect_definitions(item_id, effect_code, target_code, magnitude_bps, duration_ms, cooldown_ms, asset_key)
    SELECT item_id, 'pvp_shield', 'self', 0, 8000, 15000, N'Assets/Mobile/PVP/Items/pvp_shield.png' FROM items WHERE effect_type_code = 'pvp_shield';
GO

IF NOT EXISTS (SELECT 1 FROM pvp_spirit_speed_rules WHERE affinity_code = 'sprout')
    INSERT INTO pvp_spirit_speed_rules VALUES ('sprout', 0, 1439, 0, 'Asia/Ho_Chi_Minh', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM pvp_spirit_speed_rules WHERE affinity_code = 'dawn')
    INSERT INTO pvp_spirit_speed_rules VALUES ('dawn', 360, 719, 1000, 'Asia/Ho_Chi_Minh', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM pvp_spirit_speed_rules WHERE affinity_code = 'warm_sun')
    INSERT INTO pvp_spirit_speed_rules VALUES ('warm_sun', 720, 1079, 1000, 'Asia/Ho_Chi_Minh', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM pvp_spirit_speed_rules WHERE affinity_code = 'moonlight')
    INSERT INTO pvp_spirit_speed_rules VALUES ('moonlight', 1080, 359, 1000, 'Asia/Ho_Chi_Minh', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM pvp_rank_tiers WHERE tier_code = 'mam_sang')
    INSERT INTO pvp_rank_tiers VALUES ('mam_sang', N'Mầm Sáng', -2147483648, 1, N'Assets/Mobile/PVP/Rank/mam_sang.png', '#91B95A', 1);
IF NOT EXISTS (SELECT 1 FROM pvp_rank_tiers WHERE tier_code = 'choi_sang')
    INSERT INTO pvp_rank_tiers VALUES ('choi_sang', N'Chồi Sáng', 1100, 2, N'Assets/Mobile/PVP/Rank/choi_sang.png', '#70C987', 1);
IF NOT EXISTS (SELECT 1 FROM pvp_rank_tiers WHERE tier_code = 'tan_sang')
    INSERT INTO pvp_rank_tiers VALUES ('tan_sang', N'Tán Sáng', 1300, 3, N'Assets/Mobile/PVP/Rank/tan_sang.png', '#43B9A9', 1);
IF NOT EXISTS (SELECT 1 FROM pvp_rank_tiers WHERE tier_code = 'linh_quang')
    INSERT INTO pvp_rank_tiers VALUES ('linh_quang', N'Linh Quang', 1500, 4, N'Assets/Mobile/PVP/Rank/linh_quang.png', '#5C8DE8', 1);
IF NOT EXISTS (SELECT 1 FROM pvp_rank_tiers WHERE tier_code = 'tinh_tu')
    INSERT INTO pvp_rank_tiers VALUES ('tinh_tu', N'Tinh Tú', 1700, 5, N'Assets/Mobile/PVP/Rank/tinh_tu.png', '#9A6BE8', 1);
IF NOT EXISTS (SELECT 1 FROM pvp_rank_tiers WHERE tier_code = 'lumina')
    INSERT INTO pvp_rank_tiers VALUES ('lumina', N'Lumina', 1900, 6, N'Assets/Mobile/PVP/Rank/lumina.png', '#F3C969', 1);
GO

IF NOT EXISTS (SELECT 1 FROM roles WHERE role_code = '0')
BEGIN
    INSERT INTO roles (role_code, role_name)
    VALUES ('0', N'User');
END;

IF NOT EXISTS (SELECT 1 FROM roles WHERE role_code = '1')
BEGIN
    INSERT INTO roles (role_code, role_name)
    VALUES ('1', N'Admin');
END;

IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'otp_verify_email_expire_minutes')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('otp_verify_email_expire_minutes', '5');

IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'otp_max_attempts')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('otp_max_attempts', '5');

IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'otp_resend_cooldown_seconds')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('otp_resend_cooldown_seconds', '60');

IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'otp_send_ip_window_minutes')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('otp_send_ip_window_minutes', '10');

IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'otp_send_ip_max_count')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('otp_send_ip_max_count', '5');

IF NOT EXISTS (SELECT 1 FROM system_settings WHERE setting_key = 'pending_registration_ttl_hours')
    INSERT INTO system_settings (setting_key, setting_value) VALUES ('pending_registration_ttl_hours', '24');

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_otp_requests_verify_email_pending_user'
      AND object_id = OBJECT_ID('otp_requests')
)
BEGIN
    CREATE UNIQUE INDEX UX_otp_requests_verify_email_pending_user
        ON otp_requests (user_id, purpose_code, status_code)
        WHERE purpose_code = 'verify_email'
          AND status_code = 'pending';
END;
