using Godot;

public partial class MarkdownHelpModal : Window
{
    public override void _Ready()
    {
        Title       = "Formatting Reference";
        Exclusive   = false;
        Unresizable = true;
        Size        = new Vector2I(480, 560);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 16);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        var rtl = new RichTextLabel
        {
            BbcodeEnabled      = true,
            FitContent         = false,
            ScrollActive       = true,
            SizeFlagsVertical  = Control.SizeFlags.ExpandFill,
        };

        rtl.Text = """
[b]Inline formatting[/b]
[table=2]
[cell][b]**bold**[/b] or [b]__bold__[/b][/cell][cell]Ctrl+B[/cell]
[cell][i]*italic*[/i] or [i]_italic_[/i][/cell][cell]Ctrl+I[/cell]
[cell][u]<u>underline</u>[/u][/cell][cell]Ctrl+U[/cell]
[cell][s]~~strikethrough~~[/s][/cell][cell]Ctrl+Shift+S[/cell]
[cell][url=https://][[text](url)][/url] external link[/cell][cell]Ctrl+K[/cell]
[/table]

[b]Headings[/b]
[table=2]
[cell]# Heading 1[/cell][cell]Ctrl+1[/cell]
[cell]## Heading 2[/cell][cell]Ctrl+2[/cell]
[cell]### Heading 3[/cell][cell]Ctrl+3[/cell]
[cell]#### – ###### Headings 4–6[/cell][cell]Ctrl+4–6[/cell]
[/table]

[b]Lists and blocks[/b]
[table=2]
[cell]- Bullet list[/cell][cell]Ctrl+Shift+8[/cell]
[cell]1. Numbered list[/cell][cell]Ctrl+Shift+7[/cell]
[cell]> Blockquote[/cell][cell]Ctrl+Shift+>[/cell]
[/table]
Press Enter at the end of a list item to continue the list.
Press Enter on an empty list item to exit the list.

[b]Wikilinks[/b]
[[Name]] links to any NPC, Faction, Location, Session, Item, or Quest.
Type [[ to open autocomplete — arrow keys navigate, Tab or Enter confirm.
[[+NPC]], [[+Location]], etc. create a new entity inline.

[b]Escaping[/b]
\* \_ \~ \[ \\ — backslash before a special character renders it literally.
""";

        var closeBtn = new Button
        {
            Text                = "Close",
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };
        closeBtn.Pressed += () => QueueFree();
        CloseRequested   += () => QueueFree();

        vbox.AddChild(rtl);
        vbox.AddChild(closeBtn);
        AddChild(margin);
    }
}
