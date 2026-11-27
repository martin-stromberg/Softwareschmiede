using FluentAssertions;
using Softwareschmiede.StatusServer.Pages;

namespace Softwareschmiede.StatusServer.Tests.Pages;

/// <summary>Tests für <see cref="AuthPagesComposer"/> (Login-, Registrierungs-,
/// Profil- und Access-Denied-Seiten).</summary>
public sealed class AuthPagesComposerTests
{
    private static PageUserContext User(bool isAdmin = true)
        => new("tester", isAdmin, "token-value", "__RequestVerificationToken");

    /// <summary>Die Registrierungsseite enthält das Formular mit allen Feldern
    /// inkl. Antiforgery-Hidden-Feld.</summary>
    [Fact]
    public void RenderRegister_ContainsFormAndToken()
    {
        var html = AuthPagesComposer.RenderRegister("__RequestVerificationToken", "tok-1");

        html.Should().Contain("action=\"/register\"");
        html.Should().Contain("name=\"username\"");
        html.Should().Contain("name=\"password\"");
        html.Should().Contain("name=\"confirmPassword\"");
        html.Should().Contain("name=\"__RequestVerificationToken\" value=\"tok-1\"");
    }

    /// <summary>Das Benutzername-Feld der Registrierung kommuniziert den erlaubten
    /// Zeichensatz (Hinweistext + pattern/title) — keine Umlaute/Leerzeichen.</summary>
    [Fact]
    public void RenderRegister_ShowsUserNameRules()
    {
        var html = AuthPagesComposer.RenderRegister("__RequestVerificationToken", "tok-1");

        html.Should().Contain("Erlaubte Zeichen");
        html.Should().Contain("-._@+");
        html.Should().Contain("pattern=\"[a-zA-Z0-9._@+-]+\"");
    }

    /// <summary>Eine Fehlermeldung auf der Registrierungsseite wird HTML-escaped gerendert.</summary>
    [Fact]
    public void RenderRegister_Error_IsEncoded()
    {
        var html = AuthPagesComposer.RenderRegister("f", "t", "Fehler <b>x</b>");

        html.Should().Contain("Fehler &lt;b&gt;x&lt;/b&gt;");
        html.Should().NotContain("Fehler <b>x</b>");
    }

    /// <summary>Die Login-Seite enthält Benutzername/Passwort und das Antiforgery-Feld.</summary>
    [Fact]
    public void RenderLogin_ContainsFormAndToken()
    {
        var html = AuthPagesComposer.RenderLogin("__RequestVerificationToken", "tok-2");

        html.Should().Contain("action=\"/login\"");
        html.Should().Contain("name=\"username\"");
        html.Should().Contain("name=\"password\"");
        html.Should().Contain("name=\"__RequestVerificationToken\" value=\"tok-2\"");
        html.Should().NotContain("confirmPassword");
    }

    /// <summary>Eine Fehlermeldung auf der Login-Seite wird angezeigt.</summary>
    [Fact]
    public void RenderLogin_Error_IsShown()
    {
        var html = AuthPagesComposer.RenderLogin("f", "t", "Benutzername oder Passwort ist falsch.");

        html.Should().Contain("Benutzername oder Passwort ist falsch.");
    }

    /// <summary>Die Profilseite zeigt Benutzername/Stufe und das Passwort-Formular.</summary>
    [Fact]
    public void RenderProfile_ContainsUserAndPasswordForm()
    {
        var html = AuthPagesComposer.RenderProfile(User());

        html.Should().Contain("tester");
        html.Should().Contain("action=\"/profile/password\"");
        html.Should().Contain("name=\"currentPassword\"");
        html.Should().Contain("name=\"newPassword\"");
        html.Should().Contain("name=\"confirmPassword\"");
        html.Should().Contain("action=\"/logout\"");
    }

    /// <summary>Die Profilseite zeigt die Stufe als deutschen Anzeigenamen,
    /// nicht den internen Rollen-Schlüssel.</summary>
    [Fact]
    public void RenderProfile_Watcher_ShowsDisplayName()
    {
        var html = AuthPagesComposer.RenderProfile(User(isAdmin: false));

        // Das Ü wird HTML-encodiert ausgegeben.
        html.Should().Contain("Stufe &#220;berwacher");
        html.Should().NotContain("Ueberwacher");
    }

    /// <summary>Die Access-Denied-Seite nennt den Grund und verlinkt zur Status-Übersicht.</summary>
    [Fact]
    public void RenderAccessDenied_ContainsReasonAndLink()
    {
        var html = AuthPagesComposer.RenderAccessDenied(User(isAdmin: false));

        html.Should().Contain("Zugriff verweigert");
        html.Should().Contain("href=\"/\"");
    }

    /// <summary>Die Fehlerseite für abgelaufene Formulare erklärt die Ursache
    /// und verlinkt zurück zur Status-Übersicht.</summary>
    [Fact]
    public void RenderFormExpired_ContainsReasonAndLink()
    {
        var html = AuthPagesComposer.RenderFormExpired();

        html.Should().Contain("abgelaufen");
        html.Should().Contain("neu");
        html.Should().Contain("href=\"/\"");
    }
}
