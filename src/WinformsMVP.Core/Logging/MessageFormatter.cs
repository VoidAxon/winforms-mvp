using System;
using System.Text;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Formats log message templates that may use either Microsoft.Extensions.Logging-style
    /// named placeholders (e.g. <c>"User {UserName} did {Action}"</c>) or
    /// <see cref="string.Format(string, object[])"/>-style indexed placeholders
    /// (e.g. <c>"User {0} did {1}"</c>).
    ///
    /// <para>
    /// Named placeholders are rewritten to indexed placeholders in declaration order before
    /// formatting, so legacy call sites that came from <c>Microsoft.Extensions.Logging</c>
    /// continue to format correctly under this abstraction. Already-indexed templates pass
    /// through untouched.
    /// </para>
    /// </summary>
    public static class MessageFormatter
    {
        public static string Format(string message, object[] args)
        {
            if (string.IsNullOrEmpty(message)) return message ?? string.Empty;
            if (args == null || args.Length == 0) return message;

            var normalized = NormalizeNamedPlaceholders(message);
            try
            {
                return string.Format(normalized, args);
            }
            catch (FormatException)
            {
                return message;
            }
        }

        /// <summary>
        /// Rewrites <c>{Name}</c> placeholders to <c>{0}</c>, <c>{1}</c>, ... in declaration
        /// order. Recognises only ASCII letter/digit/underscore identifiers; anything else
        /// inside braces (including existing indexed placeholders like <c>{0}</c>, format
        /// specifiers like <c>{0:N2}</c>, and escaped braces <c>{{</c>/<c>}}</c>) is preserved.
        /// </summary>
        private static string NormalizeNamedPlaceholders(string template)
        {
            // Quick exit when there is no '{' at all.
            if (template.IndexOf('{') < 0) return template;

            var sb = new StringBuilder(template.Length);
            int i = 0;
            int nextIndex = 0;

            while (i < template.Length)
            {
                char c = template[i];

                if (c == '{')
                {
                    // Escaped opening brace "{{" — copy verbatim.
                    if (i + 1 < template.Length && template[i + 1] == '{')
                    {
                        sb.Append("{{");
                        i += 2;
                        continue;
                    }

                    // Find the matching '}'.
                    int end = template.IndexOf('}', i + 1);
                    if (end < 0)
                    {
                        // Unmatched '{' — leave the rest alone.
                        sb.Append(template, i, template.Length - i);
                        break;
                    }

                    // Extract the body between the braces, e.g. "UserName" or "0:N2".
                    string body = template.Substring(i + 1, end - i - 1);

                    // Split off any format specifier (after ':') or alignment (after ',').
                    int separator = body.IndexOfAny(new[] { ':', ',' });
                    string name = separator < 0 ? body : body.Substring(0, separator);
                    string trailing = separator < 0 ? string.Empty : body.Substring(separator);

                    if (IsNamedIdentifier(name))
                    {
                        sb.Append('{').Append(nextIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        sb.Append(trailing);
                        sb.Append('}');
                        nextIndex++;
                    }
                    else
                    {
                        // Either indexed ({0}) or malformed — copy verbatim.
                        sb.Append('{').Append(body).Append('}');
                    }

                    i = end + 1;
                }
                else if (c == '}' && i + 1 < template.Length && template[i + 1] == '}')
                {
                    sb.Append("}}");
                    i += 2;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
        }

        private static bool IsNamedIdentifier(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            // Must start with a letter or underscore (not a digit, which would be indexed).
            char first = text[0];
            if (!(char.IsLetter(first) || first == '_')) return false;
            for (int i = 1; i < text.Length; i++)
            {
                char ch = text[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '_')) return false;
            }
            return true;
        }
    }
}
