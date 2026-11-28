using System.Net;
using System.Text;
using Softwareschmiede.StatusServer.Data;
using Softwareschmiede.StatusServer.Security;

namespace Softwareschmiede.StatusServer.Pages;

/// <summary>Gemeinsamer HTML-Rahmen aller server-gerenderten Seiten
/// (Doctype/Head/CSS/Footer) samt Navigationszeile mit Abmelden-Formular.</summary>
internal static class PageShell
{
    /// <summary>Content-Type aller server-gerenderten HTML-Antworten.</summary>
    internal const string HtmlContentType = "text/html; charset=utf-8";

    /// <summary>Konfigurierter <c>PathBase</c>-Praefix (z.B. <c>/softwareschmiede</c>) fuer den
    /// Reverse-Proxy-Betrieb unter einem URL-Unterpfad; leer bei Betrieb auf dem Domain-Root.
    /// Einmalig beim Start aus der Konfiguration gesetzt (<see cref="Program"/>) und von allen
    /// server-gerenderten Links, Formular-Aktionen und Redirects als Praefix verwendet, da diese
    /// als literale, root-absolute Pfade gebaut werden und nicht automatisch von
    /// <see cref="Microsoft.AspNetCore.Builder.UsePathBaseExtensions.UsePathBase"/> erfasst werden.</summary>
    internal static string BasePath { get; set; } = string.Empty;

    /// <summary>Theme-Variablen beider Farbschemata als CSS Custom Properties:
    /// Light-Palette als <c>:root</c>-Default, Dark-Palette bei explizitem
    /// <c>data-theme="dark"</c> sowie bei <c>data-theme="auto"</c> über die
    /// <c>prefers-color-scheme</c>-Media-Query (<c>:not([data-theme="light"])</c>,
    /// damit explizites Light auch bei dunkler Systemeinstellung gewinnt).
    /// Wird von <see cref="BeginPage"/> und <c>StatusPageComposer</c> eingebettet.</summary>
    internal const string ThemeCss = """
:root {
  color-scheme: light;
  --bg: #f4f5f7; --fg: #1f2430; --surface: #fff;
  --border: #e5e7eb; --border-input: #d1d5db; --border-row: #f0f1f3;
  --label: #374151; --muted: #6b7280; --muted-faint: #9ca3af;
  --header-bg: #1f2937; --header-fg: #f8fafc; --inst-header: #cbd5e1;
  --accent: #4f46e5; --accent-hover: #4338ca; --button-fg: #fff;
  --danger: #991b1b; --danger-strong: #b91c1c; --danger-strong-hover: #991b1b; --danger-border: #f87171;
  --ok-bg: #d1fae5; --ok-fg: #065f46; --ok-border: #34d39955;
  --err-bg: #fee2e2; --err-fg: #991b1b; --err-border: #f8717155;
  --warn-bg: #fef3c7; --warn-fg: #92400e; --warn-border: #f59e0b55; --warn-strong: #f59e0b;
  --pause-bg: #e5e7eb; --pause-fg: #4b5563;
  --hover: #f3f4f6;
  --badge-online-bg: #065f46; --badge-online-fg: #d1fae5; --badge-online-dot: #34d399;
  --badge-offline-bg: #7f1d1d; --badge-offline-fg: #fee2e2; --badge-offline-dot: #f87171;
  --badge-stale-bg: #78350f; --badge-stale-fg: #fef3c7; --badge-stale-dot: #fbbf24;
  --src-fg: #fff; --src-app: #4f46e5; --src-gh: #0d9488;
  --pill-run-bg: #e0e7ff; --pill-run-fg: #3730a3;
}
:root[data-theme="dark"] {
""" + DarkThemeVars + """
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
""" + DarkThemeVars + """
  }
}
""";

    /// <summary>Deklarationsblock der Dark-Palette — in <see cref="ThemeCss"/> sowohl unter
    /// <c>:root[data-theme="dark"]</c> als auch innerhalb der
    /// <c>@media (prefers-color-scheme: dark)</c>-Regel eingesetzt, damit die Palette
    /// nur an einer Stelle gepflegt wird.</summary>
    private const string DarkThemeVars = """
  color-scheme: dark;
  --bg: #111827; --fg: #f1f5f9; --surface: #1f2937;
  --border: #374151; --border-input: #4b5563; --border-row: #374151;
  --label: #d1d5db; --muted: #9ca3af; --muted-faint: #6b7280;
  --accent: #818cf8; --accent-hover: #6366f1;
  --danger: #f87171;
  --ok-bg: #065f46; --ok-fg: #a7f3d0;
  --err-bg: #7f1d1d; --err-fg: #fecaca;
  --warn-bg: #78350f; --warn-fg: #fde68a;
  --pause-bg: #374151; --pause-fg: #d1d5db;
  --hover: #374151;
  --src-app: #6366f1; --src-gh: #14b8a6;
  --pill-run-bg: #312e81; --pill-run-fg: #c7d2fe;
""";

    /// <summary>Navigations-CSS — separat gehalten, damit auch <c>StatusPageComposer</c> es einbetten kann.</summary>
    internal const string NavCss = """
nav.topnav { display: flex; align-items: center; gap: 14px; flex-wrap: wrap;
  background: var(--surface); border: 1px solid var(--border); border-radius: 10px;
  padding: 8px 16px; margin-top: 12px; font-size: 13px; }
nav.topnav a.nav-link { color: var(--accent); font-weight: 600; }
nav.topnav .nav-user { margin-left: auto; color: var(--muted); font-size: 12.5px; }
nav.topnav form.nav-logout { display: inline; }
nav.topnav form.nav-logout button { background: none; border: 1px solid var(--border-input);
  border-radius: 6px; color: var(--label); font-size: 12.5px; padding: 3px 10px; cursor: pointer; }
nav.topnav form.nav-logout button:hover { background: var(--hover); }
""";

    private const string Css = """
* { box-sizing: border-box; margin: 0; }
body { font-family: "Segoe UI", system-ui, sans-serif; background: var(--bg); color: var(--fg); }
.page { max-width: 1080px; margin: 0 auto; padding: 24px 20px 60px; }
header.top { display: flex; align-items: center; gap: 14px; flex-wrap: wrap;
  background: var(--header-bg); color: var(--header-fg); padding: 16px 24px; border-radius: 10px; }
header.top h1 { font-size: 20px; font-weight: 600; flex: 1; }
section { background: var(--surface); border: 1px solid var(--border); border-radius: 10px;
  padding: 16px 20px; margin-bottom: 16px; margin-top: 16px; }
section h1, section h2 { font-size: 17px; font-weight: 600; margin-bottom: 10px; }
section h3 { font-size: 13px; font-weight: 600; margin-bottom: 6px; }
.muted { color: var(--muted); font-size: 13px; margin-bottom: 12px; }
.msg { background: var(--ok-bg); border: 1px solid var(--ok-border); color: var(--ok-fg);
  font-size: 13px; border-radius: 8px; padding: 8px 14px; margin-bottom: 14px; }
.msg.err { background: var(--err-bg); border-color: var(--err-border); color: var(--err-fg); }
form label { display: block; font-size: 13px; font-weight: 600; color: var(--label); margin-bottom: 10px; }
form input[type="text"], form input[type="password"], form input[type="number"], form select {
  display: block; width: 100%; max-width: 380px; margin-top: 4px; padding: 7px 10px;
  font-size: 14px; border: 1px solid var(--border-input); border-radius: 6px;
  background: var(--surface); color: var(--fg); }
form button { background: var(--accent); border: 0; border-radius: 6px; color: var(--button-fg);
  font-size: 13.5px; font-weight: 600; padding: 8px 16px; cursor: pointer; }
form button:hover { background: var(--accent-hover); }
form button[disabled] { background: var(--muted-faint); cursor: not-allowed; }
form.inline { display: inline; }
form.inline button { font-size: 12px; padding: 4px 10px; margin: 2px 4px 2px 0; }
a.action-link { display: inline-block; font-size: 12px; font-weight: 600;
  padding: 4px 10px; margin: 2px 4px 2px 0; background: var(--surface);
  border: 1px solid var(--border-input); border-radius: 6px; color: var(--label); }
a.action-link:hover { background: var(--hover); }
a.action-link.danger { color: var(--danger); border-color: var(--danger-border); }
a.action-link.danger:hover { background: var(--err-bg); }
form button.danger { background: var(--danger-strong); }
form button.danger:hover { background: var(--danger-strong-hover); }
table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
td, th { text-align: left; padding: 7px 8px; border-top: 1px solid var(--border-row); overflow-wrap: anywhere; }
tr:first-child th { border-top: 0; }
.pill { display: inline-block; font-size: 11px; font-weight: 600; padding: 1px 8px; border-radius: 999px; }
.pill.done { background: var(--ok-bg); color: var(--ok-fg); }
.pill.err { background: var(--err-bg); color: var(--err-fg); }
.pill.pause { background: var(--pause-bg); color: var(--pause-fg); }
.pill.wait { background: var(--warn-bg); color: var(--warn-fg); }
a { color: var(--accent); text-decoration: none; }
.token-once { background: var(--warn-bg); border: 1px solid var(--warn-border); color: var(--warn-fg);
  border-radius: 8px; padding: 12px 16px; margin-bottom: 16px; font-size: 13px; }
.token-once code { display: block; background: var(--surface); border: 1px dashed var(--warn-strong);
  border-radius: 6px; padding: 8px 10px; margin-top: 8px; font-size: 14px;
  color: var(--fg); overflow-wrap: anywhere; }
.empty { color: var(--muted); font-size: 13px; padding: 10px 4px; }
footer { margin-top: 26px; font-size: 11.5px; color: var(--muted-faint); text-align: center; }
""";

    /// <summary>Beginnt ein HTML-Dokument (Doctype, Head, CSS, öffnender Seiten-Container).</summary>
    /// <param name="sb">Der Ausgabepuffer.</param>
    /// <param name="title">Der Seitentitel.</param>
    /// <param name="theme">Das wirksame Farbschema — landet als <c>data-theme</c>-Attribut
    /// am <c>&lt;html&gt;</c>-Tag; <see cref="ThemePreference.Auto"/> überlässt die Wahl
    /// der <c>prefers-color-scheme</c>-Media-Query (anonyme Seiten).</param>
    public static void BeginPage(StringBuilder sb, string title, ThemePreference theme = ThemePreference.Auto)
    {
        sb.Append("<!doctype html><html lang=\"de\" data-theme=\"").Append(ThemeName(theme)).Append("\">");
        sb.Append("<head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>").Append(E(title)).Append("</title><style>");
        sb.Append(ThemeCss).Append(Css).Append(NavCss);
        sb.Append("</style></head><body><div class=\"page\">");
    }

    /// <summary>Liefert den <c>data-theme</c>-Attributwert einer Theme-Einstellung
    /// (<c>auto</c>/<c>light</c>/<c>dark</c>).</summary>
    internal static string ThemeName(ThemePreference theme)
        => theme switch
        {
            ThemePreference.Light => "light",
            ThemePreference.Dark => "dark",
            _ => "auto",
        };

    /// <summary>Rendert den dunklen Titel-Header der Seite.</summary>
    public static void AppendHeader(StringBuilder sb, string title)
        => sb.Append("<header class=\"top\"><h1>").Append(E(title)).Append("</h1></header>");

    /// <summary>Rendert die Navigationszeile für angemeldete Benutzer
    /// (Status, Profil, Admin-Links nur für <c>Administration</c>, Abmelden-Formular).</summary>
    public static void AppendNav(StringBuilder sb, PageUserContext user)
    {
        sb.Append("<nav class=\"topnav\">");
        sb.Append("<a class=\"nav-link\" href=\"").Append(BasePath).Append("/\">Status</a>");
        sb.Append("<a class=\"nav-link\" href=\"").Append(BasePath).Append("/profile\">Profil</a>");
        if (user.IsAdmin)
        {
            sb.Append("<a class=\"nav-link\" href=\"").Append(BasePath).Append("/admin/users\">Benutzer</a>");
            sb.Append("<a class=\"nav-link\" href=\"").Append(BasePath).Append("/admin/tokens\">Tokens</a>");
            sb.Append("<a class=\"nav-link\" href=\"").Append(BasePath).Append("/admin/settings\">Einstellungen</a>");
        }

        sb.Append("<span class=\"nav-user\">").Append(E(user.UserName));
        if (user.IsAdmin)
            sb.Append(" · ").Append(E(StatusRoles.DisplayName(StatusRoles.Administration)));
        sb.Append("</span>");
        sb.Append("<form class=\"nav-logout\" method=\"post\" action=\"").Append(BasePath).Append("/logout\">");
        AppendHiddenToken(sb, user.AntiforgeryFieldName, user.AntiforgeryToken);
        sb.Append("<button type=\"submit\">Abmelden</button></form>");
        sb.Append("</nav>");
    }

    /// <summary>Rendert eine Status-/Fehlermeldung, falls vorhanden.</summary>
    public static void AppendMessage(StringBuilder sb, string? message, bool isError = false)
    {
        if (!string.IsNullOrEmpty(message))
        {
            sb.Append("<div class=\"msg").Append(isError ? " err" : string.Empty)
                .Append("\">").Append(E(message)).Append("</div>");
        }
    }

    /// <summary>Rendert das Antiforgery-Hidden-Feld für ein Formular.</summary>
    public static void AppendHiddenToken(StringBuilder sb, string fieldName, string token)
        => sb.Append("<input type=\"hidden\" name=\"").Append(E(fieldName))
            .Append("\" value=\"").Append(E(token)).Append("\">");

    /// <summary>Schließt den Seiten-Container inkl. Footer und Dokument.</summary>
    public static void EndPage(StringBuilder sb)
        => sb.Append("<footer>softwareschmiede-status · server-gerendert</footer>")
            .Append("</div></body></html>");

    /// <summary>HTML-encodiert einen Text.</summary>
    public static string E(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);
}
