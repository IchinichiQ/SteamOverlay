using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SteamOverlay.SteamGameSearch.Models;
using static SteamOverlay.SteamGameSearch.SteamGameSearch;

namespace SteamOverlay.SteamGameSearch
{
    /// <summary>
    /// Логика взаимодействия для SteamGameSearchView.xaml
    /// </summary>
    public partial class SteamGameSearchView : UserControl
    {
        public SteamGame selectedGame = null;
        public bool isLoading = false;
        
        private readonly SteamOverlay _plugin;
        private readonly ObservableCollection<SteamGame> _listGames = new ObservableCollection<SteamGame>();

        public SteamGameSearchView(SteamOverlay plugin, string defaultSearch)
        {
            InitializeComponent();
            ListBoxGames.ItemsSource = _listGames;

            _plugin = plugin;

            TextBoxGameName.Text = defaultSearch;
            PerformSearch(defaultSearch);
        }

        private async void PerformSearch(string name)
        {
            // TODO: Handle errors
            GridMain.IsEnabled = false;
            GridLoading.Visibility = Visibility.Visible;

            List<SteamGame> searchResult = null;
            await Task.Run(() =>
            {
                var searcher = new SteamGameSearch();
                searchResult = searcher.SearchGameByName(name, _plugin.PlayniteApi.ApplicationSettings.Language);
            });

            _listGames.Clear();
            foreach (var game in searchResult)
            {
                _listGames.Add(game);
            }
            
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
