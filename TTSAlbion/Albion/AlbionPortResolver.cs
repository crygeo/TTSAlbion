using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NetWorkLibrery.Interfaces;
using TTSAlbion.Albion.Network;

namespace TTSAlbion.Albion;

/// <summary>
/// Resolves ports used by the Albion Online client process.
/// Finds running instances by executable name derived from the game path,
/// then delegates actual port inspection to ProcessNetworkInspector.
/// 
/// Kept thin intentionally: all OS interaction lives in ProcessNetworkInspector,
/// making this class trivially testable by substituting the inspector.
/// </summary>
public sealed class AlbionPortResolver : IProcessPortResolver
{
    private readonly string _executableName;
    private readonly ProcessNetworkInspector _inspector;

    /// <param name="gameExecutablePath">
    /// Full path to the game executable, e.g. C:\...\Albion-Online.exe
    /// The resolver extracts only the process name from it.
    /// </param>
    public AlbionPortResolver(string gameExecutablePath, ProcessNetworkInspector? inspector = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameExecutablePath);
        _executableName = Path.GetFileNameWithoutExtension(gameExecutablePath);
        _inspector = inspector ?? new ProcessNetworkInspector();
    }

    public IReadOnlySet<int> Resolve()
    {
        var processes = Process.GetProcessesByName(_executableName);

        if (processes.Length == 0)
        {
            Console.WriteLine($"[AlbionPortResolver] Process '{_executableName}' not found.");
            return new HashSet<int>();
        }

        var pids = processes.Select(p => p.Id).ToHashSet();

        foreach (var p in processes)
            p.Dispose();

        var ports = _inspector.GetPortsForProcessIds(pids);

        Console.WriteLine($"[AlbionPortResolver] Found {ports.Count} port(s) for '{_executableName}': [{string.Join(", ", ports)}]");

        return ports;
    }
}