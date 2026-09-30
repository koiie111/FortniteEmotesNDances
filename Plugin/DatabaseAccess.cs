using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using FortniteEmotes.API;

namespace FortniteEmotes;

public partial class Plugin
{
    private const string AccessTable = "fortnite_emotes_access";

    private sealed record AccessCacheEntry(HashSet<string> Models, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<ulong, AccessCacheEntry> _accessCache = new();
    private readonly ConcurrentDictionary<ulong, Task<AccessCacheEntry>> _accessLoads = new();
    private string _databaseConnectionString = "";
    private volatile bool _databaseAvailable;

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
        _accessLoads.Clear();

        // Never touch the database on the game thread: a slow query stalls the tick and
        // the engine drops clients with NETWORK_DISCONNECT_OVERFLOW.
        Task.Run(() =>
        {
            try
            {
                using var connection = new MySqlConnection(_databaseConnectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT `model` FROM `{AccessTable}` LIMIT 0";
                command.ExecuteNonQuery();
                _databaseAvailable = true;
                Logger.LogInformation("Database access checks initialized using table {Table}.", AccessTable);

                Server.NextWorldUpdate(PrefetchAccessForConnectedPlayers);
            }
            catch (Exception exception)
            {
                _databaseAvailable = false;
                Logger.LogError(exception, "Database access checks are unavailable. Players will not be able to use purchased emotes or dances.");
            }
        });
    }

    private void PrefetchAccessForConnectedPlayers()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsValid && !player.IsBot && !player.IsHLTV)
                LoadAccessAsync(player.SteamID);
        }
    }

    [GameEventHandler]
    public HookResult OnPlayerConnectFullAccess(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid && !player.IsBot && !player.IsHLTV)
            LoadAccessAsync(player.SteamID);

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnectAccess(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player != null && player.IsValid)
            _accessCache.TryRemove(player.SteamID, out _);

        return HookResult.Continue;
    }

    /// <summary>
    /// Starts (or joins) a background load of a player's purchased models. Never blocks.
    /// </summary>
    private Task<AccessCacheEntry> LoadAccessAsync(ulong steamId)
    {
        if (!_databaseAvailable || steamId == 0)
            return Task.FromResult(new AccessCacheEntry(new HashSet<string>(StringComparer.OrdinalIgnoreCase), DateTime.MaxValue));

        var task = _accessLoads.GetOrAdd(steamId, id =>
        {
            var connectionString = _databaseConnectionString;
            var lifetime = Math.Max(1, Config.Database.AccessCacheSeconds);

            return Task.Run(async () =>
            {
                try
                {
                    var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    await using var connection = new MySqlConnection(connectionString);
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = $@"
                        SELECT `model`
                        FROM `{AccessTable}`
                        WHERE `steamid` = @steamid
                          AND (`expires` = 0 OR `expires` > UNIX_TIMESTAMP())";
                    command.Parameters.AddWithValue("@steamid", id.ToString());

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        if (!reader.IsDBNull(0))
                            models.Add(reader.GetString(0));
                    }

                    var entry = new AccessCacheEntry(models, DateTime.UtcNow.AddSeconds(lifetime));
                    _accessCache[id] = entry;
                    return entry;
                }
                catch (Exception exception)
                {
                    Logger.LogError(exception, "Failed to load emote access for SteamID {SteamId}.", id);

                    // Keep previously known access on a transient failure; retry after the cache lifetime.
                    var models = _accessCache.TryGetValue(id, out var previous)
                        ? previous.Models
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var entry = new AccessCacheEntry(models, DateTime.UtcNow.AddSeconds(lifetime));
                    _accessCache[id] = entry;
                    return entry;
                }
            });
        });

        // Removed only once finished, and only if it is still the same load, so a later refresh can start.
        task.ContinueWith(t => _accessLoads.TryRemove(new KeyValuePair<ulong, Task<AccessCacheEntry>>(steamId, t)));
        return task;
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the game thread once the player's access list is known.
    /// Runs immediately when it is already cached (a stale entry is used and refreshed in the background).
    /// </summary>
    private void WithPlayerAccess(CCSPlayerController player, Action action)
    {
        var steamId = player.SteamID;

        if (!_databaseAvailable || steamId == 0 || _accessCache.ContainsKey(steamId))
        {
            IsAccessKnown(steamId); // refreshes in the background if stale
            action();
            return;
        }

        LoadAccessAsync(steamId).ContinueWith(_ =>
        {
            Server.NextWorldUpdate(() =>
            {
                if (player.IsValid && player.SteamID == steamId)
                    action();
            });
        });
    }

    /// <summary>
    /// True when the player's access list is cached. Kicks off a background (re)load when missing or stale.
    /// </summary>
    private bool IsAccessKnown(ulong steamId)
    {
        if (!_databaseAvailable || steamId == 0)
            return false;

        if (!_accessCache.TryGetValue(steamId, out var cached))
        {
            LoadAccessAsync(steamId);
            return false;
        }

        if (cached.ExpiresAt <= DateTime.UtcNow)
            LoadAccessAsync(steamId);

        return true;
    }

    private bool HasPurchasedAccess(ulong steamId, Emote emote)
    {
        if (!IsAccessKnown(steamId))
            return false;

        return _accessCache.TryGetValue(steamId, out var cached) && cached.Models.Contains(emote.Name);
    }

    private bool HasPurchasedAccess(CCSPlayerController player, Emote emote)
    {
        return HasPurchasedAccess(player.SteamID, emote);
    }
}
