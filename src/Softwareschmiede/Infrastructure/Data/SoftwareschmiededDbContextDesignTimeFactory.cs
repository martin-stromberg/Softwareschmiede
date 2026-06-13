using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Softwareschmiede.Infrastructure.Data;

/// <summary>Design-Time-Factory für EF Core Tools (Migrations).</summary>
internal sealed class SoftwareschmiededDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SoftwareschmiededDbContext>
{
    public SoftwareschmiededDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite("Data Source=softwareschmiede-design.db")
            .Options;
        return new SoftwareschmiededDbContext(options);
    }
}
