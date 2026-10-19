-- =============================================================================
-- InvoicesERP - Manual schema script (idempotent)
-- Run manually in SQL Server Management Studio (SSMS).
-- The application no longer executes any DDL at startup.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1) AppUsers
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.AppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUsers (
        Id              NVARCHAR(64)  NOT NULL CONSTRAINT PK_AppUsers PRIMARY KEY,
        Username        NVARCHAR(100) NOT NULL,
        PasswordHash    NVARCHAR(500) NOT NULL,
        DisplayName     NVARCHAR(200) NOT NULL,
        Email           NVARCHAR(250) NULL,
        Role            NVARCHAR(50)  NOT NULL,
        Status          NVARCHAR(20)  NOT NULL,
        PermissionsJson NVARCHAR(MAX) NOT NULL,
        CreatedAt       DATETIME2     NOT NULL,
        UpdatedAt       DATETIME2     NOT NULL
    );
    CREATE UNIQUE INDEX IX_AppUsers_Username ON dbo.AppUsers (Username);
    CREATE UNIQUE INDEX IX_AppUsers_Email ON dbo.AppUsers (Email)
        WHERE Email IS NOT NULL AND Email <> N'';
END;
GO

-- -----------------------------------------------------------------------------
-- 2) ProjectSupplyItems
-- -----------------------------------------------------------------------------
IF OBJECT_ID('dbo.ProjectSupplyItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProjectSupplyItems (
        Id NVARCHAR(64) NOT NULL PRIMARY KEY,
        ProjectId NVARCHAR(64) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        Unit NVARCHAR(50) NOT NULL,
        Qty DECIMAL(18,2) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Total DECIMAL(18,2) NOT NULL,
        SupplyDate DATE NOT NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_ProjectSupplyItems_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id) ON DELETE NO ACTION
    );
    CREATE INDEX IX_ProjectSupplyItems_ProjectId ON dbo.ProjectSupplyItems(ProjectId);
END;
GO

-- -----------------------------------------------------------------------------
-- 3) ContractSupplyAllocations
-- -----------------------------------------------------------------------------
IF OBJECT_ID('dbo.ContractSupplyAllocations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContractSupplyAllocations (
        Id NVARCHAR(64) NOT NULL PRIMARY KEY,
        ProjectSupplyItemId NVARCHAR(64) NOT NULL,
        ContractId NVARCHAR(64) NOT NULL,
        Qty DECIMAL(18,2) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Total DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_ContractSupplyAllocations_ProjectSupplyItems FOREIGN KEY (ProjectSupplyItemId) REFERENCES dbo.ProjectSupplyItems(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_ContractSupplyAllocations_Contracts FOREIGN KEY (ContractId) REFERENCES dbo.Contracts(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_ContractSupplyAllocations_ProjectSupplyItemId ON dbo.ContractSupplyAllocations(ProjectSupplyItemId);
    CREATE INDEX IX_ContractSupplyAllocations_ContractId ON dbo.ContractSupplyAllocations(ContractId);
END;
GO

-- -----------------------------------------------------------------------------
-- 4) InvoiceSupplyAllocations (invoices -> contract supply allocations)
-- -----------------------------------------------------------------------------
IF OBJECT_ID('dbo.InvoiceSupplyAllocations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceSupplyAllocations (
        Id NVARCHAR(64) NOT NULL PRIMARY KEY,
        InvoiceId NVARCHAR(64) NOT NULL,
        ContractSupplyAllocationId NVARCHAR(64) NOT NULL,
        Qty DECIMAL(18,2) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Total DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_InvoiceSupplyAllocations_Invoices
            FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices(Id) ON DELETE CASCADE,
        CONSTRAINT FK_InvoiceSupplyAllocations_ContractSupplyAllocations
            FOREIGN KEY (ContractSupplyAllocationId) REFERENCES dbo.ContractSupplyAllocations(Id) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceSupplyAllocations_InvoiceId' AND object_id = OBJECT_ID(N'dbo.InvoiceSupplyAllocations'))
    CREATE INDEX IX_InvoiceSupplyAllocations_InvoiceId ON dbo.InvoiceSupplyAllocations(InvoiceId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_InvoiceSupplyAllocations_ContractSupplyAllocationId' AND object_id = OBJECT_ID(N'dbo.InvoiceSupplyAllocations'))
    CREATE INDEX IX_InvoiceSupplyAllocations_ContractSupplyAllocationId ON dbo.InvoiceSupplyAllocations(ContractSupplyAllocationId);
GO

-- -----------------------------------------------------------------------------
-- 5) Contracts extra columns
-- -----------------------------------------------------------------------------
IF COL_LENGTH('dbo.Contracts', 'ModifiedAmount') IS NULL
    ALTER TABLE dbo.Contracts ADD ModifiedAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Contracts_ModifiedAmount DEFAULT(0);
IF COL_LENGTH('dbo.Contracts', 'VoAmount') IS NULL
    ALTER TABLE dbo.Contracts ADD VoAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Contracts_VoAmount DEFAULT(0);
IF COL_LENGTH('dbo.Contracts', 'ClaimsAmount') IS NULL
    ALTER TABLE dbo.Contracts ADD ClaimsAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Contracts_ClaimsAmount DEFAULT(0);
IF COL_LENGTH('dbo.Contracts', 'VatAmount') IS NULL
    ALTER TABLE dbo.Contracts ADD VatAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Contracts_VatAmount DEFAULT(0);
IF COL_LENGTH('dbo.Contracts', 'PaymentValue') IS NULL
    ALTER TABLE dbo.Contracts ADD PaymentValue DECIMAL(18,2) NULL;
GO

-- -----------------------------------------------------------------------------
-- 6) ContractPaymentValueLogs
-- -----------------------------------------------------------------------------
IF OBJECT_ID('dbo.ContractPaymentValueLogs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ContractPaymentValueLogs (
        Id NVARCHAR(64) NOT NULL PRIMARY KEY,
        ContractId NVARCHAR(64) NOT NULL,
        ProjectId NVARCHAR(64) NOT NULL,
        OldValue DECIMAL(18,2) NULL,
        NewValue DECIMAL(18,2) NULL,
        ChangedByUserId NVARCHAR(64) NULL,
        ChangedByUsername NVARCHAR(200) NULL,
        ChangedAt DATETIME2 NOT NULL,
        Note NVARCHAR(500) NULL,
        CONSTRAINT FK_ContractPaymentValueLogs_Contracts FOREIGN KEY (ContractId) REFERENCES dbo.Contracts(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_ContractPaymentValueLogs_ContractId ON dbo.ContractPaymentValueLogs(ContractId);
    CREATE INDEX IX_ContractPaymentValueLogs_ProjectId ON dbo.ContractPaymentValueLogs(ProjectId);
    CREATE INDEX IX_ContractPaymentValueLogs_ChangedAt ON dbo.ContractPaymentValueLogs(ChangedAt);
END;
GO

-- Baseline audit row for contracts that already had a PaymentValue.
INSERT INTO dbo.ContractPaymentValueLogs
    (Id, ContractId, ProjectId, OldValue, NewValue, ChangedByUserId, ChangedByUsername, ChangedAt, Note)
SELECT
    CONVERT(NVARCHAR(36), NEWID()), c.Id, c.ProjectId, NULL, c.PaymentValue,
    NULL, NULL, SYSUTCDATETIME(), N'تسجيل تأسيسي للقيمة الحالية'
FROM dbo.Contracts c
WHERE c.PaymentValue IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.ContractPaymentValueLogs l
      WHERE l.ContractId = c.Id
  );
GO

-- -----------------------------------------------------------------------------
-- 7) Cost Control tables
-- -----------------------------------------------------------------------------
IF OBJECT_ID('dbo.CostControls', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CostControls (
        Id NVARCHAR(64) NOT NULL PRIMARY KEY,
        ContractId NVARCHAR(64) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        CONSTRAINT UQ_CostControls_ContractId UNIQUE (ContractId),
        CONSTRAINT FK_CostControls_Contracts FOREIGN KEY (ContractId) REFERENCES dbo.Contracts(Id) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID('dbo.CostControlItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CostControlItems (
        Id NVARCHAR(64) NOT NULL PRIMARY KEY,
        CostControlId NVARCHAR(64) NOT NULL,
        Name NVARCHAR(300) NOT NULL,
        Code NVARCHAR(50) NULL,
        CostType NVARCHAR(20) NOT NULL,
        DisplayOrder INT NOT NULL,
        CONSTRAINT FK_CostControlItems_CostControls FOREIGN KEY (CostControlId) REFERENCES dbo.CostControls(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_CostControlItems_CostControlId ON dbo.CostControlItems(CostControlId);
END;
GO

IF OBJECT_ID('dbo.CostControlSubItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CostControlSubItems (
        Id NVARCHAR(64) NOT NULL PRIMARY KEY,
        CostControlItemId NVARCHAR(64) NOT NULL,
        Description NVARCHAR(500) NOT NULL,
        StudyValue DECIMAL(18,2) NOT NULL,
        ActualCost DECIMAL(18,2) NOT NULL,
        DisplayOrder INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        UpdatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_CostControlSubItems_Items FOREIGN KEY (CostControlItemId) REFERENCES dbo.CostControlItems(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_CostControlSubItems_ItemId ON dbo.CostControlSubItems(CostControlItemId);
END;
GO

-- -----------------------------------------------------------------------------
-- 8) Performance indexes
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_Date' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX IX_Invoices_Date ON dbo.Invoices(Date);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_Status' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX IX_Invoices_Status ON dbo.Invoices(Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_PaymentStatus' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX IX_Invoices_PaymentStatus ON dbo.Invoices(PaymentStatus);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_ContractId_Date' AND object_id = OBJECT_ID(N'dbo.Invoices'))
    CREATE INDEX IX_Invoices_ContractId_Date ON dbo.Invoices(ContractId, Date);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Projects_Name' AND object_id = OBJECT_ID(N'dbo.Projects'))
    CREATE INDEX IX_Projects_Name ON dbo.Projects(Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Contracts_Name' AND object_id = OBJECT_ID(N'dbo.Contracts'))
    CREATE INDEX IX_Contracts_Name ON dbo.Contracts(Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ExecPositions_ContractId_Version' AND object_id = OBJECT_ID(N'dbo.ExecPositions'))
    CREATE INDEX IX_ExecPositions_ContractId_Version ON dbo.ExecPositions(ContractId, VersionNumber);
GO
