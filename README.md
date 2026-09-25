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

## Credits
- [CSGO Fortnite Emotes](https://github.com/Franc1sco/Fortnite-Emotes-Extended)
- [Thirdperson Plugin](https://github.com/UgurhanK/ThirdPerson-WIP)
- [ThirdPerson-Revamped](https://github.com/KKNecmi/ThirdPerson-Revamped)
- [HidePlayers Plugin](https://github.com/qstage/CS2-HidePlayers) (was used in older version of plugin)
- K4ryuu for KitsuneMenu.
- Kolka for porting model.
- GoldKingZ for renaming sounds & creating soundeventfile + took some ideas from his no source-code version's config.
