namespace Softwareschmiede.Domain.Enums;

/// <summary>Steuert den Umgang mit bestehendem Kontext bei Folgeanweisungen.</summary>
public enum FolgeanweisungsKontextmodus
{
    /// <summary>Bestehenden Kontext vor der neuen Anweisung mitgeben.</summary>
    KontextMitgeben = 0,

    /// <summary>Bestehenden Kontext nicht mitgeben, aber Verlauf fortschreiben.</summary>
    KontextIgnorieren = 1,

    /// <summary>Bestehenden Kontext verwerfen und neuen Verlauf beginnen.</summary>
    KontextNeuBeginnen = 2
}
