using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Barcode2.DB.Entities;

namespace Barcode2.DB.Context;

public partial class MyDbContext : DbContext
{
    private readonly string? _connectionString;

    public MyDbContext()
    {
    }
    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public MyDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }
    public DbSet<SYS_PAGE> SYS_PAGE { get; set; }
    public DbSet<SYS_PAGE_EVENT> SYS_PAGE_EVENT { get; set; }
    public DbSet<SYS_PAGE_EVENT_CODE> SYS_PAGE_EVENT_CODE { get; set; }
    public DbSet<SYS_PAGE_EVENT_CODE_BACK> SYS_PAGE_EVENT_CODE_BACK { get; set; }
    public DbSet<SYS_MENU> SYS_MENU { get; set; }
    public DbSet<SYS_MODULE> SYS_MODULE { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        OnModelCreatingPartial(modelBuilder);
        modelBuilder.Entity<SYS_PAGE_EVENT_CODE>(entity =>
        {
            entity.HasKey(e => e.ID);

            // 将可能存储长文本的字段映射为 CLOB
            entity.Property(e => e.STR_EXTEND)
                .HasColumnType("CLOB"); // 替代默认的 NVARCHAR(max)
        });

        // The legacy Oracle schema stores the event primary key as VARCHAR2(32),
        // while related tables expose the same numeric identifier as NUMBER(19).
        // Keep a numeric CLR key so existing relationships and application code
        // remain strongly typed, but read and write the principal key as text.
        modelBuilder.Entity<SYS_PAGE_EVENT>()
            .Property(entity => entity.ID)
            .HasConversion(new ValueConverter<long, string>(
                value => value.ToString(CultureInfo.InvariantCulture),
                value => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
            .HasColumnType("VARCHAR2(32)")
            .IsUnicode(false)
            .HasMaxLength(32);

        // Oracle EF Core maps NUMBER(1) to bool by convention. Keep the CLR
        // value numeric so menu filtering remains an explicit IS_DELETE == 0,
        // while adapting it to the provider's NUMBER(1) mapping.
        modelBuilder.Entity<SYS_MENU>()
            .Property(e => e.IS_DELETE)
            .HasConversion(new ValueConverter<short?, bool?>(
                value => value.HasValue ? value.Value != 0 : null,
                value => value.HasValue ? (short)(value.Value ? 1 : 0) : null))
            .HasColumnType("NUMBER(1)");
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("Barcode2 数据库连接未配置。");

            optionsBuilder.UseOracle(_connectionString);
        }
    }

    partial void OnModelCreatingPartial(ModelBuilder builder);
}
