using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    /// <summary>Erstellt einen neuen DbContext auf einer echten SQLite-Dateidatenbank, optional mit EF-Interceptoren (z. B. für gezielte Lesefehler).</summary>
    /// <param name="dbPath">Dateipfad der SQLite-Datenbank.</param>
    /// <param name="interceptors">Optionale Interceptoren, die am Context registriert werden.</param>
    public static SoftwareschmiededDbContext CreateSqlite(string dbPath, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite($"Data Source={dbPath}");

        if (interceptors.Length > 0)
            builder.AddInterceptors(interceptors);

        var context = new SoftwareschmiededDbContext(builder.Options);
        context.Database.EnsureCreated();
        return context;
    }
}
