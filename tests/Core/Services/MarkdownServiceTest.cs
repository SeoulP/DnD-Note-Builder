using System.Collections.Generic;
using GdUnit4;
using DndBuilder.Core;
using static GdUnit4.Assertions;

namespace DndBuilder.Tests.Core.Services
{
    [TestSuite]
    public class MarkdownServiceTest
    {
        private static Dictionary<string, string> Empty() => new();
        private static Dictionary<string, string> WithLink(string name, string url)
            => new() { { name.ToLowerInvariant(), url } };

        // ── plain text ────────────────────────────────────────────────────────

        [TestCase]
        public void Render_EmptyString_ReturnsEmpty()
        {
            AssertThat(MarkdownService.Render("", Empty())).IsEqual("");
        }

        [TestCase]
        public void Render_PlainText_PassesThrough()
        {
            AssertThat(MarkdownService.Render("hello world", Empty())).IsEqual("hello world");
        }

        // ── inline formatting ─────────────────────────────────────────────────

        [TestCase]
        public void Render_BoldAsterisks_EmitsBBCode()
        {
            AssertThat(MarkdownService.Render("**bold**", Empty())).IsEqual("[b]bold[/b]");
        }

        [TestCase]
        public void Render_BoldUnderscores_EmitsBBCode()
        {
            AssertThat(MarkdownService.Render("__bold__", Empty())).IsEqual("[b]bold[/b]");
        }

        [TestCase]
        public void Render_ItalicAsterisk_EmitsBBCode()
        {
            AssertThat(MarkdownService.Render("*italic*", Empty())).IsEqual("[i]italic[/i]");
        }

        [TestCase]
        public void Render_ItalicUnderscore_EmitsBBCode()
        {
            AssertThat(MarkdownService.Render("_italic_", Empty())).IsEqual("[i]italic[/i]");
        }

        [TestCase]
        public void Render_Strikethrough_EmitsBBCode()
        {
            AssertThat(MarkdownService.Render("~~strike~~", Empty())).IsEqual("[s]strike[/s]");
        }

        [TestCase]
        public void Render_Underline_EmitsBBCode()
        {
            AssertThat(MarkdownService.Render("<u>under</u>", Empty())).IsEqual("[u]under[/u]");
        }

        [TestCase]
        public void Render_SnakeCase_NotItalicized()
        {
            AssertThat(MarkdownService.Render("snake_case_value", Empty())).IsEqual("snake_case_value");
        }

        [TestCase]
        public void Render_BoldInsideText_Works()
        {
            AssertThat(MarkdownService.Render("hello **world** end", Empty())).IsEqual("hello [b]world[/b] end");
        }

        [TestCase]
        public void Render_UnclosedBold_LiteralAsterisks()
        {
            // Unclosed delimiter does not span a line break; within a single line we emit literal
            AssertThat(MarkdownService.Render("**unclosed", Empty())).IsEqual("**unclosed");
        }

        [TestCase]
        public void Render_BackslashEscape_LiteralChar()
        {
            AssertThat(MarkdownService.Render(@"\*not italic\*", Empty())).IsEqual("*not italic*");
        }

        // ── headings ─────────────────────────────────────────────────────────

        [TestCase]
        public void Render_H1_EmitsFontSizeBold()
        {
            string result = MarkdownService.Render("# Title", Empty());
            AssertThat(result).IsEqual("[font_size=24][b]Title[/b][/font_size]");
        }

        [TestCase]
        public void Render_H3_EmitsCorrectSize()
        {
            string result = MarkdownService.Render("### Sub", Empty());
            AssertThat(result).IsEqual("[font_size=17][b]Sub[/b][/font_size]");
        }

        // ── block elements ────────────────────────────────────────────────────

        [TestCase]
        public void Render_Blockquote_EmitsIndentColor()
        {
            string result = MarkdownService.Render("> quote", Empty());
            AssertThat(result).IsEqual("[indent][color=#888888]quote[/color][/indent]");
        }

        [TestCase]
        public void Render_BulletItem_EmitsBulletChar()
        {
            string result = MarkdownService.Render("- item", Empty());
            AssertThat(result).IsEqual("  • item");
        }

        [TestCase]
        public void Render_OrderedItem_EmitsNumber()
        {
            string result = MarkdownService.Render("1. first", Empty());
            AssertThat(result).IsEqual("  1. first");
        }

        // ── wikilinks ─────────────────────────────────────────────────────────

        [TestCase]
        public void Render_KnownWikilink_EmitsColoredUrl()
        {
            var lookup = WithLink("Graush", "npc:5");
            string result = MarkdownService.Render("[[Graush]]", lookup);
            AssertThat(result).IsEqual("[url=npc:5][color=#d4aa70]Graush[/color][/url]");
        }

        [TestCase]
        public void Render_UnknownWikilink_EmitsDimmedBrackets()
        {
            string result = MarkdownService.Render("[[Unknown]]", Empty());
            AssertThat(result).IsEqual("[color=#888888][[Unknown]][/color]");
        }

        [TestCase]
        public void Render_WikilinkInsideBold_WikilinkTakesPrecedence()
        {
            var lookup = WithLink("Graush", "npc:5");
            string result = MarkdownService.Render("**[[Graush]]**", lookup);
            AssertThat(result).IsEqual("[b][url=npc:5][color=#d4aa70]Graush[/color][/url][/b]");
        }

        // ── external links ────────────────────────────────────────────────────

        [TestCase]
        public void Render_ExternalLink_EmitsUrl()
        {
            string result = MarkdownService.Render("[text](https://example.com)", Empty());
            AssertThat(result).IsEqual("[url=https://example.com][color=#7ab3e0]text[/color][/url]");
        }

        // ── multiline ─────────────────────────────────────────────────────────

        [TestCase]
        public void Render_MultipleLines_PreservesNewlines()
        {
            string result = MarkdownService.Render("line1\nline2", Empty());
            AssertThat(result).IsEqual("line1\nline2");
        }

        [TestCase]
        public void Render_HeadingThenText_TwoLines()
        {
            string result = MarkdownService.Render("# H\ntext", Empty());
            AssertThat(result).IsEqual("[font_size=24][b]H[/b][/font_size]\ntext");
        }
    }
}
