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
