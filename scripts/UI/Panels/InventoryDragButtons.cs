using Godot;
using System;

public partial class InventoryItemDragButton : Button
{
	public string DragItemId { get; set; } = string.Empty;

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (string.IsNullOrEmpty(DragItemId))
		{
			return default;
		}

		var preview = new Button
		{
			// Dragging should show a compact cursor ghost, not expand the source
			// texture and duplicate the full inventory button under the pointer.
			Text = string.Empty,
			CustomMinimumSize = new Vector2(42.0f, 42.0f),
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.78f),
			FocusMode = FocusModeEnum.None,
		};
		ItemIconLibrary.Apply(preview, DragItemId, 32);
		preview.IconAlignment = HorizontalAlignment.Center;
		SetDragPreview(preview);
		return DragItemId;
	}
}

public partial class InventoryEquipDropButton : Button
{
	public Func<string, bool>? CanAcceptItem { get; set; }
	public Action<string>? ItemDropped { get; set; }

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.String
			&& CanAcceptItem?.Invoke(data.AsString()) == true;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType == Variant.Type.String)
		{
			ItemDropped?.Invoke(data.AsString());
		}
	}
}
