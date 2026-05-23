namespace Softwareschmiede.Domain.Interfaces;

/// <summary>Liefert die aktuelle Benutzeridentität für benutzerbezogene Einstellungen.</summary>
public interface IBenutzerkontextService
{
    string GetBenutzerId();
}
