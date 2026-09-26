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

No registration is needed. It works on any `double` property: `WidthRequest`, `HeightRequest`,
`FontSize`, `Spacing`, `MaximumWidthRequest` and so on.

| Option | Values | Default |
|---|---|---|
| `Percent` (first argument) | `30` is 30% | — |
| `To` | `Parent`, `Window`, `Display` | `Parent` |
| `Axis` | `Width`, `Height`, `Shorter`, `Longer` | `Width` |
| `Min`, `Max` | clamp the result | 0, no limit |
| `Source` | an element, usually `{x:Reference}` | — |
| `AncestorType` | the nearest ancestor of this type | — |

From code:

```csharp
label.SetRelativeSize(Label.FontSizeProperty,
    new RelativeSize(3) { To = SizeReference.Window, Min = 12, Max = 40 });
```

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

Until the reference has a size — before the first layout, or before the element is in a window —
the property keeps its default. If the element moves to another parent or window, it follows.
Setting the property directly replaces the relative size, as it would any binding.

## Things to know

- **Not in a `Style` setter.** The size is tracked per element, and a setter is shared. It throws
  saying so.
- **Watch for layout loops.** A percentage of a parent that sizes itself to its content —
  `Auto` rows, a `VerticalStackLayout` in its stacking direction — makes the child depend on the
  parent and the parent on the child. Size against something with a size of its own: a `*` row,
  the window, or a named element.
- **No leaks through the display.** The display's change event is static; an element subscribes
  only while it is in a window and unsubscribes when it leaves.
- **Trim-safe.** The binding is a `TypedBinding`, not a string path, so iOS and Android Release
  builds do not trim away what it reads.

## Documentation

Full documentation is in the [wiki](https://github.com/melihercan/Me.Toolkit.Maui/wiki) — [Design Notes](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Design-Notes) for why each
library is shaped the way it is, [Building and Testing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Building-and-Testing) for the
multi-targeting rules, and [Publishing](https://github.com/melihercan/Me.Toolkit.Maui/wiki/Publishing) for how these packages are released.

Source: [github.com/melihercan/Me.Toolkit.Maui](https://github.com/melihercan/Me.Toolkit.Maui)

## Licence

MIT.
