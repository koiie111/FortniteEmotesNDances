using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FortniteEmotes;

public partial class Plugin
{
    private void LoadBundledEmoteCatalog(PluginConfig config)
    {
        if (config.EmoteDances.Count > 0)
            return;

        var moduleDirectory = Path.GetDirectoryName(ModulePath);
        if (string.IsNullOrEmpty(moduleDirectory))
        {
            Logger.LogError("Unable to resolve the plugin directory for the bundled emote catalog.");
            return;
        }

        var catalogPath = Path.Combine(moduleDirectory, "FortniteEmotesCatalog.json");

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            var bundledConfig = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(catalogPath), options);

            if (bundledConfig?.EmoteDances.Count > 0)
            {
                config.EmoteDances = bundledConfig.EmoteDances;
                Logger.LogInformation("Loaded {Count} emotes and dances from the bundled catalog.", config.EmoteDances.Count);
                return;
            }

            Logger.LogError("The bundled emote catalog is empty: {Path}", catalogPath);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load the bundled emote catalog from {Path}.", catalogPath);
        }
    }
}
