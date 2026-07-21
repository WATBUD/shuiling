using Godot;
using System.Collections.Generic;

// Listen-server (P2P) reachability diagnostics for "建立房間". Answers the four
// common failure layers without needing a master server:
//  1. did we actually bind/listen on the port?          -> TestPortBind
//  2. Windows Defender Firewall blocking?               -> EnsureFirewallRule (netsh)
//  3. Router/NAT has no port forward?                   -> RunNat (UPnP auto-map)
//  4. ISP CGNAT (no real public IPv4)?                  -> RunNat (external IP range check)
// UPnP also auto-opens the port (TryOpenPort) so most players can host without
// manual port forwarding. Blocking calls (UPnP discover, netsh) must run off the
// main thread — see MainMenu's threaded usage.
public static class NetworkDiagnostics
{
	public enum Level { Ok, Warn, Fail, Info }

	public readonly record struct Line(Level Level, string Message);

	public static string Marker(Level level)
	{
		return level switch
		{
			Level.Ok => "[OK] ",
			Level.Warn => "[!] ",
			Level.Fail => "[X] ",
			_ => "- ",
		};
	}

	// Main thread: can the game actually listen on this UDP port right now?
	public static Line TestPortBind(int port)
	{
		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(port, 1);
		if (error == Error.Ok)
		{
			peer.Close();
			return new Line(Level.Ok, LocaleText.F("net.diag.port_ok", port));
		}

		return new Line(Level.Fail, LocaleText.F("net.diag.port_fail", port, error.ToString()));
	}

	// Background thread: discover the router via UPnP, report the public IP,
	// auto-open the port, and flag CGNAT / private WAN addresses.
	public static List<Line> RunNat(int port)
	{
		var lines = new List<Line>();
		var upnp = new Upnp();
		int discover = upnp.Discover();
		UpnpDevice gateway = discover == (int)Upnp.UpnpResult.Success ? upnp.GetGateway() : null;
		if (gateway == null || !gateway.IsValidGateway())
		{
			lines.Add(new Line(Level.Warn, LocaleText.T("net.diag.upnp_none")));
			return lines;
		}

		string external = upnp.QueryExternalAddress();
		if (string.IsNullOrWhiteSpace(external))
		{
			lines.Add(new Line(Level.Warn, LocaleText.T("net.diag.ip_unknown")));
		}
		else
		{
			lines.Add(new Line(Level.Ok, LocaleText.F("net.diag.public_ip", external)));
			if (IsCgnatOrPrivate(external))
			{
				lines.Add(new Line(Level.Warn, LocaleText.T("net.diag.cgnat")));
			}
		}

		int mapUdp = upnp.AddPortMapping(port, port, "Shuiling UDP", "UDP", 0);
		upnp.AddPortMapping(port, port, "Shuiling TCP", "TCP", 0);
		lines.Add(mapUdp == (int)Upnp.UpnpResult.Success
			? new Line(Level.Ok, LocaleText.F("net.diag.upnp_mapped", port))
			: new Line(Level.Warn, LocaleText.F("net.diag.upnp_map_fail", port)));

		return lines;
	}

	// Background thread: best-effort Windows firewall allow rule (needs admin).
	public static Line EnsureFirewallRule(int port)
	{
		if (OS.GetName() != "Windows")
		{
			return new Line(Level.Info, LocaleText.T("net.diag.fw_skip"));
		}

		bool udp = RunNetshAddRule(port, "UDP");
		bool tcp = RunNetshAddRule(port, "TCP");
		return udp && tcp
			? new Line(Level.Ok, LocaleText.T("net.diag.fw_ok"))
			: new Line(Level.Warn, LocaleText.T("net.diag.fw_fail"));
	}

	// Fire-and-forget auto port forward used when actually hosting (no UI/locale).
	public static void TryOpenPort(int port)
	{
		try
		{
			var upnp = new Upnp();
			if (upnp.Discover() != (int)Upnp.UpnpResult.Success)
			{
				return;
			}

			UpnpDevice gateway = upnp.GetGateway();
			if (gateway == null || !gateway.IsValidGateway())
			{
				return;
			}

			upnp.AddPortMapping(port, port, "Shuiling UDP", "UDP", 0);
			upnp.AddPortMapping(port, port, "Shuiling TCP", "TCP", 0);
		}
		catch (System.Exception)
		{
			// UPnP is best-effort; failures fall back to manual port forwarding.
		}
	}

	private static bool RunNetshAddRule(int port, string protocol)
	{
		// Each argument is a separate array element so the rule name (which
		// contains spaces) is quoted correctly by the OS layer.
		var arguments = new[]
		{
			"advfirewall", "firewall", "add", "rule",
			$"name=Shuiling {protocol} {port}",
			"dir=in", "action=allow",
			$"protocol={protocol}", $"localport={port}",
		};
		int exitCode = OS.Execute("netsh", arguments);
		return exitCode == 0;
	}

	private static bool IsCgnatOrPrivate(string ip)
	{
		string[] parts = ip.Split('.');
		if (parts.Length != 4 || !int.TryParse(parts[0], out int a) || !int.TryParse(parts[1], out int b))
		{
			return false;
		}

		if (a == 10)
		{
			return true; // 10.0.0.0/8
		}
		if (a == 172 && b >= 16 && b <= 31)
		{
			return true; // 172.16.0.0/12
		}
		if (a == 192 && b == 168)
		{
			return true; // 192.168.0.0/16
		}
		if (a == 100 && b >= 64 && b <= 127)
		{
			return true; // 100.64.0.0/10 (CGNAT)
		}

		return false;
	}
}
