-- SQLCMD, SQL Server 2022, same instance. Stop Business and FileStorage first.
-- Back up both databases; create FileStorage's schema with its migrations WITHOUT starting its worker.
-- Example: sqlcmd -S localhost -E -b -i server/scripts/Split-FileStorageDatabase.sql
--   -v BusinessDatabase="DevkitBusiness" FileStorageDatabase="DevkitFileStorage"
-- Copies metadata only. Keep object keys and the actual files/mounts separately.
-- Source tables and both migration histories remain unchanged; a nonempty target is rejected.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
DECLARE @Source sysname=N'$(BusinessDatabase)', @Target sysname=N'$(FileStorageDatabase)';
IF @Source=@Target OR DB_ID(@Source) IS NULL OR DB_ID(@Target) IS NULL
    THROW 50000, 'Two distinct existing databases are required.', 1;
CREATE TABLE #Transfer (Ordinal int PRIMARY KEY, SourceTable sysname, TargetTable sysname, Predicate nvarchar(200), Copied bigint NULL);
INSERT #Transfer VALUES
    (1,N'StorageLocations',N'StorageLocations',N'1=1',NULL),
    (2,N'StoredFiles',N'StoredFiles',N'1=1',NULL),
    (3,N'FileReferences',N'FileReferences',N'1=1',NULL),
    (4,N'FileMigrationItems',N'FileMigrationItems',N'1=1',NULL),
    (5,N'WorkspaceJobs',N'StorageJobs',N'[Kind] IN (''migrate'',''cleanup'')',NULL);
DECLARE @Ordinal int, @SourceTable sysname, @TargetTable sysname, @Predicate nvarchar(200), @Sql nvarchar(max), @Columns nvarchar(max), @Count bigint;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE preflight CURSOR LOCAL FAST_FORWARD FOR SELECT Ordinal,SourceTable,TargetTable,Predicate FROM #Transfer ORDER BY Ordinal;
    OPEN preflight; FETCH NEXT FROM preflight INTO @Ordinal,@SourceTable,@TargetTable,@Predicate;
    WHILE @@FETCH_STATUS=0
    BEGIN
        IF OBJECT_ID(QUOTENAME(@Source)+N'.dbo.'+QUOTENAME(@SourceTable),N'U') IS NULL
            OR OBJECT_ID(QUOTENAME(@Target)+N'.dbo.'+QUOTENAME(@TargetTable),N'U') IS NULL
            THROW 50000, 'Source or target schema is incomplete. Run the documented migrations first.', 1;
        SET @Sql=N'SELECT @n=COUNT_BIG(*) FROM '+QUOTENAME(@Target)+N'.dbo.'+QUOTENAME(@TargetTable)+N' WITH (TABLOCKX,HOLDLOCK);';
        EXEC sys.sp_executesql @Sql,N'@n bigint OUTPUT',@Count OUTPUT;
        IF @Count<>0 THROW 50000, 'Target file tables must be empty; stop FileStorage before copying.', 1;
        FETCH NEXT FROM preflight INTO @Ordinal,@SourceTable,@TargetTable,@Predicate;
    END;
    CLOSE preflight; DEALLOCATE preflight;
    DECLARE copy_rows CURSOR LOCAL FAST_FORWARD FOR SELECT Ordinal,SourceTable,TargetTable,Predicate FROM #Transfer ORDER BY Ordinal;
    OPEN copy_rows; FETCH NEXT FROM copy_rows INTO @Ordinal,@SourceTable,@TargetTable,@Predicate;
    WHILE @@FETCH_STATUS=0
    BEGIN
        SET @Sql=N'SELECT @columns=STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),N'','') WITHIN GROUP(ORDER BY c.column_id) '
            +N'FROM '+QUOTENAME(@Target)+N'.sys.columns c JOIN '+QUOTENAME(@Target)+N'.sys.tables t ON t.object_id=c.object_id '
            +N'JOIN '+QUOTENAME(@Target)+N'.sys.schemas s ON s.schema_id=t.schema_id '
            +N'WHERE s.name=N''dbo'' AND t.name=@table AND c.is_computed=0 AND c.is_identity=0;';
        EXEC sys.sp_executesql @Sql,N'@table sysname,@columns nvarchar(max) OUTPUT',@TargetTable,@Columns OUTPUT;
        SET @Sql=N'INSERT '+QUOTENAME(@Target)+N'.dbo.'+QUOTENAME(@TargetTable)+N' ('+@Columns+N') SELECT '+@Columns
            +N' FROM '+QUOTENAME(@Source)+N'.dbo.'+QUOTENAME(@SourceTable)+N' WITH (TABLOCK,HOLDLOCK) WHERE '+@Predicate+N'; SET @n=@@ROWCOUNT;';
        EXEC sys.sp_executesql @Sql,N'@n bigint OUTPUT',@Count OUTPUT;
        UPDATE #Transfer SET Copied=@Count WHERE Ordinal=@Ordinal;
        FETCH NEXT FROM copy_rows INTO @Ordinal,@SourceTable,@TargetTable,@Predicate;
    END;
    CLOSE copy_rows; DEALLOCATE copy_rows;
    COMMIT;
    SELECT SourceTable,TargetTable,Copied FROM #Transfer ORDER BY Ordinal;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
