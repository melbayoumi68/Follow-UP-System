-- Add the standalone supply catalog and its project assignments.
-- Prerequisites: dbo.Projects and dbo.ProjectSupplyItems already exist.

IF OBJECT_ID(N'dbo.SupplyCatalogItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupplyCatalogItems (
        Id NVARCHAR(64) NOT NULL CONSTRAINT PK_SupplyCatalogItems PRIMARY KEY,
        Type NVARCHAR(120) NOT NULL,
        Material NVARCHAR(350) NOT NULL,
        Unit NVARCHAR(50) NOT NULL,
        Qty DECIMAL(18,2) NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        Total DECIMAL(18,2) NOT NULL,
        SupplyDate DATE NOT NULL,
        Note NVARCHAR(1000) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_SupplyCatalogItems_Status DEFAULT 'Active',
        CreatedAt DATETIME2 NOT NULL
    );
END;
GO

IF COL_LENGTH(N'dbo.SupplyCatalogItems', N'Status') IS NULL
BEGIN
    ALTER TABLE dbo.SupplyCatalogItems
        ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_SupplyCatalogItems_Status DEFAULT 'Active';
END;
GO

IF OBJECT_ID(N'dbo.SupplyCatalogAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupplyCatalogAssignments (
        Id NVARCHAR(64) NOT NULL CONSTRAINT PK_SupplyCatalogAssignments PRIMARY KEY,
        SupplyCatalogItemId NVARCHAR(64) NOT NULL,
        ProjectId NVARCHAR(64) NOT NULL,
        ProjectSupplyItemId NVARCHAR(64) NOT NULL,
        Qty DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIME2 NOT NULL,
        CONSTRAINT FK_SupplyCatalogAssignments_SupplyCatalogItems FOREIGN KEY (SupplyCatalogItemId) REFERENCES dbo.SupplyCatalogItems(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_SupplyCatalogAssignments_Projects FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_SupplyCatalogAssignments_ProjectSupplyItems FOREIGN KEY (ProjectSupplyItemId) REFERENCES dbo.ProjectSupplyItems(Id) ON DELETE NO ACTION
    );
    CREATE UNIQUE INDEX IX_SupplyCatalogAssignments_ProjectSupplyItemId ON dbo.SupplyCatalogAssignments(ProjectSupplyItemId);
    CREATE INDEX IX_SupplyCatalogAssignments_SupplyCatalogItemId ON dbo.SupplyCatalogAssignments(SupplyCatalogItemId);
    CREATE INDEX IX_SupplyCatalogAssignments_ProjectId ON dbo.SupplyCatalogAssignments(ProjectId);
END;
GO
