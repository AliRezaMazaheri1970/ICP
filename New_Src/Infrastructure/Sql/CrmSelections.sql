IF OBJECT_ID(N'dbo.CrmSelections', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CrmSelections](
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ProjectId] UNIQUEIDENTIFIER NOT NULL,
        [SolutionLabel] NVARCHAR(200) NOT NULL,
        [RowIndex] INT NOT NULL,
        [SelectedCrmKey] NVARCHAR(200) NOT NULL,
        [SelectedBy] NVARCHAR(200) NULL,
        [SelectedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX [IX_CrmSelections_Project_Label_Row]
    ON [dbo].[CrmSelections]([ProjectId], [SolutionLabel], [RowIndex]);
END
