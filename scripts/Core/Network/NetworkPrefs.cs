using Godot;
using System;
using System.Collections.Generic;

// Remembers servers the player has joined (address + port) so the join screen can
// offer them again. Persisted to user://network.cfg, most-recent first.
public static class NetworkPrefs
{
	private const string ConfigPath = "user://network.cfg";
	private const int MaxRecent = 8;

	public readonly record struct ServerEntry(string Address, int Port);

	public static List<ServerEntry> GetRecentServers()
	{
		var result = new List<ServerEntry>();
		var config = new ConfigFile();
		if (config.Load(ConfigPath) != Error.Ok)
		{
			return result;
		}

		string[] addresses = config.GetValue("servers", "addresses", Array.Empty<string>()).AsStringArray();
		int[] ports = config.GetValue("servers", "ports", Array.Empty<int>()).AsInt32Array();
		int count = Mathf.Min(addresses.Length, ports.Length);
		for (int i = 0; i < count; i++)
		{
			if (!string.IsNullOrWhiteSpace(addresses[i]))
			{
				result.Add(new ServerEntry(addresses[i], ports[i]));
			}
		}

		return result;
	}

	// The character a player last used when joining someone else's server. Once
	// set, joining skips character-select and reuses this "record"; empty model
	// path means no record yet (first-time join → show character-select).
	public readonly record struct GuestProfile(string ModelPath, string Name);

	public static GuestProfile GetGuestProfile()
	{
		var config = new ConfigFile();
		if (config.Load(ConfigPath) != Error.Ok)
		{
			return new GuestProfile(string.Empty, string.Empty);
		}

		string modelPath = config.GetValue("guest", "model_path", string.Empty).AsString();
		string name = config.GetValue("guest", "name", string.Empty).AsString();
		return new GuestProfile(modelPath, name);
	}

	public static bool HasGuestProfile()
	{
		return !string.IsNullOrWhiteSpace(GetGuestProfile().ModelPath);
	}

	public static void SaveGuestProfile(string modelPath, string name)
	{
		var config = new ConfigFile();
		config.Load(ConfigPath); // Preserve existing sections (recent servers).
		config.SetValue("guest", "model_path", modelPath ?? string.Empty);
		config.SetValue("guest", "name", name ?? string.Empty);
		config.Save(ConfigPath);
	}

	public static void AddRecentServer(string address, int port)
	{
		if (string.IsNullOrWhiteSpace(address))
		{
			return;
		}

		List<ServerEntry> list = GetRecentServers();
		list.RemoveAll(entry => entry.Address == address && entry.Port == port);
		list.Insert(0, new ServerEntry(address, port));
		if (list.Count > MaxRecent)
		{
			list = list.GetRange(0, MaxRecent);
		}

		var addresses = new string[list.Count];
		var ports = new int[list.Count];
		for (int i = 0; i < list.Count; i++)
		{
			addresses[i] = list[i].Address;
			ports[i] = list[i].Port;
		}

		var config = new ConfigFile();
		config.SetValue("servers", "addresses", addresses);
		config.SetValue("servers", "ports", ports);
		config.Save(ConfigPath);
	}
}
