namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Liefert die aktuelle Benutzeridentität für benutzerbezogene Einstellungen.</summary>
public interface IBenutzerkontextService
{
    /// <summary>Gibt die Benutzerkennung des aktuell angemeldeten Benutzers zurück.</summary>
    string GetBenutzerId();
}
