# Me.Toolkit.Maui.Sizing

Relative sizes for .NET MAUI: a percentage of the parent, the window, the display or another
element, recomputed as it changes — the way CSS `%`, `vw` and `vh` work.

MAUI sizes are absolute numbers. `WidthRequest="100"` is the same 100 on a phone and on a 4K
monitor, and `OnIdiom` only lets you pick a different absolute number per device. This adds a
relative one.

## Use

```xml
<ContentPage xmlns:me="https://github.com/melihercan/Me.Toolkit.Maui" ...>

    <!-- 30% of the parent's width -->
    <Image WidthRequest="{me:Relative 30}" />

    <!-- 3% of the window's width, never below 12 or above 40 -->
    <Label FontSize="{me:Relative 3, To=Window, Min=12, Max=40}" />

    <!-- a square, 15% of the display's shorter side, so it keeps its size when a phone rotates -->
    <Image WidthRequest="{me:Relative 15, To=Display, Axis=Shorter}"
           HeightRequest="{me:Relative 15, To=Display, Axis=Shorter}" />

    <!-- half of a named element, or of the nearest ancestor of a type -->
    <BoxView WidthRequest="{me:Relative 50, Source={x:Reference Card}}" />
    <BoxView WidthRequest="{me:Relative 50, AncestorType={x:Type Border}}" />
```

No registration is needed. It works on any `double` property — `WidthRequest`, `HeightRequest`,
`FontSize`, `Spacing`, `MaximumWidthRequest` and so on — and on `Margin` and `Padding`, which get the
value on every side: `Padding="{me:Relative 2, To=Window, Min=8, Max=24}"`.

| Option | Values | Default |
|---|---|---|
| `Percent` (first argument) | `30` is 30% | — |
| `Portrait`, `Landscape` | a different percentage per window orientation | `Percent` |
| `Breakpoints` | percentages by window width: `'600:50 1200:33'` | none |
| `To` | `Parent`, `Window`, `Display`, `Self` | `Parent` |
| `Offset` | added after the percentage, may be negative | 0 |
| `Axis` | `Width`, `Height`, `Shorter`, `Longer` | `Width` |
| `Min`, `Max` | clamp the result | 0, no limit |
| `Round` | whole units, before the clamp | `False` |
| `Source` | an element, usually `{x:Reference}` | — |
| `AncestorType` | the nearest ancestor of this type | — |

From code:

```csharp
label.SetRelativeSize(Label.FontSizeProperty,
    new RelativeSize(3) { To = SizeReference.Window, Min = 12, Max = 40 });
```

## Aspect ratios and gaps

`To=Self` measures the element itself, padding included, so a height can follow the element's own
width:

```xml
<!-- half the parent wide, always 16:9 -->
<Image WidthRequest="{me:Relative 50}" HeightRequest="{me:Relative 56.25, To=Self}" />
```

It must read the other dimension than the one it sets — `HeightRequest` from `Width`,
`WidthRequest` from `Height` — and anything else is refused, since it would chase its own value.

`Offset` is added after the percentage and before `Min`/`Max`, which is CSS's `calc()`:

```xml
<!-- two halves side by side with an 8 gap: each is calc(50% - 4) -->
<HorizontalStackLayout Spacing="8">
    <BoxView WidthRequest="{me:Relative 50, Offset=-4, AncestorType={x:Type VerticalStackLayout}}" />
    <BoxView WidthRequest="{me:Relative 50, Offset=-4, AncestorType={x:Type VerticalStackLayout}}" />
</HorizontalStackLayout>
```

## Breakpoints

Like CSS media queries, `Breakpoints` choose the percentage by the window's width:

```xml
<!-- full width on a phone, two columns from 600, three from 1200 -->
<Border WidthRequest="{me:Relative 100, Breakpoints='600:50 1200:33', Offset=-8}" />
```

Pairs are `width:percent`, separated by spaces, in any order. Below the smallest width, `Percent`
applies; widths are the window's, in layout units, so they mean the same on every platform. They
follow the window as it is resized. `Breakpoints` cannot be combined with `Portrait`/`Landscape` —
both choose the percentage, and rather than pick a winner it is refused.

## Rounding

`Round=True` rounds to whole units, before `Min`/`Max` so it never escapes them. It gives tidy
values and sizes that step rather than creep during a resize. It does not give whole physical
pixels: the platform snaps to those itself, so on a 150% display an odd value still lands between
two — 469 rendered as 469.333 on Windows, which is 704 pixels. Rounding a value close to 100% of its
parent can also round up past the space available, by up to half a unit, which layout then trims.

## In styles

`{me:Relative}` cannot go in a `Style` setter — a setter is shared, and a relative size is tracked
per element. `RelativeSizing`'s attached properties bridge the two:

```xml
<Style TargetType="Label">
    <Setter Property="me:RelativeSizing.FontSize" Value="3, To=Window, Min=12, Max=40" />
</Style>
```

The value is text with the same option names as `{me:Relative}`: an optional leading percentage,
then `Name=Value` pairs. Numbers are read the same way in every culture. `Source` and `AncestorType`
cannot be written as text, since text cannot name an element or a type.

`RelativeSizing.WidthRequest`, `HeightRequest`, `FontSize`, `Margin` and `Padding` are available,
and work directly on an element too: `<BoxView me:RelativeSizing.WidthRequest="25, Offset=10" />`.
`FontSize` and `Padding` find the property of whichever element they are on, including third-party
controls with a `FontSizeProperty` or `PaddingProperty`. `Breakpoints=600:50 1200:33` and
`Round=True` are written the same way.
Removing the style removes the size, and the property goes back to its default.

From code, `element.ClearRelativeSize(property)` does the same. Prefer it to `RemoveBinding`: MAUI
keeps the last value a removed binding set, for any binding, and `ClearValue` does not take it back.

## Portrait and landscape

Rotation needs nothing special: it changes the window's size, so every relative size recomputes.
`Axis=Shorter` or `Longer` gives a size rotation does not change at all.

For a *different percentage* per orientation, name one or both:

```xml
<!-- the full width in portrait, half of it side by side in landscape -->
<Image WidthRequest="{me:Relative Portrait=90, Landscape=45}" />

<!-- 50% unless the window is landscape -->
<Image WidthRequest="{me:Relative 50, Landscape=25}" />
```

Orientation is the shape of the element's **window** — landscape when it is wider than tall,
portrait otherwise, square included — whatever the size is measured against. A card is wider than
it is tall on a portrait phone too, so the reference's own shape would say "landscape" almost
everywhere. On a phone the window's shape is the screen's orientation; on a desktop, a tall narrow
window counts as portrait. Until the element is in a window, `Percent` applies.

## With OnIdiom and OnPlatform

Put them **inside**, as a named argument:

```xml
<Image WidthRequest="{me:Relative Percent={OnIdiom Phone=80, Tablet=50, Desktop=30}}" />
```

Two other spellings look reasonable and do not work, both because of how MAUI compiles XAML:

- `{me:Relative {OnIdiom ...}}` — as the unnamed first argument, `OnIdiom` is not told what type
  it is producing and throws *"Cannot determine property to provide the value for"*. Naming it
  `Percent=` fixes that.
- `{OnIdiom Desktop={me:Relative 30}, Default=100}` — nested the other way, `{me:Relative}` cannot
  see the element it is sizing, and the compiled XAML applies the result with `SetValue`, which
  cannot carry a binding. This throws a message saying which way round to write it.

## What it measures

- **Parent** is the parent's size **less its padding** — the space the element is actually laid
  out in, as a CSS percentage refers to the content box. `Source` and `AncestorType` exclude
  padding the same way. A `Border`'s stroke is not subtracted.
- **Window** is the window the element is in, and follows it as it is resized.
- **Display** is the main display in device-independent units, and changes on rotation. On a
  desktop with several monitors it is the main one, not necessarily the one the window is on.

Both are converted to the units pages are laid out in. That matters on **Mac Catalyst**, where an
app with the iPad idiom — MAUI's template default — is shown scaled to 77%: the window and display
are reported in Mac points while layout uses iPad points, 1.3 times as many. Without the conversion
every window- and display-relative size came out 23% small on a Mac.

Until the reference has a size — before the first layout, or before the element is in a window —
the property keeps its default. If the element moves to another parent or window, it follows.
Setting the property directly replaces the relative size, as it would any binding.

## Things to know

- **`{me:Relative}` is not for a `Style` setter** — use `RelativeSizing`, above. It throws
  saying so.
- **Watch for layout loops.** A percentage of a parent that sizes itself to its content —
  `Auto` rows, a `VerticalStackLayout` in its stacking direction — makes the child depend on the
  parent and the parent on the child. Size against something with a size of its own: a `*` row,
  the window, or a named element.
- **No leaks through the display.** The display's change event is static; an element subscribes
  only while it is in a window and unsubscribes when it leaves.
- **Trim-safe.** The binding is a `TypedBinding`, not a string path, so iOS and Android Release
  builds do not trim away what it reads.
- **Accessibility text size multiplies a relative font.** MAUI scales `FontSize` by the user's
  system text-size setting (`FontAutoScalingEnabled`, on by default), after this library has set it.
  So `Min` and `Max` bound the `FontSize` property, not the rendered text: with `Max=40` and the text
  size at 130%, text renders at 52. That is usually right — the user asked for bigger text. To cap
  what renders, set `FontAutoScalingEnabled="False"`, and then honour the setting some other way.
  This is how MAUI documents the setting; it has not been measured on a device here.

## Where it has run

Windows, resized; an Android 16 phone, rotated; Mac Catalyst on macOS 15, resized; an iPhone XR on
iOS 18, rotated — every card of the repository's demo checked against its expected value.

## Documentation

Full documentation is in the [wiki](https://github.com/melihercan/Me.Toolkit.Maui/wiki) — [Design Notes](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Design-Notes) for why each
library is shaped the way it is, [Building and Testing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Building-and-Testing) for the
multi-targeting rules, and [Publishing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Publishing) for how these packages are released.

Source: [github.com/melihercan/Me.Toolkit.Maui](https://github.com/melihercan/Me.Toolkit.Maui)

## Licence

MIT.
