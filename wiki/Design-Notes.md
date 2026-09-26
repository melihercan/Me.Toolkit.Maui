# Design Notes

Why the repository looks the way it does, and the invariants any change must preserve.

## The governing constraint is the opposite of Blazorme's

The `Me.Toolkit.Maui.*` package IDs are **new**. Nothing on nuget.org resolves them, so there are no
consumers and no additive-only guarantee. The API is free to be fixed properly — `CrossNfc.Current`
can become DI registration, a hand-wired `MainActivity.OnNewIntent` can become
`ConfigureLifecycleEvents`, and a dead 2017 dependency can come out of the public surface.

The API baseline still exists, and matters more here than it did in Blazorme, for a different
reason: **so that changing the surface is a decision rather than an accident.** Every change to it is
a diff to review in the commit that causes it.

## The API baseline

`PublicApiSurfaceTests` renders every public type, member signature, generic arity, enum numeric
value, `const` literal and default parameter value into `Me.Toolkit.Maui.Tests/PublicApi.approved.txt`.

It reads **metadata only**, through `MetadataLoadContext` over the built assemblies, rather than
referencing the projects. In Blazorme that was because two libraries targeted `net5.0`. Here it is
forward-looking: the ported libraries will target **MAUI platform frameworks** — `net10.0-android`,
`net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0` — and a plain `net10.0` test
project cannot reference any of them. The machinery was established in Phase 0, while the imported
Xamarin libraries were still plain `netstandard2.0` and easy, so that the baseline would survive the
port. It did.

When a change is intentional, review the diff and copy `PublicApi.received.txt` from the test output
directory over `PublicApi.approved.txt`, in the same commit. Never weaken the assertion.

The baseline renders each Me.Toolkit.Maui library from its **`net10.0`** slice only. Dumping all five slices
would make the approved file four times longer and mostly repetition, so `MultiTargetingTests` holds
the platform slices to the neutral one instead: same public surface, every framework. An
Android-only member added by accident — the easy mistake when half a plugin lives behind
`#if ANDROID` — fails there. A platform-specific member added *on purpose* means rewriting that test
to name the exception, in the commit that adds it; it must not be weakened into a subset check.

Two things make reading a platform slice work at all:

- **One `MetadataLoadContext` per assembly.** The slices of a single library are compiled against
  different worlds, and an Android `System.Runtime` and an iOS `System.Runtime` cannot share a
  resolver that matches on simple name. A single shared context silently resolves types out of
  whichever pack was enumerated first.
- **`Me.Toolkit.Maui.ReferencePaths.txt`**, written beside every assembly by `Directory.Build.targets`. A
  library build does not copy its dependencies, so `Mono.Android`, `Microsoft.iOS` and the rest are
  nowhere near `bin/`. Guessing at the workload's reference-assembly folders would be a proxy;
  the `ReferencePath` item is what the compiler was actually handed.

That this works is verified rather than assumed: a temporary type exposing
`Android.Nfc.NfcAdapter`, `CoreNFC.NFCNdefReaderSession` and
`Windows.Devices.SmartCards.SmartCardReader` rendered correctly in each slice, from a `net10.0` test
project that can reference none of them.

### `SimpleNameResolver`, and why `PathAssemblyResolver` is not used

`PathAssemblyResolver` matches on version as well as simple name, which is more precision than a
metadata dump needs and more than the toolchain reliably offers — a reference assembly and the
runtime assembly it stands in for need not agree on version, and rendering a type name does not
depend on telling them apart.

It was originally replaced for a blunter reason: `legacy/WebHostPatch` dropped a fork of
`Microsoft.Extensions.Primitives` stamped 5.9.0.0 into every output directory that referenced it,
shadowing the real assembly, so the version the metadata asked for was nowhere on disk and the
baseline threw `FileNotFoundException`. That fork is gone; the resolver stays on its own merits.

A slice resolves only against its own `Me.Toolkit.Maui.ReferencePaths.txt`, with nothing from the host
runtime mixed in, so an Android slice takes `System.Runtime` from the Android ref pack rather than
from whatever runtime the tests happen to be running on.

## The shape of a Me.Toolkit.Maui library

All four are the same shape, and the shape was settled by building rather than by reading:

- **`Microsoft.Maui.Core`, not `Microsoft.Maui.Controls`.** Core carries `MauiAppBuilder`, the
  `LifecycleEvents` builders and `Microsoft.Maui.ApplicationModel`. `Microsoft.Maui.Essentials` has
  none of the first two. An NFC plugin has no business depending on Controls, and now does not.
- **`Me.Toolkit.Maui.WebHostPatch` takes ASP.NET Core as netstandard2.0 packages**, never the
  `Microsoft.AspNetCore.App` framework reference — that framework has no mobile runtime pack.
- **`net10.0` alongside the four platform frameworks.** It is what a non-platform project resolves,
  and for `Me.Toolkit.Maui.Nfc` it is where "this platform has no implementation" lives — the role
  `Xamarinme.Nfc` gave `netstandard2.0`.
- **`AndroidGenerateResourceDesigner=false`.** Android otherwise generates a public `Resource` class
  into every library, resources or not, and it lands in the package's public surface.

The target framework list lives once, as `$(MeToolkitMauiTargetFrameworks)` in `Directory.Build.props`,
which also turns on documentation generation for the four `Me.Toolkit.Maui.*` projects — so CS1591 requires
every public member to be documented.

## What each library is, and what MAUI already does

The first question, per library, was whether it should exist at all. Verified rather than assumed —
a `net10.0-android` probe project was built to check the MAUI API, and the imported Xamarin projects
were built to check what still compiled.

### `Xamarinme.Hosting` → `Me.Toolkit.Maui.Hosting`, ported and much smaller

`XamarinHostBuilder` exposed Configuration, Services, HostEnvironment and Logging, and a `Build()`.
`MauiAppBuilder` exposes the same, member for member. `XamarinHostConfiguration` is a copy of
Blazor's `WebAssemblyHostConfiguration`; `ConfigurationManager` is the maintained equivalent. And
**MAUI registers an `IHostEnvironment` of its own** — which Phase 0 could not confirm and Phase 3
did.

So the only gap is that MAUI's environment always reports `Production`. That is what
`Me.Toolkit.Maui.Hosting` fills, and it is the whole library.

**It wraps rather than assigns**, because `MauiHostEnvironment.EnvironmentName` throws
`NotImplementedException` from its setter. The interface declares the property as settable and the
type reports `CanWrite`, so nothing short of calling it reveals otherwise — the probe that found the
registration got this wrong, and a test caught it. Everything but the name is delegated, so
`ApplicationName` and `ContentRootPath` still come from the platform, including their failure modes.

`XamarinHost` did not come across at all: it implemented `IHost` and threw
`NotImplementedException` from both `StartAsync` and `StopAsync`.

### `Xamarinme.Configuration` → `Me.Toolkit.Maui.Configuration`, ported

Two ways in — `AddAppPackageJson` for a `MauiAsset`, `AddEmbeddedResourceJson` for the embedded
resource Xamarinme used — both layering `appsettings.{environment}.json` over the base file.

**The parser is Microsoft's, referenced not vendored.** This is the opposite call from
`Me.Toolkit.Maui.Nfc`'s, and deliberately: NdefLibrary was a dead 2017 package sitting in the public API,
while `Microsoft.Extensions.Configuration.Json` is a live first-party component. Reimplementing it
to keep a zero-dependency badge would have been the same vendoring mistake Xamarinme made, in newer
clothes. MAUI does not reference it, which is the one thing this package supplies that an app could
not already do in a line.

The rendering differences are narrower than they look, and were measured rather than guessed. Both
parsers render a JSON `true` as `"True"`, through `bool.ToString()` and `JsonElement.ToString()`
respectively — the obvious assumption that Microsoft emits the raw token is wrong, and the test that
assumed it failed. **The one real difference is a JSON `null`**: `""` before, absent now.

The options object is gone, and with it three ways to turn a mistake into a `NullReferenceException`
out of `Build()` — or, for a wrong prefix, into an empty configuration and no error at all.

**The app-package path has no coverage.** MAUI's `FileSystem` on the net10.0 slice is the
reference-assembly stub and throws, so what is untested is the four lines that open the asset; both
paths funnel into the same layering code.

### `Xamarinme.WebHostPatch` → `Me.Toolkit.Maui.WebHostPatch`, with no patch in it

The two forks existed for two Mono-era problems, and **both causes are gone**:

1. `Microsoft.Net.Http.Headers` 2.2.0 calls `InplaceStringBuilder`, which
   `Microsoft.Extensions.Primitives` 5.0 deleted. The vendored fork restores the type and stamps
   itself 5.9.0.0 so it wins at bind time. This exists **only** because of the ASP.NET Core 2.2.0
   pin.
2. `Console.CancelKeyPress` threw on Mono, so `ConsoleLifetime` and `WebHostExtensions.RunAsync`
   were forked to route around it.

**The approach is unchanged, because it is the only one that works.** ASP.NET Core comes from
netstandard2.0 packages — the 2.3.x servicing line — which are copied into the app like any
assembly. That is precisely why `Xamarinme.WebHostPatch` ran on Xamarin.

The modern-looking alternative does not work, and finding that out cost a phase. The
`Microsoft.AspNetCore.App` shared framework **has no runtime pack for android, ios or maccatalyst** —
those RIDs 404 on nuget.org. A framework reference therefore compiles a *library* against the
reference assemblies quite happily and then fails the consuming *app* with `NETSDK1082`. Phase 3
shipped exactly that mistake, because the Phase 1 probe that "verified" it built a library and never
an app. A reference assembly is not a deployment.

The two forks are still gone, but for narrower reasons than "the causes evaporated":

1. `InplaceStringBuilder` is **fixed upstream**: `Microsoft.Net.Http.Headers` 2.3.11 no longer
   references it. Verified by grepping both assemblies, after first checking the same grep finds it
   in 2.2.0 — the first attempt used `strings`, which is not installed here, and silently reported
   zero for everything.
2. `CancelKeyPress` is **avoided by construction**. The call is still there in `Microsoft.AspNetCore.Hosting`
   2.3.11, but it lives in `RunAsync`; this starts and stops the host explicitly, which never enters
   that path and is what an app wants anyway. The generic host's `ConsoleLifetime` is not involved
   either, since this is the web host.

What is left is the part that was never the patch: starting and stopping the server, and knowing
what address to show the user.

The name is kept for continuity with the 30,366 downloads of `Xamarinme.WebHostPatch`, and is now a
slight misnomer. That is a deliberate trade.

**The old package is not merely obsolete, it is unsafe.** Its 2.2.0 pins carry a critical advisory
([GHSA-5rrx-jjjq-q2r5](https://github.com/advisories/GHSA-5rrx-jjjq-q2r5), Kestrel) and a moderate
one ([GHSA-prrf-397v-83xh](https://github.com/advisories/GHSA-prrf-397v-83xh), IIS), and its forked
Primitives shadows the real assembly for anything sharing an output directory — which is why this
repository needs two test projects.

### `Xamarinme.Nfc` → `Me.Toolkit.Maui.Nfc`, ported

MAUI has no NFC support, so this is the library the port was actually for. It was never published,
so there was not even an old version to stay compatible with — which is why the API is smaller than
what it replaced rather than a translation of it.

**`INfc` is the surface**, resolved from DI after `builder.UseMeToolkitMauiNfc()`. Every implementation is
`internal`, which is what allows `MultiTargetingTests` to hold all five slices to one public API.
Gone: `CrossNfc.Current` (a 2019 static locator), the per-platform public `Nfc` classes, the unused
`NfcTagStatus`, and the `Nfc.OnNewIntent(intent)` call consumers had to add to their own
`MainActivity` — that hook is now wired by the library through `ConfigureLifecycleEvents`.

**The NDEF types are ours.** `NdefLibrary` 4.1.0 was verified to restore, compile and run on
.NET 10, so dropping it was a choice rather than a forced move: it is a 2017 package with a
netstandard1.4 asset only, and it was in the *public* API, since `ReadNdefAsync` returned its
`NdefMessage`. `Me.Toolkit.Maui.Nfc` has no third-party dependencies at all.

The codec is pinned against NdefLibrary's own output. Every byte vector in `NdefTests` was captured
by running NdefLibrary 4.1.0 and recording what it emitted, because the port's real risk is a codec
that disagrees with the old one and corrupts tags silently. **Do not regenerate those vectors from
the code under test.** Chunked records are refused rather than mis-parsed.

**Android and iOS only.** Mac Catalyst has no NFC hardware API — CoreNFC is iOS-only — and Windows
would mean the 678-line PC/SC layer and three `PCSC` packages, for an external USB reader rather
than phone NFC. Those two share the platform-neutral implementation, which throws
`PlatformNotSupportedException`, so an app targeting everything needs no conditional registration.

Platform sources are selected by **explicit `Compile Include` conditions**. The `Platforms/` folder
convention is an app thing; in a class library those files compile into every target framework,
which was checked rather than assumed.

### The gap in Me.Toolkit.Maui.Nfc's coverage

**Neither platform implementation has behavioural coverage.** A net10.0 test project resolves the
net10.0 slice, which is the one with no implementation, so nothing exercises `AndroidNfc` or
`IosNfc`. They are held by the API baseline, by `MultiTargetingTests` and by source pins — which is
a real guard on their shape and none at all on their behaviour.

That includes the two most valuable fixes the port made: the `PendingIntent` flags, without which
`EnableSessionAsync` throws on every Android 12 or later device, and the CoreNFC callbacks, which
used to throw on a dispatch queue where nothing could observe it and left callers awaiting forever.

`LIMITATION_The_Android_and_iOS_implementations_have_no_behavioural_coverage` asserts the gap so it
fails if it ever closes. Closing it needs a device-test harness, which was deliberately not taken
on.

### `Me.Toolkit.Maui.Sizing`, new rather than ported

MAUI sizes are absolute numbers, and `OnIdiom`/`OnPlatform` only choose a different absolute number
per device. This adds a relative one — a percentage of the parent (less its padding), the window,
the main display, a named element or the nearest ancestor of a type — recomputed as the reference
changes, the way CSS `%`, `vw` and `vh` behave.

**A markup extension, not a converter.** A converter only runs when a binding source changes, never
learns which element it is sizing, and takes its options as one unchecked string. `{me:Relative}`
gets the element and property from `IProvideValueTarget`, builds a per-element tracker, and returns
a binding to it, so every update after that is ordinary binding machinery.

**It is the one library on `Microsoft.Maui.Controls`**, not `Core`: markup extensions, bindings,
`VisualElement` and `Window` all live there. That is a consequence of what it does, not a change to
the shape of the others.

**`TypedBinding`, not `Binding("Value")`.** A string path is resolved by reflection over an internal
type, which the trimmer cannot see, and iOS and Android Release builds trim. When the reference has
no size yet the getter reports failure, which leaves the property at its own default — `-1` for
`WidthRequest`, the platform's size for `FontSize` — rather than a value the library invented.

**The display is behind a seam**, `IDisplaySize`, because `DeviceDisplay.SetCurrent` is internal and
the net10.0 slice's `DeviceDisplay.Current` throws. It is the only reference a test cannot drive
directly. `DeviceDisplay`'s change event is static, so an element subscribes only while it is in a
window; a test asserts the subscription is dropped when it leaves.

**Nesting with `OnIdiom` works one way, and only with a named argument** — found by running the
demo, since the net10.0 slice has no idiom:

- `{me:Relative Percent={OnIdiom ...}}` works.
- `{me:Relative {OnIdiom ...}}` does not. For the positional argument MAUI's XAML source
  generator passes `OnIdiom` a null target property, and `OnIdiom` throws *"Cannot determine
  property to provide the value for"*. A named argument is passed its `PropertyInfo`.
- `{OnIdiom Desktop={me:Relative 30}}` cannot work. The target is then the `OnIdiom` extension; the
  element is only in `IProvideParentValues`, which is internal; and the generated code applies
  `OnIdiom`'s result with `SetValue`, which cannot carry a binding. It throws a message giving the
  working spelling.

**Orientation is the window's shape, not the reference's.** `Portrait` and `Landscape` override
`Percent` per orientation. The first design took the orientation from the reference, which is
wrong in the common case: a parent card is wider than it is tall on a portrait phone too, so it
would have said "landscape" nearly everywhere. The tracker therefore watches the element's window
as well as its reference whenever an override is set. A square window counts as portrait, and
outside a window `Percent` applies. MAUI has no `OnOrientation`, and a separate one nested inside
would be evaluated once at load and never again after a rotation, so it lives in the extension.

**Styles go through attached properties.** A `Setter` is shared by every element its style
applies to, and a relative size needs a tracker per element; a markup extension in a setter has no
element to track. `RelativeSizing.FontSize` and friends hold an immutable `RelativeSize`, converted
from text by `RelativeSizeTypeConverter`, and create each element's binding when set. The converter
works under the XAML source generator — checked in the demo, not assumed.

**Clearing needed more than `RemoveBinding`.** MAUI keeps the last value a removed binding set, and
`ClearValue` does not remove it — for an ordinary `Binding` too, checked side by side. So
`ClearRelativeSize` first stops the tracker, which makes the binding report no value and MAUI apply
the property's default, and only then removes the binding. Each element keeps one tracker per
property, so replacing a relative size stops the old tracker; that was only observable through the
display's listener count, and a test now watches it.

**`To=Self` and `Offset`** give aspect ratios and `calc(50% - 8)`. `Self` includes the element's
padding, since a ratio is of the outside, and a `Self` size that reads the dimension it sets is
refused.

**It has run on a phone.** Portrait to landscape on an Android 16 device, every card recomputed
live and matched its expected value; see the [Modernization Log](Modernization-Log). iOS has not
been run.

**Window and display sizes are converted to layout units**, which only differ on Mac Catalyst. An
app with the iPad idiom — MAUI's template default — is shown there scaled to 77%, and `Window.Width`
and `DeviceDisplay` report Mac points while pages are laid out in iPad points. Found by running the
demo on a Mac: a window reporting 1024 wide held a parent measuring 1267. The first fix measured the
window's root page instead, which is in layout units by definition — and every window-relative size
in the demo went to zero, because its root is a `Shell`, which reports no size. The conversion uses
Apple's fixed 0.77 for the iPad idiom and 1 for the Mac idiom; `LayoutUnits` holds it, settable so a
net10.0 test can reproduce the Mac.

**Breakpoints are a string, `'600:50 1200:33'`**, because the markup extension, the attached
properties' text form and code all need to write them, and a list type would not survive the first
two. Space-separated because the text form is already comma-separated. They key on window width in
layout units, like CSS media queries on the viewport, and are refused alongside `Portrait` and
`Landscape`, which would choose the percentage too.

**A `Thickness` target gets the value on the chosen `Sides`** — all by default, the rest 0 — through a
second typed binding rather than a converter, so it stays trim-safe.

**`Round` is `None`, `Units` or `Pixels`.** It began as a boolean for whole units, and running it
showed whole units are not whole pixels: at 150% a requested 469 rendered as 469.333, the platform's
own snapping. `Pixels` rounds with the window's `DisplayDensity` and listens to its
`DisplayDensityChanged`, so it follows a window dragged to a monitor scaled differently; after it,
requested and rendered widths matched to the fourth decimal. It had not been released, so its type
could change.

**`To=Display` is the window's monitor on Windows.** MAUI's `DeviceDisplay` only knows the main
display; WinUI's `DisplayArea.GetFromWindowId` knows the one a window is on, and MAUI raises the
window's `X` and `Y` as it is dragged. Verified across two monitors, one of them rotated to
portrait. Mac Catalyst has no per-window screen to ask, and phones have one display.

**`BreakpointsBy=Reference` is a container query**, comparing the reference's own width. It needs no
window, which is also why it is not counted among the window-dependent options.

**`SafeArea` reads the platform's insets**, because MAUI 10's `SafeAreaEdges` decides whether a
layout avoids the safe area but reports its size nowhere cross-platform — the only getter is
iOS-specific and per page. `UIWindow.SafeAreaInsets` on iOS and Mac Catalyst, `WindowInsetsCompat`
system bars and display cutout on Android, none on Windows. Insets can arrive just after the
rotation that changes them, so a size using them looks again 150 ms after its window resizes.

**The analyzer reads XAML, not C#.** MAUI already passes every `.xaml` file to analyzers as an
additional file, tagged `GenKind = Xaml`, so `Me.Toolkit.Maui.Sizing.Analyzers` parses the XML and
the markup-extension syntax itself and reports at the attribute. It mirrors `RelativeSize`'s
validation rather than sharing it — an analyzer cannot load MAUI. It ships inside the Sizing package
under `analyzers/dotnet/cs`, built by a project reference that does not reference its output, so
its Roslyn dependency never becomes the package's. Proved in a real build of the demo: an unnamed
`{OnIdiom}` inside `{me:Relative}`, which compiles and then crashes at startup, was reported as
`MainPage.xaml(274,45): warning MTKS002`. An unknown option name turned out to be a build error from
MAUI's own XAML compiler already, so the analyzer's value is in what MAUI compiles and then fails at
runtime. Everything is a warning, so a false positive cannot break a build.

**Grid columns and rows are not supported, deliberately.** A `ColumnDefinition` has no parent and no
window, and MAUI keeps the grid that owns it internal to XAML, so a relative size there has nothing
to measure. A percentage of the grid itself is what star sizing already does.

**Unlike `Me.Toolkit.Maui.Nfc`, it has real behavioural coverage.** Elements, windows, bindings and
the runtime XAML loader all run on the net10.0 slice of Controls, so the tests arrange real
elements with `IView.Arrange`, resize real windows with `IWindow.FrameChanged`, and load real XAML.
The one piece of test scaffolding is an inline dispatcher: a binding delivers source changes
through its target's dispatcher, and a bare test process has none.

## Deletions that are not ports

- **`Microsoft.Extensions.FileProviders.Xamarin`** — an empty `Class1`, referenced by nothing, not
  even by `WebHostPatch`. Never imported into this repository.
- **`Xamarin.Essentials.FileSystem.Extensions`** — one method, in the solution, referenced by
  nothing, never published, and **broken by construction**: it declares `public partial class
  FileSystem` in `namespace Xamarin.Essentials`, hijacking a Microsoft type name, and calls
  `Assembly.GetExecutingAssembly()`, which returns the *library's* assembly and so can never find the
  caller's embedded resource. `FileSystem.OpenAppPackageFileAsync` replaces it. Never imported.
- **`Microsoft.Extensions.Primitives.Patch`** — imported only because `legacy/WebHostPatch` would not
  build without it, and deleted with `legacy/` in Phase 4.

## Environment facts worth not rediscovering

- The .NET 10 SDK's `dotnet new sln` produces **`Me.Toolkit.Maui.slnx`**, not a classic `.sln`. The API
  baseline finds the repository root by that exact file name.
- The MAUI workloads present here are `android`, `ios`, `maccatalyst` and `maui-windows`. A
  `net10.0-android` MAUI app builds clean in about 20 seconds.
- The **iOS and Mac Catalyst library slices compile on Windows**. Only building and signing an *app*
  for them needs a Mac, so CI on Linux will build two slices per library rather than five — but a
  macOS runner is still needed to build the demo app for those platforms later.
- `UseMaui`/`UseMauiCore`/`UseMauiEssentials` **no longer add package references implicitly** since
  .NET 8. MSBuild says so with MA002.
- Android drops a public `Resource` designer class into every library unless
  `AndroidGenerateResourceDesigner` is turned off.
