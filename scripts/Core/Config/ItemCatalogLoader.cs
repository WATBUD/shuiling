using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class ItemCatalogDocument<T>
{
	public string System { get; set; } = string.Empty;
	public List<T> Items { get; set; } = new();
}

public sealed class ConsumableDefinition
{
	public int UniqueId { get; set; }
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
}

public sealed class MaterialItemJson
{
	public int UniqueId { get; set; }
	public string Id { get; set; } = string.Empty;
	public string NameKey { get; set; } = string.Empty;
	public float[] DropColor { get; set; } = { 0.82f, 0.92f, 1.0f, 0.95f };
}

public static class ItemCatalogLoader
{
	public const string EquipmentPath = "res://configs/items/equipment.json";
	public const string CoreSkillsPath = "res://configs/items/core_skills.json";
	public const string SupportCoresPath = "res://configs/items/support_cores.json";
	public const string ConsumablesPath = "res://configs/items/consumables.json";
	public const string MaterialsPath = "res://configs/items/materials.json";

	private static readonly object ValidationLock = new();
	private static readonly Dictionary<int, string> GlobalIds = new();
	private static readonly Dictionary<string, int> GlobalKeys = new(StringComparer.Ordinal);
	private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
	private static List<EquipmentDefinition>? _equipment;
	private static List<SkillGemDefinition>? _coreSkills;
	private static List<ConsumableDefinition>? _consumables;
	private static MonsterLootDefinition[]? _materials;

	public static List<EquipmentDefinition> LoadEquipment()
	{
		return _equipment ??= Load<EquipmentDefinition>(EquipmentPath, "equipment", item => item.UniqueId, item => item.Id);
	}

	public static List<SkillGemDefinition> LoadCoreSkills()
	{
		if (_coreSkills != null)
		{
			return _coreSkills;
		}

		_coreSkills = Load<SkillGemDefinition>(CoreSkillsPath, "core_skills", item => item.UniqueId, item => item.Id);
		_coreSkills.AddRange(Load<SkillGemDefinition>(SupportCoresPath, "support_cores", item => item.UniqueId, item => item.Id));
		return _coreSkills;
	}

	public static List<ConsumableDefinition> LoadConsumables()
	{
		return LoadConsumablesCached();
	}

	public static List<ConsumableDefinition> LoadConsumablesCached()
	{
		return _consumables ??= Load<ConsumableDefinition>(ConsumablesPath, "consumables", item => item.UniqueId, item => item.Id);
	}

	public static MonsterLootDefinition[] LoadMaterials()
	{
		if (_materials != null)
		{
			return _materials;
		}

		List<MaterialItemJson> source = Load<MaterialItemJson>(MaterialsPath, "materials", item => item.UniqueId, item => item.Id);
		var result = new MonsterLootDefinition[source.Count];
		for (int index = 0; index < source.Count; index++)
		{
			MaterialItemJson item = source[index];
			float[] color = item.DropColor ?? Array.Empty<float>();
			result[index] = new MonsterLootDefinition
			{
				UniqueId = item.UniqueId,
				Id = item.Id,
				NameKey = item.NameKey,
				DropColor = new Color(
					color.Length > 0 ? color[0] : 0.82f,
					color.Length > 1 ? color[1] : 0.92f,
					color.Length > 2 ? color[2] : 1.0f,
					color.Length > 3 ? color[3] : 0.95f),
			};
		}
		_materials = result;
		return _materials;
	}

	public static int ValidateAll()
	{
		LoadEquipment();
		LoadCoreSkills();
		LoadConsumablesCached();
		LoadMaterials();
		lock (ValidationLock)
		{
			return GlobalIds.Count;
		}
	}

	private static List<T> Load<T>(
		string path,
		string expectedSystem,
		Func<T, int> uniqueId,
		Func<T, string> stableId)
	{
		if (!FileAccess.FileExists(path))
		{
			throw new InvalidOperationException($"Required item catalog is missing: {path}");
		}

		using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		string json = file.GetAsText();
		ItemCatalogDocument<T>? document = JsonSerializer.Deserialize<ItemCatalogDocument<T>>(json, JsonOptions);
		if (document == null || document.Items.Count == 0)
		{
			throw new InvalidOperationException($"Item catalog is empty or invalid: {path}");
		}
		if (!string.Equals(document.System, expectedSystem, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Item catalog system mismatch in {path}: expected '{expectedSystem}', got '{document.System}'.");
		}

		lock (ValidationLock)
		{
			foreach (T item in document.Items)
			{
				int number = uniqueId(item);
				string key = stableId(item);
				if (number <= 0 || string.IsNullOrWhiteSpace(key))
				{
					throw new InvalidOperationException($"Every item in {path} requires a positive uniqueId and non-empty id.");
				}
				if (GlobalIds.TryGetValue(number, out string? existingNumber))
				{
					throw new InvalidOperationException($"Duplicate item uniqueId {number}: '{existingNumber}' and '{key}'.");
				}
				if (GlobalKeys.TryGetValue(key, out int existingKey))
				{
					throw new InvalidOperationException($"Duplicate item id '{key}': {existingKey} and {number}.");
				}
				GlobalIds[number] = key;
				GlobalKeys[key] = number;
			}
		}

		return document.Items;
	}

	private static JsonSerializerOptions CreateOptions()
	{
		var options = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
		};
		options.Converters.Add(new JsonStringEnumConverter());
		options.Converters.Add(new GodotColorJsonConverter());
		return options;
	}

	private sealed class GodotColorJsonConverter : JsonConverter<Color>
	{
		public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartArray)
			{
				throw new JsonException("Godot Color must be a JSON array: [r, g, b, a].");
			}

			float[] values = new float[4] { 1.0f, 0.54f, 0.24f, 0.92f };
			int index = 0;
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				if (index < values.Length && reader.TokenType == JsonTokenType.Number)
				{
					values[index++] = reader.GetSingle();
				}
			}
			return new Color(values[0], values[1], values[2], values[3]);
		}

		public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
		{
			writer.WriteStartArray();
			writer.WriteNumberValue(value.R);
			writer.WriteNumberValue(value.G);
			writer.WriteNumberValue(value.B);
			writer.WriteNumberValue(value.A);
			writer.WriteEndArray();
		}
	}
}
