using Godot;

public static class DialogHelper
{
    private const int DialogWidth = 600;

    /// <summary>Creates a ConfirmationDialog with word-wrapped text.</summary>
    public static ConfirmationDialog Make(string title = "", string text = "")
    {
        var d = new ConfirmationDialog();
        if (!string.IsNullOrEmpty(title)) d.Title = title;
        if (!string.IsNullOrEmpty(text))  d.DialogText = text;
        d.GetLabel().AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return d;
    }

    /// <summary>Sets dialog text and shows it at a fixed width so long names don't stretch it.</summary>
    public static void Show(ConfirmationDialog d, string text)
    {
        d.DialogText = text;
        d.PopupCentered(new Vector2I(DialogWidth, 0));
    }

    /// <summary>Shows an already-configured dialog at a fixed width.</summary>
    public static void Show(ConfirmationDialog d)
    {
        d.PopupCentered(new Vector2I(DialogWidth, 0));
    }
}