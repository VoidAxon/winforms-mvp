# Toast Style Presets — Design

Date: 2026-06-10

## Goal

Ship a small set of **built-in toast appearances** selectable through a single style
parameter, instead of requiring callers to write a custom `ToastRenderer`. Custom renderers
remain fully supported. Modeled after the two reference looks the user provided:

- **Soft** — light tinted background, colored filled-circle icon, dark text, colored close glyph.
- **Card** — white background, colored rounded accent bar on the left, colored filled-circle
  icon, dark text, gray close glyph.

The existing solid-color style stays as **Default**.

## Constraints (decided during brainstorming)

- Single `Message` only — **no title/body two-line model**. Type is conveyed by color/icon.
- Style is chosen via an **enum parameter** (`Default / Soft / Card`); a custom renderer still
  overrides it.
- New styles are **rounded**; `Default` stays **square**. Corner shape is per-style, not global —
  not everything is rounded.
- **No shadow** (would require per-pixel-alpha layered windows; out of scope).
- The close button display is **configurable** (show/hide).
- **No interface or method-signature changes**: `ToastOptions` already flows end-to-end into
  `ToastNotification` (`MessageService.ShowToast(text, type, options)`), so new behavior is added
  via new fields on `ToastOptions` / `ToastDefaults`.

## Architecture

### 1. Style selection and resolution precedence

- New enum `WinformsMVP.Common.ToastStyle { Default, Soft, Card }`.
- `ToastOptions` gains `ToastStyle? Style` (null = use the app-wide default).
- `ToastDefaults` gains `static ToastStyle Style` (default `ToastStyle.Default`), and its
  `Renderer` default changes from `new DefaultToastRenderer()` to `null`. `null` now means
  "no custom override — resolve from `Style`." Net behavior is unchanged because `Style`
  defaults to `Default`, which maps to `DefaultToastRenderer`.
- New internal `ToastRendererResolver` resolves the painter most-specific-first. The built-in
  renderers are stateless and held as singletons:

  1. `options.Renderer` — per-call custom renderer
  2. `options.Style` — per-call style
  3. `ToastDefaults.Renderer` — app-wide custom renderer
  4. `ToastDefaults.Style` — app-wide style (default `Default`)

  This unifies "default / style1 / style2 / custom" behind one selection point, and a custom
  renderer always wins over a style.

### 2. Corner shape as a renderer property

- `ToastRenderer` gains `public virtual int CornerRadius { get { return 0; } }`.
- `DefaultToastRenderer` inherits `0` → **square, appearance unchanged**.
- `SoftToastRenderer` / `CardToastRenderer` return a rounded radius (~10px).
- `ToastNotification` resolves its renderer once at construction and caches it. After the handle
  is created it applies a rounded window region when `CornerRadius > 0`
  (`CreateRoundRectRgn` + `SetWindowRgn`); `0` leaves the window square. Ownership of the region
  transfers to the window on `SetWindowRgn(..., bRedraw: true)`, so it is not deleted manually.
- The window region itself is not anti-aliased, so each rounded renderer also draws an
  anti-aliased rounded border just inside its bounds to soften the corner edges.

### 3. Close-button display configurable

- `ToastOptions` gains `bool? ShowCloseButton` (null = use the app-wide default).
- `ToastDefaults` gains `static bool ShowCloseButton` (default `true`).
- Resolved as `options.ShowCloseButton ?? ToastDefaults.ShowCloseButton`.
- When `false`, renderers omit the close glyph and let the message text use the freed space on
  the right for a more balanced layout.
- **Click-to-dismiss is unaffected**: clicking anywhere on the toast still closes it regardless
  of whether the glyph is drawn. The flag controls display only, not interaction.

### 4. Render context additions

`ToastRenderContext` gains two fields, supplied by `ToastNotification`:

- `int CornerRadius` — so a renderer can draw a border matching the window's rounded region.
- `bool ShowCloseButton` — so a renderer knows whether to draw the close glyph and how wide the
  message area is.

The context constructor stays `internal`; only the framework builds it.

### 5. The two new renderers

Both consume the single `Message`, are `public` (subclassable like `DefaultToastRenderer`), and
expose `protected virtual` color/icon hooks for tweak-by-subclass.

**`SoftToastRenderer`** (reference image 1):
- Light tinted background (`g.Clear(tint)`); window region clips it to rounded.
- Anti-aliased rounded border in a stronger tint.
- Left gutter: filled circle in the strong accent color with a white glyph.
- Message: dark gray, centered.
- Close glyph: strong accent color, top-right (only when `ShowCloseButton`).

**`CardToastRenderer`** (reference image 2):
- White background; anti-aliased light-gray rounded border (defines the edge without a shadow).
- Left: rounded vertical accent bar in the strong color.
- Filled circle in the strong color with a white glyph.
- Message: dark gray, left-aligned.
- Close glyph: gray, top-right (only when `ShowCloseButton`).

Per-type palette:

| Type    | Accent  | Soft tint |
|---------|---------|-----------|
| Success | #10893E | light green |
| Info    | #0078D7 | light blue  |
| Warning | #FF8C00 | light yellow |
| Error   | #E81123 | light red   |

## Testing

- `ToastRendererResolver` precedence — one case per level (per-call renderer, per-call style,
  default renderer, default style). Pure logic, xUnit.
- `CornerRadius` per renderer — `Default == 0`, `Soft`/`Card` `> 0`.
- GDI+ pixel rendering is not unit-tested; verified manually via the sample.

## Sample & docs

- `ToastDemoForm` gains buttons to show each style (and toggle the close button) for manual
  comparison.
- Update `wiki/Reference-Toast-Notifications.md` with a "Styles" section and `CHANGELOG.md`
  (Unreleased).

## Affected files

New: `Common/ToastStyle.cs`, `Services/Implementations/SoftToastRenderer.cs`,
`Services/Implementations/CardToastRenderer.cs`, `Services/Implementations/ToastRendererResolver.cs`,
resolver tests.

Modified: `Common/ToastOptions.cs`, `Services/ToastDefaults.cs`, `Common/ToastRenderer.cs`
(+ `ToastRenderContext`), `Services/Implementations/ToastNotification.cs`,
`samples/.../ToastDemo/ToastDemoForm.cs`, `wiki/Reference-Toast-Notifications.md`, `CHANGELOG.md`.

## Out of scope (YAGNI)

- Title/body two-line layout.
- Drop shadows / per-pixel-alpha layered windows.
- Separating click-to-dismiss from close-glyph display.
- Per-style configurable corner radius beyond the renderer's own value.
