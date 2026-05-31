SET QUOTED_IDENTIFIER ON;

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
        ON otp_requests (user_id)
        WHERE purpose_code = 'verify_email'
          AND status_code = 'pending';
END;
