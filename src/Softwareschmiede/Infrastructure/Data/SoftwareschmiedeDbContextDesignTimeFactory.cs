using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Softwareschmiede.Infrastructure.Data;

/// <summary>Design-Time-Factory für EF Core Tools (Migrations).</summary>
internal sealed class SoftwareschmiedeDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SoftwareschmiedeDbContext>
{
    public SoftwareschmiedeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiedeDbContext>()
            .UseSqlite("Data Source=softwareschmiede-design.db")
            .Options;
        return new SoftwareschmiedeDbContext(options);
    }
}
