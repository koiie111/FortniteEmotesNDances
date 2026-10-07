using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace FortniteEmotes;

public partial class Plugin
{
    private const string CameraModel = "models/chicken/chicken.vmdl";
    private MountedGameFileSystem? _gameFiles;
    private readonly ModelAvailability _models = new();
    private readonly HashSet<string> _reportedMissingModels = new(StringComparer.Ordinal);
    private bool _fileSystemErrorReported;

    // Called only on the game thread. FileExists queries the engine's mounted GAME search paths;
    // it does not enumerate downloaded Workshop archives or load model resources.
    private bool IsMountedModel(string model)
    {
        if (!ModelAvailability.IsValidPath(model))
            return false;

        try
        {
            _gameFiles ??= new MountedGameFileSystem(Server.GameDirectory,
                GameData.GetOffset("FortniteEmotes_IFileSystem_FileExists"));
            return _gameFiles.FileExists(model + "_c");
        }
        catch (Exception exception)
        {
            if (!_fileSystemErrorReported)
            {
                Logger.LogError(exception, "Cannot verify mounted GAME resources. Emotes are disabled until the filesystem check works.");
                _fileSystemErrorReported = true;
            }
            return false;
        }
    }

    private bool IsModelAvailable(string model)
    {
        bool available = !_pluginUnloading && ModelAvailability.IsValidPath(model) &&
            (!Config.EmoteModelCheck || _models.CanUse(model, IsMountedModel));
        if (!available && _reportedMissingModels.Add(model ?? string.Empty))
            Logger.LogError("Emote model '{Model}' is invalid, not mounted in GAME, or was not registered during this map's resource precache. Emotes using it are disabled. Check mm_extra_addons and change map after mounting the addon or hot-reloading the plugin.", model);
        if (available)
            _reportedMissingModels.Remove(model ?? string.Empty);
        return available;
    }

    private void ResetModelCheck()
    {
        _models.Clear();
        _reportedMissingModels.Clear();
        _fileSystemErrorReported = false;
    }

    private void DisposeModelCheck()
    {
        ResetModelCheck();
        _gameFiles?.Dispose();
        _gameFiles = null;
    }
}
