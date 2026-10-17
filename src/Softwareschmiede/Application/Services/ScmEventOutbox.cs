using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Domain.ValueObjects;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>Bestätigt oder rekonstruiert Remoteaktionen unabhängig von der Promptzustellung.</summary>
public sealed class ScmEventOutbox(SoftwareschmiededDbContext db, TimeProvider time)
{
    /// <summary>Persistiert eine Aktion vor dem ersten Remoteaufruf.</summary>
    public async Task<ScmOutgoingAction> EnsureAsync(ScmIncomingEvent item, string purpose, ScmResponseRequest request, CancellationToken ct)
    {
        var id = Guid.Parse(request.ActionId);
        var action = await db.ScmOutgoingActions.FindAsync([id], ct);
        if (action is not null) return action;
        action = new ScmOutgoingAction
        {
            Id = id,
            EventId = item.Id,
            AttemptId = purpose.StartsWith("ai:", StringComparison.Ordinal) ? item.DeliveryAttemptId : null,
            Purpose = purpose,
            RequestJson = JsonSerializer.Serialize(request, ScmEventStore.JsonOptions)
        };
        db.Add(action);
        await db.SaveChangesAsync(ct);
        return action;
    }

    /// <summary>Eine ungewisse Aktion wird nur abgeglichen und niemals automatisch erneut gepostet.</summary>
    public async Task ExecuteAsync(ScmOutgoingAction action, IScmEventResponder responder, CancellationToken ct)
    {
        if (action.Status == "Succeeded" || action.NextAttemptUtc > time.GetUtcNow().ToUnixTimeMilliseconds()) return;
        var request = JsonSerializer.Deserialize<ScmResponseRequest>(action.RequestJson, ScmEventStore.JsonOptions)!;
        var uncertain = action.Status is "Sending" or "Uncertain";
        try
        {
            var found = await responder.ReconcileResponseAsync(request, ct);
            if (found is null && uncertain)
            {
                action.Status = "Uncertain";
                action.Error = "Ausgang ungewiss; Remoteergebnis noch nicht wiedergefunden.";
                action.NextAttemptUtc = ScmEventStore.RetryAt(++action.Attempts, time.GetUtcNow().ToUnixTimeMilliseconds());
                await db.SaveChangesAsync(ct);
                return;
            }
            if (found is null)
            {
                action.Status = "Sending";
                action.Attempts++;
                await db.SaveChangesAsync(ct);
                found = await responder.ExecuteResponseAsync(request, ct);
            }
            action.RemoteId = found.RemoteId;
            action.Status = "Succeeded";
            action.Error = null;
            action.ErrorCode = null;
            action.NextAttemptUtc = null;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var failure = ex as ScmProviderException;
            action.Status = uncertain || failure?.OutcomeUncertain == true || (action.Status == "Sending" && failure is null) ? "Uncertain" : "Failed";
            action.ErrorCode = failure?.FailureKind.ToString() ?? "Temporary";
            action.Error = failure?.Message ?? "Remoteaktion konnte nicht bestätigt werden.";
            action.NextAttemptUtc = failure?.FailureKind is ScmProviderFailureKind.Authentication or ScmProviderFailureKind.Permission or ScmProviderFailureKind.Permanent
                ? long.MaxValue : ScmEventStore.RetryAt(action.Attempts, time.GetUtcNow().ToUnixTimeMilliseconds(), failure?.RetryAfterUtc);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
