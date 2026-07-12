using System.Text;
using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Infrastructure.Terminal;

/// <summary>Unit-Tests für <see cref="PseudoConsoleSession.WritePromptAsync"/>: Der übertragene Zeilenumbruch muss
/// mit dem Enter-Kodierungskonvention des Terminals übereinstimmen (siehe <c>KeyToVt100Encoder</c>), damit die CLI
/// den Prompt wie einen normalen Tastatur-Enter verarbeitet.</summary>
public sealed class PseudoConsoleSessionTests_WritePromptAsync
{
    /// <summary>Ein echter physischer Enter-Tastendruck wird von <c>KeyToVt100Encoder.Encode</c> als alleinstehendes
    /// <c>0x0D</c> (CR) kodiert, nicht als CRLF. Damit ein zeitgesteuert bzw. sofort versendeter Prompt von der CLI
    /// exakt wie ein normaler Tastatur-Enter verarbeitet wird, muss <see cref="PseudoConsoleSession.WritePromptAsync"/>
    /// denselben Submit-Byte senden: ein einzelnes <c>\r</c> statt <see cref="Environment.NewLine"/> (<c>\r\n</c>
    /// unter Windows). Ein zusätzliches <c>\n</c> nach dem CR wird von manchen CLI-TUIs nicht als Submit erkannt.</summary>
    [Fact]
    public async Task WritePromptAsync_SchreibtEinzelnesCarriageReturnAlsSubmit_KeinCrLf()
    {
        var inputStream = new MemoryStream();
        using var session = CreateSession(inputStream);

        await session.WritePromptAsync("Mach weiter", CancellationToken.None);

        var writtenBytes = inputStream.ToArray();
        var written = Encoding.UTF8.GetString(writtenBytes);

        written.Should().Be("Mach weiter\r", "der Submit muss als alleinstehendes CR erfolgen, analog zu KeyToVt100Encoder.Encode(Key.Enter) == 0x0D");
        written.Should().NotContain("\r\n", "ein zusätzliches LF nach dem CR wird von der CLI nicht wie ein normaler Tastatur-Enter verarbeitet");
    }

    /// <summary>Enthält der Prompttext selbst Zeilenumbrüche (mehrzeilige Promptvorlage), müssen auch diese auf CR
    /// normalisiert werden — analog zu <c>KeyToVt100Encoder.EncodeClipboardText</c>, das denselben Ansatz für
    /// eingefügten Zwischenablage-Text verwendet.</summary>
    [Fact]
    public async Task WritePromptAsync_NormalisiertEingebetteteZeilenumbruecheAufCarriageReturn()
    {
        var inputStream = new MemoryStream();
        using var session = CreateSession(inputStream);

        await session.WritePromptAsync("Zeile1\r\nZeile2\nZeile3\rZeile4", CancellationToken.None);

        var written = Encoding.UTF8.GetString(inputStream.ToArray());

        written.Should().Be("Zeile1\rZeile2\rZeile3\rZeile4\r");
    }

    private static PseudoConsoleSession CreateSession(Stream inputStream)
    {
        var pseudoConsole = PseudoConsole.Create(1, 1);
        return new PseudoConsoleSession(
            pseudoConsole,
            System.Diagnostics.Process.GetCurrentProcess(),
            inputStream,
            new MemoryStream());
    }
}
