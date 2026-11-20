namespace Softwareschmiede.Domain.Enums;

/// <summary>Steuert den Umgang mit bestehendem Kontext bei Folgeanweisungen.</summary>
public enum FollowUpContextMode
{
    /// <summary>Bestehenden Kontext vor der neuen Anweisung mitgeben.</summary>
    IncludeContext = 0,

    /// <summary>Bestehenden Kontext nicht mitgeben, aber Verlauf fortschreiben.</summary>
    IgnoreContext = 1,

    /// <summary>Bestehenden Kontext verwerfen und neuen Verlauf beginnen.</summary>
    StartNewContext = 2
}
