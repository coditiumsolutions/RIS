-- Creates Receptionists table for receptionist user accounts.
-- Safe to re-run.

USE ResidentPortal;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.Receptionists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Receptionists
    (
        uid INT IDENTITY(1, 1) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        Username NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        Email NVARCHAR(256) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Receptionists_IsActive DEFAULT (1),
        MustChangePassword BIT NOT NULL CONSTRAINT DF_Receptionists_MustChangePassword DEFAULT (0),
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Receptionists_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Receptionists PRIMARY KEY CLUSTERED (uid),
        CONSTRAINT CK_Receptionists_FullName_NotBlank CHECK (LEN(LTRIM(RTRIM(FullName))) > 0),
        CONSTRAINT CK_Receptionists_Username_NotBlank CHECK (LEN(LTRIM(RTRIM(Username))) > 0),
        CONSTRAINT CK_Receptionists_PasswordHash_NotBlank CHECK (LEN(LTRIM(RTRIM(PasswordHash))) > 0)
    );

    CREATE UNIQUE INDEX UX_Receptionists_Username ON dbo.Receptionists (Username);
END
GO
