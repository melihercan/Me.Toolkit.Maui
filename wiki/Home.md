# Me.Toolkit.Maui

Libraries and plugins for .NET MAUI applications, ported from
[Xamarinme](https://github.com/melihercan/Xamarinme) now that Xamarin is retired.

**Four of the five are published at `26.9.27`**, verified by unzipping what nuget.org serves.
`26.9.8` is unlisted: it shipped the right package ID around assemblies still called `Mauime.*`.
`26.9.14` and Sizing's `26.9.26` shipped a `net10.0` assembly stamped `1.0.0.0`; see
[Publishing](Publishing). `Me.Toolkit.Maui.Nfc` is held back as unfinished —
it builds and is tested, but its reading session has never been used against a physical tag. See
[Publishing](Publishing). `DemoApp/` is a single MAUI app with a tab per library, replacing Xamarinme's three
Xamarin.Forms solutions across 17 projects. The Xamarin sources were imported under `legacy/` for the
characterization phase and deleted once each library had been replaced — the history is the record.

The `Xamarinme.*` packages stay on nuget.org exactly as they are, and are **not** deprecated. That
is a decision, not an oversight: `Blazorme.TestHost` looked like a precedent, but it worked because
it kept the *same* package ID, so existing consumers could resolve a newer working version. Nothing
referencing `Xamarinme.WebHostPatch` will ever resolve `Me.Toolkit.Maui.WebHostPatch` — NuGet has no redirect
between IDs.

| Planned package | Replaces | Status |
|---|---|---|
| `Me.Toolkit.Maui.Nfc` | `Xamarinme.Nfc` — never published | **Ported, not published** — Android and iOS implemented, session unfinished; `IsPackable=false` |
| [`Me.Toolkit.Maui.Configuration`](https://www.nuget.org/packages/Me.Toolkit.Maui.Configuration) | [`Xamarinme.Configuration`](https://www.nuget.org/packages/Xamarinme.Configuration) 1.0.2 | **Published** 26.9.27 |
| [`Me.Toolkit.Maui.Hosting`](https://www.nuget.org/packages/Me.Toolkit.Maui.Hosting) | [`Xamarinme.Hosting`](https://www.nuget.org/packages/Xamarinme.Hosting) 1.0.3 | **Published** 26.9.27 |
| [`Me.Toolkit.Maui.WebHostPatch`](https://www.nuget.org/packages/Me.Toolkit.Maui.WebHostPatch) | [`Xamarinme.WebHostPatch`](https://www.nuget.org/packages/Xamarinme.WebHostPatch) 1.0.0 | **Published** 26.9.27 — no patch needed on .NET 10 |
| [`Me.Toolkit.Maui.Sizing`](https://www.nuget.org/packages/Me.Toolkit.Maui.Sizing) | nothing — new, not a port | **Published** 26.9.27 — relative sizes (`{me:Relative 30}`), verified rotating on an Android phone |

The repository was called Mauime. It is not, because nuget.org rejects any package ID beginning with
`Maui` as reserved — see [Publishing](Publishing) for what that took to establish.

All five multi-target `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` and
`net10.0-windows10.0.19041.0`.

This is a **port, not a framework bump**. Xamarin.Forms became MAUI, `MSBuild.Sdk.Extras` with
`MonoAndroid10.0`/`Xamarin.iOS10`/`uap10.0.19041`/`Xamarin.Mac20` became MAUI's built-in
multi-targeting over `net10.0-android`/`-ios`/`-maccatalyst`/`-windows`, and three Xamarin-shaped
libraries are being reshaped around what MAUI already provides rather than duplicating it. See
[Design Notes](Design-Notes) for what each library is and what MAUI does for you now.

The package IDs are new, so there are no existing consumers and **no additive-only guarantee**: the
API is free to be fixed. The API baseline exists so that changing it is a decision rather than an
accident.

The `Xamarinme.*` packages stay on nuget.org as they are. Xamarin retiring is what ended them.

**Careful with versions**: the Xamarinme csprojs say Configuration 1.0.3 and Hosting 1.0.4, which are
*ahead* of what was ever published. Never read a live version out of a csproj here.

## Where things are

How to use each package — install, usage, options, platform requirements — is in its README, the
same text nuget.org shows:

- **[Configuration](https://github.com/melihercan/Me.Toolkit.Maui/blob/master/Me.Toolkit.Maui.Configuration/README.md)** — `appsettings.json` from the app package or embedded resources.
- **[Hosting](https://github.com/melihercan/Me.Toolkit.Maui/blob/master/Me.Toolkit.Maui.Hosting/README.md)** — a host environment name, so `IsDevelopment()` means something.
- **[Sizing](https://github.com/melihercan/Me.Toolkit.Maui/blob/master/Me.Toolkit.Maui.Sizing/README.md)** — relative sizes: a percentage of the parent, window, display or another element.
- **[WebHostPatch](https://github.com/melihercan/Me.Toolkit.Maui/blob/master/Me.Toolkit.Maui.WebHostPatch/README.md)** — an ASP.NET Core web host and Kestrel inside a MAUI app.
- **[Nfc](https://github.com/melihercan/Me.Toolkit.Maui/blob/master/Me.Toolkit.Maui.Nfc/README.md)** — NDEF on Android and iOS; not published.

About the repository itself:

- **[Building and Testing](Building-and-Testing)** — the commands, why the build is not
  warning-free, and what the test suite covers.
- **[Design Notes](Design-Notes)** — why the repository looks like this, and the invariants.
- **[Modernization Log](Modernization-Log)** — what changed, phase by phase.
- **[Publishing](Publishing)** — the tags, why one workflow, and why it must run on Windows.
