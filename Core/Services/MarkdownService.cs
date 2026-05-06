using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DndBuilder.Core
{
    public static class MarkdownService
    {
        private static readonly int[] HeadingSizes = { 24, 20, 17, 15, 13, 12 };

        // Render markdown + [[wikilinks]] to BBCode.
        // lookup: lowercased-name → "entityType:id" (built by caller from DB).
        public static string Render(string source, Dictionary<string, string> lookup)
        {
            if (string.IsNullOrEmpty(source)) return "";

            var lines = source.Split('\n');
            var sb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(RenderLine(lines[i], lookup));
            }

            return sb.ToString();
        }

        private static string RenderLine(string line, Dictionary<string, string> lookup)
        {
            // Heading: # … ######
            var hm = Regex.Match(line, @"^(#{1,6}) (.+)$");
            if (hm.Success)
            {
                int level = hm.Groups[1].Length;
                int size  = HeadingSizes[level - 1];
                return $"[font_size={size}][b]{RenderInline(hm.Groups[2].Value, lookup)}[/b][/font_size]";
            }

            // Blockquote
            if (line.StartsWith("> "))
                return $"[indent][color=#888888]{RenderInline(line[2..], lookup)}[/color][/indent]";

            // Unordered list
            if (line.StartsWith("- "))
                return $"  • {RenderInline(line[2..], lookup)}";

            // Ordered list: "1. text"
            var om = Regex.Match(line, @"^(\d+)\. (.*)$");
            if (om.Success)
                return $"  {om.Groups[1].Value}. {RenderInline(om.Groups[2].Value, lookup)}";

            return RenderInline(line, lookup);
        }

        // Inline tokenizer: processes wikilinks, external links, bold, italic,
        // strikethrough, underline, escapes — left to right, no re-scanning.
        private static string RenderInline(string text, Dictionary<string, string> lookup)
        {
            var sb = new StringBuilder(text.Length + 32);
            int i  = 0;
            int n  = text.Length;

            while (i < n)
            {
                char c = text[i];

                // [[wikilink]] — checked before bare [
                if (c == '[' && i + 1 < n && text[i + 1] == '[')
                {
                    int close = text.IndexOf("]]", i + 2, StringComparison.Ordinal);
                    if (close >= 0)
                    {
                        string name = text.Substring(i + 2, close - i - 2);
                        if (!name.Contains('\n'))
                        {
                            if (lookup.TryGetValue(name.ToLowerInvariant(), out string url))
                                sb.Append($"[url={url}][color=#d4aa70]{EscapeBBCode(name)}[/color][/url]");
                            else
                                sb.Append($"[color=#888888][[{EscapeBBCode(name)}]][/color]");
                            i = close + 2;
                            continue;
                        }
                    }
                }

                // [text](url) external link
                if (c == '[')
                {
                    int tc = text.IndexOf(']', i + 1);
                    if (tc >= 0 && tc + 1 < n && text[tc + 1] == '(')
                    {
                        int uc = text.IndexOf(')', tc + 2);
                        if (uc >= 0)
                        {
                            string lt  = text.Substring(i + 1, tc - i - 1);
                            string url = text.Substring(tc + 2, uc - tc - 2);
                            sb.Append($"[url={url}][color=#7ab3e0]{EscapeBBCode(lt)}[/color][/url]");
                            i = uc + 1;
                            continue;
                        }
                    }
                }

                // **bold** or __bold__
                if ((c == '*' || c == '_') && i + 1 < n && text[i + 1] == c)
                {
                    string marker = new string(c, 2);
                    int close = FindClosing(text, i + 2, marker);
                    if (close >= 0)
                    {
                        string inner = text.Substring(i + 2, close - i - 2);
                        sb.Append($"[b]{RenderInline(inner, lookup)}[/b]");
                        i = close + 2;
                        continue;
                    }
                }

                // *italic* (single asterisk — greedy check for bold already passed)
                if (c == '*')
                {
                    int close = FindClosing(text, i + 1, "*");
                    if (close >= 0 && close > i + 1)
                    {
                        string inner = text.Substring(i + 1, close - i - 1);
                        sb.Append($"[i]{RenderInline(inner, lookup)}[/i]");
                        i = close + 1;
                        continue;
                    }
                }

                // _italic_ — only when opening _ is preceded by whitespace/start
                // and the following char is non-whitespace. Prevents snake_case italics.
                if (c == '_')
                {
                    bool validOpen = (i == 0 || char.IsWhiteSpace(text[i - 1]))
                                  && (i + 1 < n && !char.IsWhiteSpace(text[i + 1]));
                    if (validOpen)
                    {
                        int close = FindClosingUnderline(text, i + 1);
                        if (close >= 0)
                        {
                            string inner = text.Substring(i + 1, close - i - 1);
                            sb.Append($"[i]{RenderInline(inner, lookup)}[/i]");
                            i = close + 1;
                            continue;
                        }
                    }
                }

                // ~~strikethrough~~
                if (c == '~' && i + 1 < n && text[i + 1] == '~')
                {
                    int close = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                    if (close >= 0)
                    {
                        string inner = text.Substring(i + 2, close - i - 2);
                        sb.Append($"[s]{RenderInline(inner, lookup)}[/s]");
                        i = close + 2;
                        continue;
                    }
                }

                // <u>underline</u>
                if (c == '<' && i + 2 < n && text[i + 1] == 'u' && text[i + 2] == '>')
                {
                    int close = text.IndexOf("</u>", i + 3, StringComparison.Ordinal);
                    if (close >= 0)
                    {
                        string inner = text.Substring(i + 3, close - i - 3);
                        sb.Append($"[u]{RenderInline(inner, lookup)}[/u]");
                        i = close + 4;
                        continue;
                    }
                }

                // Backslash escape
                if (c == '\\' && i + 1 < n && IsEscapable(text[i + 1]))
                {
                    sb.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        // Find closing marker (two-char or one-char) that doesn't cross a newline.
        private static int FindClosing(string text, int from, string marker)
        {
            int idx = from;
            while (idx < text.Length)
            {
                int found = text.IndexOf(marker, idx, StringComparison.Ordinal);
                if (found < 0) return -1;
                // Don't cross line boundary
                int nl = text.IndexOf('\n', idx);
                if (nl >= 0 && nl < found) return -1;
                return found;
            }
            return -1;
        }

        // Find closing _ that is followed by whitespace/punctuation/end (CommonMark rule).
        private static int FindClosingUnderline(string text, int from)
        {
            int idx = from;
            while (idx < text.Length)
            {
                int found = text.IndexOf('_', idx);
                if (found < 0) return -1;
                int nl = text.IndexOf('\n', idx);
                if (nl >= 0 && nl < found) return -1;
                bool validClose = (found + 1 >= text.Length || char.IsWhiteSpace(text[found + 1])
                                   || char.IsPunctuation(text[found + 1]));
                if (validClose) return found;
                idx = found + 1;
            }
            return -1;
        }

        private static bool IsEscapable(char c)
            => c == '*' || c == '_' || c == '~' || c == '[' || c == ']'
            || c == '(' || c == ')' || c == '#' || c == '>' || c == '\\';

        // Escape BBCode brackets in user text so they don't corrupt the output.
        private static string EscapeBBCode(string text)
            => text.Replace("[", "[lb]");
    }
}
