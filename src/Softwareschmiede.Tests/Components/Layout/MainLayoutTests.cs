using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Softwareschmiede.Application.Services;
using Softwareschmiede.Domain.Entities;
using Softwareschmiede.Domain.Enums;
using Softwareschmiede.Components.Layout;
using Softwareschmiede.Domain.Interfaces;
using Softwareschmiede.Infrastructure.Data;

namespace Softwareschmiede.Tests.Components.Layout;

public sealed class MainLayoutTests
{
    [Fact]
    public void MainLayoutMarkup_ShouldContainRunningCountAndAutoShutdownToggle()
    {
        var root = FindRepositoryRoot();
        var razorPath = Path.Combine(root, "src", "Softwareschmiede", "Components", "Layout", "MainLayout.razor");
        var markup = File.ReadAllText(razorPath);

        markup.Should().Contain("Laufende Automatisierungen");
        markup.Should().Contain("@_runningAutomationCount");
        markup.Should().Contain("@if (_runningAutomationCount > 0)");
        markup.Should().Contain("@onchange=\"AutoShutdownChanged\"");
    }

    [Fact]
    public void OnInitialized_ShouldLoadRunningCount_SubscribeAndDisableAutoShutdown()
    {
        var runningSource = new FakeRunningAutomationStatusSource { RunningCount = 3 };
        var orchestrator = new FakeAutoShutdownOrchestrator();
        using var harness = CreateHarness(runningSource, orchestrator);
        var sut = harness.Sut;

        sut.InvokeOnInitialized();

        GetPrivateField<int>(sut, "_runningAutomationCount").Should().Be(3);
        runningSource.SubscriberCount.Should().Be(1);
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeFalse();
    }

    [Fact]
    public void AutoShutdownChanged_ShouldParseBoolValue()
    {
        using var harness = CreateHarness(new FakeRunningAutomationStatusSource(), new FakeAutoShutdownOrchestrator());
        var sut = harness.Sut;

        InvokePrivate(sut, "AutoShutdownChanged", new ChangeEventArgs { Value = true });

        GetPrivateField<bool>(sut, "_autoShutdownEnabled").Should().BeTrue();
        var orchestrator = GetInjected<FakeAutoShutdownOrchestrator>(sut, "AutoShutdownOrchestrator");
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeTrue();
    }

    [Fact]
    public void AutoShutdownChanged_ShouldParseStringBoolValue()
    {
        using var harness = CreateHarness(new FakeRunningAutomationStatusSource(), new FakeAutoShutdownOrchestrator());
        var sut = harness.Sut;

        InvokePrivate(sut, "AutoShutdownChanged", new ChangeEventArgs { Value = "true" });

        GetPrivateField<bool>(sut, "_autoShutdownEnabled").Should().BeTrue();
        var orchestrator = GetInjected<FakeAutoShutdownOrchestrator>(sut, "AutoShutdownOrchestrator");
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeTrue();
    }

    [Fact]
    public void AutoShutdownChanged_ShouldFallbackToFalse_OnInvalidValue()
    {
        using var harness = CreateHarness(new FakeRunningAutomationStatusSource(), new FakeAutoShutdownOrchestrator());
        var sut = harness.Sut;

        InvokePrivate(sut, "AutoShutdownChanged", new ChangeEventArgs { Value = "ungueltig" });

        GetPrivateField<bool>(sut, "_autoShutdownEnabled").Should().BeFalse();
        var orchestrator = GetInjected<FakeAutoShutdownOrchestrator>(sut, "AutoShutdownOrchestrator");
        orchestrator.SetEnabledCalls.Should().ContainSingle().Which.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldUnsubscribeFromRunningCountChanged()
    {
        using var harness = CreateHarness();
        var runningSource = harness.RunningSource;
        var sut = harness.Sut;
        sut.InvokeOnInitialized();

        sut.Dispose();

        runningSource.SubscriberCount.Should().Be(0);
    }

    [Theory]
    [InlineData(BenachrichtigungsModus.Deaktiviert, false, BenachrichtigungsEntscheidung.Unterdrueckt, "KanalDeaktiviert", 0)]
    [InlineData(BenachrichtigungsModus.Banner, false, BenachrichtigungsEntscheidung.Gesendet, "ToastAngezeigt", 1)]
    [InlineData(BenachrichtigungsModus.Banner, true, BenachrichtigungsEntscheidung.Gesendet, "ToastAngezeigt", 1)]
    [InlineData(BenachrichtigungsModus.Ton, false, BenachrichtigungsEntscheidung.Gesendet, "ToastAngezeigt", 1)]
    public async Task VerarbeiteToastAsync_ShouldHonorModeMatrix(
        BenachrichtigungsModus modus,
        bool istAufgabenseite,
        BenachrichtigungsEntscheidung erwarteteEntscheidung,
        string erwarteterGrund,
        int erwarteteToasts)
    {
        using var harness = CreateHarness();
        var ereignis = CreateEreignis();

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteToastAsync",
            ereignis,
            harness.BenutzerId,
            modus,
            istAufgabenseite);

        var toasts = GetToasts(harness.Sut);
        toasts.Should().HaveCount(erwarteteToasts);

        var audit = harness.Db.BenachrichtigungsDispatchLogs
            .Where(log => log.Kanal == BenachrichtigungsKanal.Banner)
            .Single();
        audit.Entscheidung.Should().Be(erwarteteEntscheidung);
        audit.Grund.Should().Be(erwarteterGrund);
        audit.Modus.Should().Be(modus);
    }

    [Theory]
    [InlineData(BenachrichtigungsModus.Deaktiviert, false, BenachrichtigungsEntscheidung.Unterdrueckt, "KanalDeaktiviert", 0)]
    [InlineData(BenachrichtigungsModus.Banner, false, BenachrichtigungsEntscheidung.Gesendet, "StandardtonFallback", 1)]
    [InlineData(BenachrichtigungsModus.Banner, true, BenachrichtigungsEntscheidung.Gesendet, "StandardtonFallback", 1)]
    [InlineData(BenachrichtigungsModus.Ton, false, BenachrichtigungsEntscheidung.Gesendet, "StandardtonFallback", 1)]
    public async Task VerarbeiteTonAsync_ShouldHonorModeMatrix(
        BenachrichtigungsModus modus,
        bool istAufgabenseite,
        BenachrichtigungsEntscheidung erwarteteEntscheidung,
        string erwarteterGrund,
        int erwarteteAudioAufrufe)
    {
        using var harness = CreateHarness();
        harness.JsRuntime.NextResult = "played";
        var ereignis = CreateEreignis();

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            ereignis,
            harness.BenutzerId,
            modus,
            istAufgabenseite);

        harness.JsRuntime.Invocations.Should().HaveCount(erwarteteAudioAufrufe);

        var audit = harness.Db.BenachrichtigungsDispatchLogs
            .Where(log => log.Kanal == BenachrichtigungsKanal.Ton)
            .Single();
        audit.Entscheidung.Should().Be(erwarteteEntscheidung);
        audit.Grund.Should().Be(erwarteterGrund);
        audit.Modus.Should().Be(modus);
    }

    [Fact]
    public async Task VerarbeiteTonAsync_ShouldUseUserAudio_WhenAvailable()
    {
        using var harness = CreateHarness();
        var mp3Bytes = new byte[] { 0x49, 0x44, 0x33, 0x04, 0x00, 0x00 };
        await harness.EinstellungenService.UploadAudioAsync(harness.BenutzerId, "signal.mp3", "audio/mpeg", mp3Bytes);
        var ereignis = CreateEreignis();

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            ereignis,
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);

        harness.JsRuntime.Invocations.Should().ContainSingle();
        var invocation = harness.JsRuntime.Invocations[0];
        invocation.Identifier.Should().Be("softwareschmiedeNotifications.playAlert");
        invocation.Args.Should().ContainInOrder(Convert.ToBase64String(mp3Bytes), "audio/mpeg");

        var audit = harness.Db.BenachrichtigungsDispatchLogs
            .Where(log => log.Kanal == BenachrichtigungsKanal.Ton)
            .Single();
        audit.Entscheidung.Should().Be(BenachrichtigungsEntscheidung.Gesendet);
        audit.Grund.Should().Be("BenutzerdefinierterTon");
    }

    [Fact]
    public async Task VerarbeiteTonAsync_ShouldUseDefaultAudioFallback_WhenNoUserAudioExists()
    {
        using var harness = CreateHarness();
        var ereignis = CreateEreignis();

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            ereignis,
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);

        harness.JsRuntime.Invocations.Should().ContainSingle();
        harness.JsRuntime.Invocations[0].Args.Should().ContainInOrder((object?)null, null);

        var audit = harness.Db.BenachrichtigungsDispatchLogs
            .Where(log => log.Kanal == BenachrichtigungsKanal.Ton)
            .Single();
        audit.Entscheidung.Should().Be(BenachrichtigungsEntscheidung.Gesendet);
        audit.Grund.Should().Be("StandardtonFallback");
    }

    [Fact]
    public async Task VerarbeiteTonAsync_ShouldCreateWarningToastAndAudit_WhenDeferred()
    {
        using var harness = CreateHarness();
        harness.JsRuntime.NextResult = "deferred";

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            CreateEreignis(),
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);

        var toast = GetToasts(harness.Sut).Single();
        toast.CssClass.Should().Be("toast-warning");
        toast.Nachricht.Should().Contain("verzögert");

        var audit = harness.Db.BenachrichtigungsDispatchLogs
            .Where(log => log.Kanal == BenachrichtigungsKanal.Ton)
            .Single();
        audit.Entscheidung.Should().Be(BenachrichtigungsEntscheidung.Zurueckgestellt);
        audit.Grund.Should().Be("AutoplayBlockiert");
    }

    [Fact]
    public async Task VerarbeiteTonAsync_ShouldCreateErrorToastAndAudit_WhenFailed()
    {
        using var harness = CreateHarness();
        harness.JsRuntime.NextResult = "failed";

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            CreateEreignis(),
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);

        var toast = GetToasts(harness.Sut).Single();
        toast.CssClass.Should().Be("toast-error");
        toast.Nachricht.Should().Contain("nicht abgespielt");

        var audit = harness.Db.BenachrichtigungsDispatchLogs
            .Where(log => log.Kanal == BenachrichtigungsKanal.Ton)
            .Single();
        audit.Entscheidung.Should().Be(BenachrichtigungsEntscheidung.Fehlgeschlagen);
        audit.Grund.Should().Be("AudioPlaybackFehler");
    }

    [Fact]
    public async Task VerarbeiteTonAsync_ShouldCreateErrorToastAndAudit_WhenJsInteropThrows()
    {
        using var harness = CreateHarness();
        harness.JsRuntime.Exception = new InvalidOperationException("js boom");

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            CreateEreignis(),
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);

        var toast = GetToasts(harness.Sut).Single();
        toast.CssClass.Should().Be("toast-error");

        var audit = harness.Db.BenachrichtigungsDispatchLogs
            .Where(log => log.Kanal == BenachrichtigungsKanal.Ton)
            .Single();
        audit.Entscheidung.Should().Be(BenachrichtigungsEntscheidung.Fehlgeschlagen);
        audit.Grund.Should().Be("JsInteropFehler");
    }

    [Fact]
    public async Task VerarbeiteBenachrichtigungen_ShouldDeduplicateByEreignisId_PerChannel()
    {
        using var harness = CreateHarness();
        var ereignis = CreateEreignis();

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteToastAsync",
            ereignis,
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);
        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            ereignis,
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);

        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteToastAsync",
            ereignis,
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);
        await InvokePrivateAsync(
            harness.Sut,
            "VerarbeiteTonAsync",
            ereignis,
            harness.BenutzerId,
            BenachrichtigungsModus.Ton,
            false);

        harness.Db.BenachrichtigungsDispatchLogs.Should().HaveCount(2);
        harness.Db.BenachrichtigungsDispatchLogs.Should().ContainSingle(log => log.Kanal == BenachrichtigungsKanal.Banner);
        harness.Db.BenachrichtigungsDispatchLogs.Should().ContainSingle(log => log.Kanal == BenachrichtigungsKanal.Ton);
        GetToasts(harness.Sut).Should().HaveCount(1);
        harness.JsRuntime.Invocations.Should().HaveCount(1);
    }

    private static void SetInjectedProperty(object target, string propertyName, object value)
    {
        var property = typeof(MainLayout).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist for test setup.");
        property!.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = typeof(MainLayout).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"Field {fieldName} should exist.");
        return (T)field!.GetValue(target)!;
    }

    private static T GetInjected<T>(object target, string propertyName)
        where T : class
    {
        var property = typeof(MainLayout).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull($"Property {propertyName} should exist.");
        return (property!.GetValue(target) as T)!;
    }

    private static object? InvokePrivate(object target, string methodName, params object?[] args)
    {
        var method = typeof(MainLayout)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(m =>
                m.Name.Equals(methodName, StringComparison.Ordinal)
                && m.GetParameters().Length == args.Length);
        method.Should().NotBeNull($"Method {methodName} should exist.");
        return method!.Invoke(target, args);
    }

    private static async Task InvokePrivateAsync(object target, string methodName, params object?[] args)
    {
        var result = InvokePrivate(target, methodName, args);
        if (result is Task task)
        {
            await task;
        }
    }

    private static KiAufgabenAbschlussEreignis CreateEreignis()
    {
        return new KiAufgabenAbschlussEreignis(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Aufgabe A",
            AufgabeStatus.InArbeit,
            DateTimeOffset.UtcNow);
    }

    private static List<ToastSnapshot> GetToasts(object target)
    {
        var field = typeof(MainLayout).GetField("_toasts", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        var list = (System.Collections.IEnumerable)field!.GetValue(target)!;
        var result = new List<ToastSnapshot>();
        foreach (var item in list)
        {
            item.Should().NotBeNull();
            var type = item!.GetType();
            var titel = type.GetProperty("Titel")!.GetValue(item)?.ToString() ?? string.Empty;
            var nachricht = type.GetProperty("Nachricht")!.GetValue(item)?.ToString() ?? string.Empty;
            var cssClass = type.GetProperty("CssClass")!.GetValue(item)?.ToString() ?? string.Empty;
            result.Add(new ToastSnapshot(titel, nachricht, cssClass));
        }

        return result;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Softwareschmiede.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with Softwareschmiede.slnx not found.");
    }

    private sealed class FakeRunningAutomationStatusSource : IRunningAutomationStatusSource
    {
        public int RunningCount { get; set; }

        public event Action<int, int>? RunningCountChanged;

        public int SubscriberCount => RunningCountChanged?.GetInvocationList().Length ?? 0;

        public int GetRunningCount() => RunningCount;

        public bool IsRunning(Guid aufgabeId) => RunningCount > 0;
    }

    private sealed class FakeAutoShutdownOrchestrator : IAutoShutdownOrchestrator
    {
        public List<bool> SetEnabledCalls { get; } = [];

        public void SetEnabled(bool enabled) => SetEnabledCalls.Add(enabled);
    }

    private sealed class FakeBenutzerkontextService : IBenutzerkontextService
    {
        public string GetBenutzerId() => "test-user";
    }

    private sealed class FakeNavigationManager : NavigationManager
    {
        public FakeNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        public string NextResult { get; set; } = "played";
        public Exception? Exception { get; set; }
        public List<JsCall> Invocations { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add(new JsCall(identifier, args ?? []));
            if (Exception is not null)
            {
                throw Exception;
            }

            object? value = typeof(TValue) == typeof(string) ? NextResult : default(TValue);
            return new ValueTask<TValue>((TValue?)value!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }
    }

    private sealed class TestMainLayout : MainLayout
    {
        public void InvokeOnInitialized() => OnInitialized();
    }

    private sealed record ToastSnapshot(string Titel, string Nachricht, string CssClass);

    private sealed record JsCall(string Identifier, object?[] Args);

    private static MainLayoutHarness CreateHarness(
        IRunningAutomationStatusSource? runningSource = null,
        IAutoShutdownOrchestrator? autoShutdownOrchestrator = null)
    {
        var effectiveRunningSource = runningSource as FakeRunningAutomationStatusSource ?? new FakeRunningAutomationStatusSource();
        var effectiveAutoShutdownOrchestrator = autoShutdownOrchestrator as FakeAutoShutdownOrchestrator ?? new FakeAutoShutdownOrchestrator();
        var options = new DbContextOptionsBuilder<SoftwareschmiededDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new SoftwareschmiededDbContext(options);
        var benachrichtigungsHub = new KiAufgabenBenachrichtigungsHub(NullLogger<KiAufgabenBenachrichtigungsHub>.Instance);
        var benachrichtigungsEinstellungen = new BenachrichtigungsEinstellungenService(db);
        var auditService = new BenachrichtigungsAuditService(db, NullLogger<BenachrichtigungsAuditService>.Instance);
        var navigationManager = new FakeNavigationManager();
        var jsRuntime = new FakeJsRuntime();
        var sut = new TestMainLayout();
        SetInjectedProperty(sut, "RunningAutomationStatusSource", effectiveRunningSource);
        SetInjectedProperty(sut, "AutoShutdownOrchestrator", effectiveAutoShutdownOrchestrator);
        SetInjectedProperty(sut, "BenachrichtigungsHub", benachrichtigungsHub);
        SetInjectedProperty(sut, "BenachrichtigungsEinstellungen", benachrichtigungsEinstellungen);
        SetInjectedProperty(sut, "BenachrichtigungsAudit", auditService);
        SetInjectedProperty(sut, "Benutzerkontext", new FakeBenutzerkontextService());
        SetInjectedProperty(sut, "NavigationManager", navigationManager);
        SetInjectedProperty(sut, "JsRuntime", jsRuntime);
        SetInjectedProperty(sut, "Logger", NullLogger<MainLayout>.Instance);
        return new MainLayoutHarness(
            db,
            sut,
            effectiveRunningSource,
            effectiveAutoShutdownOrchestrator,
            benachrichtigungsEinstellungen,
            jsRuntime,
            "test-user");
    }

    private sealed class MainLayoutHarness : IDisposable
    {
        public MainLayoutHarness(
            SoftwareschmiededDbContext db,
            TestMainLayout sut,
            FakeRunningAutomationStatusSource runningSource,
            FakeAutoShutdownOrchestrator autoShutdownOrchestrator,
            BenachrichtigungsEinstellungenService einstellungenService,
            FakeJsRuntime jsRuntime,
            string benutzerId)
        {
            Db = db;
            Sut = sut;
            RunningSource = runningSource;
            AutoShutdownOrchestrator = autoShutdownOrchestrator;
            EinstellungenService = einstellungenService;
            JsRuntime = jsRuntime;
            BenutzerId = benutzerId;
        }

        public SoftwareschmiededDbContext Db { get; }
        public TestMainLayout Sut { get; }
        public FakeRunningAutomationStatusSource RunningSource { get; }
        public FakeAutoShutdownOrchestrator AutoShutdownOrchestrator { get; }
        public BenachrichtigungsEinstellungenService EinstellungenService { get; }
        public FakeJsRuntime JsRuntime { get; }
        public string BenutzerId { get; }

        public void Dispose()
        {
            Sut.Dispose();
            Db.Dispose();
        }
    }
}
