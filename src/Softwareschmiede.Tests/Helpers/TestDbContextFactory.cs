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

    /// <summary>
    /// Erstellt einen neuen DbContext auf einer echten SQLite-Datenbank, optional mit
    /// EF-Interceptoren (z. B. für gezielte Lesefehler). Das Schema legt der Aufrufer
    /// selbst an (EnsureCreated/Migrate), damit auch Locking-Szenarien testbar bleiben.
    /// </summary>
    /// <param name="connectionString">Der SQLite-Connection-String (z. B. <c>Data Source=...</c>).</param>
    /// <param name="interceptors">Optionale Interceptoren, die am Context registriert werden.</param>
    public static SoftwareschmiededDbContext CreateSqlite(string connectionString, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseSqlite(connectionString);

        if (interceptors.Length > 0)
            builder.AddInterceptors(interceptors);

        return new SoftwareschmiededDbContext(builder.Options);
    }
}
