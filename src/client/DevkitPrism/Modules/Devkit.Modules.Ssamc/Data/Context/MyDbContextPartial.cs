using Microsoft.EntityFrameworkCore;
using SSAMC.DB.Entities;

namespace SSAMC.DB;

public partial class MyDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder builder)
    {
        // 自动注册实体
        builder.ApplyConfigurationsFromAssembly(typeof(MyDbContext).Assembly);
    }
}
