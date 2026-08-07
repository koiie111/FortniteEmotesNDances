using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using FortniteEmotes.API;

namespace FortniteEmotes;

public partial class Plugin
{
    private const string AccessTable = "fortnite_emotes_access";

    private sealed record AccessCacheEntry(HashSet<string> Models, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<ulong, AccessCacheEntry> _accessCache = new();
    private string _databaseConnectionString = "";
    private bool _databaseAvailable;

    private void InitializeDatabaseAccess()
    {
        var database = Config.Database;
        var builder = new MySqlConnectionStringBuilder
        {
            Server = database.Host,
            Port = database.Port,
            UserID = database.User,
            Password = database.Password,
            Database = database.DatabaseName,
            Pooling = true,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 5
        };

        _databaseConnectionString = builder.ConnectionString;
        _accessCache.Clear();

        try
        {
            using var connection = new MySqlConnection(_databaseConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT `model` FROM `{AccessTable}` LIMIT 0";
            command.ExecuteNonQuery();
            _databaseAvailable = true;
            Logger.LogInformation("Database access checks initialized using table {Table}.", AccessTable);
        }
        catch (Exception exception)
        {
            _databaseAvailable = false;
            Logger.LogError(exception, "Database access checks are unavailable. Players will not be able to use purchased emotes or dances.");
        }
    }

    private bool HasPurchasedAccess(ulong steamId, Emote emote)
    {
        if (!_databaseAvailable || steamId == 0)
            return false;

        var now = DateTime.UtcNow;
        if (_accessCache.TryGetValue(steamId, out var cached) && cached.ExpiresAt > now)
            return cached.Models.Contains(emote.Name);

        try
        {
            var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var connection = new MySqlConnection(_databaseConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT `model`
                FROM `{AccessTable}`
                WHERE `steamid` = @steamid
                  AND (`expires` = 0 OR `expires` > UNIX_TIMESTAMP())";
            command.Parameters.AddWithValue("@steamid", steamId.ToString());

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    models.Add(reader.GetString(0));
            }

            var lifetime = Math.Max(1, Config.Database.AccessCacheSeconds);
            _accessCache[steamId] = new AccessCacheEntry(models, now.AddSeconds(lifetime));
            return models.Contains(emote.Name);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load emote access for SteamID {SteamId}.", steamId);
            _accessCache[steamId] = new AccessCacheEntry(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                now.AddSeconds(Math.Max(1, Config.Database.AccessCacheSeconds))
            );
            return false;
        }
    }

    private bool HasPurchasedAccess(CounterStrikeSharp.API.Core.CCSPlayerController player, Emote emote)
    {
        return HasPurchasedAccess(player.SteamID, emote);
    }
}
