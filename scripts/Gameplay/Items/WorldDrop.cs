using Godot;

public partial class WorldDrop : Node3D
{
	[Export] public string ItemId { get; set; } = string.Empty;
	[Export] public int Amount { get; set; } = 1;
	[Export] public int GoldAmount { get; set; }
	[Export] public float PickupRadius { get; set; } = 1.65f;
	[Export] public float LifetimeSeconds { get; set; } = 90.0f;

	private float _age;
	private float _bobPhase;
	private Label3D? _label;

	public bool IsGoldDrop => GoldAmount > 0;
	public bool IsCollected { get; private set; }

	public override void _Ready()
	{
		AddToGroup("world_drops");
		_bobPhase = (float)GD.RandRange(0.0, Mathf.Tau);
		BuildVisual();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_age += step;
		if (_age >= LifetimeSeconds)
		{
			QueueFree();
			return;
		}

		_bobPhase += step * 3.4f;
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y + step * 65.0f, 0.0f);
		if (_label != null)
		{
			_label.Position = new Vector3(0.0f, 0.92f + Mathf.Sin(_bobPhase) * 0.08f, 0.0f);
		}
	}

	public bool TryCollect(PlayerController player)
	{
		if (IsCollected || !IsInstanceValid(player) || GlobalPosition.DistanceTo(player.GlobalPosition) > PickupRadius)
		{
			return false;
		}

		IsCollected = true;
		if (IsGoldDrop)
		{
			player.AddGold(GoldAmount);
		}
		else if (!string.IsNullOrWhiteSpace(ItemId))
		{
			player.AddInventoryItem(ItemId, Mathf.Max(Amount, 1));
		}

		QueueFree();
		return true;
	}

	private void BuildVisual()
	{
		var color = IsGoldDrop
			? new Color(1.0f, 0.78f, 0.18f, 0.96f)
			: GetItemColor(ItemId);

		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color * 0.45f,
			Roughness = 0.35f,
			Metallic = IsGoldDrop ? 0.45f : 0.12f,
		};

		Mesh mesh = IsGoldDrop
			? new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.24f, Height = 0.12f, RadialSegments = 24 }
			: new BoxMesh { Size = new Vector3(0.42f, 0.42f, 0.42f) };

		var body = new MeshInstance3D
		{
			Name = IsGoldDrop ? "GoldDropVisual" : "ItemDropVisual",
			Mesh = mesh,
			Position = new Vector3(0.0f, 0.24f, 0.0f),
			RotationDegrees = IsGoldDrop ? new Vector3(90.0f, 0.0f, 0.0f) : new Vector3(18.0f, 35.0f, 0.0f),
		};
		body.SetSurfaceOverrideMaterial(0, material);
		AddChild(body);

		if (!IsGoldDrop)
		{
			var glow = new MeshInstance3D
			{
				Name = "ItemGlow",
				Mesh = new SphereMesh { Radius = 0.32f, Height = 0.42f },
				Position = new Vector3(0.0f, 0.24f, 0.0f),
			};
			var glowMaterial = new StandardMaterial3D
			{
				AlbedoColor = new Color(color.R, color.G, color.B, 0.28f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				EmissionEnabled = true,
				Emission = color * 0.25f,
			};
			glow.SetSurfaceOverrideMaterial(0, glowMaterial);
			AddChild(glow);
		}

		_label = new Label3D
		{
			Name = "DropLabel",
			Text = GetDisplayText(),
			Position = new Vector3(0.0f, 0.92f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 18,
			PixelSize = 0.007f,
			OutlineSize = 5,
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = 260.0f,
		};
		_label.OutlineModulate = new Color(0.02f, 0.02f, 0.018f, 0.96f);
		_label.Modulate = IsGoldDrop ? new Color(1.0f, 0.88f, 0.32f) : color.Lightened(0.25f);
		AddChild(_label);
	}

	private string GetDisplayText()
	{
		if (IsGoldDrop)
		{
			return LocaleText.F("drop.gold", GoldAmount);
		}

		string name = ItemId == "monster_trophy" ? LocaleText.T("item.monster_trophy") : LocaleText.T(BuildCatalog.GetItemNameKey(ItemId));
		return Amount > 1 ? $"{name} x{Amount}" : name;
	}

	private static Color GetItemColor(string itemId)
	{
		if (itemId.StartsWith("equip."))
		{
			return new Color(0.50f, 0.78f, 1.0f, 0.95f);
		}

		if (itemId.StartsWith("gem.attribute."))
		{
			return new Color(0.96f, 0.46f, 1.0f, 0.95f);
		}

		if (itemId.StartsWith("gem.skill."))
		{
			return new Color(0.40f, 1.0f, 0.66f, 0.95f);
		}

		if (itemId.StartsWith("gem.ai."))
		{
			return new Color(1.0f, 0.62f, 0.34f, 0.95f);
		}

		return new Color(0.82f, 0.92f, 1.0f, 0.95f);
	}
}
