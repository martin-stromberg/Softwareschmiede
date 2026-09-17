using Softwareschmiede.App.Services;
using Softwareschmiede.Application.Services.Updates;

namespace Softwareschmiede.App.ViewModels;

/// <summary>
/// Bündelt die ausschließlich für den Update-Ablauf des Hauptfensters benötigten Dienste,
/// damit der <see cref="MainWindowViewModel"/>-Konstruktor nicht für jeden Update-Kollaborateur
/// einen eigenen Parameter benötigt.
/// </summary>
/// <param name="UpdateService">Orchestriert Prüfung, Vorbereitung und Start des Updates.</param>
/// <param name="CliUpdateSafetyService">Bewertet aktive CLI-Aufgaben vor einem Update.</param>
/// <param name="UpdateProgressDialogService">Zeigt den Fortschrittsdialog der Update-Vorbereitung.</param>
/// <param name="VersuchProtokoll">Optionale Instrumentierung der Updateversuche (E2E-Umgebung).</param>
public sealed record MainWindowUpdateDienste(
    IUpdateService? UpdateService,
    ICliUpdateSafetyService? CliUpdateSafetyService,
    IUpdateProgressDialogService? UpdateProgressDialogService,
    IUpdateVersuchProtokoll? VersuchProtokoll);
