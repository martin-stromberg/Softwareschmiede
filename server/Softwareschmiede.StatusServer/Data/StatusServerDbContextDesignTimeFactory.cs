using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Softwareschmiede.StatusServer.Data;

/// <summary>Design-Time-Factory für <c>dotnet ef migrations</c>-Befehle.</summary>
internal sealed class StatusServerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<StatusServerDbContext>
{
    /// <inheritdoc/>
    public StatusServerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StatusServerDbContext>()
            .UseSqlite("Data Source=statusserver-design.db")
            .Options;
        return new StatusServerDbContext(options);
    }
}
