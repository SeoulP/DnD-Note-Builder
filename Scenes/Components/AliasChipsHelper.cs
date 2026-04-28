using DndBuilder.Core;
using DndBuilder.Core.Models;
using Godot;

public static class AliasChipsHelper
{
    public static void Reload(
        VBoxContainer container,
        DatabaseService db,
        string entityType,
        int entityId,
        int campaignId,
        System.Action onReload)
    {
        if (container == null) return;
        foreach (Node child in container.GetChildren()) child.QueueFree();

        // Header: dimmed "Aliases" label + "+" button
        var hdr    = new HBoxContainer(); hdr.AddThemeConstantOverride("separation", 4);
        var lbl    = new Label { Text = "Aliases" };
        lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        var addBtn = new Button { Text = "+", Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand, TooltipText = "Add alias" };
        hdr.AddChild(lbl); hdr.AddChild(addBtn);
        container.AddChild(hdr);

        // Chip flow
        var chipFlow = new HFlowContainer();
        chipFlow.AddThemeConstantOverride("h_separation", 4);
        chipFlow.AddThemeConstantOverride("v_separation", 4);
        container.AddChild(chipFlow);

        foreach (var alias in db.EntityAliases.GetForEntity(entityType, entityId))
        {
            int capturedId = alias.Id;
            chipFlow.AddChild(Chip.MakeAlias(alias.Alias, () => { db.EntityAliases.Delete(capturedId); onReload(); }));
        }

        addBtn.Pressed += () =>
        {
            var input     = new LineEdit { PlaceholderText = "alias...", CustomMinimumSize = new Vector2(100, 0) };
            bool done     = false;
            chipFlow.AddChild(input);
            input.GrabFocus();

            void Submit(string text)
            {
                if (done) return;
                done = true;
                input.QueueFree();
                string t = text.Trim();
                if (string.IsNullOrEmpty(t)) return;
                db.EntityAliases.Add(new EntityAlias { CampaignId = campaignId, EntityType = entityType, EntityId = entityId, Alias = t });
                onReload();
            }

            input.TextSubmitted += Submit;
            input.FocusExited   += () => Submit(input.Text);
        };
    }
}
