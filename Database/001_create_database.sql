-- Creates ResidentPortal when it is missing. Safe to re-run.
-- This script does not create logins and does not store passwords.

IF DB_ID(N'ResidentPortal') IS NULL
BEGIN
    CREATE DATABASE ResidentPortal;
END
GO
