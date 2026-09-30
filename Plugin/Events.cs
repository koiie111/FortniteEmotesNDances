using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;

namespace FortniteEmotes;

public partial class Plugin
{
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundPreStart(EventRoundPrestart @event, GameEventInfo @info)
    {
        EmitSoundExtension.ClearSounds();
        playerWeapons.Clear();
        playerItems.Clear();
        g_bRoundEnd = false;
        foreach (var settings in g_PlayerSettings.ToList())
        {
            if (!settings.Value.IsDancing)
                continue;

            // Stop properly so emote props are removed instead of being left behind dancing
            var player = Utilities.GetPlayerFromSteamId(settings.Key);
            if (player != null && player.IsValidPlayer())
            {
                StopEmote(player, true);
            }
            else
            {
                RemoveEmoteEntities(settings.Value);
                settings.Value.Reset();
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo @info)
    {
        g_bRoundEnd = true;

        var roundRestartDelay = ConVar.Find("mp_round_restart_delay")?.GetPrimitiveValue<float>() ?? 7.0f;
        AddTimer(roundRestartDelay - 0.2f, () =>
        {
            foreach (var settings in g_PlayerSettings)
            {
                if (settings.Value.IsDancing)
                {
                    var player = Utilities.GetPlayerFromSteamId(settings.Key);

                    if (player != null)
                    {
                        StopEmote(player);
                    }
                }
            }
        });
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo @info)
    {
        if (!Config.StopEmoteAfterFreezetimeEnd)
            return HookResult.Continue;

        foreach (var settings in g_PlayerSettings)
        {
            if (settings.Value.IsDancing)
            {
                var player = Utilities.GetPlayerFromSteamId(settings.Key);

                if (player != null)
                {
                    StopEmote(player);
                }
            }
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo @info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        StopEmote(player, true);

        Server.NextWorldUpdate(() =>
        {
            if (player == null || !player.IsValid)
                return;

            if (Config.EmoteMenuType == 2 && Menu != null && Menu.GetMenus(player) != null && Menu.GetMenus(player)!.Where(m => m.RequiresFreeze)?.Count() > 0)
                SetPlayerMoveType(player, MoveType_t.MOVETYPE_OBSOLETE);
        });
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo @info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var steamID = player.SteamID;
        if (g_PlayerSettings.ContainsKey(steamID) && g_PlayerSettings[steamID].IsDancing)
        {
            StopEmote(player);
        }
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo @info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var steamID = player.SteamID;
        if (g_PlayerSettings.ContainsKey(steamID) && g_PlayerSettings[steamID].IsDancing)
        {
            StopEmote(player);
        }
        return HookResult.Continue;
    }
}