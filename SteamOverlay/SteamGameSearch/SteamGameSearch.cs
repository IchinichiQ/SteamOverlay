using HtmlAgilityPack;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SteamOverlay.SteamGameSearch
{
    internal class SteamGameSearch
    {
        ILogger logger = LogManager.GetLogger();

        public class SteamGame
        {
            public string Name { get; set; }
            public string ReleaseDate { get; set; }
            public int Id { get; set; }
            public string BannerUrl { get; set; }
        }

        // From https://partner.steamgames.com/doc/store/localization, table at the bottom
        private Dictionary<string, string> playniteToSteamLanguageDict = new Dictionary<string, string>
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

        private string SearchUrl = "https://store.steampowered.com/search/?term={0}&l={1}";

        public List<SteamGame> SearchGameByName(string name, string playniteLanguage)
        {
            List<SteamGame> foundGames = new List<SteamGame>();

            string steamLanguage = "english";
            if (playniteToSteamLanguageDict.ContainsKey(playniteLanguage))
                steamLanguage = playniteToSteamLanguageDict[playniteLanguage];

            HtmlWeb web = new HtmlWeb();
            string searchUrl = String.Format(SearchUrl, HttpUtility.HtmlEncode(name), steamLanguage);
            logger.Info($"Making a request to {searchUrl}");
            var htmlDoc = web.Load(searchUrl);
            logger.Info($"Html downloaded");
            
            foreach (HtmlNode node in htmlDoc.DocumentNode.SelectNodes("//a[contains(@class,'search_result_row')]"))
            {
                SteamGame game = new SteamGame();

                game.Id = int.Parse(node.GetAttributeValue("data-ds-appid", "-1"));
                if (game.Id == -1)
                    continue;

                game.Name = node.SelectSingleNode(".//span[contains(@class, 'title')]").InnerText;
                game.ReleaseDate = node.SelectSingleNode(".//div[contains(@class,'search_released')]").InnerText;
                game.BannerUrl = node.SelectSingleNode(".//div[contains(@class,'search_capsule')]").SelectSingleNode("img").GetAttributeValue("src", "");

                foundGames.Add(game);
            }

            logger.Info($"Found {foundGames.Count} games");

            return foundGames;
        }
    }
}
