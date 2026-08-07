using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Logging;
using Menu;
using Menu.Enums;
using FortniteEmotes.API;

namespace FortniteEmotes;
public partial class Plugin
{
    public KitsuneMenu Menu { get; private set; } = null!;

    private void Menu_OnLoad()
    {
        Menu = new KitsuneMenu(this);
    }

    private void ShowKitsuneMenu(CCSPlayerController player, bool isDance)
    {
        if (Menu == null)
        {
            Logger.LogError($"Menu object is null. Cannot show fortnite menu to {player.PlayerName}.");
            return;
        }

        string title = isDance ? Localizer.ForPlayer(player, "emote.dancemenutitle") : Localizer.ForPlayer(player, "emote.emotemenutitle");

        List<MenuItem> items = [];
        var emoteMap = new Dictionary<int, string>();
        int i = 0;

        foreach (var emote in Config.EmoteDances)
        {
            if (isDance && emote.IsEmote)
                continue;
            else if (!isDance && !emote.IsEmote)
                continue;

            bool hasPerm = HasPurchasedAccess(player, emote);

            items.Add(new MenuItem(hasPerm ? MenuItemType.Button : MenuItemType.Text, [new MenuValue(Localizer.ForPlayer(player, emote.Name))]));
            emoteMap[i++] = Localizer.ForPlayer(player, emote.Name);
        }

        if (items.Count == 0)
        {
            player.PrintToChat($" {Localizer.ForPlayer(player, "emote.prefix")} No {(isDance ? "dances" : "emotes")} found.");
            return;
        }

        Menu?.ShowScrollableMenu(player, title, items, (buttons, menu, selected) =>
        {
            if (selected == null) return;

            if (buttons == MenuButtons.Select && emoteMap.TryGetValue(menu.Option, out var emoteSelected))
            {
                var emote = GetEmoteByName(emoteSelected, !isDance);

                if (emote == null)
                    return;

                string error = "";
                if (!PlayEmote(player, emote, ref error))
                {
                    if (!string.IsNullOrEmpty(error))
                        player.PrintToChat(error);
                }
            }
        }, false, freezePlayer: true, disableDeveloper: !Config.KitsuneMenuDeveloperDisplay);
    }

    private void ShowChatMenu(CCSPlayerController player, bool isDance)
    {
        string title = isDance ? Localizer.ForPlayer(player, "emote.dancemenutitle") : Localizer.ForPlayer(player, "emote.emotemenutitle");

        var menu = new ChatMenu(title);

        var OnEmoteSelect = (CCSPlayerController player, ChatMenuOption option) =>
        {
            MenuManager.CloseActiveMenu(player);

            string emoteName = option.Text;

            var emote = GetEmoteByName(emoteName, !isDance);

            if (emote == null)
                return;

            string error = "";
            if (!PlayEmote(player, emote, ref error))
            {
                if (!string.IsNullOrEmpty(error))
                    player.PrintToChat(error);
            }
        };

        foreach (var emote in Config.EmoteDances)
        {
            if (isDance && emote.IsEmote)
                continue;
            else if (!isDance && !emote.IsEmote)
                continue;

            bool hasPerm = HasPurchasedAccess(player, emote);

            string emoteName = $"{Localizer.ForPlayer(player, $"{emote.Name}")}";

            menu.AddMenuOption(emoteName, OnEmoteSelect, !hasPerm);
        }

        MenuManager.OpenChatMenu(player, menu);
    }

    private void ShowCenterMenu(CCSPlayerController player, bool isDance)
    {
        string title = isDance ? Localizer.ForPlayer(player, "emote.dancemenutitle") : Localizer.ForPlayer(player, "emote.emotemenutitle");

        var menu = new CenterHtmlMenu(title, this);

        var OnEmoteSelect = (CCSPlayerController player, ChatMenuOption option) =>
        {
            MenuManager.CloseActiveMenu(player);

            string emoteName = option.Text;

            var emote = GetEmoteByName(emoteName, !isDance);

            if (emote == null)
                return;

            string error = "";
            if (!PlayEmote(player, emote, ref error))
            {
                player.PrintToChat(error);
            }
        };

        foreach (var emote in Config.EmoteDances)
        {
            if (isDance && emote.IsEmote)
                continue;
            else if (!isDance && !emote.IsEmote)
                continue;

            bool hasPerm = HasPurchasedAccess(player, emote);

            string emoteName = $"{Localizer.ForPlayer(player, $"{emote.Name}")}";

            menu.AddMenuOption(emoteName, OnEmoteSelect, !hasPerm);
        }

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    public string GetEmoteTranslation(string name)
    {
        return g_EmoteTransMap.ContainsKey(name) ? g_EmoteTransMap[name] : name;
    }

    public Emote? GetEmoteBySelector(string selector, bool emote)
    {
        if (int.TryParse(selector, out var number))
        {
            if (number < 1)
                return null;

            return Config.EmoteDances
                .Where(item => item.IsEmote == emote)
                .ElementAtOrDefault(number - 1);
        }

        return GetEmoteByName(selector, emote, true);
    }

    public Emote? GetEmoteByName(string name, bool emote, bool partial = false)
    {
        if (partial)
        {
            var sname = name.ToLower();

            var list = Config.EmoteDances
                .Where(s => s.IsEmote == emote)
                .OrderBy(s =>
                {
                    var emoteName = s.Name.ToLower();
                    var emoteNameTranslated = GetEmoteTranslation(s.Name).ToLower();

                    var ems = emoteName.Split('_').ToList();

                    foreach (var em in ems)
                    {
                        // Don't search Emote_ and Dance_ prefix
                        if (em.Equals("Emote_") || em.Equals("Dance_"))
                        {
                            continue;
                        }

                        if (em.Equals(sname))
                            return -1;
                    }

                    ems = emoteNameTranslated.Split(' ').ToList();

                    foreach (var em in ems)
                    {
                        if (em.Equals(sname))
                            return -1;
                    }

                    return 1;
                })
                .Where(s =>
                {
                    var ems = s.Name.ToLower().Split('_').ToList();
                    foreach (var em in ems)
                    {
                        if (em.Equals(sname))
                            return true;
                    }

                    ems = GetEmoteTranslation(s.Name).ToLower().Split(' ').ToList();
                    foreach (var em in ems)
                    {
                        if (em.Equals(sname))
                            return true;
                    }

                    return false;
                })
                .ToList();

            return list.Count > 0 ? list.First() : null;
        }
        return Config.EmoteDances.FirstOrDefault(x => GetEmoteTranslation(x.Name).Equals(name) && x.IsEmote == emote);
    }
}
