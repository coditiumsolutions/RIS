-- Creates VisitorRequests table for visitor pre-registration.
-- Safe to re-run.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.VisitorRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VisitorRequests
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        ResidentId INT NOT NULL,
        VisitorName NVARCHAR(150) NOT NULL,
        Phone NVARCHAR(30) NOT NULL,
        Purpose NVARCHAR(300) NOT NULL,
        Duration NVARCHAR(30) NOT NULL,
        ArrivalDate DATE NOT NULL,
        NumberOfGuests INT NOT NULL CONSTRAINT DF_VisitorRequests_NumberOfGuests DEFAULT (1),
        CNIC NVARCHAR(30) NULL,
        VehicleRegistrationNo NVARCHAR(30) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT DF_VisitorRequests_Status DEFAULT (N'Pending'),
        IsWalkIn BIT NOT NULL CONSTRAINT DF_VisitorRequests_IsWalkIn DEFAULT (0),
        CheckedInAt DATETIME2(0) NULL,
        CheckedOutAt DATETIME2(0) NULL,
        CheckedInByUserId INT NULL,
        CheckedInByUserType NVARCHAR(20) NULL,
        ApprovedByUserId INT NULL,
        ApprovedAt DATETIME2(0) NULL,
        DeclinedByUserId INT NULL,
        DeclinedAt DATETIME2(0) NULL,
        DeclineReason NVARCHAR(500) NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_VisitorRequests_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_VisitorRequests_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_VisitorRequests PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT FK_VisitorRequests_Residents FOREIGN KEY (ResidentId) REFERENCES dbo.Residents (uid),
        CONSTRAINT CK_VisitorRequests_VisitorName_NotBlank CHECK (LEN(LTRIM(RTRIM(VisitorName))) > 0),
        CONSTRAINT CK_VisitorRequests_Phone_NotBlank CHECK (LEN(LTRIM(RTRIM(Phone))) > 0),
        CONSTRAINT CK_VisitorRequests_Purpose_NotBlank CHECK (LEN(LTRIM(RTRIM(Purpose))) > 0),
        CONSTRAINT CK_VisitorRequests_Duration_NotBlank CHECK (LEN(LTRIM(RTRIM(Duration))) > 0),
        CONSTRAINT CK_VisitorRequests_NumberOfGuests_Positive CHECK (NumberOfGuests >= 1),
        CONSTRAINT CK_VisitorRequests_Status_Valid CHECK ([Status] IN (N'Pending', N'Approved', N'Declined', N'CheckedIn', N'CheckedOut', N'Cancelled'))
    );

    CREATE INDEX IX_VisitorRequests_ResidentId ON dbo.VisitorRequests (ResidentId);
    CREATE INDEX IX_VisitorRequests_ArrivalDate ON dbo.VisitorRequests (ArrivalDate) INCLUDE ([Status], VisitorName);
    CREATE INDEX IX_VisitorRequests_Status ON dbo.VisitorRequests ([Status]) INCLUDE (ArrivalDate, VisitorName);
    CREATE INDEX IX_VisitorRequests_CreatedAt ON dbo.VisitorRequests (CreatedAt DESC);
END
GO
