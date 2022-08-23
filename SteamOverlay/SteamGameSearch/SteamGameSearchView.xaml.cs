using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static SteamOverlay.SteamGameSearch.SteamGameSearch;

namespace SteamOverlay.SteamGameSearch
{
    /// <summary>
    /// Логика взаимодействия для SteamGameSearchView.xaml
    /// </summary>
    public partial class SteamGameSearchView : UserControl
    {
        private readonly SteamOverlay plugin;
        private readonly ObservableCollection<SteamGame> listGames = new ObservableCollection<SteamGame>();
        internal SteamGame selectedGame = null;
        internal bool isLoading = false;

        public SteamGameSearchView(SteamOverlay plugin, string defaultSearch)
        {
            InitializeComponent();
            ListBoxGames.ItemsSource = listGames;

            this.plugin = plugin;

            TextBoxGameName.Text = defaultSearch;
            PerformSearch(defaultSearch);
        }

        private async void PerformSearch(string name)
        {
            GridMain.IsEnabled = false;
            GridLoading.Visibility = Visibility.Visible;

            List<SteamGame> searchResult = null;
            await Task.Run(() =>
            {
                SteamGameSearch searcher = new SteamGameSearch();
                searchResult = searcher.SearchGameByName(name, plugin.PlayniteApi.ApplicationSettings.Language);
            });

            listGames.Clear();
            foreach (var game in searchResult)
                listGames.Add(game);

            GridMain.IsEnabled = true;
            GridLoading.Visibility = Visibility.Hidden;
        }

        private void ButtonSelect_Click(object sender, RoutedEventArgs e)
        {
            selectedGame = (SteamGame)ListBoxGames.SelectedItem;
            Window.GetWindow(this).Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch(TextBoxGameName.Text);
        }
    }
}
