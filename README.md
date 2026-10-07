# Fortnite Emotes & Dances

This plugin allows players to use Emotes & Dances just like Fortnite.

**This took a *lot* of trial-and-error to find the best way to use thirdperson and a *lot* of effort create config file with each emote / dance. Sponsor if you would like to support me :)**

[![Watch Preview Video](https://raw.githubusercontent.com/cruze03/FortniteEmotesNDances/main/git_assets/preview_emotes.gif)](https://www.youtube.com/watch?v=hNeWEU2_Qss)

<p align="center">
  <a href="https://discord.gg/ua2PKexnBP"><img src="https://discordapp.com/api/guilds/734350057431629925/widget.png?style=banner2" alt="CruzeCoding Discord"></a>
</p>

## Commands
- `css_emotes` - Open emotes menu.
- `css_dances` - Open dances menu.
- `css_emotes <number>` - Play an emote by its 1-based position in the player's available emotes list (for example, `bind x "css_emotes 1"`).
- `css_dances <number>` - Play a dance by its 1-based position in the player's available dances list.
- `css_etriggers` - Print all chat triggers for emotes/dances in chat.
- `css_setemote <name/#userid> emoteName` (`@css/root` required)
- `css_setdance <name/#userid> danceName` (`@css/root` required)

## Dependencies
- [Metamod](https://www.metamodsource.net/downloads.php?branch=dev)
- [CounterStrikeSharp v361+](https://github.com/roflmuffin/CounterStrikeSharp/releases/latest)
- [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager/releases/latest)
- [CSSharpPatcher](https://github.com/samyycX/CSSharpPatcher) (optional but recommended to fix volume)
- MySQL/MariaDB with the `fortnite_emotes_access` table from the accompanying website migration

## Purchased access

This fork replaces per-emote CounterStrikeSharp permissions and VIP/group access with database purchases. A player may use an emote or dance only when `fortnite_emotes_access` contains a non-expired row matching the player's SteamID64 and the emote's canonical config `Name` (stored in the `model` column). Admin `setemote` and `setdance` commands intentionally bypass the purchase check.

The `css_emotes` and `css_dances` menus show only entries currently available to that player.

If `EmoteDances` is empty in the generated CounterStrikeSharp configuration, the plugin automatically loads the complete 80-entry catalog shipped next to the plugin DLL.

Configure the connection in `configs/plugins/FortniteEmotesNDances/FortniteEmotesNDances.json`:

```json
"Database": {
  "Host": "127.0.0.1",
  "Port": 3306,
  "User": "root",
  "Password": "change-me",
  "DatabaseName": "cs2admin",
  "AccessCacheSeconds": 30
}
```

`expires` is a Unix timestamp. Set it to `0` for permanent access. Newly purchased access becomes visible after `AccessCacheSeconds` (30 seconds by default).

## Installation
- After installing all the dependencies, drag and drop this plugin's latest release under `addons/counterstrikesharp`
- Open `cfg/multiaddonmanager/multiaddonmanager.cfg` and edit `mm_extra_addons` to add `3328582199`.
- Restart server.

## Model safety and overflow investigation

Keep `EmoteModelCheck: true` (the default). Models must have a valid relative `.vmdl` path, be visible through the engine's mounted `GAME` search paths, and be registered during the current map's resource precache. Downloaded Workshop VPKs and unmounted `csgo_addons` directories do not prove availability. A failed filesystem check denies playback. The deferred `SetModel` callback rechecks the captured model, so changing an API `Emote` object or unmounting an addon cannot bypass the initial check. The camera model follows the same checks; the visible player clone uses the model already assigned to the live pawn.

Install the updated `gamedata/fortnite_emotes.json` along with the plugin and **restart the server**, because CounterStrikeSharp loads gamedata at startup. After hot-reloading this plugin, adding models to the config, or mounting an addon after precache, change the map before playing those models. Setting `EmoteModelCheck: false` explicitly bypasses mount/precache protection and logs a warning.

The check uses `VFileSystem017::FileExists` with path ID `GAME`. Its vtable slot is defined in gamedata for Windows and Linux (21), based on the CS2 SDK's [IAppSystem](https://github.com/alliedmodders/hl2sdk/blob/cs2/public/appframework/IAppSystem.h) and [IFileSystem](https://github.com/alliedmodders/hl2sdk/blob/cs2/public/filesystem.h). MultiAddonManager mounts its addons into that search path. No VPK tree parsing, recursive Workshop scan, or background native filesystem calls are needed.

The plugin does not explicitly kick players. `NETWORK_DISCONNECT_OVERFLOW` is an engine disconnect reason; that text alone does not identify which plugin caused it. Missing resources, server stalls and excessive network traffic are plausible contributors, but an actual overflow was **not reproduced or measured on a live CS2 server** during this review. This update closes the model-check bypass, coalesces repeated requests while a player's initial database query is pending, bounds repeating sounds to at most four emissions per second per dancer, stops emote timers on map changes, and cleans up props/timers on disconnect. It also removes string conversions from tick input checks, skips idle tick/transmit work, shares one player snapshot per transmit callback, and replaces full prop scans on stop with lookups of the three tracked props.

Remaining load depends on concurrent dancers: movable cameras still trace every tick, each dance creates up to three networked props, and starting/stopping a dance removes/recreates weapons. Admin commands bypass the player cooldown. For an ongoing overflow, correlate server frame times and disconnect timestamps with these operations, client/server console errors and other plugins. A mounted server resource also cannot prove every client's addon download succeeded or that the resource/dependencies are valid.

Validation: `dotnet test Tests/FortniteEmotes.Tests.csproj -c Release` checks unknown, unmounted, unregistered, invalid, changing-map and deferred-use cases without requiring CS2. `dotnet publish Plugin/FortniteEmotesNDances.csproj -c Release` builds the deployment package. On a staging server, verify playback with the addon mounted; deny playback with the VPK downloaded but removed from `mm_extra_addons`; deny playback after unmounting between request and world update; verify hot reload stays blocked until map change; and spam requests during a delayed initial database load to confirm only the latest action runs. The native filesystem binding and client disconnect behavior require those live checks on each supported OS.

## Credits
- [CSGO Fortnite Emotes](https://github.com/Franc1sco/Fortnite-Emotes-Extended)
- [Thirdperson Plugin](https://github.com/UgurhanK/ThirdPerson-WIP)
- [ThirdPerson-Revamped](https://github.com/KKNecmi/ThirdPerson-Revamped)
- [HidePlayers Plugin](https://github.com/qstage/CS2-HidePlayers) (was used in older version of plugin)
- K4ryuu for KitsuneMenu.
- Kolka for porting model.
- GoldKingZ for renaming sounds & creating soundeventfile + took some ideas from his no source-code version's config.
