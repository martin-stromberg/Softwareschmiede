using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Softwareschmiede.Application.Services.Updates;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Application.Services;

/// <summary>
/// Generischer Service zum Lesen und Schreiben von Anwendungseinstellungen (Key-Value-Paare).
/// </summary>
public sealed class AppSettingService
{
    /// <summary>Schlüssel für die X-Koordinate des Hauptfensters.</summary>
    public const string WindowPositionXKey = "window.position.x";

    /// <summary>Schlüssel für die Y-Koordinate des Hauptfensters.</summary>
    public const string WindowPositionYKey = "window.position.y";

    /// <summary>Schlüssel für die Breite des Hauptfensters.</summary>
    public const string WindowWidthKey = "window.size.width";

    /// <summary>Schlüssel für die Höhe des Hauptfensters.</summary>
    public const string WindowHeightKey = "window.size.height";

    /// <summary>Schlüssel für den Dark-Mode-Status.</summary>
    public const string DesignModeKey = "ui.designmode.name";

    /// <summary>Schlüssel für das Standard-KI-Plugin.</summary>
    public const string DefaultKiPluginKey = "ki.plugin.default";

    /// <summary>Schlüssel für das Standard-SCM-Plugin.</summary>
    public const string DefaultScmPluginKey = "scm.plugin.default";

    /// <summary>Schlüssel für das Log-Level.</summary>
    public const string LogLevelKey = "logging.level";

    /// <summary>Schlüssel für die Prioritäts-Reihenfolge der IDE-Plugins (kommagetrennte Liste von Plugin-Prefixen).</summary>
    public const string IdePluginOrderKey = "plugins.ide.order";

    /// <summary>Schlüssel für den Laufzeit-Schalter "SCM-Ereignisüberwachung aktiviert".</summary>
    public const string ScmMonitoringEnabledKey = "scm.monitoring.enabled";

    /// <summary>Schlüssel für den globalen Schalter der Issue-Aufbereitung (Erkennung neu angelegter Issues und interner Aufbereitungsauftrag).</summary>
    public const string ScmIssuePreparationEnabledKey = "scm.issueaufbereitung.enabled";

    /// <summary>Schlüssel für den Plugin-Prefix, mit dem die Issue-Aufbereitung ausgeführt werden soll; leer bedeutet Fallback auf das globale Standard-Plugin.</summary>
    public const string ScmIssuePreparationPluginKey = "scm.issueaufbereitung.plugin";

    /// <summary>Schlüssel für den Schalter, ob die Issue-Aufbereitung ihre Analyse als Issue-Kommentar dokumentieren soll.</summary>
    public const string ScmIssuePreparationCommentEnabledKey = "scm.issueaufbereitung.comment.enabled";

    /// <summary>Schlüssel für das konfigurierbare SCM-Trigger-Keyword.</summary>
    public const string ScmTriggerKeywordKey = "scm.trigger.keyword";

    /// <summary>Schlüssel für das konfigurierbare SCM-Verzögerungs-Keyword.</summary>
    public const string ScmDelayKeywordKey = "scm.delay.keyword";

    /// <summary>Schlüssel für den SCM-Prompt-Anhang, der bei jeder Ereignisart angehängt wird.</summary>
    public const string ScmPromptAppendGeneralKey = "scm.prompt.append.general";

    /// <summary>Schlüssel für den SCM-Prompt-Anhang bei Issue-Beschreibungen (neues/geändertes Issue).</summary>
    public const string ScmPromptAppendIssueKey = "scm.prompt.append.issue";

    /// <summary>Schlüssel für den SCM-Prompt-Anhang bei Kommentaren auf Issues.</summary>
    public const string ScmPromptAppendIssueCommentKey = "scm.prompt.append.issuecomment";

    /// <summary>Schlüssel für den SCM-Prompt-Anhang bei Pull-Request-Beschreibungen (neuer/geänderter PR).</summary>
    public const string ScmPromptAppendPullRequestKey = "scm.prompt.append.pr";

    /// <summary>Schlüssel für den SCM-Prompt-Anhang bei Kommentaren und Reviews auf Pull Requests.</summary>
    public const string ScmPromptAppendPullRequestCommentKey = "scm.prompt.append.prcomment";

    /// <summary>Schlüssel für den Laufzeit-Schalter "SCM-Abschlussüberwachung aktiviert".</summary>
    public const string ScmTaskCompletionEnabledKey = "scm.taskcompletion.enabled";

    /// <summary>Schlüssel für die Wartezeit (Minuten) zwischen erkanntem Referenz-Schluss und dem Beenden der Aufgabe.</summary>
    public const string ScmTaskCompletionWaitMinutesKey = "scm.taskcompletion.wait.minutes";

    /// <summary>Standardwert der Abschluss-Wartezeit in Minuten.</summary>
    public const int ScmTaskCompletionDefaultWaitMinutes = 60;

    /// <summary>Schlüssel für den Update-Modus (<see cref="UpdateMode"/>-Wert als Zahl gespeichert).</summary>
    public const string UpdateModeKey = "updates.mode";

    /// <summary>Schlüssel für den Update-Pfad (Kanalselektor; siehe <c>UpdatePfadKanalResolver</c>).</summary>
    public const string UpdatePfadKey = "updates.pfad";

    /// <summary>Fester EF-Abfragetag der gemeinsamen Update-Einstellungs-Leseabfrage für gezielte Testinterception.</summary>
    public const string UpdateSettingsReadTag = "UpdateSettings.Read";

    /// <summary>Schlüssel für den Anzeigemodus der Aufgabenliste in der Seitenleiste (<c>AufgabenListenModus</c>-Name).</summary>
    public const string AktiveAufgabenListenModusKey = "aktiveaufgaben.listenmodus";

    /// <summary>Schlüssel für den Anzeigemodus der Aufgabenliste im Dashboard (<c>AufgabenListenModus</c>-Name).</summary>
    public const string AktiveAufgabenListenModusDashboardKey = "aktiveaufgaben.listenmodus.dashboard";

    /// <summary>Schlüssel für den Backmerge-Aktivierungsmodus (<see cref="ScmBackmergeMode"/>-Wert als Zahl gespeichert).</summary>
    public const string ScmBackmergeModeKey = "scm.backmerge.mode";

    /// <summary>Schlüssel für den Prüfmodus der SCM-Abschlussüberwachung (<see cref="ScmTaskCompletionCheckMode"/>-Wert als Zahl gespeichert).</summary>
    public const string ScmTaskCompletionCheckModeKey = "scm.taskcompletion.scope";

    /// <summary>Schlüssel für die automatische Aktivierung von Draft-Backmerge-PRs.</summary>
    public const string ScmBackmergeDraftAktivierungKey = "scm.backmerge.draft_activation";

    /// <summary>Schlüssel für die Admin-Merge-Fallback-Option des Backmerge-Dienstes.</summary>
    public const string ScmBackmergeAdminMergeKey = "scm.backmerge.admin_merge";

    /// <summary>Schlüssel für den Kontomodus der SCM-Ereignisüberwachung (<see cref="ScmKontoModus"/>-Wert als Zahl gespeichert).</summary>
    public const string ScmAccountModeKey = "scm.monitoring.accountmode";

    /// <summary>Schlüsselpräfix für die projektbezogene Backmerge-Freigabe; der vollständige Schlüssel ist <c>plugins.backmerge.project.{projektId}</c>.</summary>
    public const string BackmergeProjektKeyPrefix = "plugins.backmerge.project.";

    /// <summary>Schlüssel für den Schalter "Herunterfahren erzwingen" des automatischen OS-Shutdowns.</summary>
    public const string AutoShutdownErzwingenKey = "autoshutdown.force";

    /// <summary>Schlüssel für die Toleranzzeit (Minuten) des Auto-Shutdown-Countdowns.</summary>
    public const string AutoShutdownToleranceMinutesKey = "autoshutdown.toleranz.minuten";

    /// <summary>Standardwert der Auto-Shutdown-Toleranzzeit in Minuten.</summary>
    public const int AutoShutdownDefaultToleranceMinutes = 5;

    /// <summary>Maximalwert der Auto-Shutdown-Toleranzzeit in Minuten.</summary>
    public const int AutoShutdownMaxToleranceMinutes = 1440;

    private readonly SoftwareschmiedeDbContext _db;
    private readonly ILogger<AppSettingService> _logger;

    /// <inheritdoc cref="AppSettingService"/>
    public AppSettingService(
        SoftwareschmiedeDbContext db,
        ILogger<AppSettingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Liest den Wert einer Einstellung. Gibt <c>null</c> zurück, wenn kein Wert gespeichert ist.</summary>
    public async Task<string?> GetSettingAsync(string schluessel, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == schluessel, ct);

        return setting?.Value;
    }

    /// <summary>Liest den Wert einer Einstellung als <see cref="int"/>. Gibt <c>null</c> zurück, wenn kein Wert gespeichert ist oder der Wert kein gültiger Integer ist.</summary>
    public async Task<int?> GetIntSettingAsync(string schluessel, CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(schluessel, ct);
        if (int.TryParse(wert, out var result))
            return result;
        return null;
    }

    /// <summary>Liest den Wert einer Einstellung als <see cref="bool"/>. Gibt <c>null</c> zurück, wenn kein Wert gespeichert ist oder der Wert kein gültiger Boolean ist.</summary>
    public async Task<bool?> GetBoolSettingAsync(string schluessel, CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(schluessel, ct);
        if (bool.TryParse(wert, out var result))
            return result;
        return null;
    }

    /// <summary>Speichert oder überschreibt eine Einstellung. Übergibt man <c>null</c>, wird der Wert auf null gesetzt (nicht gelöscht).</summary>
    public async Task SetSettingAsync(string schluessel, string? wert, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings
            .FirstOrDefaultAsync(s => s.Key == schluessel, ct);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Id = Guid.NewGuid(),
                Key = schluessel
            };
            _db.AppSettings.Add(setting);
        }

        setting.Value = wert;
        setting.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Einstellung '{Schluessel}' gespeichert.", schluessel);
    }

    /// <summary>Speichert eine Ganzzahl-Einstellung.</summary>
    public Task SetIntSettingAsync(string schluessel, int wert, CancellationToken ct = default)
        => SetSettingAsync(schluessel, wert.ToString(), ct);

    /// <summary>Speichert eine Boolean-Einstellung.</summary>
    public Task SetBoolSettingAsync(string schluessel, bool wert, CancellationToken ct = default)
        => SetSettingAsync(schluessel, wert.ToString(), ct);

    /// <summary>
    /// Ermittelt, ob die SCM-Ereignisüberwachung aktuell aktiv ist. Der über
    /// <see cref="SetBoolSettingAsync"/> unter <see cref="ScmMonitoringEnabledKey"/> persistierte
    /// Laufzeit-Schalter (GUI-Einstellung) gilt ohne Neustart; ohne gespeicherten Eintrag ist die
    /// Überwachung aktiviert (<see langword="true"/>).
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn die SCM-Ereignisüberwachung aktiv ist.</returns>
    public async Task<bool> GetScmMonitoringEnabledAsync(CancellationToken ct = default)
        => await GetBoolSettingAsync(ScmMonitoringEnabledKey, ct) ?? true;

    /// <summary>
    /// Ermittelt, ob die Issue-Aufbereitung (Erkennung neu angelegter Issues und interner
    /// Aufbereitungsauftrag) global freigeschaltet ist. Standard: <see langword="false"/> (opt-in).
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn die Issue-Aufbereitung global aktiviert ist.</returns>
    public async Task<bool> GetScmIssuePreparationEnabledAsync(CancellationToken ct = default)
        => await GetBoolSettingAsync(ScmIssuePreparationEnabledKey, ct) ?? false;

    /// <summary>
    /// Liest den konfigurierten Plugin-Prefix der Issue-Aufbereitung
    /// (<see cref="ScmIssuePreparationPluginKey"/>). Fehlende oder leere Werte liefern
    /// <see langword="null"/> (Fallback auf das globale Standard-Plugin); gespeicherte Werte werden
    /// getrimmt zurückgegeben.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Der Plugin-Prefix oder <see langword="null"/> für den Standard-Fallback.</returns>
    public async Task<string?> GetScmIssuePreparationPluginPrefixAsync(CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(ScmIssuePreparationPluginKey, ct);
        return string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
    }

    /// <summary>
    /// Ermittelt, ob die Issue-Aufbereitung ihre Analyse als Issue-Kommentar dokumentieren soll
    /// (<see cref="ScmIssuePreparationCommentEnabledKey"/>). Standard: <see langword="true"/>.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn die Analyse kommentiert werden soll.</returns>
    public async Task<bool> GetScmIssuePreparationCommentEnabledAsync(CancellationToken ct = default)
        => await GetBoolSettingAsync(ScmIssuePreparationCommentEnabledKey, ct) ?? true;

    /// <summary>Bildet den Einstellungsschlüssel der projektbezogenen Backmerge-Freigabe.</summary>
    /// <param name="projectId">Die Projekt-ID.</param>
    /// <returns>Den vollständigen Einstellungsschlüssel <c>plugins.backmerge.project.{projektId}</c>.</returns>
    public static string BackmergeProjektKey(Guid projectId) => $"{BackmergeProjektKeyPrefix}{projectId}";

    /// <summary>
    /// Liest den Backmerge-Aktivierungsmodus unter <see cref="ScmBackmergeModeKey"/>.
    /// Fehlende oder ungültige gespeicherte Werte fallen auf
    /// <see cref="ScmBackmergeMode.Disabled"/> zurück.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Der effektive Backmerge-Modus.</returns>
    public async Task<ScmBackmergeMode> GetScmBackmergeModusAsync(CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(ScmBackmergeModeKey, ct);
        return int.TryParse(wert, out var mode) && Enum.IsDefined((ScmBackmergeMode)mode)
            ? (ScmBackmergeMode)mode
            : ScmBackmergeMode.Disabled;
    }

    /// <summary>Speichert den Backmerge-Aktivierungsmodus unter <see cref="ScmBackmergeModeKey"/>.</summary>
    /// <param name="mode">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> ist kein definierter <see cref="ScmBackmergeMode"/>-Wert.</exception>
    public Task SetScmBackmergeModusAsync(ScmBackmergeMode mode, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unbekannter Backmerge-Modus.");

        return SetIntSettingAsync(ScmBackmergeModeKey, (int)mode, ct);
    }

    /// <summary>
    /// Liest den Kontomodus der SCM-Ereignisüberwachung unter <see cref="ScmAccountModeKey"/>.
    /// Fehlende oder ungültige gespeicherte Werte fallen auf
    /// <see cref="ScmKontoModus.Benutzerkonto"/> zurück.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Der effektive Kontomodus.</returns>
    public async Task<ScmKontoModus> GetScmAccountModeAsync(CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(ScmAccountModeKey, ct);
        return int.TryParse(wert, out var mode) && Enum.IsDefined((ScmKontoModus)mode)
            ? (ScmKontoModus)mode
            : ScmKontoModus.Benutzerkonto;
    }

    /// <summary>Speichert den Kontomodus der SCM-Ereignisüberwachung unter <see cref="ScmAccountModeKey"/>.</summary>
    /// <param name="mode">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> ist kein definierter <see cref="ScmKontoModus"/>-Wert.</exception>
    public Task SetScmAccountModeAsync(ScmKontoModus mode, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unbekannter SCM-Kontomodus.");

        return SetIntSettingAsync(ScmAccountModeKey, (int)mode, ct);
    }

    /// <summary>Ermittelt, ob die projektbezogene Backmerge-Freigabe für das Projekt gesetzt ist (Standard: <see langword="false"/>).</summary>
    /// <param name="projectId">Die Projekt-ID.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn Backmerge-PRs für dieses Projekt automatisch gemergt werden dürfen.</returns>
    public async Task<bool> GetBackmergeProjektAktivAsync(Guid projectId, CancellationToken ct = default)
        => await GetBoolSettingAsync(BackmergeProjektKey(projectId), ct) ?? false;

    /// <summary>Setzt die projektbezogene Backmerge-Freigabe für das Projekt.</summary>
    /// <param name="projectId">Die Projekt-ID.</param>
    /// <param name="aktiv">Der zu speichernde Schalter.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SetBackmergeProjektAktivAsync(Guid projectId, bool aktiv, CancellationToken ct = default)
        => SetBoolSettingAsync(BackmergeProjektKey(projectId), aktiv, ct);

    /// <summary>
    /// Ermittelt, ob Draft-Backmerge-Pull-Requests vor dem Merge automatisch aktiviert werden
    /// (<see cref="ScmBackmergeDraftAktivierungKey"/>). Standard: <see langword="false"/>.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn Draft-Backmerge-PRs automatisch aktiviert werden.</returns>
    public async Task<bool> GetScmBackmergeDraftAktivierungAsync(CancellationToken ct = default)
        => await GetBoolSettingAsync(ScmBackmergeDraftAktivierungKey, ct) ?? false;

    /// <summary>Setzt die automatische Aktivierung von Draft-Backmerge-Pull-Requests (<see cref="ScmBackmergeDraftAktivierungKey"/>).</summary>
    /// <param name="aktiv">Der zu speichernde Schalter.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SetScmBackmergeDraftAktivierungAsync(bool aktiv, CancellationToken ct = default)
        => SetBoolSettingAsync(ScmBackmergeDraftAktivierungKey, aktiv, ct);

    /// <summary>
    /// Ermittelt, ob der Backmerge-Dienst nach einem blockierten regulären Merge einen
    /// Admin-Merge-Fallback (Umgehung des Branch-Schutzes) ausführen darf
    /// (<see cref="ScmBackmergeAdminMergeKey"/>). Standard: <see langword="false"/>.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn der Admin-Merge-Fallback erlaubt ist.</returns>
    public async Task<bool> GetScmBackmergeAdminMergeAsync(CancellationToken ct = default)
        => await GetBoolSettingAsync(ScmBackmergeAdminMergeKey, ct) ?? false;

    /// <summary>Setzt die Admin-Merge-Fallback-Option des Backmerge-Dienstes (<see cref="ScmBackmergeAdminMergeKey"/>).</summary>
    /// <param name="aktiv">Der zu speichernde Schalter.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SetScmBackmergeAdminMergeAsync(bool aktiv, CancellationToken ct = default)
        => SetBoolSettingAsync(ScmBackmergeAdminMergeKey, aktiv, ct);

    /// <summary>
    /// Ermittelt, ob die SCM-Abschlussüberwachung ereignisgesteuerter Aufgaben aktiv ist. Der über
    /// <see cref="SetBoolSettingAsync"/> unter <see cref="ScmTaskCompletionEnabledKey"/> persistierte
    /// Laufzeit-Schalter (GUI-Einstellung) gilt ohne Neustart; ohne gespeicherten Eintrag ist die
    /// Überwachung aktiviert (<see langword="true"/>).
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn die Abschlussüberwachung aktiv ist.</returns>
    public async Task<bool> GetScmTaskCompletionEnabledAsync(CancellationToken ct = default)
        => await GetBoolSettingAsync(ScmTaskCompletionEnabledKey, ct) ?? true;

    /// <summary>
    /// Liest die Wartezeit in Minuten zwischen dem erkannten Schließen/Mergen der verknüpften
    /// SCM-Referenz und dem automatischen Beenden der Aufgabe (<see cref="ScmTaskCompletionWaitMinutesKey"/>).
    /// Fehlende oder ungültige Werte fallen auf <see cref="ScmTaskCompletionDefaultWaitMinutes"/> zurück;
    /// gespeicherte Werte werden auf 0 bis 43200 (30 Tage) begrenzt.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die wirksame Wartezeit in Minuten.</returns>
    public async Task<int> GetScmTaskCompletionWaitMinutesAsync(CancellationToken ct = default)
    {
        var wert = await GetIntSettingAsync(ScmTaskCompletionWaitMinutesKey, ct);
        return wert is null ? ScmTaskCompletionDefaultWaitMinutes : Math.Clamp(wert.Value, 0, 43200);
    }

    /// <summary>
    /// Liest den Prüfmodus der SCM-Abschlussüberwachung unter <see cref="ScmTaskCompletionCheckModeKey"/>.
    /// Fehlende oder ungültige gespeicherte Werte fallen auf
    /// <see cref="ScmTaskCompletionCheckMode.EventDriven"/> zurück.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Der effektive Prüfmodus.</returns>
    public async Task<ScmTaskCompletionCheckMode> GetScmTaskCompletionCheckModeAsync(CancellationToken ct = default)
    {
        var wert = await GetSettingAsync(ScmTaskCompletionCheckModeKey, ct);
        return int.TryParse(wert, out var mode) && Enum.IsDefined((ScmTaskCompletionCheckMode)mode)
            ? (ScmTaskCompletionCheckMode)mode
            : ScmTaskCompletionCheckMode.EventDriven;
    }

    /// <summary>Speichert den Prüfmodus der SCM-Abschlussüberwachung unter <see cref="ScmTaskCompletionCheckModeKey"/>.</summary>
    /// <param name="mode">Der zu speichernde Modus.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> ist kein definierter <see cref="ScmTaskCompletionCheckMode"/>-Wert.</exception>
    public Task SetScmTaskCompletionCheckModeAsync(ScmTaskCompletionCheckMode mode, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unbekannter Prüfmodus der SCM-Abschlussüberwachung.");

        return SetIntSettingAsync(ScmTaskCompletionCheckModeKey, (int)mode, ct);
    }

    /// <summary>
    /// Liest das konfigurierte SCM-Trigger-Keyword unter <see cref="ScmTriggerKeywordKey"/>.
    /// Fehlende oder ungültige (leere bzw. nur aus Leerraum bestehende) Werte fallen auf
    /// <see cref="ScmEventValidationService.DefaultTriggerKeyword"/> zurück; gespeicherte Werte
    /// werden getrimmt zurückgegeben.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Das effektive Trigger-Keyword.</returns>
    public async Task<string> GetScmTriggerKeywordAsync(CancellationToken ct = default)
        => ScmEventValidationService.NormalizeTriggerKeyword(await GetSettingAsync(ScmTriggerKeywordKey, ct));

    /// <summary>
    /// Liest das konfigurierte SCM-Verzögerungs-Keyword unter <see cref="ScmDelayKeywordKey"/>.
    /// Fehlende oder ungültige (leere bzw. nur aus Leerraum bestehende) Werte fallen auf
    /// <see cref="ScmDelayDirective.DefaultDelayKeyword"/> zurück; gespeicherte Werte
    /// werden getrimmt zurückgegeben.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Das effektive Verzögerungs-Keyword.</returns>
    public async Task<string> GetScmDelayKeywordAsync(CancellationToken ct = default)
        => ScmDelayDirective.NormalizeDelayKeyword(await GetSettingAsync(ScmDelayKeywordKey, ct));

    /// <summary>
    /// Liest alle fünf SCM-Prompt-Anhänge (<see cref="ScmPromptAppendGeneralKey"/>,
    /// <see cref="ScmPromptAppendIssueKey"/>, <see cref="ScmPromptAppendIssueCommentKey"/>,
    /// <see cref="ScmPromptAppendPullRequestKey"/> und <see cref="ScmPromptAppendPullRequestCommentKey"/>)
    /// in einer einzigen Datenbankabfrage. Schlüssel ohne gespeicherten Eintrag fehlen im Ergebnis;
    /// die fachliche Auswertung (Ereignisart-Mapping, Kombination) übernimmt <see cref="ScmPromptAppendix"/>.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Eine Zuordnung von Schlüssel zu gespeichertem Wert.</returns>
    public async Task<IReadOnlyDictionary<string, string?>> GetScmPromptAppendicesAsync(CancellationToken ct = default)
        => await GetSettingsAsync(ScmPromptAppendix.SettingKeys, ct);

    /// <summary>Liest die Werte mehrerer Einstellungen anhand ihrer Schlüssel in einer einzigen Datenbankabfrage.</summary>
    /// <param name="schluessel">Die zu lesenden Schlüssel.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Eine Zuordnung von Schlüssel zu Wert; Schlüssel ohne gespeicherten Eintrag fehlen im Ergebnis.</returns>
    public async Task<IReadOnlyDictionary<string, string?>> GetSettingsAsync(IReadOnlyCollection<string> schluessel, CancellationToken ct = default)
    {
        if (schluessel.Count == 0)
            return new Dictionary<string, string?>();

        return await _db.AppSettings
            .AsNoTracking()
            .Where(s => schluessel.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
    }

    /// <summary>Liest alle Fenstergeometrie-Einstellungen in einer einzigen Datenbankabfrage.</summary>
    public async Task<WindowGeometrySettings> GetWindowGeometryAsync(CancellationToken ct = default)
    {
        var keys = new[]
        {
            WindowPositionXKey,
            WindowPositionYKey,
            WindowWidthKey,
            WindowHeightKey
        };

        var values = await _db.AppSettings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        static int? ParseInt(Dictionary<string, string?> d, string key)
            => d.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : null;

        return new WindowGeometrySettings(
            ParseInt(values, WindowPositionXKey),
            ParseInt(values, WindowPositionYKey),
            ParseInt(values, WindowWidthKey),
            ParseInt(values, WindowHeightKey));
    }

    /// <summary>Speichert alle Fenstergeometrie-Einstellungen in einer einzigen Transaktion.</summary>
    public async Task SetWindowGeometryAsync(WindowGeometrySettings geometry, CancellationToken ct = default)
    {
        var updates = new[]
        {
            (WindowPositionXKey, (geometry.X ?? 0).ToString()),
            (WindowPositionYKey, (geometry.Y ?? 0).ToString()),
            (WindowWidthKey,     (geometry.Width ?? 1280).ToString()),
            (WindowHeightKey,    (geometry.Height ?? 800).ToString()),
        };

        var keys = updates.Select(u => u.Item1).ToArray();

        var bestehende = await _db.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, ct);

        foreach (var (schluessel, wert) in updates)
        {
            if (bestehende.TryGetValue(schluessel, out var setting))
            {
                setting.Value = wert;
                setting.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.AppSettings.Add(new AppSetting
                {
                    Id = Guid.NewGuid(),
                    Key = schluessel,
                    Value = wert,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Fenstergeometrie gespeichert.");
    }

    /// <summary>
    /// Liest Update-Modus und Update-Pfad gemeinsam in einer einzigen Datenbankabfrage
    /// (Abfragetag <see cref="UpdateSettingsReadTag"/>). Fehlende oder ungültige gespeicherte Werte
    /// fallen auf die Defaults <see cref="UpdateMode.CheckOnly"/> bzw. den Pfad
    /// <see cref="UpdatePathChannelResolver.StableVersionsPath"/> zurück;
    /// Datenbank-Lesefehler werden nicht abgefangen und propagieren an den Aufrufer.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die gelesenen Update-Einstellungen.</returns>
    public async Task<UpdateSettings> GetUpdateSettingsAsync(CancellationToken ct = default)
    {
        var keys = new[] { UpdateModeKey, UpdatePfadKey };

        var values = await _db.AppSettings
            .AsNoTracking()
            .TagWith(UpdateSettingsReadTag)
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var mode = UpdateMode.CheckOnly;
        if (values.TryGetValue(UpdateModeKey, out var modeValue)
            && int.TryParse(modeValue, out var modeNumber)
            && Enum.IsDefined((UpdateMode)modeNumber))
        {
            mode = (UpdateMode)modeNumber;
        }

        values.TryGetValue(UpdatePfadKey, out var pathValue);
        var updatePath = UpdatePathChannelResolver.NormalizePath(pathValue);

        return new UpdateSettings(mode, updatePath);
    }

    /// <summary>
    /// Validiert den Update-Modus und den Update-Pfad und schreibt beide gemeinsam
    /// in einem einzigen Speichervorgang.
    /// </summary>
    /// <param name="settings">Die zu speichernden Update-Einstellungen.</param>
    /// <param name="ct">Abbruchtoken.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="settings"/> enthält einen nicht definierten <see cref="UpdateMode"/>-Wert oder einen unbekannten Update-Pfad.</exception>
    public async Task SetUpdateSettingsAsync(UpdateSettings settings, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(settings.Mode))
            throw new ArgumentOutOfRangeException(nameof(settings), settings.Mode, "Unbekannter Update-Modus.");

        if (!UpdatePathChannelResolver.IstGueltigerPfad(settings.UpdatePfad))
            throw new ArgumentOutOfRangeException(nameof(settings), settings.UpdatePfad, "Unbekannter Update-Pfad.");

        var updates = new[]
        {
            (UpdateModeKey, ((int)settings.Mode).ToString()),
            (UpdatePfadKey, UpdatePathChannelResolver.NormalizePath(settings.UpdatePfad)),
        };

        var keys = updates.Select(u => u.Item1).ToArray();

        var bestehende = await _db.AppSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, ct);

        foreach (var (schluessel, wert) in updates)
        {
            if (bestehende.TryGetValue(schluessel, out var setting))
            {
                setting.Value = wert;
                setting.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.AppSettings.Add(new AppSetting
                {
                    Id = Guid.NewGuid(),
                    Key = schluessel,
                    Value = wert,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogDebug("Update-Einstellungen gespeichert.");
    }

    /// <summary>
    /// Ermittelt, ob das Herunterfahren beim Auto-Shutdown erzwungen werden soll
    /// (<see cref="AutoShutdownErzwingenKey"/>). Standard: <see langword="false"/>.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns><see langword="true"/>, wenn der Shutdown erzwungen werden soll.</returns>
    public async Task<bool> GetAutoShutdownErzwingenAsync(CancellationToken ct = default)
        => await GetBoolSettingAsync(AutoShutdownErzwingenKey, ct) ?? false;

    /// <summary>Setzt den Schalter "Herunterfahren erzwingen" des Auto-Shutdowns (<see cref="AutoShutdownErzwingenKey"/>).</summary>
    /// <param name="force">Der zu speichernde Schalter.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SetAutoShutdownErzwingenAsync(bool force, CancellationToken ct = default)
        => SetBoolSettingAsync(AutoShutdownErzwingenKey, force, ct);

    /// <summary>
    /// Liest die Toleranzzeit in Minuten des Auto-Shutdown-Countdowns
    /// (<see cref="AutoShutdownToleranceMinutesKey"/>). Fehlende oder ungültige Werte fallen auf
    /// <see cref="AutoShutdownDefaultToleranceMinutes"/> zurück; gespeicherte Werte werden auf
    /// 0 bis <see cref="AutoShutdownMaxToleranceMinutes"/> begrenzt.
    /// </summary>
    /// <param name="ct">Abbruchtoken.</param>
    /// <returns>Die wirksame Toleranzzeit in Minuten.</returns>
    public async Task<int> GetAutoShutdownToleranceMinutesAsync(CancellationToken ct = default)
    {
        var wert = await GetIntSettingAsync(AutoShutdownToleranceMinutesKey, ct);
        return wert is null ? AutoShutdownDefaultToleranceMinutes : Math.Clamp(wert.Value, 0, AutoShutdownMaxToleranceMinutes);
    }

    /// <summary>Speichert die Toleranzzeit (Minuten) des Auto-Shutdown-Countdowns (<see cref="AutoShutdownToleranceMinutesKey"/>).</summary>
    /// <param name="toleranceMinutes">Die zu speichernde Toleranzzeit in Minuten.</param>
    /// <param name="ct">Abbruchtoken.</param>
    public Task SetAutoShutdownToleranceMinutesAsync(int toleranceMinutes, CancellationToken ct = default)
        => SetIntSettingAsync(AutoShutdownToleranceMinutesKey, toleranceMinutes, ct);
}

/// <summary>Fenstergeometrie-Einstellungen (Position und Größe des Hauptfensters).</summary>
public sealed record WindowGeometrySettings(int? X, int? Y, int? Width, int? Height);
