-- QA-008 / QA-016: Add SecurityStamp for session invalidation and lockout fields for rate limiting
-- Safe to re-run (idempotent)

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- Add SecurityStamp and lockout columns to Residents
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'SecurityStamp')
BEGIN
    ALTER TABLE dbo.Residents ADD SecurityStamp NVARCHAR(36) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'FailedLoginAttempts')
BEGIN
    ALTER TABLE dbo.Residents ADD FailedLoginAttempts INT NOT NULL CONSTRAINT DF_Residents_FailedLoginAttempts DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'LockoutEndUtc')
BEGIN
    ALTER TABLE dbo.Residents ADD LockoutEndUtc DATETIME2(0) NULL;
END
GO

-- Add SecurityStamp and lockout columns to AdminUsers
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AdminUsers') AND name = N'SecurityStamp')
BEGIN
    ALTER TABLE dbo.AdminUsers ADD SecurityStamp NVARCHAR(36) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AdminUsers') AND name = N'FailedLoginAttempts')
BEGIN
    ALTER TABLE dbo.AdminUsers ADD FailedLoginAttempts INT NOT NULL CONSTRAINT DF_AdminUsers_FailedLoginAttempts DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AdminUsers') AND name = N'LockoutEndUtc')
BEGIN
    ALTER TABLE dbo.AdminUsers ADD LockoutEndUtc DATETIME2(0) NULL;
END
GO

-- Add SecurityStamp and lockout columns to Receptionists
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Receptionists') AND name = N'SecurityStamp')
BEGIN
    ALTER TABLE dbo.Receptionists ADD SecurityStamp NVARCHAR(36) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Receptionists') AND name = N'FailedLoginAttempts')
BEGIN
    ALTER TABLE dbo.Receptionists ADD FailedLoginAttempts INT NOT NULL CONSTRAINT DF_Receptionists_FailedLoginAttempts DEFAULT (0);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Receptionists') AND name = N'LockoutEndUtc')
BEGIN
    ALTER TABLE dbo.Receptionists ADD LockoutEndUtc DATETIME2(0) NULL;
END
GO

-- Generate initial SecurityStamp for existing accounts that don't have one
UPDATE dbo.Residents SET SecurityStamp = LOWER(NEWID()) WHERE SecurityStamp IS NULL;
UPDATE dbo.AdminUsers SET SecurityStamp = LOWER(NEWID()) WHERE SecurityStamp IS NULL;
UPDATE dbo.Receptionists SET SecurityStamp = LOWER(NEWID()) WHERE SecurityStamp IS NULL;
GO

PRINT 'SecurityStamp and lockout columns added successfully.';
GO
