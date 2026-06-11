namespace Softwareschmiede.Domain.Interfaces;

/// <summary>
/// Plattformspezifische Abstraktion für das Abspielen von Audiodateien.
/// Die WPF-Implementierung verwendet <c>System.Windows.Media.MediaPlayer</c>.
/// </summary>
public interface IBenachrichtigungsAudioService
{
    /// <summary>
    /// Spielt eine Audiodatei ab (MP3, WAV oder OGG).
    /// Die Implementierung muss thread-safe gegenüber dem UI-Dispatcher sein.
    /// </summary>
    Task PlayAudioAsync(string filePath, CancellationToken ct = default);
}
