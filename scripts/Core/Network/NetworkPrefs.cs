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
