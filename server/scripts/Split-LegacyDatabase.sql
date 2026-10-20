-- SQLCMD mode, SQL Server 2022. Run only against backed-up staging databases first.
-- Supply -v LegacyDatabase="DevkitLegacy" IdentityDatabase="DevkitIdentity" BusinessDatabase="DevkitBusiness".
-- Stop the old application and both new applications before copying.
-- Create both target schemas using their NEW migrations, without bootstrap seeding.
-- Source data and all migration-history tables are left unchanged.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
DECLARE @Legacy sysname = N'$(LegacyDatabase)', @Identity sysname = N'$(IdentityDatabase)', @Business sysname = N'$(BusinessDatabase)';
IF @Legacy = @Identity OR @Legacy = @Business OR @Identity = @Business
    THROW 50000, 'Source and target database names must all be different.', 1;
IF DB_ID(@Legacy) IS NULL OR DB_ID(@Identity) IS NULL OR DB_ID(@Business) IS NULL
    THROW 50000, 'All three databases must already exist on this SQL Server instance.', 1;
CREATE TABLE #Transfer (TargetDatabase sysname NOT NULL, TableName sysname NOT NULL, Copied bit NOT NULL DEFAULT 0,
    RowCount bigint NULL, PRIMARY KEY (TargetDatabase, TableName));
INSERT #Transfer (TargetDatabase, TableName) VALUES
    (@Identity, N'Roles'),
    (@Identity, N'Users'),
    (@Identity, N'WebMenus'),
    (@Identity, N'WebMenuState'),
    (@Identity, N'RoleClaims'),
    (@Identity, N'RefreshTokens'),
    (@Identity, N'UserClaims'),
    (@Identity, N'UserLogins'),
    (@Identity, N'UserRoles'),
    (@Identity, N'UserTokens'),
    (@Business, N'BufferedModuleCommands'),
    (@Business, N'FileMigrationItems'),
    (@Business, N'ModuleCommandSequences'),
    (@Business, N'ModuleInstances'),
    (@Business, N'StorageLocations'),
    (@Business, N'StudyKnowledgeBases'),
    (@Business, N'StudyMistakes'),
    (@Business, N'StudyProjects'),
    (@Business, N'StudyQueryHistory'),
    (@Business, N'WorkspaceJobs'),
    (@Business, N'StoredFiles'),
    (@Business, N'StudyQuestions'),
    (@Business, N'StudySources'),
    (@Business, N'StudyAttempts'),
    (@Business, N'FileReferences'),
    (@Business, N'StudySourceVersions'),
    (@Business, N'StudyChunks');
CREATE TABLE #Dependencies (TargetDatabase sysname, Child sysname, Parent sysname);
DECLARE @Target sysname, @Table sysname, @Sql nvarchar(max), @Columns nvarchar(max), @Rows bigint;
DECLARE dependencies CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT TargetDatabase FROM #Transfer;
OPEN dependencies; FETCH NEXT FROM dependencies INTO @Target;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'INSERT #Dependencies SELECT @db, child.name, parent.name FROM ' + QUOTENAME(@Target) + N'.sys.foreign_keys fk '
        + N'JOIN ' + QUOTENAME(@Target) + N'.sys.tables child ON child.object_id = fk.parent_object_id '
        + N'JOIN ' + QUOTENAME(@Target) + N'.sys.tables parent ON parent.object_id = fk.referenced_object_id;';
    EXEC sys.sp_executesql @Sql, N'@db sysname', @Target;
    FETCH NEXT FROM dependencies INTO @Target;
END;
CLOSE dependencies; DEALLOCATE dependencies;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE preflight CURSOR LOCAL FAST_FORWARD FOR SELECT TargetDatabase, TableName FROM #Transfer;
    OPEN preflight; FETCH NEXT FROM preflight INTO @Target, @Table;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF OBJECT_ID(QUOTENAME(@Legacy) + N'.dbo.' + QUOTENAME(@Table), N'U') IS NULL
            THROW 50000, 'Legacy schema is incomplete. Upgrade the legacy schema before transferring data.', 1;
        IF OBJECT_ID(QUOTENAME(@Target) + N'.dbo.' + QUOTENAME(@Table), N'U') IS NULL
            THROW 50000, 'Target schema is incomplete. Run both new migrations first.', 1;
        SET @Sql = N'SELECT @n = COUNT_BIG(*) FROM ' + QUOTENAME(@Target) + N'.dbo.' + QUOTENAME(@Table) + N' WITH (TABLOCKX, HOLDLOCK)';
        IF @Table = N'WebMenuState' SET @Sql += N' WHERE Id <> 1 OR Version <> 0';
        EXEC sys.sp_executesql @Sql, N'@n bigint OUTPUT', @Rows OUTPUT;
        IF @Rows <> 0 THROW 50000, 'Target contains application data. Refusing to overwrite or merge it.', 1;
        FETCH NEXT FROM preflight INTO @Target, @Table;
    END;
    CLOSE preflight; DEALLOCATE preflight;
    WHILE EXISTS (SELECT 1 FROM #Transfer WHERE Copied = 0)
    BEGIN
        SET @Target = NULL;
        SELECT TOP (1) @Target = t.TargetDatabase, @Table = t.TableName FROM #Transfer t WHERE t.Copied = 0
        AND NOT EXISTS (SELECT 1 FROM #Dependencies d JOIN #Transfer parent
            ON parent.TargetDatabase = d.TargetDatabase AND parent.TableName = d.Parent
            WHERE d.TargetDatabase = t.TargetDatabase AND d.Child = t.TableName AND d.Parent <> d.Child AND parent.Copied = 0)
        ORDER BY t.TargetDatabase, t.TableName;
        IF @Target IS NULL THROW 50000, 'A cross-table foreign-key cycle prevents safe transfer.', 1;
        SET @Sql = N'SELECT @cols = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), N'','') WITHIN GROUP (ORDER BY c.column_id) FROM '
            + QUOTENAME(@Target) + N'.sys.columns c JOIN ' + QUOTENAME(@Target) + N'.sys.tables t ON t.object_id = c.object_id '
            + N'JOIN ' + QUOTENAME(@Target) + N'.sys.schemas s ON s.schema_id = t.schema_id WHERE t.name = @table AND s.name = N''dbo'' AND c.is_computed = 0;';
        EXEC sys.sp_executesql @Sql, N'@table sysname, @cols nvarchar(max) OUTPUT', @Table, @Columns OUTPUT;
        SET @Sql = CASE WHEN @Table = N'WebMenuState' THEN N'DELETE ' + QUOTENAME(@Target) + N'.dbo.[WebMenuState]; ' ELSE N'' END
            + N'INSERT ' + QUOTENAME(@Target) + N'.dbo.' + QUOTENAME(@Table) + N' (' + @Columns + N') SELECT ' + @Columns
            + N' FROM ' + QUOTENAME(@Legacy) + N'.dbo.' + QUOTENAME(@Table) + N' WITH (TABLOCK, HOLDLOCK); SET @n = ROWCOUNT_BIG();';
        EXEC sys.sp_executesql @Sql, N'@n bigint OUTPUT', @Rows OUTPUT;
        UPDATE #Transfer SET Copied = 1, RowCount = @Rows WHERE TargetDatabase = @Target AND TableName = @Table;
    END;
    COMMIT TRANSACTION;
    SELECT TargetDatabase, TableName, RowCount FROM #Transfer ORDER BY TargetDatabase, TableName;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
