namespace Softwareschmiede.Domain.Enums;

/// <summary>Terminal-Fähigkeiten eines KI-CLI-Plugins für den interaktiven Einbettungspfad.</summary>
[Flags]
public enum TerminalProviderCapabilities
{
    /// <summary>Keine besonderen Terminal-Fähigkeiten deklariert.</summary>
    None = 0,

    /// <summary>Die CLI kann in einer Pseudo Console (ConPTY/PTY) betrieben werden.</summary>
    SupportsPty = 1,

    /// <summary>Die CLI benötigt eine echte Pseudo Console und verweigert/versagt ohne TTY.</summary>
    RequiresPty = 2
}
