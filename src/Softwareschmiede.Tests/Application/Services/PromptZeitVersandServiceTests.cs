using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Terminal;

namespace Softwareschmiede.Tests.Application.Services;

/// <summary>Unit-Tests für PromptZeitVersandService.</summary>
public sealed class PromptZeitVersandServiceTests : IDisposable
{
    private readonly KiAusfuehrungsService _kiService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly PromptZeitVersandService _sut;

    /// <summary>PromptZeitVersandServiceTests.</summary>
    public PromptZeitVersandServiceTests()
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _kiService = new KiAusfuehrungsService(NullLogger<KiAusfuehrungsService>.Instance, NullLoggerFactory.Instance, scopeFactoryMock.Object);
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _sut = new PromptZeitVersandService(_kiService, _timeProvider, NullLogger<PromptZeitVersandService>.Instance);
    }

    /// <summary>Dispose.</summary>
    public void Dispose() => _kiService.Dispose();

    /// <summary>
    /// Registriert für eine neue Aufgabe eine gültige <see cref="PseudoConsoleSession"/> beim
    /// <see cref="KiAusfuehrungsService"/>. Nutzt bewusst den klassischen <c>StartCliAsync</c>-Pfad mit einem
    /// lange laufenden Ping-Kommando (statt ConPTY mit einer per Tastatureingabe simulierten Shell): Die äußere
    /// ConPTY-<c>cmd.exe</c>-Shell dieser Sandbox kann sich (siehe CLAUDE.md, ConPTY-Isolationsproblem)
    /// unvorhersehbar innerhalb weniger Sekunden selbst beenden, was das Handle aus der internen
    /// Verwaltung entfernt und Tests, die eine über mehrere <c>FakeTimeProvider.Advance</c>-Aufrufe hinweg
    /// gültige Session voraussetzen, nichtdeterministisch scheitern lässt. Der Ping-Prozess bleibt dagegen
    /// zuverlässig für die Testdauer aktiv; die <see cref="PseudoConsoleSession"/> wird manuell an das Handle
    /// angehängt, damit <see cref="KiAusfuehrungsService.GetPseudoConsoleSession"/> sie zurückgibt.
    /// </summary>
    /// <returns>Die ID der Aufgabe, für die eine Session registriert wurde.</returns>
    private async Task<Guid> StartCliSessionAsync()
    {
        var aufgabeId = Guid.NewGuid();
        var pluginMock = new Mock<IKiPlugin>();
        pluginMock.Setup(p => p.StartCliAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 30 > nul",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

        var handle = await _kiService.StartCliAsync(aufgabeId, pluginMock.Object, Path.GetTempPath());

        var pseudoConsole = PseudoConsole.Create(1, 1);
        handle.PseudoConsoleSession = new PseudoConsoleSession(pseudoConsole, handle.Process, new MemoryStream(), new MemoryStream());

        return aufgabeId;
    }

    /// <summary>Liegt die Zielzeit in der Vergangenheit, wird der Prompt sofort versendet und es bleibt kein Eintrag in der Warteschlange.</summary>
    [Fact]
    public async Task SchedulePromptAsync_ZielzeitInVergangenheit_SendetSofort()
    {
        var aufgabeId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(-1));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "eine Zielzeit in der Vergangenheit muss sofort versendet werden");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull("ein sofort versendeter Prompt darf keinen Eintrag in der Warteschlange hinterlassen");
    }

    /// <summary>Liegt die Zielzeit in der Zukunft, wird der Prompt gepuffert und der Status ist über GetScheduledPromptStatus abrufbar.</summary>
    [Fact]
    public async Task SchedulePromptAsync_ZielzeitInZukunft_PuffertPrompt()
    {
        var aufgabeId = Guid.NewGuid();
        var targetTime = _timeProvider.GetUtcNow().AddMinutes(5);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", targetTime);

        var status = _sut.GetScheduledPromptStatus(aufgabeId);
        status.Should().NotBeNull();
        status!.PromptText.Should().Be("Testprompt");
        status.TargetTime.Should().Be(targetTime);
    }

    /// <summary>Erreicht der FakeTimeProvider die Zielzeit, wird der Prompt automatisch versendet und PromptSent gefeuert.</summary>
    [Fact]
    public async Task Timer_BeiErreichenDerZielzeit_SendetPromptAutomatisch()
    {
        var aufgabeId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        finished.Should().Be(versendet.Task, "der Timer muss beim Erreichen der Zielzeit automatisch feuern");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull("nach dem Versand darf kein Eintrag mehr in der Warteschlange sein");
    }

    /// <summary>CancelScheduledPrompt entfernt den geplanten Prompt; auch nach Ablauf der Zielzeit erfolgt kein Versand mehr.</summary>
    [Fact]
    public async Task CancelScheduledPrompt_EntferntGeplantenPrompt()
    {
        var aufgabeId = await StartCliSessionAsync();
        var versendet = new TaskCompletionSource<Guid>();
        _sut.PromptSent += id => versendet.TrySetResult(id);

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        _sut.CancelScheduledPrompt(aufgabeId);
        _timeProvider.Advance(TimeSpan.FromMinutes(10));

        var finished = await Task.WhenAny(versendet.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));
        finished.Should().NotBe(versendet.Task, "ein stornierter Prompt darf auch nach Ablauf der Zielzeit nicht versendet werden");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull();
    }

    /// <summary>Ein zweiter geplanter Prompt für dieselbe Aufgabe ersetzt den ersten und storniert dessen Timer.</summary>
    [Fact]
    public async Task SchedulePromptAsync_ZweiterPromptFuerSelbeAufgabe_ErsetztErsten()
    {
        var aufgabeId = Guid.NewGuid();
        var ersteZielzeit = _timeProvider.GetUtcNow().AddMinutes(5);
        var zweiteZielzeit = _timeProvider.GetUtcNow().AddMinutes(10);

        await _sut.SchedulePromptAsync(aufgabeId, "ErsterPrompt", ersteZielzeit);
        await _sut.SchedulePromptAsync(aufgabeId, "ZweiterPrompt", zweiteZielzeit);

        _sut.GetScheduledPromptStatus(aufgabeId)!.PromptText.Should().Be("ZweiterPrompt");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _sut.GetScheduledPromptStatus(aufgabeId).Should().NotBeNull(
            "der erste (ersetzte) Timer darf zu seiner ursprünglichen Zielzeit nicht mehr feuern");
        _sut.GetScheduledPromptStatus(aufgabeId)!.PromptText.Should().Be(
            "ZweiterPrompt",
            "der Eintrag muss weiterhin der zweite, ersetzende Prompt sein");

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull(
            "der zweite (ersetzende) Timer muss zu seiner Zielzeit feuern und den Eintrag entfernen");
    }

    /// <summary>Ist zur Fälligkeit keine aktive CLI-Session mehr vorhanden, wird der Prompt still verworfen: kein Event, keine Exception, Eintrag wird entfernt.</summary>
    [Fact]
    public async Task Timer_OhneSession_VerwirftPromptStill()
    {
        var aufgabeId = Guid.NewGuid();
        var versendet = false;
        _sut.PromptSent += _ => versendet = true;

        await _sut.SchedulePromptAsync(aufgabeId, "Testprompt", _timeProvider.GetUtcNow().AddMinutes(5));

        var act = () => _timeProvider.Advance(TimeSpan.FromMinutes(5));
        act.Should().NotThrow();

        await Task.Delay(TimeSpan.FromMilliseconds(500));

        versendet.Should().BeFalse("ohne aktive CLI-Session darf kein PromptSent-Event gefeuert werden");
        _sut.GetScheduledPromptStatus(aufgabeId).Should().BeNull("der Eintrag muss trotz stillem Verwerfen entfernt werden");
    }
}
