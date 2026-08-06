using Devkit.Modules.Ssamc.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SSAMC.DB;

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
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        OnModelCreatingPartial(modelBuilder);
        modelBuilder.Entity<SYS_PAGE_EVENT_CODE>(entity =>
        {
            entity.HasKey(e => e.ID);

            // 将可能存储长文本的字段映射为 CLOB
            entity.Property(e => e.STR_EXTEND)
                  .HasColumnType("CLOB");  // 替代默认的 NVARCHAR(max)
        });
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
                throw new InvalidOperationException("SSAMC 数据库连接未配置。");

            optionsBuilder.UseOracle(_connectionString);
        }
    }

    public MyDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }
    public DbSet<SYS_PAGE> SYS_PAGE { get; set; }
    public DbSet<SYS_PAGE_EVENT> SYS_PAGE_EVENT { get; set; }
    public DbSet<SYS_PAGE_EVENT_CODE> SYS_PAGE_EVENT_CODE { get; set; }
    public DbSet<SYS_PAGE_EVENT_CODE_BACK> SYS_PAGE_EVENT_CODE_BACK { get; set; }
    partial void OnModelCreatingPartial(ModelBuilder builder);
}
