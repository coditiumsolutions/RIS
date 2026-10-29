-- Resident Portal authentication schema.
-- Safe to re-run. Password hashes are created by the application, never by this script.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        RoleName NVARCHAR(50) NOT NULL,
        CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName),
        CONSTRAINT CK_Roles_RoleName_NotBlank CHECK (LEN(LTRIM(RTRIM(RoleName))) > 0)
    );
END
GO

IF OBJECT_ID(N'dbo.Residents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Residents
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        ResidentName NVARCHAR(150) NOT NULL,
        Username NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        Email NVARCHAR(256) NULL,
        Mobile NVARCHAR(30) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Residents_IsActive DEFAULT (1),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Residents_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Residents PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT CK_Residents_ResidentName_NotBlank CHECK (LEN(LTRIM(RTRIM(ResidentName))) > 0),
        CONSTRAINT CK_Residents_Username_NotBlank CHECK (LEN(LTRIM(RTRIM(Username))) > 0),
        CONSTRAINT CK_Residents_PasswordHash_NotBlank CHECK (LEN(LTRIM(RTRIM(PasswordHash))) > 0)
    );

    CREATE UNIQUE INDEX UX_Residents_Username ON dbo.Residents (Username);
END
GO

IF OBJECT_ID(N'dbo.AdminUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUsers
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        Username NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        Email NVARCHAR(256) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AdminUsers_IsActive DEFAULT (1),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AdminUsers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AdminUsers PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT CK_AdminUsers_FullName_NotBlank CHECK (LEN(LTRIM(RTRIM(FullName))) > 0),
        CONSTRAINT CK_AdminUsers_Username_NotBlank CHECK (LEN(LTRIM(RTRIM(Username))) > 0),
        CONSTRAINT CK_AdminUsers_PasswordHash_NotBlank CHECK (LEN(LTRIM(RTRIM(PasswordHash))) > 0)
    );

    CREATE UNIQUE INDEX UX_AdminUsers_Username ON dbo.AdminUsers (Username);
END
GO

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        UserType NVARCHAR(20) NOT NULL,
        UserId INT NULL,
        Action NVARCHAR(300) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT CK_AuditLogs_UserType_NotBlank CHECK (LEN(LTRIM(RTRIM(UserType))) > 0),
        CONSTRAINT CK_AuditLogs_Action_NotBlank CHECK (LEN(LTRIM(RTRIM(Action))) > 0)
    );

    CREATE INDEX IX_AuditLogs_CreatedAt ON dbo.AuditLogs (CreatedAt DESC) INCLUDE (UserType, UserId, Action);
END
GO

INSERT INTO dbo.Roles (RoleName)
SELECT v.RoleName
FROM (VALUES (N'Resident'), (N'Admin')) AS v (RoleName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Roles AS r
    WHERE r.RoleName = v.RoleName
);
GO
