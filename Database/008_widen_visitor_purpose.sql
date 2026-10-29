-- QA-044: Widen VisitorRequests.Purpose to NVARCHAR(500) to match the view-model limit.
-- Safe to re-run.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.VisitorRequests')
      AND name = N'Purpose'
      AND max_length <> 1000 -- NVARCHAR(500) is 1000 bytes
)
BEGIN
    ALTER TABLE dbo.VisitorRequests ALTER COLUMN Purpose NVARCHAR(500) NOT NULL;
END
GO

PRINT 'VisitorRequests.Purpose column checked.';
GO
