using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Barcode2.Core.ApiCodeCollector;
using Barcode2.DB.Context;
using Barcode2.DB.Entities;
using Barcode2.Servers;
using Xunit;

namespace Devkit.Loading.Tests;

public sealed class ApiUpdateServerDatabaseTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<MyDbContext> _options;
    private readonly ApiUpdateServer _server;

    public ApiUpdateServerDatabaseTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<MyDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var db = new MyDbContext(_options);
        db.Database.EnsureCreated();
        _server = new SqliteApiUpdateServer(_options);
    }

    [Fact]
    public void Batch_updates_details_through_code_and_name_matched_events()
    {
        SeedEvent(1, "CODE001", "First Name", 101, "old-code");
        SeedEvent(2, "CODE002", "Second Name", 102, "old-name");

        var result = _server.UpdateExtendBatch(
            [
                new ApiExtendUpdateRequest(ApiLookupKind.Code, "CODE001", "code-extension"),
                new ApiExtendUpdateRequest(ApiLookupKind.Name, "Second Name", "name-extension")
            ],
            "unused");

        Assert.True(result);
        using var db = new MyDbContext(_options);
        Assert.Equal("code-extension", db.SYS_PAGE_EVENT_CODE.Single(entity => entity.ID == 101).STR_EXTEND);
        Assert.Equal("name-extension", db.SYS_PAGE_EVENT_CODE.Single(entity => entity.ID == 102).STR_EXTEND);
        Assert.False(string.IsNullOrWhiteSpace(db.SYS_PAGE_EVENT.Single(entity => entity.ID == 1).DT_UP));
        Assert.False(string.IsNullOrWhiteSpace(db.SYS_PAGE_EVENT_CODE.Single(entity => entity.ID == 102).DT_UP));
    }

    [Fact]
    public void Event_id_uses_the_legacy_oracle_string_conversion()
    {
        using var db = new MyDbContext(_options);
        var property = db.Model.FindEntityType(typeof(SYS_PAGE_EVENT))!
            .FindProperty(nameof(SYS_PAGE_EVENT.ID))!;
        var converter = property.GetValueConverter()!;

        Assert.Equal(typeof(string), converter.ProviderClrType);
        Assert.Equal("1987760499496050688", converter.ConvertToProvider(1987760499496050688L));
        Assert.Equal(1987760499496050688L, converter.ConvertFromProvider("1987760499496050688"));
    }

    [Fact]
    public void Duplicate_main_event_rolls_back_the_entire_batch()
    {
        SeedEvent(1, "GOOD", "Good", 101, "old-good");
        SeedEvent(2, "DUPLICATE", "Duplicate One", 102, "old-one");
        SeedEvent(3, "DUPLICATE", "Duplicate Two", 103, "old-two");

        var exception = Assert.Throws<InvalidOperationException>(() => _server.UpdateExtendBatch(
            [
                new ApiExtendUpdateRequest(ApiLookupKind.Code, "GOOD", "new-good"),
                new ApiExtendUpdateRequest(ApiLookupKind.Code, "DUPLICATE", "new-duplicate")
            ],
            "unused"));

        Assert.Contains("ApiCode", exception.Message);
        Assert.Contains("DUPLICATE", exception.Message);
        Assert.Contains("主表命中 2 条", exception.Message);
        using var db = new MyDbContext(_options);
        Assert.Equal("old-good", db.SYS_PAGE_EVENT_CODE.Single(entity => entity.ID == 101).STR_EXTEND);
        Assert.All(db.SYS_PAGE_EVENT, entity => Assert.Null(entity.DT_UP));
    }

    [Fact]
    public void Missing_main_event_is_a_successful_no_op()
    {
        var result = _server.UpdateExtendName("Missing", "extension", "unused");

        Assert.True(result);
        using var db = new MyDbContext(_options);
        Assert.Empty(db.SYS_PAGE_EVENT);
        Assert.Empty(db.SYS_PAGE_EVENT_CODE);
    }

    [Fact]
    public void Duplicate_name_reports_name_and_rolls_back()
    {
        SeedEvent(1, "CODE001", "Duplicate Name", 101, "old-one");
        SeedEvent(2, "CODE002", "Duplicate Name", 102, "old-two");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _server.UpdateExtendName("Duplicate Name", "new-value", "unused"));

        Assert.Contains("ApiName", exception.Message);
        Assert.Contains("Duplicate Name", exception.Message);
        using var db = new MyDbContext(_options);
        Assert.All(db.SYS_PAGE_EVENT_CODE, entity => Assert.StartsWith("old-", entity.STR_EXTEND));
    }

    [Fact]
    public void Invalid_detail_cardinality_rolls_back_the_batch()
    {
        SeedEvent(1, "CODE001", "Name", 101, "old-one");
        using (var db = new MyDbContext(_options))
        {
            db.SYS_PAGE_EVENT_CODE.Add(new SYS_PAGE_EVENT_CODE
            {
                ID = 102,
                ID_EVENT = 1,
                STR_EXTEND = "old-two"
            });
            db.SaveChanges();
        }

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _server.UpdateExtendCode("CODE001", "new-value", "unused"));

        Assert.Contains("关联副表命中 2 条", exception.Message);
        using var verification = new MyDbContext(_options);
        Assert.All(verification.SYS_PAGE_EVENT_CODE, entity => Assert.StartsWith("old-", entity.STR_EXTEND));
    }

    public void Dispose() => _connection.Dispose();

    private void SeedEvent(long eventId, string code, string name, long detailId, string extendCode)
    {
        using var db = new MyDbContext(_options);
        db.SYS_PAGE_EVENT.Add(new SYS_PAGE_EVENT
        {
            ID = eventId,
            STR_CODE = code,
            STR_NAME = name
        });
        db.SYS_PAGE_EVENT_CODE.Add(new SYS_PAGE_EVENT_CODE
        {
            ID = detailId,
            ID_EVENT = eventId,
            STR_EXTEND = extendCode
        });
        db.SaveChanges();
    }

    private sealed class SqliteApiUpdateServer(DbContextOptions<MyDbContext> options)
        : ApiUpdateServer(Mock.Of<IApiScanner>())
    {
        protected override MyDbContext CreateDbContext(string connectionString) => new(options);
    }
}
