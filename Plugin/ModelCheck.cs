using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;

namespace FortniteEmotes;

public partial class Plugin
{
    private sealed record ModelStatus(bool Available, DateTime CheckedAt);

    private readonly ConcurrentDictionary<string, ModelStatus> _modelStatus = new();
    private readonly ConcurrentDictionary<string, byte> _modelChecksRunning = new();
    private static readonly TimeSpan MissingModelRecheck = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Reports whether a model's compiled resource exists on disk (loose addon file or inside a mounted VPK).
    /// Setting a missing model on a prop stalls the server and gets the player kicked with
    /// NETWORK_DISCONNECT_OVERFLOW. The disk is never read on the game thread: the result comes from a cache
    /// filled by <see cref="StartModelCheck"/>, and an unknown model fails open while its check runs in the background.
    /// </summary>
    private bool IsModelAvailable(string model)
    {
        if (!Config.EmoteModelCheck || string.IsNullOrWhiteSpace(model))
            return true;

        if (_modelStatus.TryGetValue(model, out var status))
        {
            if (!status.Available && DateTime.UtcNow - status.CheckedAt >= MissingModelRecheck)
                CheckModelInBackground(model);

            return status.Available;
        }

        CheckModelInBackground(model);
        return true;
    }

    /// <summary>Warms the model cache for every configured emote model without blocking the game thread.</summary>
    private void StartModelCheck()
    {
        if (!Config.EmoteModelCheck)
            return;

        foreach (var model in Config.EmoteDances.Select(e => e.Model).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct())
            CheckModelInBackground(model);
    }

    private void CheckModelInBackground(string model)
    {
        if (!_modelChecksRunning.TryAdd(model, 0))
            return;

        string gameDir = Server.GameDirectory;

        Task.Run(() =>
        {
            try
            {
                bool inspected = false;
                bool found = FindModel(gameDir, model + "_c", ref inspected);

                // Nothing could be inspected: fail open rather than block emotes on a broken check.
                bool available = found || !inspected;

                bool wasMissing = _modelStatus.TryGetValue(model, out var previous) && !previous.Available;
                _modelStatus[model] = new ModelStatus(available, DateTime.UtcNow);

                if (!available && !wasMissing)
                    Logger.LogError("Emote model '{Model}' was not found in any mounted addon. Make sure the addon containing it is listed in mm_extra_addons (MultiAddonManager) and is published with this file. Emotes using it are disabled.", model);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Model availability check failed for {Model}: {Message}", model, ex.Message);
                _modelStatus[model] = new ModelStatus(true, DateTime.UtcNow);
            }
            finally
            {
                _modelChecksRunning.TryRemove(model, out _);
            }
        });
    }

    private static bool FindModel(string gameDir, string compiledPath, ref bool inspected)
    {
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
