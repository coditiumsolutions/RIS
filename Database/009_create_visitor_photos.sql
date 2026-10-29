-- Creates VisitorPhotos for optional check-in / check-out camera captures.
-- Additive only. Safe to re-run (IF NOT EXISTS).

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.VisitorPhotos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VisitorPhotos
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        VisitorRequestId INT NOT NULL,
        Kind NVARCHAR(20) NOT NULL,
        FilePath NVARCHAR(80) NOT NULL,
        ContentType NVARCHAR(50) NOT NULL,
        SizeBytes INT NOT NULL,
        CapturedByReceptionistId INT NULL,
        CapturedByAdminId INT NULL,
        CapturedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_VisitorPhotos_CapturedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VisitorPhotos PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT FK_VisitorPhotos_VisitorRequests FOREIGN KEY (VisitorRequestId) REFERENCES dbo.VisitorRequests (uid),
        CONSTRAINT CK_VisitorPhotos_Kind_Valid CHECK (Kind IN (N'CheckIn', N'CheckOut')),
        CONSTRAINT CK_VisitorPhotos_FilePath_NotBlank CHECK (LEN(LTRIM(RTRIM(FilePath))) > 0),
        CONSTRAINT CK_VisitorPhotos_ContentType_NotBlank CHECK (LEN(LTRIM(RTRIM(ContentType))) > 0),
        CONSTRAINT CK_VisitorPhotos_SizeBytes_Positive CHECK (SizeBytes > 0),
        CONSTRAINT CK_VisitorPhotos_CapturedBy_OneOrNone CHECK (
            NOT (CapturedByReceptionistId IS NOT NULL AND CapturedByAdminId IS NOT NULL)
        )
    );

    CREATE INDEX IX_VisitorPhotos_VisitorRequestId ON dbo.VisitorPhotos (VisitorRequestId) INCLUDE (Kind, CapturedAtUtc);
    CREATE INDEX IX_VisitorPhotos_CapturedAtUtc ON dbo.VisitorPhotos (CapturedAtUtc DESC);
END
GO

PRINT 'VisitorPhotos table checked.';
GO
