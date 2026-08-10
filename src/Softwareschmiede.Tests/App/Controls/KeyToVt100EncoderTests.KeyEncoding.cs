using System.Text;
using System.Windows.Input;
using System.Windows.Interop;
using FluentAssertions;
using Softwareschmiede.App.Controls;
using static Softwareschmiede.Tests.Helpers.WpfUnitTestHelpers;

namespace Softwareschmiede.Tests.App.Controls;

/// <summary>Unit-Tests für <see cref="KeyToVt100Encoder.Encode"/>: Strg+Pfeiltasten-Navigation
/// (wortweise Cursor-Bewegung) sowie Alt-/Alt Gr-Erkennung.</summary>
public sealed partial class KeyToVt100EncoderTests
{
    /// <summary>Strg+Links muss die VT100-Sequenz für wortweise Navigation nach links liefern.</summary>
    [Fact]
    public void Encode_CtrlLeftKey_ReturnsVt100ControlLeftSequence()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Left, Key.LeftCtrl);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().Equal(Encoding.ASCII.GetBytes("\x1b[1;5D"));
        });
    }

    /// <summary>Strg+Rechts muss die VT100-Sequenz für wortweise Navigation nach rechts liefern.</summary>
    [Fact]
    public void Encode_CtrlRightKey_ReturnsVt100ControlRightSequence()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Right, Key.LeftCtrl);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().Equal(Encoding.ASCII.GetBytes("\x1b[1;5C"));
        });
    }

    /// <summary>Strg+Hoch ist keine der neu eingeführten Sequenzen; das Verhalten bleibt unverändert
    /// bei der regulären Pfeiltasten-Sequenz (kein Sonderverhalten für Strg+Hoch/Runter).</summary>
    [Fact]
    public void Encode_CtrlUpKey_ReturnsUnchangedUpSequence()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Up, Key.LeftCtrl);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().Equal(Encoding.ASCII.GetBytes("\x1b[A"));
        });
    }

    /// <summary>Strg+Runter ist keine der neu eingeführten Sequenzen; das Verhalten bleibt unverändert
    /// bei der regulären Pfeiltasten-Sequenz.</summary>
    [Fact]
    public void Encode_CtrlDownKey_ReturnsUnchangedDownSequence()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Down, Key.LeftCtrl);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().Equal(Encoding.ASCII.GetBytes("\x1b[B"));
        });
    }

    /// <summary>Shift+Strg+Links liefert weiterhin die Strg+Links-Sequenz: Shift beeinflusst die
    /// Strg-Pfeiltasten-Erkennung nicht (Markierungs-Verhalten wird terminal-seitig gehandhabt,
    /// siehe Offener Punkt #2 im Umsetzungsplan).</summary>
    [Fact]
    public void Encode_CtrlShiftLeftKey_ReturnsVt100ControlLeftSequence()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Left, Key.LeftCtrl, Key.LeftShift);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().Equal(Encoding.ASCII.GetBytes("\x1b[1;5D"));
        });
    }

    /// <summary>Alt (Alt Gr) mit einer beliebigen Taste ohne vordefinierte Kombination muss
    /// <see langword="null"/> liefern, damit die Zeichenkomposition über <c>OnTextInput</c> erfolgt.</summary>
    [Fact]
    public void Encode_AltModifierWithoutSpecificKey_ReturnsNull()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.A, Key.LeftAlt);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().BeNull("Alt-Zeichen sollen über OnTextInput komponiert werden");
        });
    }

    /// <summary>Alt+Shift muss ebenfalls <see langword="null"/> liefern.</summary>
    [Fact]
    public void Encode_AltShiftCombination_ReturnsNull()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.A, Key.LeftAlt, Key.LeftShift);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().BeNull("Alt+Shift-Kombinationen sollen ebenfalls über OnTextInput komponiert werden");
        });
    }

    /// <summary>Windows meldet Alt Gr auf deutschen (und anderen) Tastaturlayouts als gleichzeitiges
    /// Control+Alt. Für eine Buchstaben-Taste im Bereich A-Z (z. B. Alt Gr+Q → "@" auf deutschem
    /// QWERTZ-Layout) darf dies nicht als Strg+Buchstabe-Steuerzeichen kodiert werden, sondern muss
    /// <see langword="null"/> liefern, damit die Zeichenkomposition über <c>OnTextInput</c> erfolgt.</summary>
    [Fact]
    public void Encode_AltGrAsCtrlAltWithLetterKey_ReturnsNull()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Q, Key.LeftCtrl, Key.RightAlt);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().BeNull("Alt Gr wird als gleichzeitiges Control+Alt gemeldet und darf nicht als Strg-Steuerzeichen kodiert werden");
        });
    }

    /// <summary>Alt Gr über <c>Key.LeftCtrl</c>+<c>Key.LeftAlt</c> (alternative Meldung durch Windows) muss
    /// ebenso <see langword="null"/> liefern wie die Kombination mit <c>Key.RightAlt</c>.</summary>
    [Fact]
    public void Encode_AltGrAsCtrlLeftAltWithLetterKey_ReturnsNull()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Q, Key.LeftCtrl, Key.LeftAlt);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().BeNull("Alt Gr wird als gleichzeitiges Control+Alt gemeldet und darf nicht als Strg-Steuerzeichen kodiert werden");
        });
    }

    /// <summary>Normales Strg+Q ohne gleichzeitiges Alt muss weiterhin als Strg-Steuerzeichen (0x11) kodiert
    /// werden — der Fix für Alt Gr darf dieses bestehende Verhalten nicht beeinträchtigen.</summary>
    [Fact]
    public void Encode_CtrlQWithoutAlt_ReturnsControlCharacter()
    {
        RunOnSta(() =>
        {
            var args = CreateKeyEventArgs(Key.Q, Key.LeftCtrl);

            var result = KeyToVt100Encoder.Encode(args);

            result.Should().Equal([(byte)0x11], "Strg+Q ohne Alt muss weiterhin als Steuerzeichen kodiert werden");
        });
    }

    private static KeyEventArgs CreateKeyEventArgs(Key key, params Key[] modifierKeys)
    {
        // KeyEventArgs erfordert eine nicht-null PresentationSource; ein reales (unsichtbares) HwndSource-Fenster
        // dient hier nur zur Erfüllung dieser Konstruktor-Anforderung, wird von Encode() nicht angesprochen.
        using var hwndSource = new HwndSource(new HwndSourceParameters("KeyToVt100EncoderTests_KeyEncoding"));
        var keyboard = new TestKeyboardDevice(modifierKeys);
        return new KeyEventArgs(keyboard, hwndSource, 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
    }
}
