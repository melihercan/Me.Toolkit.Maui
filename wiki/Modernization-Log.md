# Modernization Log

Xamarinme sat untouched from January 2022. Xamarin has since been retired, so this is a port to
.NET MAUI rather than a framework bump: a new repository, new package IDs, and a per-library
question of whether the library should exist at all.

Each phase is one commit on `master`.

## Phase 0 — characterization tests

No production code changed. The point was to pin **current** behaviour, defects included, before
touching anything.

The Xamarin sources for the four libraries were imported verbatim into `legacy/`, together with the
vendored `Microsoft.Extensions.Primitives.Patch` that `WebHostPatch` cannot build without. Two
Xamarinme projects were **not** imported, because there is nothing there to port: an empty
`Class1` (`Microsoft.Extensions.FileProviders.Xamarin`) and a one-method file-system helper that
cannot work by construction (`Xamarin.Essentials.FileSystem.Extensions`). See
[Design Notes](Design-Notes.md#deletions-that-are-not-ports).

A `net10.0` xUnit v3 project was added with 62 tests, plus `PublicApi.approved.txt` as the API
baseline.

### What could be exercised

Better than Blazorme's starting position: the legacy libraries are `netstandard2.0`, which a
`net10.0` test project can reference, so three of the four could be characterized **behaviourally**
rather than through metadata alone.

| Library | Builds today? | Coverage in Phase 0 |
|---|---|---|
| Configuration | Yes, 0 warnings | Behavioural — 19 tests |
| Hosting | Yes, 0 warnings | Behavioural — 13 tests |
| WebHostPatch | Yes, 4 advisory warnings | Behavioural for the lifetime half; API baseline for `RunPatchedAsync`, which needs a live ASP.NET Core 2.2 `IWebHost` |
| Nfc | **No** | **None** — see below |

`legacy/Nfc` is not in the solution and has no coverage. `MSBuild.Sdk.Extras/3.0.23` needs desktop
`msbuild.exe` for its `MonoAndroid10.0`, `Xamarin.iOS10`, `uap10.0.19041` and `Xamarin.Mac20`
targets, and the `netstandard2.0` slice fails on its own terms. Its two defects are pinned as source
assertions, and become real tests in the phase that ports it. That gap is stated rather than papered
over.

### Nineteen defects pinned

Most were found by reading. **Four were found only by building or running**, which is the argument
for doing this phase at all:

| Found by | Defect |
|---|---|
| Building `legacy/Nfc` | The `netstandard2.0` slice does not compile: `NETSTANDARD2_0` is undefined under this toolchain, so `CrossNfc` compiles `new Nfc()` against a type that slice cannot see |
| Restoring the tests | `Xamarinme.WebHostPatch` pulls a **critical** Kestrel advisory and a moderate IIS one, inherited by all 30,366 downloads of 1.0.0 |
| Running the tests | The vendored Primitives fork really does shadow the real assembly — the test project itself ends up running on 5.9.0.0 |
| Running the tests | Newtonsoft renders JSON `true` as `"True"` and `null` as `""`, so the library's own README example is wrong about its own output |

The rest, by library:

- **Configuration** — no argument validation anywhere (null options and null assembly are both a
  `NullReferenceException` out of `Build()`); a null `Prefix` is worse still, silently loading
  nothing so the app runs on defaults it never chose; `AssemblyVersion` 1.0.0 against `Version`
  1.0.3.
- **Hosting** — `XamarinHost` claims `IHost` and throws `NotImplementedException` from `StartAsync`
  and `StopAsync`; `Dispose()` is empty, so the `ServiceProvider` it owns is never disposed; the
  environment lookup wraps `GetProperty`/`GetString` in a `try` with an **empty catch**, so a typo in
  the setting name is indistinguishable from not setting it; a JSON `null` there overwrites the
  "Production" default with `null`; `JsonDocument.Parse` sits one line *above* that `try`, so the one
  failure a user is most likely to cause is the one that escapes; `Build()` registers
  `IConfiguration` but not `IConfigurationRoot`, and can be called twice, registering the
  configuration twice; `XamarinHost` is exported from the internals namespace with no public
  constructor.
- **WebHostPatch** — `ShutdownPatched` is an extension method on `IWebHost` that never touches its
  own parameter, raising a private *static* event instead, so shutdown is process-global and calling
  it on a host that was never run silently does nothing; `Dispose` unsubscribes
  `Console.CancelKeyPress`, the very API the fork exists to avoid and one `WaitForStartAsync` never
  subscribes; `OnProcessExit` waits for the shutdown block with a timeout, logs that it timed out,
  and then waits again with no timeout at all.
- **Nfc** — the `netstandard2.0` slice above, and about 180 lines of dead code behind an `#if false`
  in `Pcsc.cs`, including a whole second `Pcsc()` constructor.

### The tests were proven, not assumed

Every Phase 0 test was checked by breaking what it guards: an invented type in the approved baseline
produced 1 failure, changing a fixture value produced 4, and uncommenting the `CancelKeyPress`
subscription produced exactly the 1 expected failure. The suite then ran **30 times** in a row clean.

### One consequence to carry forward

**The zero-warning bar cannot be enforced yet.** `legacy/WebHostPatch`'s 2.2.0 pins produce four
NuGet advisory warnings, and `-warnaserror` turns advisories into errors. There is therefore no CI
workflow yet; it arrives when `legacy/` goes. The warnings are deliberately left visible rather than
suppressed with `NoWarn`.

## Phase 1 — the MAUI skeleton

Four library projects, no library code: `Me.Toolkit.Maui.Nfc`, `Me.Toolkit.Maui.Configuration`, `Me.Toolkit.Maui.Hosting` and
`Me.Toolkit.Maui.WebHostPatch`, each multi-targeting `net10.0`, `net10.0-android`, `net10.0-ios`,
`net10.0-maccatalyst` and `net10.0-windows10.0.19041.0`. Twenty assemblies, all building clean.

The point of a skeleton phase is to settle the build questions while the answer to "did that work?"
is still unambiguous. Four were settled by building, not by reading:

- **`Microsoft.Maui.Core` is the right dependency**, not `Microsoft.Maui.Controls` and not
  `Microsoft.Maui.Essentials`. Essentials gives `Platform.CurrentActivity` but has no
  `MauiAppBuilder` and no `LifecycleEvents`; Core has all three. An NFC plugin has no business
  depending on Controls.
- **`Me.Toolkit.Maui.WebHostPatch` needs no packages at all.** `WebApplication.CreateSlimBuilder()` plus
  `UseKestrel` compiles clean against `net10.0-android` with a `Microsoft.AspNetCore.App` framework
  reference and nothing else. **This conclusion was wrong**, and Phase 6 explains why: the probe
  built a library, and the framework reference only fails when an *app* needs a runtime pack.
- **Android generates a public `Resource` class into every library**, even one with no Android
  resources, and it landed in all four packages' public surface. Turned off with
  `AndroidGenerateResourceDesigner=false`. Blazorme had to live with the equivalent Razor wart;
  this one has a knob.
- **iOS and Mac Catalyst library slices compile on Windows.** Only app builds and signing need a
  Mac.

`Directory.Build.props` and `Directory.Build.targets` were added at the root, with **empty shields
under `legacy/`** so the imported sources keep building exactly as they did. Those two shields are
the only files added under `legacy/`.

### The baseline machinery now reaches the platform slices

This was the claim Phase 0 made and could not test, since everything it covered was plain
netstandard2.0. Making it true needed two changes:

- one `MetadataLoadContext` **per assembly**, because an Android `System.Runtime` and an iOS
  `System.Runtime` cannot share a simple-name resolver;
- `Me.Toolkit.Maui.ReferencePaths.txt` beside every assembly, written from the `ReferencePath` item, because
  a library build leaves its dependencies nowhere near `bin/`.

Verified with a temporary type exposing `Android.Nfc.NfcAdapter`,
`CoreNFC.NFCNdefReaderSession` and `Windows.Devices.SmartCards.SmartCardReader`: every slice rendered
correctly, from a `net10.0` test project that can reference none of them. `MultiTargetingTests`
caught the divergence at the same time, which is exactly its job.

Also verified: a `net10.0` project referencing `Me.Toolkit.Maui.Nfc` resolves the `net10.0` slice byte for
byte, never the Android one. That is why the platform slices can only be covered through metadata.

74 tests, green in Debug and Release, 30 consecutive clean runs. The three new tests were each
proven by breaking what they guard.

## Phase 2 — Me.Toolkit.Maui.Nfc

The port of the one library with a clear reason to exist. `legacy/Nfc` was deleted in the same
commit; it had never built on this toolchain anyway.

Three decisions were taken before any code was written, and all three removed dependencies rather
than adding them, so **the phase added no NuGet packages at all**:

- **NDEF is implemented here.** `NdefMessage`, `NdefRecord` and `NdefTypeNameFormat` replace
  NdefLibrary 4.1.0 — a 2017 package with a netstandard1.4 asset that sat in the public API, since
  `ReadNdefAsync` returned its type. It was verified to restore, compile *and run* on .NET 10 before
  being dropped, so this was a choice rather than a forced move.
- **Android and iOS only.** Mac Catalyst and Windows would have meant the 678-line PC/SC layer and
  three `PCSC` packages, for an external USB reader rather than phone NFC. They share the
  platform-neutral implementation, which throws `PlatformNotSupportedException`.
- **DI instead of a static locator.** `builder.UseMeToolkitMauiNfc()` registers `INfc`; `CrossNfc.Current`
  is gone.

### The API is smaller than what it replaced

Every implementation is `internal`, so all five slices expose one surface —
`INfc`, `MeToolkitMauiNfcExtensions`, `NfcTagDetectedEventArgs` and the three NDEF types.
`CrossNfc`, the per-platform public `Nfc` classes, the manual `OnNewIntent` hook and the unused
`NfcTagStatus` are all gone. `MultiTargetingTests` stopped being vacuous the moment there was a
surface to compare.

`UseMeToolkitMauiNfc` wires Android's `OnNewIntent` through `ConfigureLifecycleEvents`, so consumers no
longer hand-edit `MainActivity`.

### Defects fixed, and how far each is proved

The two Phase 0 pins were rewritten as `FIXED_`. Reading the implementations turned up four more,
none of them visible from the API:

| Fixed | How it is proved |
|---|---|
| `PendingIntent.GetActivity(..., 0)` threw on **Android 12 and later** — API 31 made FLAG_MUTABLE/FLAG_IMMUTABLE mandatory, so `EnableSessionAsync` crashed on every current phone | source pin |
| iOS threw from inside CoreNFC completion callbacks, on the session's dispatch queue, where the throw could not reach the awaiting caller — a failed read or write left the caller awaiting forever | source pin |
| `Dispose()` was empty on Android, so the `ActivityStateChanged` subscription kept the instance alive for the life of the process | source-visible |
| `catch { if (ndef.IsConnected) ... }` ran on a variable still `null` whenever the failure happened first, masking the real error with a `NullReferenceException`; also `throw ex;` in three places, resetting the stack trace | source-visible |
| The activity was captured in the constructor and went stale on recreation; `DispatchQueue.CurrentQueue` has been obsolete since iOS 6 | source-visible |

**None of the Android or iOS fixes has behavioural coverage**, and
`LIMITATION_The_Android_and_iOS_implementations_have_no_behavioural_coverage` asserts that fact so
it fails if it ever stops being true. A net10.0 test project resolves the net10.0 slice, which is
the one with no implementation. Closing that gap needs the device-test harness that was deliberately
not taken on.

What *is* covered by running it: the NDEF codec, the DI registration and the platform-neutral
implementation — 40 tests.

### The NDEF codec is pinned against NdefLibrary's own output

The port's real risk is a codec that disagrees with the old one, which would corrupt tags silently.
So the byte vectors in `NdefTests` were not invented: each was produced by running NdefLibrary 4.1.0
on .NET 10 and capturing what it emitted — a short text record, a two-record message, a record with
an identifier, a 300-byte payload using the four-byte length field, and the empty record. Me.Toolkit.Maui
produces the same bytes for all of them.

Malformed input is refused rather than mis-parsed, including a four-byte payload length claiming
more than the buffer holds, which would otherwise overflow to a negative length or try to allocate
4 GB.

### The forked Primitives split the test project in two

Adding a reference to `Me.Toolkit.Maui.Nfc` broke **thirty tests that had nothing to do with NFC**.
`Microsoft.Maui.Core` wants `Microsoft.Extensions.Primitives` 10.0.0; `legacy/WebHostPatch`'s
vendored fork is stamped 5.9.0.0 and occupies that filename in the output directory; the real one
therefore never arrives and everything needing it fails with `FileNotFoundException`.

That fork cannot share a process with modern `Microsoft.Extensions`, so the tests were split:
`Me.Toolkit.Maui.Tests` for the libraries and the API baseline, `Legacy.Tests` for the characterization of
`legacy/`. Making them coexist would have meant hiding the very defect the fork is pinned for.
`Legacy.Tests` dies with `legacy/`.

It also removed the last heuristic from the baseline machinery: `legacy/` lost its
`Directory.Build.targets` shield so its assemblies emit `Me.Toolkit.Maui.ReferencePaths.txt` too, and there
is now one code path for resolving any assembly instead of a manifest for Me.Toolkit.Maui and a bin-scan for
legacy.

120 tests, green in Debug and Release. Every new test was proven by breaking what it guards:
inverting the message-begin flag failed 10, restoring the immutable `PendingIntent` failed 1, and
letting the unsupported platform succeed silently failed 1.

## Phase 3 — the remaining three libraries

`Me.Toolkit.Maui.Configuration`, `Me.Toolkit.Maui.Hosting` and `Me.Toolkit.Maui.WebHostPatch`, which completes the port. One
package was added, `Microsoft.Extensions.Configuration.Json`; the other two libraries have no NuGet
dependencies at all.

Two facts settled the designs, both established by running code rather than reading about it — and
one of them only after the first attempt failed:

- **MAUI already registers an `IHostEnvironment`.** That was Phase 0's open question. It is
  `MauiHostEnvironment`, and it always reports `Production`.
- **Its `EnvironmentName` setter throws `NotImplementedException`.** The probe that found the
  registration checked `CanWrite` — true, because the interface declares a setter — and concluded
  the name could simply be assigned. It cannot. Only calling it says so, and what said so was a test.
  `Me.Toolkit.Maui.Hosting` wraps the platform environment instead, delegating everything but the name, so
  `ApplicationName` and `ContentRootPath` still come from the platform.

### Me.Toolkit.Maui.Configuration

`AddAppPackageJson` reads a `MauiAsset`; `AddEmbeddedResourceJson` reads an embedded resource, which
is the path Xamarinme offered and still works. Both layer `appsettings.{environment}.json` over the
base file. The options object that could be left half-filled is gone, and with it the three ways
Xamarinme turned a mistake into a `NullReferenceException` out of `Build()` — or, for a wrong
prefix, into an empty configuration and no error at all.

The parsing is `Microsoft.Extensions.Configuration.Json`'s. Xamarinme vendored a copy of Microsoft's
Newtonsoft-era parser and froze it; this references the maintained one. That is the opposite call
from `Me.Toolkit.Maui.Nfc`'s, and deliberately so: NdefLibrary was a dead 2017 package, while this is a live
first-party component, and reimplementing it would have been the same vendoring mistake in newer
clothes.

**A guess got corrected here too.** The obvious expectation was that Microsoft's parser renders a
JSON `true` as `"true"` where Newtonsoft rendered `"True"`. It does not — `JsonElement.ToString()`
also produces `"True"`, and the test asserting otherwise failed. The one real difference is a JSON
`null`: `""` before, absent now. The Xamarinme README's claim that
`Configuration["Logging:IncludeScopes"]` reads `false` was simply wrong, and was never about the
parser being old.

The app-package path has no coverage: MAUI's `FileSystem` on the net10.0 slice is the
reference-assembly stub and throws. Both paths funnel into the same layering code, so what is
untested is the four lines that open the asset.

### Me.Toolkit.Maui.WebHostPatch

No packages, no forks — a `Microsoft.AspNetCore.App` framework reference and nothing else.
`IMeToolkitMauiWebHost` starts and stops a `WebApplication` on Kestrel and reports the address it bound to,
read from the server rather than the options so a `Port` of 0 reports the port the operating system
chose. Listening on every interface reports the machine's LAN address rather than `0.0.0.0`.

`NetworkAddress.GetLocalAddress()` replaces the demo's `NetworkHelper`, with its bug fixed: that
version picked the first qualifying interface and only then filtered its addresses, so a machine
whose first interface held nothing but a link-local address reported none at all.

**These tests start a real Kestrel and make real requests to it** — bind, serve, stop, rebind the
freed port, restart. The claim that .NET 10 needs no patch is worth more demonstrated than asserted.
Doing it turned up that `ListenLocalhost(0)` is refused by Kestrel, since it binds both loopback
addresses and they would get different ports; binding `127.0.0.1` explicitly is the fix.

### Documentation is now generated and enforced

`GenerateDocumentationFile` is on for the four `Me.Toolkit.Maui.*` libraries, so CS1591 requires every public
member to be documented and the packages will ship IntelliSense. Blazorme had to leave this off
because its surface had no XML docs at all; here they came with the code. Keyed on project name
rather than `IsPackable`, because `Directory.Build.props` is imported before the project body sets
it.

176 tests, green in Debug and Release. Every new test was proven by breaking what it guards:
reversing the configuration overlay order failed 1, ignoring the requested environment name failed
7, and reporting the configured port instead of the bound one failed 4.

## Phase 4 — legacy retired, CI added

`legacy/` and `Legacy.Tests` are deleted. Everything they characterized has been ported, and the
only thing they were still doing was holding the build hostage: `Xamarinme.WebHostPatch`'s ASP.NET
Core 2.2.0 pins carry a critical and a moderate advisory, and `-warnaserror` turns those into
errors.

The build is now **clean with `-warnaserror` in Debug and Release**, and CI enforces it.

The API baseline lost its three `Xamarinme.*` assemblies. That is the second deliberate baseline
change of the modernization, and the diff was reviewed to confirm it removed exactly those and
touched nothing under `Me.Toolkit.Maui.*`.

Two pieces of machinery got simpler with the fork gone:

- `TestAssemblies` no longer carries a legacy assembly list, a project-folder map or a separate
  framework-agnostic lookup. One path finds any assembly.
- `SimpleNameResolver` stays, but not for the reason it was written. It replaced
  `PathAssemblyResolver` because the forked `Microsoft.Extensions.Primitives` made the requested
  version unfindable; it earns its place now because a reference assembly and the runtime assembly
  it stands in for need not agree on version, and a metadata dump does not care either way.

### The workflow

One job on **`windows-latest`**, deliberately, not `ubuntu-latest`. `Directory.Build.props` drops
`net10.0-ios` and `net10.0-maccatalyst` on Linux and `net10.0-windows10.0.19041.0` everywhere but
Windows, so a Linux job would build two slices per library instead of five — and
`MultiTargetingTests` would pass while covering less than half of what ships. The iOS and Mac
Catalyst *library* slices compile on Windows; only building and signing an app for them needs a Mac,
so a macOS job becomes necessary when the demo app lands, not before.

`dotnet workload restore Me.Toolkit.Maui.slnx` installs what the solution's target frameworks need rather
than a hardcoded list that would drift from `Directory.Build.props`. That it accepts a `.slnx` at
all was checked rather than assumed.

**The workflow itself is unverified until it runs on a runner** — there is no way to execute
GitHub Actions from here, and saying otherwise would be exactly the kind of proxy this repository
keeps refusing. What *was* verified is its command sequence, run locally in order on a Windows
machine, which is the same platform the job uses.

Publishing is not set up yet: it needs the package versions and metadata that have not been written,
and a NuGet Trusted Publishing policy scoped to `Me.Toolkit.Maui.*` and bound to this repository.

## Phase 5 — package metadata

All four packages are ready to publish, and none of them has been. `dotnet pack` produces
`Me.Toolkit.Maui.Configuration`, `Me.Toolkit.Maui.Hosting`, `Me.Toolkit.Maui.Nfc` and `Me.Toolkit.Maui.WebHostPatch` at **26.9.8**, each
with five target-framework slices, XML documentation for every slice, a README and the icon.

Versions are date-based — `<Version>26.09.08</Version>`, which NuGet normalises to `26.9.8` —
matching Blazorme and Utilme. The tag spelling trap comes with it: a version check compares raw
csproj text, so the tag has to be `v26.09.08`.

Shared identity lives in `Directory.Build.props`; `Version`, `Product`, `Description`,
`PackageTags` and `PackageReleaseNotes` stay per project so the four can move independently.
`GeneratePackageOnBuild` is deliberately absent — Xamarinme set it, which is why building that
repository quietly produced `.nupkg` files at versions that were never published.

The release notes say what each package replaces and what changed, rather than "Creation." — which
is what `Xamarinme.WebHostPatch` 1.0.0 shipped with. `Me.Toolkit.Maui.Configuration`'s names the one
behavioural difference a Xamarinme user would trip over: a JSON `null` used to read as an empty
string and is now simply absent.

**Verified by unzipping the four packages**, not by reading the build log: id, version, icon,
readme, copyright, tags, dependency groups, all five slices, all five XML files, and the icon
compared byte for byte against `doc/me.png`. A plain build was confirmed to emit no package at all.

### The Xamarinme packages stay as they are

Decided rather than defaulted. `Blazorme.TestHost` looked like a precedent — a broken 1.0.0 rescued
by multi-targeting a new version back to net8.0 — but it does not transfer: that worked because it
was the **same package ID**, so existing consumers could resolve a newer, working version of the
package they already referenced. `Xamarinme.WebHostPatch` and `Me.Toolkit.Maui.WebHostPatch` are different
IDs, and NuGet has no redirect between IDs at any target framework. Targeting further back would not
help either, since the implementation is `WebApplication` on ASP.NET Core 10.

The only mechanism that crosses package IDs is deprecation with an alternate package, and the
decision is not to deprecate.

## Decisions taken before Phase 0

- **Fresh git history.** Me.Toolkit.Maui does not carry Xamarinme's 153 commits.
- **All four libraries are ported.** The recommendation was `Me.Toolkit.Maui.Nfc` alone — Hosting and
  Configuration are `MauiAppBuilder`, and WebHostPatch's two reasons to exist are both gone on
  .NET 10 — and the decision was to port all four anyway. `Me.Toolkit.Maui.WebHostPatch` will therefore be a
  Kestrel-in-MAUI convenience layer, not a fork of Microsoft code.
- **No deprecations.** The `Xamarinme.*` packages stay on nuget.org as they are.
- **Platform-agnostic tests only.** Unit tests for the shared slices plus the metadata API baseline;
  no device test harness.

## Phase 6 — the demo app, and a correction

One MAUI app replaces Xamarinme's three Xamarin.Forms solutions across 17 projects: four tabs, one
per library, on Syncfusion's `SfTabView` with ReactiveUI view models. It is in the solution and
deliberately out of CI.

### Me.Toolkit.Maui.WebHostPatch was rebuilt, because Phase 3 got it wrong

Phase 3 rebuilt the library on `WebApplication` and a `Microsoft.AspNetCore.App` framework
reference, and recorded that "a framework reference is all you need on .NET 10". **That is false on
every mobile platform**, and it made the package a trap: it would install into a MAUI app and break
the Android build with `NETSDK1082`.

The mistake was a verification one. The Phase 1 probe compiled a *library* targeting
`net10.0-android` against the ASP.NET Core reference assemblies, which works for any target
framework, and I concluded the capability was there. Deployment needs a **runtime pack**, and
`Microsoft.AspNetCore.App.Runtime.android-arm64`, `.ios-arm64` and `.maccatalyst-x64` do not exist —
they 404 on nuget.org. Nothing failed until an *app* was built, in this phase. A reference assembly
is not a deployment, and "it compiles" is not "it runs".

The fix is to do what Xamarinme did, because it was right: **ASP.NET Core as netstandard2.0
packages**, which are copied into the app like any assembly and need no runtime pack. That is the
whole reason the original worked on Xamarin, and the author's write-up says so directly — Xamarin
supported netstandard2.0, so the 2.2 line was the last one it could consume.

What improves on the original:

- **The 2.3.x servicing line instead of 2.2.0**, which is advisory-clean. 2.2.0 carries a critical
  Kestrel advisory and a moderate IIS one.
- **The Primitives fork is gone.** `Microsoft.Net.Http.Headers` 2.3.11 no longer references
  `InplaceStringBuilder`, so there is nothing left to shadow. Checked by grepping the assemblies —
  after the first attempt used `strings`, which is not installed on this machine and reported zero
  for everything including the 2.2.0 assemblies that provably contain it. The instrument check
  caught two false findings.
- **The `WebHostExtensions` fork is unnecessary.** `Console.CancelKeyPress` is still called in
  2.3.11, but from `RunAsync`; starting and stopping the host explicitly never enters that path, and
  blocking until shutdown is not what an app wants. The generic host's `ConsoleLifetime` is not
  involved either.

Verified by building: a `net10.0-android` MAUI app consuming the netstandard2.0 web library builds
clean with zero warnings and no runtime pack, and the demo app now builds for all four platforms.
Not verified: that it *runs* on a device. That needs hardware.

### ReactiveUI 24 is not the ReactiveUI anyone remembers

Three surprises, each found by a failure rather than by reading:

- **It has dropped System.Reactive** and reimplemented the operators on its own primitives. Adding
  Rx.NET alongside makes every `Select`, `Merge` and `Subscribe` ambiguous, so it is ReactiveUI's
  operators or Rx's, not both. `Signal.FromEventPattern` and `ObserveOn(ISequencer)` cover what the
  demo needs.
- **`ReactiveCommand<Unit, Unit>` is now `ReactiveCommand<RxVoid, RxVoid>`**, and `RxApp` is gone;
  the UI-thread sequencer is supplied explicitly, which is better anyway.
- **It must be initialized explicitly.** Without `RxAppBuilder.CreateReactiveUIBuilder()...BuildApp()`
  the first `WhenAnyValue` throws and the app fail-fasts at startup with no output. ReactiveUI's own
  error message suggests a call order that does not compile.

`ReactiveUI.Maui` was not used: it requires `Microsoft.Maui.Controls` 10.0.100, and the installed
workload's `$(MauiVersion)` is 10.0.20, so taking it would mean bumping MAUI across the repository
to satisfy a demo. The core package has no MAUI dependency.

### The demo really starts

Building an app is not running one. The Windows head was launched and watched: the first attempt
fail-fasted with exit code `0xC0000409` and no output, which was the missing ReactiveUI
initialization. A stock MAUI template app was run first to establish that the environment was not at
fault. After the fix it stays up, which exercises the configuration load, the DI graph, all four
view models, the Syncfusion XAML and the ReactiveUI initialization.

### CI nearly tested nothing

The first version of the test step passed `-warnaserror` to `dotnet test`. MTP forwards flags it
does not recognise to the test executable, which ran **zero tests**. It exits 5, so the job would
have failed rather than passing silently — but the lesson stands, and the comment in `ci.yml` now
names the specific mistake rather than the general rule.

CI builds the four libraries by name rather than the solution, because the demo is in the solution
and out of CI.

## Phase 7 — the web host on a device, and the address it reported

`Me.Toolkit.Maui.WebHostPatch` had never actually served a request. Everything about it was argued from build
output: the `NETSDK1082` reasoning, the choice of netstandard2.0 packages, the claim that the two
Xamarinme forks were unnecessary. All of it was correct, and none of it was evidence.

Run on a physical Android device, Kestrel started, served over both USB (`adb forward`) and real
Wi-Fi, stopped, released the port, and started again on the same one. That is the claim settled:
ASP.NET Core 2.3.x netstandard2.0 packages, on .NET 10's Android runtime, answering requests.

### The address was wrong

The demo displayed:

```
OPEN THIS FROM ANOTHER DEVICE
http://0.0.0.0:5001/
```

`NetworkAddress.GetLocalAddress()` required `NetworkInterfaceType.Ethernet` or `Wireless80211`. .NET
classifies an interface on Linux by reading `/sys/class/net/<name>/type`, and Android's SELinux
policy denies that file — to an app, and even to the more privileged `shell` user. The device
reported:

```
wlan0 |Unknown |Up|[192.168.1.16]
dummy0|Unknown |Up|[]
lo    |Loopback|Up|[127.0.0.1]
```

So the filter rejected the only working interface and the code fell back to the bound `0.0.0.0`.
Interface type now orders candidates instead of qualifying them, and loopback is excluded by address
— a check that holds whether or not classification works. `dummy0` is why every interface is
searched rather than the first plausible one.

Android does classify `lo` correctly, so the loopback check is defensive rather than the thing that
was broken. An earlier version of this entry, the code comment and the README all claimed otherwise;
the device data is what corrected them.

### The test that could not have caught it

It asserted "routable IPv4, **or null**", and null is exactly what Android returned. Allowing null
was meant to tolerate a build agent with no network; what it tolerated was the defect. It is
rewritten into six tests over the selection rules, which need no network at all. Two of them were
watched failing against the old logic — `expected 192.168.1.16, found null`, the same symptom seen
on the phone — before the fix was restored.

Testing those rules needs interfaces that cannot be constructed: `NetworkInterface` is abstract and
its address collections have internal constructors. Selection therefore takes a candidate record,
exposed to the tests through `InternalsVisibleTo`.

### `adb install` does not deploy your code

Between the fix and the evidence sat an hour of wrong conclusions. `adb install -r` reported
**Success** twice while the app kept running hour-old C#. .NET Android **Debug** builds use fast
deployment: assemblies are pushed to `/data/data/<pkg>/files/.__override__/<abi>/` and are *not*
packed into the APK, so installing the APK updates only the native shell. The device's
`Me.Toolkit.Maui.WebHostPatch.dll` was timestamped 15:00 against a 15:09 build.

Deploy with `dotnet build DemoApp/DemoApp.csproj -c Debug -f net10.0-android -t:Install`, and check
`adb shell run-as <pkg> ls -la files/.__override__/<abi>/` before concluding anything about
behaviour on device.

The failure mode is worth naming: the evidence said the fix did not work, and the honest reading of
that was that the diagnosis was wrong. It was the deployment that was wrong. Temporary
instrumentation in the demo — dumping every interface into the status label — is what separated the
two, and it is the same technique that found the ReactiveUI initialization crash in Phase 6.

## Phase 8 — publishing

`publish.yml`, modelled on Blazorme's, with the same single-workflow rule: a NuGet Trusted Publishing
policy binds to one workflow file *and* one repository, so one file covers every package and every
future version.

Two things differ from Blazorme.

**It runs on `windows-latest`, and that decides package contents.** `Directory.Build.props` builds
`net10.0-ios` and `net10.0-maccatalyst` only off Linux, and `net10.0-windows10.0.19041.0` only on
Windows. Packing on `ubuntu-latest` would produce a perfectly valid package containing two target
frameworks instead of five, publish it with a green tick, and strand iOS, Mac Catalyst and Windows
consumers permanently, since a published version cannot be replaced. The job therefore ends by
opening each `.nupkg` and failing if any expected slice is missing — the `runs-on` line is the
consequence, that step is the rule. It was checked both ways: against the three real packages, and
against a synthetic package built the way Linux would build it.

**`Me.Toolkit.Maui.Nfc` is held back.** It builds, ships in the demo and has tests, but its reading session is
unfinished and has never been exercised against a physical tag. The project sets `IsPackable=false`,
so `dotnet pack` cannot produce it by accident, and there is no `nfc-v*` trigger. It is still *built*
by the publish job, because `PublicApiSurfaceTests` and `MultiTargetingTests` read every library's
assemblies off disk and fail outright if it is missing.

The tag resolver and the version check were exercised locally across every tag shape, including the
ones that must be rejected. `v26.09.08` is the tag; `v26.9.8` fails the version check, because the
comparison is against the raw csproj text and NuGet normalises only afterwards.

## Phase 9 — the name, and a package that carried the old one

`Mauime` cannot be published. nuget.org refuses any package ID beginning with `Maui`, and there is no
API to ask in advance — the only way to learn a prefix is reserved is to be refused by a push. That
took four attempts to establish, and two of them failed on mistakes in the workflow rather than on
anything about the packages:

| Attempt | Result |
|---|---|
| `Mauime.*` | `409 The package ID is reserved` — reported as success, because `--skip-duplicate` cannot tell a refusal from a genuine duplicate |
| `Toolkit.Mauime.*` | `403` — the Trusted Publishing policy still had the old glob |
| `Toolkit.Mauime.*` | `409 This package ID has been reserved` — `Toolkit.*` belongs to someone too |
| `Me.Toolkit.Maui.*` | `201 Created` |

`Toolkit.*` was chosen after observing that `Toolkit.Data` and `Toolkit.CodeBase` exist and
concluding the prefix was free. That inference was wrong in a way worth recording: a reservation
blocks only *new* IDs, which is the very reason `Xamarinme.*` survives while `Mauime.*` does not. The
same rule was applied in one direction and not the other.

`--skip-duplicate` no longer runs on a tag push. It exists for deliberately re-running a release
that is already out, and it turned three hard refusals into a green tick claiming success.

### 26.9.8 shipped the new ID around the old assemblies

To find out whether a prefix was acceptable, only `<PackageId>` was changed — four lines, rather
than renaming the repository before knowing the name was usable. That was the right order, and its
consequence was missed: 26.9.8 published as `Me.Toolkit.Maui.Configuration` containing
`lib/net10.0/Mauime.Configuration.dll`. Consuming it meant writing `using Mauime.Configuration;`
against a package called `Me.Toolkit.Maui.Configuration`.

The rename landed afterwards and the *new* packing was verified, but the already-published version
was never re-examined. 26.9.9 is the same code with the assemblies and namespaces matching the ID;
26.9.8 is unlisted.

The check that would have caught it — read what is inside the package that is actually on nuget.org,
not the one just built — is worth more than any of the build-time verification around it.

`publish.yml` now opens every `.nupkg` before pushing and fails if an assembly name does not match
the package ID. It was checked against the real 26.9.9 packages and against a synthetic package
carrying exactly the 26.9.8 mismatch.

### Two more things the rename broke

**The Trusted Publishing policy matches on repository name, not ID.** Renaming the repository left
the policy pointing at `Mauime`; the next publish failed at the token exchange with *"No matching
trust policy owned by user 'melihercan' was found"*, while the policy still displayed as Active. It
cannot be repaired by editing — the edit form has no Repository field — and recreating it with the
old name reproduces the fault silently, because GitHub redirects the old name and nuget.org stores
what it was given. The wiki had claimed a rename was safe, on the strength of an inference from
Blazorme's setup rather than anything verified.

**Validation is not instant.** 26.9.9 was accepted with `201 Created`, then took about three hours
to become downloadable. In between, the version is taken but absent everywhere public, so a push
that succeeded looks exactly like one that failed. Fifteen minutes of polling was read as evidence
of failure; it was evidence of nothing.

## Phase 10 — Me.Toolkit.Maui.Sizing

The first library that is not a port. MAUI sizes are absolute; this adds relative ones —
`WidthRequest="{me:Relative 30}"` is 30% of the parent — against the parent, the window, the
display, a named element or an ancestor type, with `Min`/`Max` clamping, recomputed as the reference
changes. It is a markup extension rather than the converter first proposed, because a converter
never learns which element it is sizing. See [Design Notes](Design-Notes) for the shape.

It takes `Microsoft.Maui.Controls`, the only library to, and no other package.

**Behaviourally tested on the net10.0 slice**, which Nfc could not be: Controls' elements, windows,
bindings and XAML loader all run there. The first run failed every test in which a size changed
after binding — a binding delivers updates through a dispatcher, and a test process has none — so
the tests install an inline one, as MAUI's own do. Four mutations — ignoring padding, keeping the
display subscription, not watching ancestors, ignoring `Window` — each turned tests red.

**Running the demo on Windows found what the tests could not**, since the net10.0 slice has no
idiom. The obvious `{me:Relative {OnIdiom ...}}` crashed at startup: MAUI's XAML source generator
passes a positional markup-extension argument no target property, so `OnIdiom` cannot tell it is
producing a `double`. Named, `Percent={OnIdiom ...}`, it works. The other nesting,
`{OnIdiom Desktop={me:Relative 30}}`, cannot work at all — the element is hidden behind an internal
interface and the result is applied with `SetValue` — so it now throws a message giving the spelling
that does. The crash was diagnosed by logging the unhandled exception to a file, the same technique
as Phase 6.

Verified on the running app, by reading each element's laid-out width back into a label: the 30%
bar followed the window from 197 to 397 to 81; the `OnIdiom` bar stayed at exactly 4/3 of it at
every size; the window-relative font held at its `Max` of 40 and its `Min` of 12; and 15% of the
display's shorter side read 216, which is 15% of 1440 — a 4K monitor at 150% — so the
pixel-to-device-independent conversion is right. iOS and Android are compile-verified only.

Not published at first; the `sizing-v*` trigger was added once it had run on a phone.

`Portrait` and `Landscape` followed, for a different percentage per orientation. Orientation is the
window's shape rather than the reference's — a card is landscape-shaped on a portrait phone — and
three mutations (taking it from the reference, not listening to the window, counting a square as
landscape) each failed tests. On the running demo the same bar read 685 in a landscape window and
711 in a narrower portrait one: the window shrank and the bar grew, because the percentage switched
from 45 to 90.

Then `To=Self` for aspect ratios, `Offset` for `calc()`, and `RelativeSizing` attached properties so
a `Style` can carry a relative size. Two findings:

- **A removed binding leaves its last value behind**, and `ClearValue` does not take it back — for
  any MAUI binding, shown side by side with a plain `Binding`. A label taken out of a relative style
  kept its last font size. `ClearRelativeSize` now makes the binding push the default first.
- **MAUI's runtime XAML loader is not thread-safe on first use.** With two test classes loading
  XAML, one run in three failed with a duplicate-key error in its assembly cache. They now share an
  xUnit collection; fifteen consecutive runs were clean. An app loads XAML on the UI thread and never
  meets it.

Six mutations, and one survived at first: not stopping a replaced tracker changes nothing visible,
because its binding is already gone. A test now counts the display's listeners instead. On the
running demo a 16:9 box read 762 × 429 and 195 × 110, two halves with `Offset=-4` filled their row
exactly at 758 + 8 + 758, and a style-applied headline sat at its `Max` of 48 and at 18.7 in a
narrow window.

### On a phone, rotated

Everything above was verified by resizing a desktop window. Then it ran on a Samsung Galaxy A17
(Android 16, 1080×2340 at 450 dpi, so 384×832 device-independent), rotated by hand, and read from
`adb exec-out screencap` screenshots of the same measured-value labels:

| | Portrait | Landscape |
|---|---|---|
| `Portrait=90, Landscape=45` | 290 — 90% of 322 | 309 — 45% of 687 |
| 30% of the parent | 97 | 206 |
| 3% of the window, clamped 12–40 | 12.0, at `Min` | 25.0 — 3% of 832 |
| 15% of the display's shorter side | 58 | 58 |
| `OnIdiom Phone=80` | 258 | 549 |
| 50%, `Offset=-4`, twice | 157 each | 340 each |
| style, 4% of the window | 15.4 | 33.3 |
| 16:9, `To=Self` | 161 × 91 | not captured |

The app kept its process through the rotation, so these are live recomputations, not a restart.
The display row is the one only a device could settle: 58 in both orientations is 15% of
1080 px ÷ 2.8125, so the pixel-to-device-independent conversion holds on Android, and `Shorter`
does not change on rotation. `OnIdiom` picked the phone value on a real phone.

Rotated back to portrait, every value returned exactly — 290, 258, 161 × 91, 157 — in the same
process. Not checked: iOS.

## Phase 11 — publishing Sizing, and a version that was never stamped

`Me.Toolkit.Maui.Sizing` 26.9.26 was the first push of a new package ID, and the Trusted Publishing
policy accepted it. CI had been green, the publish job's checks had passed, and nuget.org answered
`201 Created`.

Unzipping what nuget.org then served found the fault none of that could: the `net10.0` assembly
read **`1.0.0.0`**, the other four `26.9.26.0`. The three packages released at `26.9.14` had it too,
so it dated from `bf35edd`, which took the version out of the csprojs and passed it to build and
pack — but not to test. `dotnet test` rebuilt the `net10.0` slice of each referenced library without
it, over the stamped build, and `Pack --no-build` packed the result. Reproduced locally before
anything was changed.

The test project is now built with the version and tested with `--no-build`, and a new publish step
reads the version out of every assembly; it was run against the served 26.9.26, which it fails, and
a 26.9.27 built the fixed way, which it passes. All four packages were re-released as `26.9.27` —
identical code, since nothing in the three older libraries had changed since `26.9.14` — and the
served packages were unzipped again, every assembly reading `26.9.27.0`.

The release notes first said 26.9.26 "is unlisted", and were corrected before tagging: unlisting is
done by hand, later, and the notes are baked into the package for good.

## Phase 12 — a Mac, an iPhone, and what they found

The demo app's 18 compiled-binding warnings (`MAUIG2045`) had one cause: each tab set its
`BindingContext` to a child view model while its bindings were checked against `MainViewModel`.
Declaring the child's type on the tab's root fixed all of them; the generated code confirmed the
`BindingContext` binding itself still compiles against the parent. The tabs now bind without
reflection, and the demo builds with no warnings on Windows and Mac Catalyst.

**Mac Catalyst and iOS ran for the first time** — on a Mac mini with macOS 15.8 and Xcode 26.3, and
an iPhone XR on iOS 18.7. On Mac Catalyst the whole demo came up: Configuration read its
`appsettings.json` from the app package, a path never run anywhere else, and Hosting reported
Development. Values were read through the macOS accessibility tree, since an SSH session cannot
capture other apps' windows without a Screen Recording grant.

It found a real bug in Sizing: window- and display-relative sizes were 23% small, because Mac
Catalyst shows an iPad-idiom app scaled to 77% and reports the window and display in Mac points. The
first fix — measure the root page instead — sent every window-relative size to zero, because the
demo's root is a `Shell`, which reports no size; the tests had used a `ContentPage` and passed. The
second converts with Apple's 0.77. Verified on the Mac in a landscape and a portrait window: 3% of a
1024-wide window read 30.7 before and 39.9-equivalent after (clamped at 40), 23.4 at 600 wide, the
display 162 before and 210 after.

On the iPhone, signing needed the Mac's login keychain, which an SSH session holds locked; the
owner ran a script that unlocks it and builds in one session, so no password passed through the
automation. The NFC entitlement was left out of that build, because a wildcard development profile
cannot grant it. Every Sizing card matched its expected value in portrait and in landscape, as read
off the phone by its owner — iOS 17 and later need a root-owned developer tunnel to screenshot
remotely.

Then three additions: `Breakpoints` choosing the percentage by window width, `Round`, and relative
`Margin` and `Padding`. On Windows the breakpoint bar read 90%, 60% and 35% across the three ranges.
`Round` was found to give whole units but not whole pixels: at 150% scaling a requested 469
rendered as 469.333, which is 704 pixels, and rounding near 100% can overshoot the parent by half a
unit. The documentation says so rather than promising sharper rendering. How a relative font
combines with the system's text size is documented from MAUI's behaviour, not measured.

That finding turned into `Round=Pixels`, and three more followed, tested on Windows while the
platform run waits for the full set:

- **`Round` became `None`/`Units`/`Pixels`.** `Pixels` rounds with the window's density: requested
  591.3333 rendered 591.3333 — 887 pixels at 150% — where `Units` had been moved by a third of a
  unit. It re-rounds on `DisplayDensityChanged`.
- **`Sides`** puts a relative margin or padding on chosen sides only.
- **`BreakpointsBy=Reference`** compares the reference's width, like a CSS container query.
- **`To=Display` measures the window's own monitor on Windows**, through WinUI's `DisplayArea`.
  Dragged between a 3840-wide landscape monitor and a 2160-wide portrait one, 5% of the display's
  width read 128 and 72.

Grid column and row sizes were considered and left out: a `ColumnDefinition` has nothing to measure
against, and a percentage of its own grid is star sizing.

Then a safe-area option and a build-time analyzer. `SafeArea=True` subtracts the notch and system
bars from `To=Window`, read from each platform since MAUI reports them nowhere cross-platform; it is
tested against a stand-in iPhone in landscape and awaits the device run. The analyzer,
`Me.Toolkit.Maui.Sizing.Analyzers`, reads the app's XAML and reports five rules. It is the one
package added in this phase — `Microsoft.CodeAnalysis.CSharp` 4.14.0, which an analyzer cannot do
without — and it ships inside the Sizing package, whose only dependency is still MAUI. Two things it
taught: MAUI's XAML compiler already errors on an unknown option name, so the analyzer earns its
keep on what compiles and then fails at runtime; and diagnostic messages are format strings, so
the braces in `{me:Relative}` have to be doubled — a test formats every message for that reason.

## Phase 13 — the platform run

Every feature on every platform: an Android 16 phone and an iPhone XR in both orientations, Mac
Catalyst in a restored and a resized window, Windows across two monitors. It found three things the
tests and Windows could not:

- **`SafeArea` read zero on Android.** An element joins its window before Android has computed the
  insets, and the window never resized to prompt a second look. It now reads `WindowMetrics` on
  Android 11 and later, which know the insets at any time, and looks again 150 ms after joining a
  window. Then: portrait height 83 against a safe 75, landscape width 83 against a safe 75 — the
  bars moving from top and bottom to the sides as the phone turned.
- **Mac Catalyst's `Window.Width` changes units.** Restored at launch it is in layout units, after a
  resize in Mac points, so a fixed conversion was wrong one way or the other: window-relative sizes
  read 30% large until the first resize. The window is now measured from `UIWindow.Bounds` on iOS and
  Mac Catalyst; correct at launch (779 for a 600-point window) and after a visible resize to 1300 ×
  700 (1688).
- **Decimals nobody wanted.** A 15.4 font and a 300.444 width prompted the owner to ask what they
  were for. Whole units are now the default.

CI then failed one test the local runs never had: a size expected to be 33 read -1, "differed by
34". Not rounding — -1 is an unset `WidthRequest`. MAUI holds a child's parent weakly, and a test
helper built a `Grid` around an element and kept only the element; under CI's parallel load a
collection landed between the helper returning and the size being applied, and the grid was gone.
Reproduced by collecting at exactly that point, which gave CI's message word for word. The test
helpers now keep everything they arrange, and a test collects there on purpose. An app is not
affected: its page tree holds every parent.

On the iPhone, the notch: 10% of the landscape width read 90, and 80 with `SafeArea` — 896 less 48
on each side. Values were read off the phone through a mirror, since iOS will not screenshot
remotely without a root-owned tunnel.

Released as **26.9.28**. The first publish run failed at its build step, before anything was
pushed: publish.yml builds the test project with `-warnaserror` and the analyzer test had two
`xUnit1051` warnings that CI's lenient test build had let through. CI now builds the test project as
strictly; the unreleased tag was moved to the fix. What nuget.org served was then unzipped: all
five `lib` assemblies and the analyzer at 26.9.28.0, the analyzer under `analyzers/dotnet/cs`, and
`Microsoft.Maui.Controls` still the only dependency.

## Settled, and not to be reopened

- **`26.9.8` is the version.** Date-based, matching Blazorme and Utilme. Publishing it closes the
  door on ever shipping a `1.x`, and that was weighed and accepted.
- **`Me.Toolkit.Maui.*` targets .NET 10 only.** No reaching back to `net8.0` or `net9.0`. MAUI apps on older
  .NET cannot use these packages, which is a deliberate limit rather than an oversight — there are
  no consumers to strand, and `Me.Toolkit.Maui.WebHostPatch` would need ASP.NET Core 8/9 rather than 10.
- **The `Xamarinme.*` packages stay on nuget.org as they are**, and are not deprecated. See Phase 5
  for why `Blazorme.TestHost` is not a precedent for rescuing them.
- **No PC/SC.** Mac Catalyst and Windows throw `PlatformNotSupportedException` rather than pulling
  in three `PCSC` packages for an external USB reader.

## What is still open

Closed since this list was first written: the Trusted Publishing policy exists and has published
every release since `26.9.9`; `Me.Toolkit.Maui.Sizing` was added and published; the wiki publishes
itself from `wiki/`; the demo has run on Mac Catalyst and an iPhone.

- **The `Me.Toolkit.Maui.*` prefix reservation.** Not yet requested; it is an email from the owner to
  nuget.org. See [Publishing](Publishing#reserving-the-prefix).
- **Unlisting the 1.0.0.0 releases.** `26.9.14` of Configuration, Hosting and WebHostPatch and
  Sizing's `26.9.26` are superseded by `26.9.27` but still listed. Deferred by decision; done by hand
  on nuget.org.
- **`Me.Toolkit.Maui.Nfc`.** Unfinished, unpublished, and never exercised against a physical tag. The
  Android foreground-dispatch path and the CoreNFC session have no behavioural coverage; the demo's
  NFC tab renders but has not been used to read anything.
- **iOS and Mac Catalyst beyond the demo.** The demo has now run on both, and Sizing is verified
  there feature by feature; but only Configuration, Hosting and Sizing were exercised; `Me.Toolkit.Maui.WebHostPatch` has not served a request on
  either, and its note that iOS stops a backgrounded server is inherited from the Xamarin era.
