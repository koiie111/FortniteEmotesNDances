using System.Text;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;

namespace FortniteEmotes;

public partial class Plugin
{
    private readonly Dictionary<string, DateTime> _missingModels = new();
    private readonly HashSet<string> _availableModels = new();
    private static readonly TimeSpan MissingModelRecheck = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Checks that a model's compiled resource exists on disk (loose addon file or inside a mounted VPK)
    /// before spawning a prop with it. Setting a missing model on a prop stalls the server and gets
    /// the player kicked with NETWORK_DISCONNECT_OVERFLOW. Fails open when nothing could be inspected.
    /// </summary>
    private bool IsModelAvailable(string model)
    {
        if (!Config.EmoteModelCheck || string.IsNullOrWhiteSpace(model))
            return true;

        if (_availableModels.Contains(model))
            return true;

        if (_missingModels.TryGetValue(model, out var checkedAt) && DateTime.UtcNow - checkedAt < MissingModelRecheck)
            return false;

        bool inspected = false;
        bool found;
        try
        {
            found = FindModel(model + "_c", ref inspected);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Model availability check failed for {Model}: {Message}", model, ex.Message);
            return true;
        }

        if (found || !inspected)
        {
            _missingModels.Remove(model);
            _availableModels.Add(model);
            return true;
        }

        if (!_missingModels.ContainsKey(model))
            Logger.LogError("Emote model '{Model}' was not found in any mounted addon. Make sure the addon containing it is listed in mm_extra_addons (MultiAddonManager) and is published with this file. Emotes using it are disabled.", model);

        _missingModels[model] = DateTime.UtcNow;
        return false;
    }

    private static bool FindModel(string compiledPath, ref bool inspected)
    {
        string gameDir = Server.GameDirectory;

        var looseRoots = new List<string> { Path.Combine(gameDir, "csgo") };
        string addonsDir = Path.Combine(gameDir, "csgo_addons");
        if (Directory.Exists(addonsDir))
            looseRoots.AddRange(Directory.EnumerateDirectories(addonsDir));

        foreach (var root in looseRoots)
        {
            inspected = true;
            if (File.Exists(Path.Combine(root, compiledPath)))
                return true;
        }

        var vpks = new List<string>();
        string csgoDir = Path.Combine(gameDir, "csgo");
        if (Directory.Exists(csgoDir))
            vpks.AddRange(Directory.EnumerateFiles(csgoDir, "*_dir.vpk"));

        string binDir = Path.Combine(gameDir, "bin");
        if (Directory.Exists(binDir))
        {
            foreach (var platform in Directory.EnumerateDirectories(binDir))
            {
                string workshop = Path.Combine(platform, "steamapps", "workshop", "content", "730");
                if (Directory.Exists(workshop))
                    vpks.AddRange(Directory.EnumerateFiles(workshop, "*.vpk", SearchOption.AllDirectories));
            }
        }

        string dir = Path.GetDirectoryName(compiledPath)?.Replace('\\', '/') ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(compiledPath);
        string ext = Path.GetExtension(compiledPath).TrimStart('.');

        foreach (var vpk in vpks)
        {
            var tree = ReadVpkTree(vpk);
            if (tree == null)
                continue;

            inspected = true;
            if (VpkTreeContains(tree, ext, dir, name))
                return true;
        }

        return false;
    }

    private static byte[]? ReadVpkTree(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);

        if (fs.Length < 28 || br.ReadUInt32() != 0x55AA1234)
            return null;

        uint version = br.ReadUInt32();
        uint treeSize = br.ReadUInt32();
        if (version is not (1 or 2) || treeSize == 0 || treeSize > fs.Length)
            return null;

        fs.Position = version == 1 ? 12 : 28;
        return br.ReadBytes((int)treeSize);
    }

    // Tree layout: ext\0 { dir\0 { name\0 <entry> ... \0 } \0 } \0. Entry data is fixed-size, so walk it properly.
    private static bool VpkTreeContains(byte[] tree, string ext, string dir, string name)
    {
        int pos = 0;

        string ReadString()
        {
            int start = pos;
            while (pos < tree.Length && tree[pos] != 0) pos++;
            string s = Encoding.UTF8.GetString(tree, start, pos - start);
            pos++;
            return s;
        }

        while (pos < tree.Length)
        {
            string e = ReadString();
            if (e.Length == 0) break;

            while (pos < tree.Length)
            {
                string d = ReadString();
                if (d.Length == 0) break;

                while (pos < tree.Length)
                {
                    string n = ReadString();
                    if (n.Length == 0) break;

                    if (e == ext && string.Equals(d, dir, StringComparison.OrdinalIgnoreCase) && string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                        return true;

                    // CRC(4) preload size(2) archive index(2) offset(4) length(4) terminator(2) + preload bytes
                    if (pos + 18 > tree.Length) return false;
                    ushort preload = BitConverter.ToUInt16(tree, pos + 4);
                    pos += 18 + preload;
                }
            }
        }

        return false;
    }
}
