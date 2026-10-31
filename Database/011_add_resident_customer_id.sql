-- Add mandatory unique Residents.CustomerID for PMS linkage.
-- Safe to re-run (idempotent). Run on the server BEFORE merging the app change.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Residents', N'U') IS NULL
BEGIN
    RAISERROR('dbo.Residents does not exist.', 16, 1);
    RETURN;
END
GO

-- 1) Add nullable column if missing
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Residents')
      AND name = N'CustomerID'
)
BEGIN
    ALTER TABLE dbo.Residents ADD CustomerID NVARCHAR(50) NULL;
END
GO

-- 2) Backfill existing test rows that are NULL or blank
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Residents')
      AND name = N'CustomerID'
)
BEGIN
    UPDATE dbo.Residents
    SET CustomerID = N'TEST-' + RIGHT(N'00000' + CAST(uid AS VARCHAR(10)), 5)
    WHERE CustomerID IS NULL
       OR LTRIM(RTRIM(CustomerID)) = N'';
END
GO

-- 3) Enforce NOT NULL once every row has a value
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Residents')
      AND name = N'CustomerID'
      AND is_nullable = 1
)
AND NOT EXISTS (
    SELECT 1
    FROM dbo.Residents
    WHERE CustomerID IS NULL
       OR LTRIM(RTRIM(CustomerID)) = N''
)
BEGIN
    ALTER TABLE dbo.Residents ALTER COLUMN CustomerID NVARCHAR(50) NOT NULL;
END
GO

-- 4) Unique index if missing
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Residents')
      AND name = N'CustomerID'
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Residents')
      AND name = N'IX_Residents_CustomerID'
)
BEGIN
    CREATE UNIQUE INDEX IX_Residents_CustomerID ON dbo.Residents (CustomerID);
END
GO

PRINT 'Residents.CustomerID column/index checked.';
GO
