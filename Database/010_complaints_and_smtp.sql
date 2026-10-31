-- Complaint Management + SMTP settings (additive only).
-- Safe to re-run (IF NOT EXISTS / NOT EXISTS seeds).
-- Run after 009_create_visitor_photos.sql when that script is applied.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

-- Ensure Residents.Email exists (already present in 002; keep idempotent).
IF COL_LENGTH(N'dbo.Residents', N'Email') IS NULL
BEGIN
    ALTER TABLE dbo.Residents ADD Email NVARCHAR(256) NULL;
END
GO

IF OBJECT_ID(N'dbo.ComplaintCategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplaintCategories
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ComplaintCategories_IsActive DEFAULT (1),
        SortOrder INT NOT NULL CONSTRAINT DF_ComplaintCategories_SortOrder DEFAULT (0),
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ComplaintCategories_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ComplaintCategories PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT UQ_ComplaintCategories_Name UNIQUE (Name),
        CONSTRAINT CK_ComplaintCategories_Name_NotBlank CHECK (LEN(LTRIM(RTRIM(Name))) > 0)
    );
END
GO

IF OBJECT_ID(N'dbo.Complaints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Complaints
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        ResidentId INT NOT NULL,
        CategoryId INT NOT NULL,
        Priority NVARCHAR(20) NOT NULL,
        Description NVARCHAR(2000) NOT NULL,
        PreferredVisitTime NVARCHAR(200) NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_Complaints_Status DEFAULT (N'Initiated'),
        DeclineReason NVARCHAR(500) NULL,
        WorkerName NVARCHAR(150) NULL,
        WorkerPhone NVARCHAR(30) NULL,
        AssignedAtUtc DATETIME2(0) NULL,
        ResolvedAtUtc DATETIME2(0) NULL,
        ClosedAtUtc DATETIME2(0) NULL,
        SlaDueAtUtc DATETIME2(0) NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Complaints_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_Complaints_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Complaints PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT FK_Complaints_Residents FOREIGN KEY (ResidentId) REFERENCES dbo.Residents (uid),
        CONSTRAINT FK_Complaints_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.ComplaintCategories (uid),
        CONSTRAINT CK_Complaints_Priority_Valid CHECK (Priority IN (N'Urgent', N'Normal', N'Low')),
        CONSTRAINT CK_Complaints_Status_Valid CHECK ([Status] IN (
            N'Initiated', N'Declined', N'Approved', N'Assigned', N'WorkInProgress',
            N'Resolved', N'Closed', N'Cancelled')),
        CONSTRAINT CK_Complaints_Description_NotBlank CHECK (LEN(LTRIM(RTRIM(Description))) > 0)
    );

    CREATE INDEX IX_Complaints_ResidentId ON dbo.Complaints (ResidentId);
    CREATE INDEX IX_Complaints_Status ON dbo.Complaints ([Status]) INCLUDE (Priority, CategoryId, CreatedAtUtc);
    CREATE INDEX IX_Complaints_Priority ON dbo.Complaints (Priority);
    CREATE INDEX IX_Complaints_CreatedAtUtc ON dbo.Complaints (CreatedAtUtc DESC);
    CREATE INDEX IX_Complaints_SlaDueAtUtc ON dbo.Complaints (SlaDueAtUtc);
    CREATE INDEX IX_Complaints_WorkerName ON dbo.Complaints (WorkerName);
END
GO

IF OBJECT_ID(N'dbo.ComplaintHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplaintHistory
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        ComplaintId INT NOT NULL,
        ActorUserType NVARCHAR(20) NOT NULL,
        ActorUserId INT NULL,
        ActorDisplayName NVARCHAR(150) NULL,
        FromStatus NVARCHAR(30) NULL,
        ToStatus NVARCHAR(30) NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ComplaintHistory_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ComplaintHistory PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT FK_ComplaintHistory_Complaints FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints (uid),
        CONSTRAINT CK_ComplaintHistory_ActorUserType_NotBlank CHECK (LEN(LTRIM(RTRIM(ActorUserType))) > 0)
    );

    CREATE INDEX IX_ComplaintHistory_ComplaintId ON dbo.ComplaintHistory (ComplaintId, CreatedAtUtc);
END
GO

IF OBJECT_ID(N'dbo.ComplaintPhotos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplaintPhotos
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        ComplaintId INT NOT NULL,
        FilePath NVARCHAR(80) NOT NULL,
        ContentType NVARCHAR(50) NOT NULL,
        SizeBytes INT NOT NULL,
        UploadedByUserType NVARCHAR(20) NOT NULL,
        UploadedByUserId INT NULL,
        CapturedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_ComplaintPhotos_CapturedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ComplaintPhotos PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT FK_ComplaintPhotos_Complaints FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints (uid),
        CONSTRAINT CK_ComplaintPhotos_FilePath_NotBlank CHECK (LEN(LTRIM(RTRIM(FilePath))) > 0),
        CONSTRAINT CK_ComplaintPhotos_SizeBytes_Positive CHECK (SizeBytes > 0)
    );

    CREATE INDEX IX_ComplaintPhotos_ComplaintId ON dbo.ComplaintPhotos (ComplaintId);
END
GO

IF OBJECT_ID(N'dbo.SmtpSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmtpSettings
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        Host NVARCHAR(256) NULL,
        Port INT NOT NULL CONSTRAINT DF_SmtpSettings_Port DEFAULT (587),
        UseSsl BIT NOT NULL CONSTRAINT DF_SmtpSettings_UseSsl DEFAULT (1),
        Username NVARCHAR(256) NULL,
        PasswordProtected NVARCHAR(2000) NULL,
        FromEmail NVARCHAR(256) NULL,
        FromName NVARCHAR(150) NULL,
        EmailNotificationsEnabled BIT NOT NULL CONSTRAINT DF_SmtpSettings_EmailNotificationsEnabled DEFAULT (0),
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_SmtpSettings_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SmtpSettings PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT CK_SmtpSettings_Port_Valid CHECK (Port > 0 AND Port <= 65535)
    );
END
GO

-- Seed a single SMTP settings row when empty.
IF NOT EXISTS (SELECT 1 FROM dbo.SmtpSettings)
BEGIN
    INSERT INTO dbo.SmtpSettings (Host, Port, UseSsl, EmailNotificationsEnabled, UpdatedAtUtc)
    VALUES (NULL, 587, 1, 0, SYSUTCDATETIME());
END
GO

-- Seed complaint categories
INSERT INTO dbo.ComplaintCategories (Name, IsActive, SortOrder, CreatedAtUtc)
SELECT v.Name, 1, v.SortOrder, SYSUTCDATETIME()
FROM (VALUES
    (N'HVAC', 1),
    (N'Electric', 2),
    (N'Water', 3),
    (N'Plumbing', 4),
    (N'Civil', 5),
    (N'Cleaning', 6),
    (N'Other', 7)
) AS v (Name, SortOrder)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.ComplaintCategories AS c WHERE c.Name = v.Name
);
GO

-- Seed SLA hours into AppSettings (short values; no column changes).
INSERT INTO dbo.AppSettings (SettingKey, SettingValue, Description)
SELECT v.SettingKey, v.SettingValue, v.Description
FROM (VALUES
    (N'ComplaintSlaUrgentHours', N'24', N'Target resolution hours for Urgent complaints.'),
    (N'ComplaintSlaNormalHours', N'72', N'Target resolution hours for Normal complaints.'),
    (N'ComplaintSlaLowHours', N'168', N'Target resolution hours for Low complaints.')
) AS v (SettingKey, SettingValue, Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.AppSettings AS s WHERE s.SettingKey = v.SettingKey
);
GO

PRINT 'Complaints, history, photos, SMTP settings, and SLA seeds checked.';
GO
