using WinformsMVP.Logging;
using Xunit;

namespace WinformsMVP.Samples.Tests.Logging
{
    /// <summary>
    /// Behavioural tests for <see cref="MessageFormatter"/>. The formatter is load-bearing
    /// because it lets call sites use Microsoft.Extensions.Logging-style named placeholders
    /// (e.g. <c>"User {UserName}"</c>) against the in-house abstraction without changes.
    /// </summary>
    /// <remarks>
    /// Format-specifier assertions use alignment and hex (<c>:x</c>) to stay culture-insensitive.
    /// Numeric specifiers like <c>:N2</c> would render differently under fr-FR vs. en-US.
    /// </remarks>
    public class MessageFormatterTests
    {
        // ───── Trivial / degenerate inputs ─────────────────────────────────

        [Fact]
        public void NullMessage_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, MessageFormatter.Format(null, new object[] { "x" }));
        }

        [Fact]
        public void EmptyMessage_ReturnsEmptyString()
        {
            Assert.Equal(string.Empty, MessageFormatter.Format(string.Empty, new object[] { "x" }));
        }

        [Fact]
        public void NullArgs_ReturnsTemplateUnchanged()
        {
            Assert.Equal("hello {Name}", MessageFormatter.Format("hello {Name}", null));
        }

        [Fact]
        public void EmptyArgs_ReturnsTemplateUnchanged()
        {
            Assert.Equal("hello {Name}", MessageFormatter.Format("hello {Name}", new object[0]));
        }

        [Fact]
        public void NoPlaceholders_ReturnsTemplateUnchanged()
        {
            Assert.Equal("plain text", MessageFormatter.Format("plain text", new object[] { "x" }));
        }

        // ───── Indexed placeholders (legacy string.Format style) ───────────

        [Fact]
        public void IndexedPlaceholder_FormatsAsStringFormatWould()
        {
            Assert.Equal(
                "User Alice",
                MessageFormatter.Format("User {0}", new object[] { "Alice" }));
        }

        [Fact]
        public void IndexedPlaceholderWithHexFormat_PreservesFormat()
        {
            Assert.Equal(
                "value: ff",
                MessageFormatter.Format("value: {0:x2}", new object[] { 255 }));
        }

        [Fact]
        public void IndexedPlaceholderWithAlignment_PreservesAlignment()
        {
            Assert.Equal(
                "[   abc]",
                MessageFormatter.Format("[{0,6}]", new object[] { "abc" }));
        }

        [Fact]
        public void IndexedPlaceholderWithAlignmentAndFormat_BothPreserved()
        {
            Assert.Equal(
                "[    ff]",
                MessageFormatter.Format("[{0,6:x2}]", new object[] { 255 }));
        }

        // ───── Named placeholders (M.E.L. style) ───────────────────────────

        [Fact]
        public void NamedPlaceholder_RewrittenToIndexed()
        {
            Assert.Equal(
                "User Alice",
                MessageFormatter.Format("User {UserName}", new object[] { "Alice" }));
        }

        [Fact]
        public void MultipleNamedPlaceholders_RewrittenInDeclarationOrder()
        {
            Assert.Equal(
                "Alice did Save",
                MessageFormatter.Format("{UserName} did {Action}", new object[] { "Alice", "Save" }));
        }

        [Fact]
        public void NamedPlaceholderWithHexFormat_FormatPreserved()
        {
            Assert.Equal(
                "value: ff",
                MessageFormatter.Format("value: {Count:x2}", new object[] { 255 }));
        }

        [Fact]
        public void NamedPlaceholderWithAlignment_AlignmentPreserved()
        {
            Assert.Equal(
                "[   abc]",
                MessageFormatter.Format("[{Name,6}]", new object[] { "abc" }));
        }

        [Fact]
        public void NamedPlaceholderWithAlignmentAndFormat_BothPreserved()
        {
            Assert.Equal(
                "[    ff]",
                MessageFormatter.Format("[{Value,6:x2}]", new object[] { 255 }));
        }

        [Fact]
        public void UnderscoreIdentifier_TreatedAsNamedPlaceholder()
        {
            Assert.Equal(
                "value=42",
                MessageFormatter.Format("value={_priv}", new object[] { 42 }));
        }

        [Fact]
        public void IdentifierWithDigits_TreatedAsNamedPlaceholder()
        {
            Assert.Equal(
                "ok",
                MessageFormatter.Format("{Name1}", new object[] { "ok" }));
        }

        // ───── Escaped braces ──────────────────────────────────────────────

        [Fact]
        public void EscapedOpeningBrace_PassesThrough()
        {
            Assert.Equal(
                "literal { brace",
                MessageFormatter.Format("literal {{ brace", new object[] { "x" }));
        }

        [Fact]
        public void EscapedClosingBrace_PassesThrough()
        {
            Assert.Equal(
                "literal } brace",
                MessageFormatter.Format("literal }} brace", new object[] { "x" }));
        }

        [Fact]
        public void EscapedBracesAroundIdentifier_DoNotBecomePlaceholder()
        {
            // {{Name}} should render as "{Name}" literal, not interpolate.
            Assert.Equal(
                "{Name}",
                MessageFormatter.Format("{{Name}}", new object[] { "Alice" }));
        }

        [Fact]
        public void MixedEscapedAndRealPlaceholder_RealOneInterpolates()
        {
            Assert.Equal(
                "{0} = Alice",
                MessageFormatter.Format("{{0}} = {UserName}", new object[] { "Alice" }));
        }

        // ───── Malformed templates ─────────────────────────────────────────

        [Fact]
        public void UnmatchedOpeningBrace_FallsBackToOriginalMessage()
        {
            // NormalizeNamedPlaceholders copies the tail verbatim when '{' has no matching '}'.
            // string.Format then throws FormatException -> we return the original template.
            Assert.Equal(
                "broken {Name",
                MessageFormatter.Format("broken {Name", new object[] { "Alice" }));
        }

        [Fact]
        public void HyphenInIdentifier_TreatedAsMalformed_FallsBackToOriginalMessage()
        {
            // "{User-Name}" is not a valid identifier; copied verbatim by NormalizeNamedPlaceholders.
            // string.Format throws -> original message returned.
            Assert.Equal(
                "x {User-Name} y",
                MessageFormatter.Format("x {User-Name} y", new object[] { "Alice" }));
        }

        [Fact]
        public void EmptyBraces_FallsBackToOriginalMessage()
        {
            // "{}" has empty body. Copied verbatim, then string.Format throws FormatException
            // on the "{}" token -> original message returned.
            Assert.Equal(
                "x {} y",
                MessageFormatter.Format("x {} y", new object[] { "Alice" }));
        }

        [Fact]
        public void IdentifierStartingWithDigit_FallsBackToOriginalMessage()
        {
            // "{1Name}" can't be a valid identifier (digit start) and can't be a pure index
            // (mixed letters). Body is copied verbatim, then string.Format fails -> original.
            Assert.Equal(
                "{1Name}",
                MessageFormatter.Format("{1Name}", new object[] { "Alice" }));
        }

        // ───── Mixed indexed + named (boundary behaviour) ──────────────────

        [Fact]
        public void MixedNamedAndIndexed_NamedRewriteCollidesWithLiteralIndex()
        {
            // Documenting current behaviour: {Name} becomes {0}, the literal {0} stays {0}.
            // Both point at args[0], so the output looks "duplicated". Callers should pick
            // one style per template. This test pins the behaviour so regressions are
            // visible rather than silent.
            Assert.Equal(
                "Alice / Alice",
                MessageFormatter.Format("{Name} / {0}", new object[] { "Alice", "Bob" }));
        }

        // ───── Performance/quick-exit path ─────────────────────────────────

        [Fact]
        public void TemplateWithoutOpeningBrace_TakesQuickExitPath()
        {
            // Path coverage: NormalizeNamedPlaceholders returns input untouched when there is
            // no '{' at all. The subsequent string.Format runs on the unchanged template,
            // which has no placeholders, so args are unused but no exception is thrown.
            Assert.Equal(
                "plain message",
                MessageFormatter.Format("plain message", new object[] { "ignored" }));
        }
    }
}
