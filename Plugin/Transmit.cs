using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace FortniteEmotes;

public partial class Plugin
{
    public void Transmit_OnLoad()
    {
        if (Config.EmoteHidePlayers != 0)
        {
            RegisterListener<Listeners.CheckTransmit>(Hook_CheckTransmit);
        }
    }

    public void Transmit_OnUnload()
    {
        if (Config.EmoteHidePlayers != 0)
        {
            RemoveListener<Listeners.CheckTransmit>(Hook_CheckTransmit);
        }
    }

    private void Hook_CheckTransmit(CCheckTransmitInfoList infoList)
    {
        if (Config.EmoteHidePlayers == 0 || !g_PlayerSettings.Values.Any(settings => settings.IsDancing))
            return;

        List<CCSPlayerController>? targets = null;
        foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
        {
            if (!player.IsValidPlayer())
                continue;

            var steamID = player!.SteamID;

            if (!g_PlayerSettings.TryGetValue(steamID, out var settings) || !settings.IsDancing ||
                player.Pawn.Value?.As<CCSPlayerPawnBase>().PlayerState == CSPlayerState.STATE_OBSERVER_MODE)
                continue;

            targets ??= Utilities.GetPlayers();
            foreach (var target in targets)
            {
                if (target.IsHLTV || target.Slot == player.Slot)
                    continue;

                var pawn = target.PlayerPawn.Value;

                if (pawn == null || !pawn.IsValid)
                    continue;

                switch (Config.EmoteHidePlayers)
                {
                    case 1:
                        if (player.Team != target.Team)
                            info.TransmitEntities.Remove((int)pawn.Index);
                        break;
                    case 2:
                        if (player.Team == target.Team)
                            info.TransmitEntities.Remove((int)pawn.Index);
                        break;
                    case 3:
                        info.TransmitEntities.Remove((int)pawn.Index);
                        break;
                }
            }
        }
    }
}
