-- Extends Residents table with address/unit fields and adds Receptionist role.
-- Safe to re-run. Adds columns and role only if they do not already exist.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- Add Receptionist role
INSERT INTO dbo.Roles (RoleName)
SELECT v.RoleName
FROM (VALUES (N'Receptionist')) AS v (RoleName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.Roles AS r
    WHERE r.RoleName = v.RoleName
);
GO

-- Add extended fields to Residents table
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'UnitNo')
BEGIN
    ALTER TABLE dbo.Residents ADD UnitNo NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'Street')
BEGIN
    ALTER TABLE dbo.Residents ADD Street NVARCHAR(150) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'Tower')
BEGIN
    ALTER TABLE dbo.Residents ADD Tower NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'Floor')
BEGIN
    ALTER TABLE dbo.Residents ADD [Floor] NVARCHAR(20) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'Block')
BEGIN
    ALTER TABLE dbo.Residents ADD Block NVARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'Mobile2')
BEGIN
    ALTER TABLE dbo.Residents ADD Mobile2 NVARCHAR(30) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'Status')
BEGIN
    ALTER TABLE dbo.Residents ADD [Status] NVARCHAR(30) NOT NULL CONSTRAINT DF_Residents_Status DEFAULT (N'Active');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Residents') AND name = N'MustChangePassword')
BEGIN
    ALTER TABLE dbo.Residents ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_Residents_MustChangePassword DEFAULT (0);
END
GO

-- Also add MustChangePassword to AdminUsers for consistency
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AdminUsers') AND name = N'MustChangePassword')
BEGIN
    ALTER TABLE dbo.AdminUsers ADD MustChangePassword BIT NOT NULL CONSTRAINT DF_AdminUsers_MustChangePassword DEFAULT (0);
END
GO
