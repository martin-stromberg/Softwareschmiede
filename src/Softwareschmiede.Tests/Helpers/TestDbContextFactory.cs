using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Erstellt eine frische InMemory-Datenbankinstanz für jeden Test.</summary>
public static class TestDbContextFactory
{
    /// <summary>Erstellt einen neuen DbContext mit einer einzigartigen InMemory-Datenbank.</summary>
    public static SoftwareschmiededDbContext Create()
    {
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new SoftwareschmiededDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
