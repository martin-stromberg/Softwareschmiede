using System.Net;
using System.Text;
using Softwareschmiede.StatusContracts;
using Softwareschmiede.StatusServer.GitHub;

namespace Softwareschmiede.StatusServer.Status;

/// <summary>
/// Rendert die read-only Status-Seite als server-seitiges HTML
/// (Sektionen, Quellen-/Zeitstempel-Badges, Offline-/Stale-Markierung, GitHub-Links, responsives CSS).
/// </summary>
public sealed class StatusPageComposer
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Erstellt den Composer.</summary>
    /// <param name="timeProvider">Die Zeitquelle für Anzeige- und Veraltungsberechnungen.</param>
    public StatusPageComposer(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Rendert die komplette Status-Seite.</summary>
    /// <param name="instances">Die bekannten App-Instanzen aus dem Snapshot-Cache.</param>
    /// <param name="gitHub">Die aggregierten GitHub-Repository-Stati.</param>
    /// <param name="refreshSeconds">Das Meta-Refresh-Intervall der Seite.</param>
    /// <returns>Das vollständige HTML-Dokument.</returns>
    public string Render(
        IReadOnlyList<InstanceSnapshotView> instances,
        IReadOnlyList<GitHubRepositoryStatus> gitHub,
        int refreshSeconds)
    {
        var now = _timeProvider.GetUtcNow();
        var sb = new StringBuilder(16_000);
        sb.Append("<!doctype html><html lang=\"de\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta http-equiv=\"refresh\" content=\"").Append(refreshSeconds).Append("\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>Softwareschmiede-Status</title><style>");
        sb.Append(Css);
        sb.Append("</style></head><body><div class=\"page\">");

        RenderHeader(sb, instances, now);
        sb.Append("<div class=\"meta-line\">Stand ")
            .Append(FmtTime(now, now))
            .Append(" UTC · Aktualisierung alle ")
            .Append(refreshSeconds)
            .Append(" s · <a href=\"/status.json\">/status.json</a></div>");

        if (instances.Count == 0)
        {
            sb.Append("<div class=\"stale-note\">Es hat sich noch keine App-Instanz am Relay registriert. ")
                .Append("Sobald in den App-Einstellungen die Remote-Übersicht aktiviert ist ")
                .Append("(Einstellungen → Automatisierung → Remote-Übersicht), erscheinen hier die App-Sektionen.</div>");
            sb.Append("<section><h2>App-Instanzen</h2><div class=\"empty\">")
                .Append("Keine App-Instanz hat bisher Daten geliefert.</div></section>");
        }
        else
        {
            foreach (var instance in instances)
                RenderInstance(sb, instance, now);
        }

        foreach (var repo in gitHub)
            RenderGitHubRepository(sb, repo, now);

        sb.Append("<footer>softwareschmiede-status · server-gerendert · read-only</footer>");
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private void RenderHeader(StringBuilder sb, IReadOnlyList<InstanceSnapshotView> instances, DateTimeOffset now)
    {
        var anyConnected = instances.Any(i => i.Connected);
        var badge = instances.Count == 0
            ? ("offline", "keine Instanz")
            : anyConnected
                ? (instances.Any(i => i.IsStale) ? ("stale", "teilweise veraltet") : ("online", "online"))
                : ("offline", "offline");
        sb.Append("<header class=\"top\"><h1>Softwareschmiede-Status</h1>");
        sb.Append("<span class=\"badge ").Append(badge.Item1).Append("\"><span class=\"dot\"></span>")
            .Append(badge.Item2).Append("</span>");
        foreach (var i in instances)
        {
            sb.Append("<span class=\"inst\" style=\"color:#cbd5e1\">Instanz „")
                .Append(E(i.InstanceName ?? "(ohne Namen)"))
                .Append("“ · ")
                .Append(E(ShortId(i.InstanceId)))
                .Append("</span>");
        }

        sb.Append("</header>");
    }

    private void RenderInstance(StringBuilder sb, InstanceSnapshotView instance, DateTimeOffset now)
    {
        var displayName = instance.InstanceName ?? ShortId(instance.InstanceId);
        if (!instance.Connected)
        {
            sb.Append("<div class=\"stale-note\">Instanz „").Append(E(displayName))
                .Append("“ offline seit ").Append(FmtTime(instance.LastSeenUtc, now)).Append(" UTC")
                .Append(" – die App-Sektionen sind veraltet.</div>");
        }
        else if (instance.IsStale)
        {
            sb.Append("<div class=\"stale-note\">Instanz „").Append(E(displayName))
                .Append("“ liefert seit ").Append(FmtTime(instance.LastSeenUtc, now))
                .Append(" UTC keinen Snapshot – Daten ggf. veraltet.</div>");
        }

        var snapshot = instance.Snapshot;
        var dim = instance.IsStale ? " dim" : string.Empty;
        if (snapshot is null)
        {
            sb.Append("<section class=\"").Append(dim.TrimStart()).Append("\"><h2>Instanz ")
                .Append(E(displayName)).Append(" <span class=\"src app\">App</span></h2>")
                .Append("<div class=\"empty\">Noch kein Snapshot empfangen.</div></section>");
            return;
        }

        var items = snapshot.WorkItems ?? [];
        var waiting = items.Where(i => IsWaiting(i, now)).ToList();
        var running = items.Where(i => !IsWaiting(i, now)).ToList();
        var sourceTag = "<span class=\"src app\">App · " + FmtTime(instance.LastSeenUtc, now) + " UTC</span>";
        var instanceTag = "<span class=\"inst\">Instanz " + E(displayName) + "</span>";

        sb.Append("<section class=\"").Append(dim.TrimStart()).Append("\"><h2>Laufende Aufgaben ")
            .Append("<span class=\"count\">(").Append(running.Count).Append(")</span> ")
            .Append(sourceTag).Append(" ").Append(instanceTag).Append("</h2>");
        if (running.Count == 0)
            sb.Append("<div class=\"empty\">Keine laufenden Aufgaben.</div>");
        else
        {
            sb.Append("<table>");
            foreach (var item in running)
                RenderWorkItemRow(sb, item, now);
            sb.Append("</table>");
        }

        sb.Append("</section>");

        var waitingEvents = (snapshot.ScmEvents ?? [])
            .Where(e => e.Status is "Wartend" or "Verzögert")
            .ToList();

        sb.Append("<section class=\"").Append(dim.TrimStart()).Append("\"><h2>Wartet auf Anwender ")
            .Append("<span class=\"count\">(").Append(waiting.Count + waitingEvents.Count).Append(")</span> ")
            .Append(sourceTag).Append(" ").Append(instanceTag).Append("</h2>");
        if (waiting.Count == 0 && waitingEvents.Count == 0)
            sb.Append("<div class=\"empty\">Keine wartenden Einträge.</div>");
        else
        {
            sb.Append("<table>");
            foreach (var item in waiting)
                RenderWorkItemRow(sb, item, now);
            foreach (var e in waitingEvents)
                RenderScmEventRow(sb, e, now);
            sb.Append("</table>");
        }

        sb.Append("</section>");

        var events = snapshot.ScmEvents ?? [];
        sb.Append("<section class=\"").Append(dim.TrimStart()).Append("\"><h2>Letzte SCM-Ereignisse ")
            .Append("<span class=\"count\">(").Append(events.Count).Append(")</span> ")
            .Append(sourceTag).Append(" ").Append(instanceTag).Append("</h2>");
        if (events.Count == 0)
            sb.Append("<div class=\"empty\">Keine Ereignisse.</div>");
        else
        {
            sb.Append("<table>");
            foreach (var e in events)
                RenderScmEventRow(sb, e, now);
            sb.Append("</table>");
        }

        sb.Append("</section>");
    }

    private static bool IsWaiting(StatusSnapshotWorkItem item, DateTimeOffset now)
        => item.RunStatus == "WaitingForInput" || (item.PausedUntilUtc is { } until && until > now);

    private void RenderWorkItemRow(StringBuilder sb, StatusSnapshotWorkItem item, DateTimeOffset now)
    {
        sb.Append("<tr><td class=\"num\">");
        AppendLinked(sb, item.IssueNumber is { } n ? $"#{n}" : "–", item.IssueUrl);
        sb.Append("</td><td>").Append(E(item.Title));
        if (!string.IsNullOrEmpty(item.RepositoryName))
            sb.Append(" <span class=\"inst\">").Append(E(item.RepositoryName)).Append("</span>");
        sb.Append("</td><td>").Append(RunPill(item, now)).Append("</td><td class=\"muted\">");
        var details = new List<string>();
        if (!string.IsNullOrEmpty(item.Kind))
            details.Add(item.Kind);
        if (item.PausedUntilUtc is { } until && until > now)
            details.Add("pausiert bis " + FmtTime(until, now) + " UTC");
        else if (item.LastHeartbeatUtc is { } hb)
            details.Add("Heartbeat " + FmtTime(hb, now) + " UTC");
        sb.Append(E(string.Join(" · ", details)));
        sb.Append("</td><td class=\"right\">");
        // Fremd-Snapshots können null-Listen liefern — defensiv lesen wie WorkItems/ScmEvents.
        foreach (var pr in item.PullRequests ?? [])
        {
            if (IsSafeHttpUrl(pr.Url))
                sb.Append("<a href=\"").Append(E(pr.Url)).Append("\">PR #").Append(pr.Number).Append(" ↗</a> ");
            else
                sb.Append("PR #").Append(pr.Number).Append(' ');
        }

        if (!string.IsNullOrEmpty(item.IssueUrl))
            AppendLinked(sb, "Issue", item.IssueUrl);
        sb.Append("</td></tr>");
    }

    private static string RunPill(StatusSnapshotWorkItem item, DateTimeOffset now)
    {
        if (item.PausedUntilUtc is { } until && until > now)
            return "<span class=\"pill pause\">pausiert</span>";
        if (item.RunStatus == "WaitingForInput")
            return "<span class=\"pill wait\">Wartet auf Eingabe</span>";
        if (item.HasActiveRun || item.RunStatus == "Running")
            return "<span class=\"pill run\">läuft</span>";
        return "<span class=\"pill wait\">" + E(item.RunStatus ?? item.Status) + "</span>";
    }

    private static void RenderScmEventRow(StringBuilder sb, StatusSnapshotScmEvent e, DateTimeOffset now)
    {
        sb.Append("<tr><td class=\"muted\">").Append(FmtTime(e.ReceivedUtc, now)).Append("</td><td>");
        AppendLinked(sb, e.ReferenceDisplay ?? "Ereignis", e.Url);
        sb.Append("</td><td>").Append(ScmStatusPill(e.Status)).Append("</td><td class=\"muted\">")
            .Append(E(e.Reason ?? string.Empty)).Append("</td></tr>");
    }

    private static string ScmStatusPill(string status) => status switch
    {
        "Verarbeitet" or "Aufgenommen" => "<span class=\"pill done\">" + E(status) + "</span>",
        "Abgelehnt" or "Fehlgeschlagen" => "<span class=\"pill err\">" + E(status) + "</span>",
        "Wartend" or "Verzögert" => "<span class=\"pill wait\">" + E(status) + "</span>",
        _ => "<span class=\"pill pause\">" + E(status) + "</span>",
    };

    private static void RenderGitHubRepository(StringBuilder sb, GitHubRepositoryStatus repo, DateTimeOffset now)
    {
        var sourceTag = repo.FetchedUtc is { } f
            ? "<span class=\"src gh\">GitHub · " + FmtTime(f, now) + " UTC</span>"
            : "<span class=\"src gh\">GitHub</span>";

        if (repo.Error is { } error)
        {
            sb.Append("<div class=\"stale-note\">GitHub-Abruf für ")
                .Append(E(repo.DisplayName)).Append(" fehlgeschlagen");
            if (repo.ErrorUtc is { } errUtc)
                sb.Append(" (").Append(FmtTime(errUtc, now)).Append(" UTC)");
            sb.Append(": ").Append(E(error)).Append("</div>");
        }

        sb.Append("<section><h2>");
        if (IsSafeHttpUrl(repo.Url))
            sb.Append("<a href=\"").Append(E(repo.Url)).Append("\">").Append(E(repo.DisplayName)).Append("</a>");
        else
            sb.Append(E(repo.DisplayName));
        sb.Append(" ").Append(sourceTag);
        if (repo.OpenIssuesCount is { } issues)
            sb.Append(" <span class=\"count\">· ").Append(issues).Append(" offene Issues</span>");
        sb.Append("</h2><h3 class=\"count\">Offene Pull Requests (")
            .Append(repo.OpenPullRequests.Count).Append(")</h3>");
        if (repo.OpenPullRequests.Count == 0)
            sb.Append("<div class=\"empty\">Keine offenen Pull Requests.</div>");
        else
        {
            sb.Append("<table>");
            foreach (var pr in repo.OpenPullRequests)
            {
                sb.Append("<tr><td class=\"num\">");
                AppendLinked(sb, $"#{pr.Number}", pr.Url);
                sb.Append("</td><td>");
                AppendLinked(sb, pr.Title, pr.Url);
                sb.Append("</td></tr>");
            }

            sb.Append("</table>");
        }

        sb.Append("<h3 class=\"count\" style=\"margin-top:12px\">Letzte Releases (")
            .Append(repo.Releases.Count).Append(")</h3>");
        if (repo.Releases.Count == 0)
            sb.Append("<div class=\"empty\">Keine Releases.</div>");
        else
        {
            sb.Append("<table>");
            foreach (var release in repo.Releases)
            {
                sb.Append("<tr><td class=\"num\">");
                AppendLinked(sb, release.TagName, release.Url);
                sb.Append("</td><td>").Append(E(release.Name))
                    .Append("</td><td class=\"muted\">")
                    .Append(release.PublishedUtc is { } p ? p.ToString("dd.MM.yyyy") : string.Empty)
                    .Append("</td></tr>");
            }

            sb.Append("</table>");
        }

        sb.Append("</section>");
    }

    /// <summary>Gibt <paramref name="text"/> als Link aus, sofern <paramref name="url"/> eine
    /// erlaubte absolute http(s)-URL ist; sonst als reinen Text (Scheme-Whitelist gegen
    /// aktive Protokolle wie <c>javascript:</c>/<c>data:</c> aus Fremd-Snapshots).</summary>
    private static void AppendLinked(StringBuilder sb, string text, string? url)
    {
        if (IsSafeHttpUrl(url))
            sb.Append("<a href=\"").Append(E(url)).Append("\">").Append(E(text)).Append(" ↗</a>");
        else
            sb.Append(E(text));
    }

    private static bool IsSafeHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string ShortId(string instanceId)
        => instanceId.Length <= 8 ? instanceId : instanceId[..8] + "…";

    /// <summary>Uhrzeit-Anzeige; bei abweichendem Kalendertag oder &gt;24 h Abstand wird
    /// zusätzlich das Datum ausgegeben (UTC bleibt an der Aufrufstelle gekennzeichnet).</summary>
    private static string FmtTime(DateTimeOffset? value, DateTimeOffset now)
        => value is { } v
            ? (v.Date != now.Date || Math.Abs((now - v).TotalHours) >= 24
                ? v.ToString("dd.MM. HH:mm:ss")
                : v.ToString("HH:mm:ss"))
            : "–";

    private static string E(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private const string Css = """
* { box-sizing: border-box; margin: 0; }
body { font-family: "Segoe UI", system-ui, sans-serif; background: #f4f5f7; color: #1f2430; }
.page { max-width: 1080px; margin: 0 auto; padding: 24px 20px 60px; }
header.top { display: flex; align-items: center; gap: 14px; flex-wrap: wrap;
  background: #1f2937; color: #f8fafc; padding: 16px 24px; border-radius: 10px; }
header.top h1 { font-size: 20px; font-weight: 600; flex: 1; }
.badge { display: inline-flex; align-items: center; gap: 7px; padding: 4px 12px;
  border-radius: 999px; font-size: 13px; font-weight: 600; }
.badge .dot { width: 9px; height: 9px; border-radius: 50%; }
.badge.online { background: #065f46; color: #d1fae5; } .badge.online .dot { background: #34d399; }
.badge.offline { background: #7f1d1d; color: #fee2e2; } .badge.offline .dot { background: #f87171; }
.badge.stale { background: #78350f; color: #fef3c7; } .badge.stale .dot { background: #fbbf24; }
.meta-line { margin: 10px 4px 22px; font-size: 12.5px; color: #6b7280; }
section { background: #fff; border: 1px solid #e5e7eb; border-radius: 10px;
  padding: 16px 20px; margin-bottom: 16px; }
section h2 { font-size: 15px; font-weight: 600; margin-bottom: 10px;
  display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
section h3 { font-size: 13px; font-weight: 600; margin-bottom: 6px; }
.src { font-size: 11px; font-weight: 500; color: #fff; border-radius: 4px; padding: 1px 7px; }
.src.app { background: #4f46e5; } .src.gh { background: #0d9488; }
.count { color: #6b7280; font-weight: 500; }
table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
td, th { text-align: left; padding: 7px 8px; border-top: 1px solid #f0f1f3; overflow-wrap: break-word; }
tr:first-child td { border-top: 0; }
td.num { width: 70px; font-family: Consolas, monospace; color: #4f46e5; }
td.right, th.right { text-align: right; }
td.muted { color: #6b7280; font-size: 12.5px; }
.pill { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; overflow-wrap: normal; }
.pill.run { background: #e0e7ff; color: #3730a3; }
.pill.wait { background: #fef3c7; color: #92400e; }
.pill.done { background: #d1fae5; color: #065f46; }
.pill.err { background: #fee2e2; color: #991b1b; }
.pill.pause { background: #e5e7eb; color: #4b5563; }
a { color: #4f46e5; text-decoration: none; }
.inst { font-size: 12px; color: #6b7280; font-weight: 500; min-width: 0; overflow-wrap: break-word; }
.stale-note { display: flex; align-items: center; gap: 8px; background: #fef3c7;
  border: 1px solid #f59e0b55; color: #92400e; font-size: 12.5px;
  border-radius: 8px; padding: 8px 14px; margin-bottom: 14px; }
.dim td { color: #6b7280; }
.empty { color: #6b7280; font-size: 13px; padding: 10px 4px; }
footer { margin-top: 26px; font-size: 11.5px; color: #9ca3af; text-align: center; }
@media (max-width: 600px) {
  .page { padding: 12px 8px 40px; }
  header.top { padding: 12px 14px; }
  section { padding: 12px 10px; }
  td, th { padding: 6px 4px; }
  td.num { width: auto; }
}
""";
}
