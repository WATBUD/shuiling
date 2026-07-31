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
			Text = Text,
			Icon = Icon,
			CustomMinimumSize = new Vector2(64.0f, 72.0f),
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.88f),
		};
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
