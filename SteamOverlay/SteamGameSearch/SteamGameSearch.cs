using HtmlAgilityPack;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Web;
using SteamOverlay.SteamGameSearch.Models;

namespace SteamOverlay.SteamGameSearch
{
    internal class SteamGameSearch
    {
        private const string SearchUrl = "https://store.steampowered.com/search/?term={0}&l={1}";
        // From https://partner.steamgames.com/doc/store/localization, table at the bottom
        private static readonly Dictionary<string, string> PlayniteToSteamLanguageDict = new Dictionary<string, string>
        {
            {"ar_SA", "arabic"},
            {"zh_CN", "schinese"},
            {"zh_TW", "tchinese"},
            {"cs_CZ", "czech"},
            {"nl_NL", "dutch"},
            {"en_US", "english"},
            {"fi_FI", "finnish"},
            {"fr_FR", "french"},
            {"de_DE", "german"},
            {"el_GR", "greek"},
            {"hu_HU", "hungarian"},
            {"it_IT", "italian"},
            {"ja_JP", "japanese"},
            {"ko_KR", "koreana"},
            {"no_NO", "norwegian"},
            {"pl_PL", "polish"},
            {"pt_PT", "portuguese"},
            {"pt_BR", "brazilian"},
            {"ro_RO", "romanian"},
            {"ru_RU", "russian"},
            {"es_ES", "spanish"},
            {"sv_SV", "swedish"},
            {"tr_TR", "turkish"},
            {"uk_UA", "ukrainian"},
            {"vi_VN", "vietnamese"}
        };
        
        private readonly ILogger _logger = LogManager.GetLogger();

        public List<SteamGame> SearchGameByName(string name, string playniteLanguage)
        {
            var foundGames = new List<SteamGame>();

            var steamLanguage = "english";
            if (PlayniteToSteamLanguageDict.ContainsKey(playniteLanguage))
                steamLanguage = PlayniteToSteamLanguageDict[playniteLanguage];

            var web = new HtmlWeb();
            var searchUrl = String.Format(SearchUrl, HttpUtility.HtmlEncode(name), steamLanguage);
            _logger.Info($"Making a request to {searchUrl}");
            var htmlDoc = web.Load(searchUrl);
            _logger.Info($"Html downloaded");
            
            foreach (var node in htmlDoc.DocumentNode.SelectNodes("//a[contains(@class,'search_result_row')]"))
            {
                var game = new SteamGame();

                game.Id = int.Parse(node.GetAttributeValue("data-ds-appid", "-1"));
                if (game.Id == -1)
                    continue;

                game.Name = node.SelectSingleNode(".//span[contains(@class, 'title')]").InnerText;
                game.ReleaseDate = node.SelectSingleNode(".//div[contains(@class,'search_released')]").InnerText;
                game.BannerUrl = node.SelectSingleNode(".//div[contains(@class,'search_capsule')]").SelectSingleNode("img").GetAttributeValue("src", "");

                foundGames.Add(game);
            }

            _logger.Info($"Found {foundGames.Count} games");

            return foundGames;
        }
    }
}
