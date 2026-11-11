using System.Net;
using System.Text.RegularExpressions;

namespace SmartTask.Application.Services.Ai;

/// <summary>
/// Cleans model output before it reaches the client: decodes HTML entities (the prompt escapes
/// <c>&lt;</c>/<c>&gt;</c>, so the model may echo them), strips HTML tags, normalises line endings to
/// <c>\n</c>, collapses runs of blank lines and trims. Markdown lists are left alone.
/// </summary>
/// <remarks>
/// Decoding comes <b>before</b> stripping, so an encoded tag such as <c>&amp;lt;script&amp;gt;</c> is
/// removed too. The Angular client still renders the text as text; this is defence in depth.
/// </remarks>
internal static partial class AiOutputSanitizer
{
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(value);
        text = HtmlTag().Replace(text, string.Empty);
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        text = ExtraBlankLines().Replace(text, "\n\n");
        return text.Trim();
    }

    /// <summary>Opening, closing and self-closing tags, and comments. <c>a &lt; b</c> is not a tag and stays.</summary>
    [GeneratedRegex(@"<!--.*?-->|</?[A-Za-z][^<>]*>", RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExtraBlankLines();
}
