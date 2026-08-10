using System.Windows.Input;
using System.Windows.Threading;

namespace Softwareschmiede.Tests.Helpers;

/// <summary>Wiederverwendbare Hilfen für WPF-Unit-Tests: STA-Thread-Ausführung und ein simuliertes
/// Keyboard-Device für synthetische <see cref="KeyEventArgs"/>.</summary>
public static class WpfUnitTestHelpers
{
    /// <summary>Führt die übergebene Aktion auf einem STA-Thread mit Dispatcher-SynchronizationContext aus
    /// (WPF-Objekte erfordern ein STA-Apartment). Eine in der Aktion geworfene Exception wird auf dem
    /// aufrufenden Thread erneut geworfen.</summary>
    /// <param name="action">Die auf dem STA-Thread auszuführende Aktion.</param>
    public static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                action();
            }
            catch (Exception ex) { exception = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
            throw exception;
    }

    /// <summary>Simuliertes <see cref="KeyboardDevice"/>, dessen Tastenzustände über die im Konstruktor
    /// übergebenen gedrückten Tasten gesteuert werden (für synthetische <see cref="KeyEventArgs"/> in Tests).</summary>
    public sealed class TestKeyboardDevice : KeyboardDevice
    {
        private readonly HashSet<Key> _downKeys;

        /// <summary>Erstellt ein simuliertes Keyboard-Device, bei dem die übergebenen Tasten als gedrückt gelten.</summary>
        /// <param name="downKeys">Die als gedrückt zu simulierenden Tasten.</param>
        public TestKeyboardDevice(params Key[] downKeys)
            : base(InputManager.Current)
        {
            _downKeys = [.. downKeys];
        }

        /// <summary>Liefert den simulierten Tastenzustand: <see cref="KeyStates.Down"/> für im Konstruktor
        /// übergebene Tasten, sonst <see cref="KeyStates.None"/>.</summary>
        /// <param name="key">Die abzufragende Taste.</param>
        /// <returns>Der simulierte Tastenzustand.</returns>
        protected override KeyStates GetKeyStatesFromSystem(Key key)
            => _downKeys.Contains(key) ? KeyStates.Down : KeyStates.None;
    }
}
