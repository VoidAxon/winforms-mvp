# Toast Style Presets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add built-in toast appearances (`Default` / `Soft` / `Card`) selectable via a `ToastStyle` enum, with rounded corners for the new styles and a configurable close-button display — without changing any public interface or method signature.

**Architecture:** A `ToastStyle` enum plus a `Style` field on `ToastOptions`/`ToastDefaults` feeds an internal `ToastRendererResolver` that picks a painter most-specific-first (per-call renderer → per-call style → default renderer → default style). Corner shape becomes a `ToastRenderer.CornerRadius` virtual property; `ToastNotification` applies a rounded window region from the resolved renderer. A `ShowCloseButton` flag flows into `ToastRenderContext` so renderers can hide the close glyph and reclaim the space.

**Tech Stack:** .NET Framework (net40;net48), SDK-style C#, GDI+ (`System.Drawing`), Win32 P/Invoke (`gdi32`/`user32`), xUnit.

---

## File Structure

- `src/WinformsMVP/Common/ToastStyle.cs` — **new** enum `{ Default, Soft, Card }`.
- `src/WinformsMVP/Common/ToastOptions.cs` — **modify**: add `ToastStyle? Style`, `bool? ShowCloseButton`.
- `src/WinformsMVP/Common/ToastRenderer.cs` — **modify**: add `virtual int CornerRadius`; add `CornerRadius` + `ShowCloseButton` to `ToastRenderContext` (and its internal ctor).
- `src/WinformsMVP/Services/ToastDefaults.cs` — **modify**: add `Style`, `ShowCloseButton`; change `Renderer` default to `null`.
- `src/WinformsMVP/Services/Implementations/DefaultToastRenderer.cs` — **modify**: honor `ShowCloseButton`.
- `src/WinformsMVP/Services/Implementations/ToastDrawing.cs` — **new** internal helper: rounded-rect `GraphicsPath`.
- `src/WinformsMVP/Services/Implementations/SoftToastRenderer.cs` — **new** public renderer (image 1).
- `src/WinformsMVP/Services/Implementations/CardToastRenderer.cs` — **new** public renderer (image 2).
- `src/WinformsMVP/Services/Implementations/ToastRendererResolver.cs` — **new** internal resolver + style→renderer singletons.
- `src/WinformsMVP/Services/Implementations/ToastNotification.cs` — **modify**: resolve renderer once, pass new context fields, apply rounded window region.
- `tests/WinformsMVP.Samples.Tests/Services/ToastRendererResolverTests.cs` — **new** tests.
- `tests/WinformsMVP.Samples.Tests/Services/ToastRendererTests.cs` — **new** tests (CornerRadius + render smoke).
- `samples/WinformsMVP.Samples/ToastDemo/ToastDemoForm.cs` — **modify**: style buttons + close-button toggle; rename the private demo renderer.
- `wiki/Reference-Toast-Notifications.md`, `CHANGELOG.md` — **modify**: document the feature.

Build command used throughout: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Test command used throughout: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`

---

## Task 1: Add the `ToastStyle` enum

**Files:**
- Create: `src/WinformsMVP/Common/ToastStyle.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace WinformsMVP.Common
{
    /// <summary>
    /// Selects a built-in toast appearance. Map to a renderer via the framework's resolver;
    /// a custom <see cref="ToastRenderer"/> set on <c>ToastOptions.Renderer</c> or
    /// <c>ToastDefaults.Renderer</c> overrides the style.
    /// </summary>
    public enum ToastStyle
    {
        /// <summary>Solid color background with white text and a left icon (square corners).</summary>
        Default,

        /// <summary>Light tinted background, filled-circle icon, dark text (rounded corners).</summary>
        Soft,

        /// <summary>White card with a colored left accent bar and filled-circle icon (rounded corners).</summary>
        Card
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Common/ToastStyle.cs
git commit -m "feat(common): add ToastStyle enum (Default/Soft/Card)"
```

---

## Task 2: Add `CornerRadius` to `ToastRenderer`

**Files:**
- Modify: `src/WinformsMVP/Common/ToastRenderer.cs`

- [ ] **Step 1: Add the virtual property**

In `ToastRenderer` (the abstract class), add the property just above the `Render` method:

```csharp
        /// <summary>
        /// Corner radius in pixels the toast window should be rounded to. <c>0</c> (the default)
        /// means square. The framework reads this from the resolved renderer and applies a rounded
        /// window region, so the shape travels with the renderer — custom renderers can round
        /// themselves by overriding this.
        /// </summary>
        public virtual int CornerRadius
        {
            get { return 0; }
        }
```

- [ ] **Step 2: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded (`DefaultToastRenderer` inherits `0` → still square).

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Common/ToastRenderer.cs
git commit -m "feat(common): add ToastRenderer.CornerRadius (default 0 = square)"
```

---

## Task 3: Add `Style` and `ShowCloseButton` to `ToastOptions`

**Files:**
- Modify: `src/WinformsMVP/Common/ToastOptions.cs`

- [ ] **Step 1: Add the two properties**

Add inside the `ToastOptions` class, after the existing `Renderer` property:

```csharp
        /// <summary>
        /// Built-in style for this toast. <c>null</c> = use <c>ToastDefaults.Style</c>. Ignored when
        /// <see cref="Renderer"/> is set (a custom renderer always wins).
        /// </summary>
        public ToastStyle? Style { get; set; }

        /// <summary>
        /// Whether this toast draws a close glyph. <c>null</c> = use <c>ToastDefaults.ShowCloseButton</c>.
        /// Display only — clicking anywhere on the toast still dismisses it regardless.
        /// </summary>
        public bool? ShowCloseButton { get; set; }
```

- [ ] **Step 2: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Common/ToastOptions.cs
git commit -m "feat(common): add Style and ShowCloseButton to ToastOptions"
```

---

## Task 4: Add `Style` / `ShowCloseButton` to `ToastDefaults`, default `Renderer` to null

**Files:**
- Modify: `src/WinformsMVP/Services/ToastDefaults.cs`

- [ ] **Step 1: Add `Style` and `ShowCloseButton`**

Add after the `Opacity` property:

```csharp
        /// <summary>App-wide toast style. Default: <see cref="ToastStyle.Default"/>.</summary>
        public static ToastStyle Style { get; set; } = ToastStyle.Default;

        /// <summary>Whether toasts show a close glyph by default. Default: <c>true</c>.</summary>
        public static bool ShowCloseButton { get; set; } = true;
```

- [ ] **Step 2: Change the `Renderer` default to null and update its doc**

Replace the existing `Renderer` property:

```csharp
        /// <summary>
        /// App-wide custom painter for toasts. Default: <c>null</c>, meaning "no custom override —
        /// resolve the painter from <see cref="Style"/>." Set it to take over every toast's
        /// appearance, or override per toast via <see cref="ToastOptions.Renderer"/>. A non-null
        /// value here wins over <see cref="Style"/> but loses to a per-toast renderer/style.
        /// </summary>
        public static ToastRenderer Renderer { get; set; }
```

The `using WinformsMVP.Services.Implementations;` import is now unused by this property but other members may still need it — leave the file's existing usings as they are; the build will warn only if truly unused (remove the import only if the build emits CS8019/unused warning for it).

- [ ] **Step 3: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded. (`ToastNotification` still falls back via its `FallbackRenderer`, so a null default is safe.)

- [ ] **Step 4: Commit**

```bash
git add src/WinformsMVP/Services/ToastDefaults.cs
git commit -m "feat(services): add ToastDefaults.Style/ShowCloseButton; Renderer default null"
```

---

## Task 5: Flow `CornerRadius` + `ShowCloseButton` through the render context

This wires the new context fields end-to-end and makes the close-button toggle work for the
existing `Default` style. Renderer resolution stays the current `??` chain for now (Task 9 swaps
it for the resolver).

**Files:**
- Modify: `src/WinformsMVP/Common/ToastRenderer.cs` (the `ToastRenderContext` class)
- Modify: `src/WinformsMVP/Services/Implementations/ToastNotification.cs`
- Modify: `src/WinformsMVP/Services/Implementations/DefaultToastRenderer.cs`

- [ ] **Step 1: Extend `ToastRenderContext`**

In `ToastRenderContext`, update the internal constructor and add two properties. Replace the
constructor and add the properties after `Font`:

```csharp
        internal ToastRenderContext(Graphics graphics, Rectangle bounds, string message, ToastType type, Font font, int cornerRadius, bool showCloseButton)
        {
            Graphics = graphics;
            Bounds = bounds;
            Message = message;
            Type = type;
            Font = font;
            CornerRadius = cornerRadius;
            ShowCloseButton = showCloseButton;
        }
```

```csharp
        /// <summary>The corner radius (px) of the toast window; <c>0</c> means square. Draw a
        /// matching rounded border to soften the non-anti-aliased window region edge.</summary>
        public int CornerRadius { get; }

        /// <summary>Whether the renderer should draw a close glyph.</summary>
        public bool ShowCloseButton { get; }
```

- [ ] **Step 2: Resolve `ShowCloseButton` in `ToastNotification` and pass the new fields**

In `ToastNotification`, add a field next to the others:

```csharp
        private readonly bool _showCloseButton;
```

In the constructor, after `_renderer = options.Renderer;`, add:

```csharp
            _showCloseButton = options.ShowCloseButton ?? ToastDefaults.ShowCloseButton;
```

Replace the `Render(Graphics g)` method body's context construction so it passes the new fields:

```csharp
        private void Render(Graphics g)
        {
            // Resolve the painter: per-toast override, else app-wide default, else hard fallback.
            ToastRenderer renderer = _renderer ?? ToastDefaults.Renderer ?? FallbackRenderer;
            renderer.Render(new ToastRenderContext(g, new Rectangle(0, 0, _width, _height), _message, _type, _font, renderer.CornerRadius, _showCloseButton));
        }
```

- [ ] **Step 3: Make `DefaultToastRenderer` honor `ShowCloseButton`**

In `DefaultToastRenderer.Render`, replace the two `g.DrawString` calls for the message and close
glyph with:

```csharp
                // Message (fills the middle; reclaims the close-glyph gutter when it is hidden)
                int messageRight = context.ShowCloseButton ? width - 30 : width - 20;
                g.DrawString(context.Message, font, Brushes.White, new RectangleF(60, 10, messageRight - 60, height - 20), leftMiddle);
                // Close glyph (top-right corner) — only when requested
                if (context.ShowCloseButton)
                {
                    g.DrawString("✖", closeFont, Brushes.White, new RectangleF(width - 30, 5, 20, 20), centered);
                }
```

- [ ] **Step 4: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Common/ToastRenderer.cs src/WinformsMVP/Services/Implementations/ToastNotification.cs src/WinformsMVP/Services/Implementations/DefaultToastRenderer.cs
git commit -m "feat(toast): flow CornerRadius/ShowCloseButton through render context"
```

---

## Task 6: Add the rounded-rectangle drawing helper

**Files:**
- Create: `src/WinformsMVP/Services/Implementations/ToastDrawing.cs`

- [ ] **Step 1: Create the helper**

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>Shared GDI+ helpers for the built-in toast renderers.</summary>
    internal static class ToastDrawing
    {
        /// <summary>
        /// Builds a rounded-rectangle path for <paramref name="bounds"/> with the given corner
        /// <paramref name="radius"/>. A radius of <c>0</c> (or larger than half the smaller side)
        /// is clamped to a sensible value.
        /// </summary>
        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int max = System.Math.Min(bounds.Width, bounds.Height) / 2;
            if (radius < 1) radius = 1;
            if (radius > max) radius = max;
            int d = radius * 2;

            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/ToastDrawing.cs
git commit -m "feat(toast): add ToastDrawing.RoundedRectangle helper"
```

---

## Task 7: Add `SoftToastRenderer` (image 1)

**Files:**
- Create: `src/WinformsMVP/Services/Implementations/SoftToastRenderer.cs`

- [ ] **Step 1: Create the renderer**

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// A soft, light-tinted toast: a pale background colored by type, a filled-circle icon in the
    /// left gutter, dark centered message text, and a colored close glyph. Rounded corners.
    /// </summary>
    /// <remarks>Override the <c>GetXxx</c> hooks to recolor or re-icon without rewriting the layout.</remarks>
    public class SoftToastRenderer : ToastRenderer
    {
        /// <summary>Rounded corners for the soft style.</summary>
        public override int CornerRadius
        {
            get { return 10; }
        }

        public override void Render(ToastRenderContext context)
        {
            Graphics g = context.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int width = context.Bounds.Width;
            int height = context.Bounds.Height;
            Font font = context.Font;

            Color tint = GetTintColor(context.Type);
            Color accent = GetAccentColor(context.Type);

            g.Clear(tint);

            // Anti-aliased rounded border softens the non-AA window region edge.
            using (var pen = new Pen(accent, 1.5f))
            using (var border = ToastDrawing.RoundedRectangle(new Rectangle(0, 0, width - 1, height - 1), context.CornerRadius))
            {
                g.DrawPath(pen, border);
            }

            int diameter = height - 28;
            if (diameter > 36) diameter = 36;
            if (diameter < 16) diameter = 16;
            var circle = new Rectangle(16, (height - diameter) / 2, diameter, diameter);

            int textLeft = circle.Right + 12;
            int textRight = context.ShowCloseButton ? width - 32 : width - 16;

            using (var iconFont = new Font(font.FontFamily, font.Size * 1.3f, FontStyle.Bold))
            using (var accentBrush = new SolidBrush(accent))
            using (var textBrush = new SolidBrush(GetTextColor()))
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var message = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.FillEllipse(accentBrush, circle);
                g.DrawString(GetIcon(context.Type), iconFont, Brushes.White, circle, centered);

                g.DrawString(context.Message, font, textBrush, new RectangleF(textLeft, 8, textRight - textLeft, height - 16), message);

                if (context.ShowCloseButton)
                {
                    using (var closeFont = new Font(font.FontFamily, font.Size, FontStyle.Bold))
                    {
                        g.DrawString("✖", closeFont, accentBrush, new RectangleF(width - 28, 6, 20, 20), centered);
                    }
                }
            }
        }

        /// <summary>The pale background color for a toast kind. Override to recolor.</summary>
        protected virtual Color GetTintColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(222, 243, 228);
                case ToastType.Warning: return Color.FromArgb(251, 243, 213);
                case ToastType.Error: return Color.FromArgb(251, 224, 225);
                case ToastType.Info:
                default: return Color.FromArgb(220, 235, 251);
            }
        }

        /// <summary>The strong accent color (icon circle, border, close glyph). Override to recolor.</summary>
        protected virtual Color GetAccentColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(16, 137, 62);
                case ToastType.Warning: return Color.FromArgb(255, 140, 0);
                case ToastType.Error: return Color.FromArgb(232, 17, 35);
                case ToastType.Info:
                default: return Color.FromArgb(0, 120, 215);
            }
        }

        /// <summary>The message text color. Override to recolor.</summary>
        protected virtual Color GetTextColor()
        {
            return Color.FromArgb(64, 64, 64);
        }

        /// <summary>The icon glyph for a toast kind. Override to re-icon.</summary>
        protected virtual string GetIcon(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return "✓";
                case ToastType.Warning: return "!";
                case ToastType.Error: return "✕";
                case ToastType.Info:
                default: return "i";
            }
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/SoftToastRenderer.cs
git commit -m "feat(toast): add SoftToastRenderer (light tinted, rounded)"
```

---

## Task 8: Add `CardToastRenderer` (image 2)

**Files:**
- Create: `src/WinformsMVP/Services/Implementations/CardToastRenderer.cs`

- [ ] **Step 1: Create the renderer**

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// A white card toast: a colored rounded accent bar on the left, a filled-circle icon, dark
    /// left-aligned message text, and a gray close glyph. Rounded corners; no shadow.
    /// </summary>
    /// <remarks>Override the <c>GetXxx</c> hooks to recolor or re-icon without rewriting the layout.</remarks>
    public class CardToastRenderer : ToastRenderer
    {
        /// <summary>Rounded corners for the card style.</summary>
        public override int CornerRadius
        {
            get { return 10; }
        }

        public override void Render(ToastRenderContext context)
        {
            Graphics g = context.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int width = context.Bounds.Width;
            int height = context.Bounds.Height;
            Font font = context.Font;

            Color accent = GetAccentColor(context.Type);

            g.Clear(GetBackgroundColor());

            // Subtle rounded border in place of a shadow.
            using (var pen = new Pen(GetBorderColor(), 1f))
            using (var border = ToastDrawing.RoundedRectangle(new Rectangle(0, 0, width - 1, height - 1), context.CornerRadius))
            {
                g.DrawPath(pen, border);
            }

            // Left accent bar (rounded, inset vertically).
            using (var accentBrush = new SolidBrush(accent))
            using (var bar = ToastDrawing.RoundedRectangle(new Rectangle(6, 10, 6, height - 20), 3))
            {
                g.FillPath(accentBrush, bar);
            }

            int diameter = height - 28;
            if (diameter > 36) diameter = 36;
            if (diameter < 16) diameter = 16;
            var circle = new Rectangle(20, (height - diameter) / 2, diameter, diameter);

            int textLeft = circle.Right + 12;
            int textRight = context.ShowCloseButton ? width - 32 : width - 16;

            using (var iconFont = new Font(font.FontFamily, font.Size * 1.3f, FontStyle.Bold))
            using (var accentBrush = new SolidBrush(accent))
            using (var textBrush = new SolidBrush(GetTextColor()))
            using (var closeBrush = new SolidBrush(GetCloseColor()))
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var message = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.FillEllipse(accentBrush, circle);
                g.DrawString(GetIcon(context.Type), iconFont, Brushes.White, circle, centered);

                g.DrawString(context.Message, font, textBrush, new RectangleF(textLeft, 8, textRight - textLeft, height - 16), message);

                if (context.ShowCloseButton)
                {
                    using (var closeFont = new Font(font.FontFamily, font.Size, FontStyle.Bold))
                    {
                        g.DrawString("✖", closeFont, closeBrush, new RectangleF(width - 28, 6, 20, 20), centered);
                    }
                }
            }
        }

        /// <summary>The card background color. Override to recolor.</summary>
        protected virtual Color GetBackgroundColor()
        {
            return Color.White;
        }

        /// <summary>The card border color. Override to recolor.</summary>
        protected virtual Color GetBorderColor()
        {
            return Color.FromArgb(230, 230, 230);
        }

        /// <summary>The message text color. Override to recolor.</summary>
        protected virtual Color GetTextColor()
        {
            return Color.FromArgb(64, 64, 64);
        }

        /// <summary>The close glyph color. Override to recolor.</summary>
        protected virtual Color GetCloseColor()
        {
            return Color.FromArgb(153, 153, 153);
        }

        /// <summary>The strong accent color (icon circle, accent bar). Override to recolor.</summary>
        protected virtual Color GetAccentColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(16, 137, 62);
                case ToastType.Warning: return Color.FromArgb(255, 140, 0);
                case ToastType.Error: return Color.FromArgb(232, 17, 35);
                case ToastType.Info:
                default: return Color.FromArgb(0, 120, 215);
            }
        }

        /// <summary>The icon glyph for a toast kind. Override to re-icon.</summary>
        protected virtual string GetIcon(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return "✓";
                case ToastType.Warning: return "!";
                case ToastType.Error: return "✕";
                case ToastType.Info:
                default: return "i";
            }
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/CardToastRenderer.cs
git commit -m "feat(toast): add CardToastRenderer (white card, accent bar, rounded)"
```

---

## Task 9: Add `ToastRendererResolver` (TDD)

**Files:**
- Create: `tests/WinformsMVP.Samples.Tests/Services/ToastRendererResolverTests.cs`
- Create: `src/WinformsMVP/Services/Implementations/ToastRendererResolver.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests <see cref="ToastRendererResolver"/>, the pure precedence logic that picks a toast
    /// painter most-specific-first. No windows are created.
    /// </summary>
    public class ToastRendererResolverTests
    {
        private sealed class StubRenderer : ToastRenderer
        {
            public override void Render(ToastRenderContext context) { }
        }

        [Fact]
        public void PerCallRenderer_WinsOverEverything()
        {
            var perCall = new StubRenderer();
            var result = ToastRendererResolver.Resolve(perCall, ToastStyle.Card, new StubRenderer(), ToastStyle.Soft);
            Assert.Same(perCall, result);
        }

        [Fact]
        public void PerCallStyle_WinsOverDefaults_WhenNoPerCallRenderer()
        {
            var result = ToastRendererResolver.Resolve(null, ToastStyle.Soft, new StubRenderer(), ToastStyle.Card);
            Assert.IsType<SoftToastRenderer>(result);
        }

        [Fact]
        public void DefaultRenderer_WinsOverDefaultStyle_WhenNoPerCallValues()
        {
            var defaultRenderer = new StubRenderer();
            var result = ToastRendererResolver.Resolve(null, null, defaultRenderer, ToastStyle.Soft);
            Assert.Same(defaultRenderer, result);
        }

        [Fact]
        public void DefaultStyle_IsUsed_WhenNothingElseSet()
        {
            var result = ToastRendererResolver.Resolve(null, null, null, ToastStyle.Card);
            Assert.IsType<CardToastRenderer>(result);
        }

        [Fact]
        public void ForStyle_Default_ReturnsDefaultRenderer()
        {
            Assert.IsType<DefaultToastRenderer>(ToastRendererResolver.ForStyle(ToastStyle.Default));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: FAIL — `ToastRendererResolver` does not exist (compile error).

- [ ] **Step 3: Implement the resolver**

```csharp
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Picks the toast painter most-specific-first: per-call renderer, then per-call style, then
    /// the app-wide default renderer, then the app-wide default style. The built-in style renderers
    /// are stateless and shared as singletons.
    /// </summary>
    internal static class ToastRendererResolver
    {
        private static readonly DefaultToastRenderer Default = new DefaultToastRenderer();
        private static readonly SoftToastRenderer Soft = new SoftToastRenderer();
        private static readonly CardToastRenderer Card = new CardToastRenderer();

        /// <summary>Maps a <see cref="ToastStyle"/> to its built-in renderer singleton.</summary>
        public static ToastRenderer ForStyle(ToastStyle style)
        {
            switch (style)
            {
                case ToastStyle.Soft: return Soft;
                case ToastStyle.Card: return Card;
                case ToastStyle.Default:
                default: return Default;
            }
        }

        /// <summary>
        /// Resolves the renderer for a toast. A custom renderer (per call, then app-wide) always
        /// wins over a style; a per-call value always wins over the app-wide default.
        /// </summary>
        public static ToastRenderer Resolve(ToastRenderer perCallRenderer, ToastStyle? perCallStyle, ToastRenderer defaultRenderer, ToastStyle defaultStyle)
        {
            if (perCallRenderer != null) return perCallRenderer;
            if (perCallStyle.HasValue) return ForStyle(perCallStyle.Value);
            if (defaultRenderer != null) return defaultRenderer;
            return ForStyle(defaultStyle);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: PASS (all 5 resolver tests).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/ToastRendererResolver.cs tests/WinformsMVP.Samples.Tests/Services/ToastRendererResolverTests.cs
git commit -m "feat(toast): add ToastRendererResolver with precedence (tested)"
```

---

## Task 10: Renderer CornerRadius + render smoke tests (TDD)

**Files:**
- Create: `tests/WinformsMVP.Samples.Tests/Services/ToastRendererTests.cs`

- [ ] **Step 1: Write the tests**

```csharp
using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests the built-in renderers' corner shape and that they paint without throwing. Pixel
    /// output is verified manually via the sample, not asserted here.
    /// </summary>
    public class ToastRendererTests
    {
        [Fact]
        public void DefaultRenderer_IsSquare()
        {
            Assert.Equal(0, new DefaultToastRenderer().CornerRadius);
        }

        [Fact]
        public void SoftRenderer_IsRounded()
        {
            Assert.True(new SoftToastRenderer().CornerRadius > 0);
        }

        [Fact]
        public void CardRenderer_IsRounded()
        {
            Assert.True(new CardToastRenderer().CornerRadius > 0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Renderers_PaintWithoutThrowing(bool showClose)
        {
            ToastRenderer[] renderers =
            {
                new DefaultToastRenderer(),
                new SoftToastRenderer(),
                new CardToastRenderer()
            };

            using (var bitmap = new Bitmap(350, 80))
            using (var g = Graphics.FromImage(bitmap))
            using (var font = new Font("Segoe UI", 10f))
            {
                foreach (var renderer in renderers)
                {
                    foreach (ToastType type in new[] { ToastType.Info, ToastType.Success, ToastType.Warning, ToastType.Error })
                    {
                        var context = new ToastRenderContext(g, new Rectangle(0, 0, 350, 80), "Sample message", type, font, renderer.CornerRadius, showClose);
                        renderer.Render(context); // must not throw
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: PASS. (The `ToastRenderContext` internal constructor is reachable via the existing
`InternalsVisibleTo("WinformsMVP.Samples.Tests")`.)

- [ ] **Step 3: Commit**

```bash
git add tests/WinformsMVP.Samples.Tests/Services/ToastRendererTests.cs
git commit -m "test(toast): cover renderer corner shape and render smoke"
```

---

## Task 11: Wire `ToastNotification` to the resolver + apply the rounded window region

**Files:**
- Modify: `src/WinformsMVP/Services/Implementations/ToastNotification.cs`

- [ ] **Step 1: Replace the renderer field with a resolved-once renderer**

Remove these two lines:

```csharp
        private readonly ToastRenderer _renderer; // per-toast override; may be null (falls back to defaults)

        // Last-resort painter if both the per-toast and app-wide renderers are null.
        private static readonly DefaultToastRenderer FallbackRenderer = new DefaultToastRenderer();
```

Add in their place:

```csharp
        private readonly ToastRenderer _renderer; // resolved once at construction; never null
```

In the constructor, replace:

```csharp
            _renderer = options.Renderer; // resolved against ToastDefaults.Renderer at paint time
```

with:

```csharp
            _renderer = ToastRendererResolver.Resolve(
                options.Renderer, options.Style, ToastDefaults.Renderer, ToastDefaults.Style);
```

- [ ] **Step 2: Simplify `Render` to use the resolved renderer**

Replace the `Render(Graphics g)` method:

```csharp
        private void Render(Graphics g)
        {
            _renderer.Render(new ToastRenderContext(g, new Rectangle(0, 0, _width, _height), _message, _type, _font, _renderer.CornerRadius, _showCloseButton));
        }
```

- [ ] **Step 3: Apply the rounded window region after the handle is created**

In `CreatePopupAt`, insert a call right after `CreateHandle(cp);` and before `ApplyOpacity();`:

```csharp
            CreateHandle(cp);
            ApplyCornerRadius();
            ApplyOpacity();
```

Add the method (next to `ApplyOpacity`):

```csharp
        /// <summary>
        /// Rounds the popup to the resolved renderer's <see cref="ToastRenderer.CornerRadius"/> by
        /// applying a window region. A radius of <c>0</c> leaves the window square. The OS takes
        /// ownership of the region on <c>SetWindowRgn(..., true)</c>, so it must not be deleted here.
        /// </summary>
        private void ApplyCornerRadius()
        {
            int radius = _renderer.CornerRadius;
            if (radius <= 0 || Handle == IntPtr.Zero)
            {
                return;
            }

            // CreateRoundRectRgn treats the right/bottom as exclusive, so use width/height + 1.
            IntPtr region = CreateRoundRectRgn(0, 0, _width + 1, _height + 1, radius * 2, radius * 2);
            SetWindowRgn(Handle, region, true);
        }
```

- [ ] **Step 4: Add the P/Invoke declarations**

In the `#region Win32 interop` block, add next to the other `DllImport`s:

```csharp
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
```

- [ ] **Step 5: Build and run the full test suite**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Then: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: Build succeeded; all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/ToastNotification.cs
git commit -m "feat(toast): resolve renderer via resolver and round the window region"
```

---

## Task 12: Update the sample to showcase styles + close-button toggle

**Files:**
- Modify: `samples/WinformsMVP.Samples/ToastDemo/ToastDemoForm.cs`

- [ ] **Step 1: Rename the private demo renderer to avoid clashing with the new built-in**

The form has a private nested `CardToastRenderer`. The framework now ships a public
`CardToastRenderer`, so rename the nested one to `DarkCardToastRenderer` and update its single use.

Replace the nested class declaration line:

```csharp
        private sealed class CardToastRenderer : ToastRenderer
```

with:

```csharp
        private sealed class DarkCardToastRenderer : ToastRenderer
```

In `ShowWithCustomRenderer`, replace:

```csharp
            options.Renderer = new CardToastRenderer();
```

with:

```csharp
            options.Renderer = new DarkCardToastRenderer();
```

- [ ] **Step 2: Add a style selector and a close-button checkbox to the options panel**

Add fields next to the other control fields:

```csharp
        private ComboBox _styleCombo;
        private CheckBox _showCloseCheck;
```

In `InitializeComponent`, after the `_durationNum` row (the block ending with the `durationLabel`),
insert two new rows before the `// --- Action buttons ---` comment:

```csharp
            y += rowH;
            _styleCombo = new ComboBox
            {
                Location = new Point(fieldX, y),
                Size = new Size(160, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _styleCombo.Items.AddRange(new object[] { ToastStyle.Default, ToastStyle.Soft, ToastStyle.Card });
            _styleCombo.SelectedItem = ToastStyle.Soft;
            var styleLabel = MakeLabel("Style:", labelX, y + 3);

            y += rowH;
            _showCloseCheck = new CheckBox
            {
                Text = "Show close button",
                Location = new Point(fieldX, y),
                Size = new Size(180, 24),
                Checked = true
            };
            var styleHintLabel = MakeLabel("Close glyph:", labelX, y + 3);
```

- [ ] **Step 3: Register the new controls**

In the `this.Controls.AddRange(new Control[] { ... })` call, add to the list (after the
`durationLabel, _durationNum,` line):

```csharp
                styleLabel, _styleCombo,
                styleHintLabel, _showCloseCheck,
```

- [ ] **Step 4: Feed the new controls into `CurrentOptions`**

Replace the `CurrentOptions` method:

```csharp
        /// <summary>Builds a <see cref="ToastOptions"/> from the current control values.</summary>
        private ToastOptions CurrentOptions()
        {
            return new ToastOptions
            {
                Position = (ToastPosition)_positionCombo.SelectedItem,
                Size = new Size((int)_widthNum.Value, (int)_heightNum.Value),
                Font = new Font("Segoe UI", (float)_fontSizeNum.Value),
                Duration = (int)_durationNum.Value,
                Style = (ToastStyle)_styleCombo.SelectedItem,
                ShowCloseButton = _showCloseCheck.Checked
            };
        }
```

- [ ] **Step 5: Grow the form so the new rows fit**

In `InitializeComponent`, change:

```csharp
            this.Size = new Size(520, 714);
```

to:

```csharp
            this.Size = new Size(520, 782);
```

- [ ] **Step 6: Build the sample**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Expected: Build succeeded.

- [ ] **Step 7: Manual verification**

Run: `dotnet run --project samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
In the launcher open the Toast demo. For each `Style` (Default/Soft/Card): fire Info/Success/
Warning/Error and confirm — Default is square with a solid color; Soft is a rounded light-tinted
box with a colored circle icon; Card is a rounded white box with a left accent bar. Toggle
"Show close button" off and confirm the ✖ disappears while clicking the toast still dismisses it.

- [ ] **Step 8: Commit**

```bash
git add samples/WinformsMVP.Samples/ToastDemo/ToastDemoForm.cs
git commit -m "docs(samples): demo toast styles and close-button toggle"
```

---

## Task 13: Documentation (wiki + changelog)

**Files:**
- Modify: `wiki/Reference-Toast-Notifications.md`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Add a "Styles" section to the wiki**

Open `wiki/Reference-Toast-Notifications.md`. After the section that documents `ToastOptions`/
`ToastDefaults` appearance, add:

```markdown
## Styles

Toasts have three built-in appearances, selected by `ToastStyle`:

| Style     | Shape   | Look |
|-----------|---------|------|
| `Default` | square  | Solid color background, white text and icon. |
| `Soft`    | rounded | Light tinted background, filled-circle icon, dark text. |
| `Card`    | rounded | White card, colored left accent bar, filled-circle icon, dark text. |

Pick a style per call or app-wide:

```csharp
Messages.ShowToast("Saved", ToastType.Success, new ToastOptions { Style = ToastStyle.Soft });
ToastDefaults.Style = ToastStyle.Card; // app-wide default
```

Corner shape travels with the renderer via `ToastRenderer.CornerRadius` (Default `0` = square).
A custom `Renderer` always wins over `Style`. Resolution order, most-specific first:
`ToastOptions.Renderer` → `ToastOptions.Style` → `ToastDefaults.Renderer` → `ToastDefaults.Style`.

### Close button

The close glyph is display-only — clicking anywhere on a toast dismisses it regardless. Hide it
per call or app-wide:

```csharp
Messages.ShowToast("No close glyph", ToastType.Info, new ToastOptions { ShowCloseButton = false });
ToastDefaults.ShowCloseButton = false; // app-wide
```

The built-in `SoftToastRenderer` / `CardToastRenderer` are `public` and expose `protected virtual`
color/icon hooks — subclass to recolor or re-icon without rewriting the layout, the same way you
can subclass `DefaultToastRenderer`.
```

- [ ] **Step 2: Add a CHANGELOG entry**

In `CHANGELOG.md`, under the `## [Unreleased]` → `### Added (追加)` list (the first one, at the top of the file), add:

```markdown
- Built-in toast styles `ToastStyle.Soft` / `ToastStyle.Card` (rounded) alongside the existing
  `Default` (square), selectable via `ToastOptions.Style` / `ToastDefaults.Style`. Custom renderers
  still override styles. Public `SoftToastRenderer` / `CardToastRenderer` with `protected virtual`
  color/icon hooks. New `ToastRenderer.CornerRadius` rounds the toast window.
- Configurable toast close glyph via `ToastOptions.ShowCloseButton` / `ToastDefaults.ShowCloseButton`
  (display only; click-to-dismiss is unchanged).
```


- [ ] **Step 3: Build the whole solution and run all tests as a final gate**

Run: `dotnet build winforms-mvp.sln`
Then: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: Build succeeded; all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add wiki/Reference-Toast-Notifications.md CHANGELOG.md
git commit -m "docs: document toast styles and close-button toggle"
```

---

## Self-Review Notes

- **Spec coverage:** ToastStyle enum (Task 1), Style/ShowCloseButton on options+defaults (Tasks 3–4),
  resolver precedence (Task 9), CornerRadius-as-renderer-property + window region (Tasks 2, 5, 11),
  Soft/Card renderers (Tasks 7–8), close-button display incl. Default (Task 5), tests (Tasks 9–10),
  sample (Task 12), wiki+CHANGELOG (Task 13). No signature/interface changes (verified:
  `MessageService.ShowToast` passes `ToastOptions` straight to `ToastNotification`).
- **Type consistency:** `ToastRendererResolver.Resolve(perCallRenderer, perCallStyle, defaultRenderer, defaultStyle)`
  and `ForStyle(style)` match between Task 9's definition and Task 11's call. `ToastRenderContext`'s
  7-arg internal ctor (Task 5) matches every construction (Tasks 5, 10, 11). `CornerRadius` /
  `ShowCloseButton` names are consistent throughout.
- **Staging:** every task builds on its own; Task 5 deliberately keeps the old `??` resolution so it
  compiles before the resolver exists, and Task 11 replaces it.
```
