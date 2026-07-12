---
name: entityframework-database
description: "Use when implementing or reviewing Entity Framework database setup, schema evolution, migrations, and upgrade safety for existing installations."
---

# Entity Framework Database Handling

## Purpose
Ensure database changes are versioned, repeatable, and safe for upgrades of existing installations.

## Rules
1. Use EF Core migrations for all schema changes.
1a. Migrationen ausschließlich per EF-CLI erzeugen: `dotnet ef migrations add <Name> --project <Infrastructure-Projekt> --startup-project <Startup-Projekt>` (Bash-Tool). Migrationsdateien niemals per Write-Tool handschriftlich anlegen — dabei fehlt fast immer die zugehörige `.Designer.cs`-Datei und der `ModelSnapshot` wird nicht aktualisiert. Ein Hook (`check_ef_migration_manual.py`) blockiert handschriftlich per Write erstellte Migrationsdateien. Nach der Generierung dürfen Up/Down per Edit angepasst werden (siehe Workflow Schritt 3).
2. Do not use EnsureCreated/EnsureDeleted in production startup paths.
3. Startup initialization must run migrations via MigrateAsync.
4. Preserve data during upgrades; avoid destructive schema operations unless explicitly approved.
5. For legacy databases created without migration history, add a controlled transition step before first migration run.
6. Every model change requires either a new migration or an explicit rationale why no migration is needed.
7. Validate migration changes with build + tests before completion.

## Workflow
1. Update entity/model configuration.
2. Generate migration via `dotnet ef migrations add <Name> --project <Infrastructure-Projekt> --startup-project <Startup-Projekt>` (Bash) — nie per Write-Tool.
3. Review generated Up/Down for data safety.
4. Apply migrations at app startup (or deployment pipeline) using MigrateAsync.
5. Run automated tests and a local upgrade test against an existing database copy.

## Startup Guidance
- Preferred: dbContext.Database.MigrateAsync()
- Transitional case (legacy DB without __EFMigrationsHistory):
  - Detect existing schema + missing migration history.
  - Bootstrap history once in a controlled way.
  - Continue with MigrateAsync.

## Updating Entities in Disconnected Scenarios

EF Core recommends the **Load-and-Patch** pattern for disconnected scenarios (e.g. API controllers, background services). Never attach or re-insert a detached entity graph — EF cannot track what changed.

### Pattern

Die Patch-Logik gehört **nicht in den Controller oder Service**, sondern in eine `Update`-Methode der Entität oder eine dedizierte Erweiterungsklasse. Der aufrufende Code bleibt dadurch schlank:

```csharp
var existing = await context.Set<TEntity>()
    .Include(e => e.ChildCollection)
    .FirstOrDefaultAsync(e => e.Id == dto.Id, cancellationToken);

if (existing is null)
    return NotFound();

existing.Update(dto);

await context.SaveChangesAsync(cancellationToken);
```

Die Patch-Logik selbst lebt auf der Entität oder in einer Erweiterungsklasse:

```csharp
// Option A – Methode auf der Entität (bevorzugt, wenn die Entität das DTO kennt)
public sealed class Endpoint
{
    public void Update(EndpointDto dto)
    {
        Name = dto.Name;
        UpdatedAt = DateTimeOffset.UtcNow;

        Headers.Synchronize(
            dto.Headers,
            (e, d) => e.Id == d.Id,
            (e, d) => e.Value = d.Value,
            d => new Header { Value = d.Value });
    }
}

// Option B – Erweiterungsklasse (wenn Entität und DTO in verschiedenen Schichten leben)
public static class EndpointExtensions
{
    public static void Update(this Endpoint endpoint, EndpointDto dto)
    {
        endpoint.Name = dto.Name;
        endpoint.UpdatedAt = DateTimeOffset.UtcNow;

        endpoint.Headers.Synchronize(
            dto.Headers,
            (e, d) => e.Id == d.Id,
            (e, d) => e.Value = d.Value,
            d => new Header { Value = d.Value });
    }
}
```

### Collection synchronization helper

Add this extension once to the Infrastructure layer:

```csharp
public static class CollectionSyncExtensions
{
    public static void Synchronize<TEntity, TDto>(
        this ICollection<TEntity> existing,
        IReadOnlyCollection<TDto> incoming,
        Func<TEntity, TDto, bool> match,
        Action<TEntity, TDto> update,
        Func<TDto, TEntity> create)
    {
        // Remove items no longer present
        foreach (var item in existing.Where(e => !incoming.Any(d => match(e, d))).ToList())
            existing.Remove(item);

        foreach (var dto in incoming)
        {
            var entity = existing.FirstOrDefault(e => match(e, dto));
            if (entity is null)
                existing.Add(create(dto));
            else
                update(entity, dto);
        }
    }
}
```

### Rules
- Patch-Logik (Properties + Collections) in eine `Update`-Methode der Entität oder eine Erweiterungsklasse auslagern — nie inline im Controller oder Service.
- Bevorzuge Option A (Methode auf der Entität), wenn Entität und DTO in derselben Schicht liegen; Option B sonst.
- Always `Include` every collection that will be modified before patching.
- Never call `context.Update()` or `context.Attach()` on a full detached object graph.
- Scalar-only updates on a tracked entity do not need `Update()` — change tracking handles them.
- Use `FirstOrDefaultAsync` + explicit 404 handling; never rely on `Find` for eager-loaded graphs.

## Quality Gates
- No EnsureCreated in production code paths.
- No silent schema drift.
- Migration history table present and consistent.
- Tests green after migration changes.
- Disconnected updates use Load-and-Patch; no raw Attach/Update on detached graphs.
