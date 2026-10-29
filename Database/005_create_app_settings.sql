-- Creates AppSettings table for configurable application settings.
-- Safe to re-run. Seeds default settings only if they do not exist.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.AppSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppSettings
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        SettingKey NVARCHAR(100) NOT NULL,
        SettingValue NVARCHAR(500) NOT NULL,
        Description NVARCHAR(500) NULL,
        UpdatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AppSettings_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AppSettings PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT UQ_AppSettings_SettingKey UNIQUE (SettingKey),
        CONSTRAINT CK_AppSettings_SettingKey_NotBlank CHECK (LEN(LTRIM(RTRIM(SettingKey))) > 0)
    );
END
GO

-- Seed default settings (only if they don't exist)
-- VisitorApprovalRequired: false = auto-approve (this customer), true = admin must approve
INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description)
SELECT v.SettingKey, v.SettingValue, v.Description
FROM (VALUES 
    (N'VisitorApprovalRequired', N'false', N'When false, visitor requests are auto-approved. When true, admin must approve each request.'),
    (N'MaxGuestsPerVisit', N'10', N'Maximum number of guests allowed per visitor request. Configurable pending business decision.')
) AS v (SettingKey, SettingValue, Description)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.AppSettings AS s
    WHERE s.SettingKey = v.SettingKey
);
GO
