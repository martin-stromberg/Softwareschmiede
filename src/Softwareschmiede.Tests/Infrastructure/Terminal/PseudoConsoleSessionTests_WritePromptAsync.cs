using System.Text;
using FluentAssertions;
using Softwareschmiede.Infrastructure.Terminal;
using Softwareschmiede.Tests.Helpers;

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

    /// <summary>Mit gesetzter <see cref="PseudoConsoleSessionContext.PromptSubmitDelay"/> (Devin CLI)
    /// werden Prompttext und Submit-<c>\r</c> als zwei getrennte Schreib-Batches gesendet: Ein <c>\r</c>
    /// im selben Eingabe-Read wie der Text wertet die Devin CLI als Zeilenumbruch statt als Submit.</summary>
    [Fact]
    public async Task WritePromptAsync_MitSubmitDelay_SendetTextUndCrInGetrenntenBatches()
    {
        var inputStream = new WriteRecordingStream();
        using var session = TestPseudoConsoleSessionFactory.Create(
            inputStream, new MemoryStream(), promptSubmitDelay: TimeSpan.FromMilliseconds(50));

        await session.WritePromptAsync("Mach weiter", CancellationToken.None);

        inputStream.Batches.Should().HaveCount(2, "Text und Submit müssen als separate Eingabe-Batches gesendet werden");
        Encoding.UTF8.GetString(inputStream.Batches[0]).Should().Be("Mach weiter");
        inputStream.Batches[1].Should().Equal([(byte)'\r'], "der Submit bleibt ein alleinstehendes CR, nur zeitlich getrennt");
    }

    /// <summary>Auch im verzögerten Modus werden eingebettete Zeilenumbrüche weiterhin als <c>\r</c>
    /// im Text-Batch übertragen (in Devin = Zeilenumbruch im Entwurf); nur der Submit wandert in den zweiten Batch.</summary>
    [Fact]
    public async Task WritePromptAsync_MitSubmitDelay_EingebetteteZeilenumbruecheBleibenImTextBatch()
    {
        var inputStream = new WriteRecordingStream();
        using var session = TestPseudoConsoleSessionFactory.Create(
            inputStream, new MemoryStream(), promptSubmitDelay: TimeSpan.FromMilliseconds(50));

        await session.WritePromptAsync("Zeile1\r\nZeile2\nZeile3", CancellationToken.None);

        inputStream.Batches.Should().HaveCount(2);
        Encoding.UTF8.GetString(inputStream.Batches[0]).Should().Be("Zeile1\rZeile2\rZeile3");
        inputStream.Batches[1].Should().Equal([(byte)'\r']);
    }

    /// <summary>Die Eingabesperre wird über Text, Verzögerung und Submit gehalten: Ein parallel
    /// eintreffender Tastatur-Input darf nicht zwischen Prompttext und Submit rutschen.</summary>
    [Fact]
    public async Task WritePromptAsync_MitSubmitDelay_FremdinputWartetHinterDemSubmit()
    {
        var inputStream = new WriteRecordingStream();
        using var session = TestPseudoConsoleSessionFactory.Create(
            inputStream, new MemoryStream(), promptSubmitDelay: TimeSpan.FromMilliseconds(150));

        var promptTask = session.WritePromptAsync("Mach weiter", CancellationToken.None);
        var keyboardTask = Task.Run(async () =>
        {
            await Task.Delay(30);
            await session.WriteInputAsync(new byte[] { (byte)'x' }, CancellationToken.None);
        });

        await Task.WhenAll(promptTask, keyboardTask);

        inputStream.Batches.Should().HaveCount(3);
        inputStream.Batches[1].Should().Equal([(byte)'\r'], "der Submit-Batch muss vor dem Fremdinput gesendet werden");
        inputStream.Batches[2].Should().Equal([(byte)'x']);
    }

    /// <summary>Mit gesetzter Submit-Wiederholung (<see cref="PseudoConsoleSessionContext.PromptSubmitRepeatCount"/>)
    /// wird der Submit-<c>\r</c> mehrfach als jeweils eigener geflushter Batch gesendet: Ein zu früher
    /// Versuch kann bei großen Prompts noch im Paste-/Burst-Fenster der CLI verschluckt werden, spätere
    /// Versuche senden den Entwurf zuverlässig ab (empirisch an der Devin CLI vermessen).</summary>
    [Fact]
    public async Task WritePromptAsync_MitSubmitWiederholung_SendetSubmitMehrfachAlsEigeneBatches()
    {
        var inputStream = new WriteRecordingStream();
        using var session = TestPseudoConsoleSessionFactory.Create(
            inputStream, new MemoryStream(),
            promptSubmitDelay: TimeSpan.FromMilliseconds(40),
            promptSubmitRepeatCount: 2,
            promptSubmitRepeatInterval: TimeSpan.FromMilliseconds(20));

        await session.WritePromptAsync("Mach weiter", CancellationToken.None);

        inputStream.Batches.Should().HaveCount(4, "Text plus erster Submit plus zwei Wiederholungen ergeben vier getrennte Batches");
        Encoding.UTF8.GetString(inputStream.Batches[0]).Should().Be("Mach weiter");
        inputStream.Batches[1].Should().Equal([(byte)'\r']);
        inputStream.Batches[2].Should().Equal([(byte)'\r']);
        inputStream.Batches[3].Should().Equal([(byte)'\r']);
    }

    /// <summary>Bei einem Prompt oberhalb der Chunk-Größe bleibt der wiederholte Submit jeweils ein
    /// eigener Ein-Byte-Batch hinter allen Text-Chunks — genau der Fall, der produktiv an mehreren
    /// KB langen SCM-Prompts scheiterte, wenn nur ein einziger Submit-Versuch gesendet wurde.</summary>
    [Fact]
    public async Task WritePromptAsync_MitSubmitWiederholung_GrosserPrompt_SendetSubmitsHinterAllenTextChunks()
    {
        var prompt = new string('x', 10_000);
        var expectedTextChunks = (int)Math.Ceiling(Encoding.UTF8.GetByteCount(prompt) / 4096.0);
        const int repeatCount = 3;

        var inputStream = new WriteRecordingStream();
        using var session = TestPseudoConsoleSessionFactory.Create(
            inputStream, new MemoryStream(),
            promptSubmitDelay: TimeSpan.FromMilliseconds(40),
            promptSubmitRepeatCount: repeatCount,
            promptSubmitRepeatInterval: TimeSpan.FromMilliseconds(20));

        await session.WritePromptAsync(prompt, CancellationToken.None);

        inputStream.Batches.Should().HaveCount(expectedTextChunks + 1 + repeatCount);
        string.Concat(inputStream.Batches.Take(expectedTextChunks).Select(Encoding.UTF8.GetString))
            .Should().Be(prompt, "die Text-Chunks liefern gemeinsam den vollständigen Prompttext");
        inputStream.Batches.Skip(expectedTextChunks).Should().AllSatisfy(
            batch => batch.Should().Equal([(byte)'\r'], "jeder Submit-Versuch bleibt ein eigener Ein-Byte-Batch"));
    }

    /// <summary>Ohne Wiederholungskonfiguration bleibt es beim einmaligen Submit (Bestandsverhalten).</summary>
    [Fact]
    public async Task WritePromptAsync_MitSubmitDelay_OhneWiederholung_SendetGenauEinenSubmit()
    {
        var inputStream = new WriteRecordingStream();
        using var session = TestPseudoConsoleSessionFactory.Create(
            inputStream, new MemoryStream(), promptSubmitDelay: TimeSpan.FromMilliseconds(40));

        await session.WritePromptAsync("Mach weiter", CancellationToken.None);

        inputStream.Batches.Should().HaveCount(2);
        inputStream.Batches[1].Should().Equal([(byte)'\r']);
    }

    /// <summary>Die Eingabesperre wird über Text, Verzögerung und <em>alle</em> Submit-Versuche gehalten:
    /// Fremdinput darf auch nicht zwischen zwei Wiederholungs-Submits rutschen.</summary>
    [Fact]
    public async Task WritePromptAsync_MitSubmitWiederholung_FremdinputWartetHinterAllenSubmits()
    {
        var inputStream = new WriteRecordingStream();
        using var session = TestPseudoConsoleSessionFactory.Create(
            inputStream, new MemoryStream(),
            promptSubmitDelay: TimeSpan.FromMilliseconds(40),
            promptSubmitRepeatCount: 2,
            promptSubmitRepeatInterval: TimeSpan.FromMilliseconds(40));

        var promptTask = session.WritePromptAsync("Mach weiter", CancellationToken.None);
        var keyboardTask = Task.Run(async () =>
        {
            await Task.Delay(70);
            await session.WriteInputAsync(new byte[] { (byte)'x' }, CancellationToken.None);
        });

        await Task.WhenAll(promptTask, keyboardTask);

        inputStream.Batches.Should().HaveCount(5);
        inputStream.Batches[^1].Should().Equal([(byte)'x'], "der Fremdinput darf erst hinter dem letzten Submit-Versuch ankommen");
        inputStream.Batches.Skip(1).Take(3).Should().AllSatisfy(batch => batch.Should().Equal([(byte)'\r']));
    }

    private static PseudoConsoleSession CreateSession(Stream inputStream)
    {
        return TestPseudoConsoleSessionFactory.Create(inputStream, new MemoryStream());
    }

    /// <summary>Stream, der jeden Schreibaufruf als eigenen Batch aufzeichnet (zum Nachweis
    /// getrennter Eingabe-Batches im verzögerten Submit-Modus).</summary>
    private sealed class WriteRecordingStream : MemoryStream
    {
        /// <summary>Aufgezeichnete Byte-Batches in Schreibreihenfolge.</summary>
        public List<byte[]> Batches { get; } = [];

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
            => Batches.Add(buffer.AsSpan(offset, count).ToArray());

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Batches.Add(buffer.AsSpan(offset, count).ToArray());
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Batches.Add(buffer.ToArray());
            return ValueTask.CompletedTask;
        }
    }
}
