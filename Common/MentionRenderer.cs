using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace EventsApp.Common
{
    public static class MentionRenderer
    {
        private static readonly Regex MentionRegex = new(
            @"(?<![A-Za-z0-9_])@([A-Za-z0-9._]{3,30})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Renders text with @username tokens turned into linked styled spans.
        /// Output is HTML-safe — non-mention text is HTML-encoded.
        /// </summary>
        public static IHtmlContent RenderWithMentions(string? text)
        {
            if (string.IsNullOrEmpty(text)) return HtmlString.Empty;

            var sb = new StringBuilder(text.Length + 32);
            var lastIndex = 0;
            foreach (Match match in MentionRegex.Matches(text))
            {
                if (match.Index > lastIndex)
                {
                    sb.Append(WebUtility.HtmlEncode(text.Substring(lastIndex, match.Index - lastIndex)));
                }
                var username = match.Groups[1].Value;
                sb.Append("<a class=\"evt-mention\" href=\"/Profiles/ByName/")
                  .Append(WebUtility.UrlEncode(username))
                  .Append("\" data-mention=\"")
                  .Append(WebUtility.HtmlEncode(username))
                  .Append("\">@")
                  .Append(WebUtility.HtmlEncode(username))
                  .Append("</a>");
                lastIndex = match.Index + match.Length;
            }
            if (lastIndex < text.Length)
            {
                sb.Append(WebUtility.HtmlEncode(text.Substring(lastIndex)));
            }
            return new HtmlString(sb.ToString());
        }
    }
}
