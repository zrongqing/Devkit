using Devkit.Business.Domain.Modules;
using Microsoft.EntityFrameworkCore;

namespace Devkit.Business.Infrastructure.Persistence;

public sealed class BusinessDbContext(DbContextOptions<BusinessDbContext> options) : DbContext(options)
{
    public DbSet<ModuleInstance> ModuleInstances => Set<ModuleInstance>();
    public DbSet<BufferedModuleCommand> BufferedModuleCommands => Set<BufferedModuleCommand>();
    public DbSet<ModuleCommandSequence> ModuleCommandSequences => Set<ModuleCommandSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BusinessDbContext).Assembly);
    }
}
