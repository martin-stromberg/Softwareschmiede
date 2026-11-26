using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Softwareschmiede.StatusServer.Tests;

/// <summary><see cref="WebApplicationFactory{TEntryPoint}"/> für den Status-Server mit
/// überschreibbarer Konfiguration (Tokens, Basic-Auth, GitHub-Repos).</summary>
public class StatusWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>Die zu verwendenden Relay-Tokens (Standard: <c>test-token</c>).</summary>
    public IReadOnlyList<string> RelayTokens { get; init; } = ["test-token"];

    /// <summary>Optionaler Basic-Auth-Benutzername für die Seiten-Endpunkte.</summary>
    public string? PageUsername { get; init; }

    /// <summary>Optionales Basic-Auth-Passwort für die Seiten-Endpunkte.</summary>
    public string? PagePassword { get; init; }

    /// <summary>Optionaler Relay-Token-Dev-Wert (nur Development).</summary>
    public string? DevToken { get; init; }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        foreach (var (index, token) in RelayTokens.Index())
            builder.UseSetting($"Relay:Tokens:{index}", token);
        if (DevToken is not null)
            builder.UseSetting("Relay:DevToken", DevToken);
        if (PageUsername is not null)
            builder.UseSetting("Page:Username", PageUsername);
        if (PagePassword is not null)
            builder.UseSetting("Page:Password", PagePassword);
    }
}
