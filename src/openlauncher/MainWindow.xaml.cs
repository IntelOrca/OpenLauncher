using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
using IntelOrca.OpenLauncher.Core;

namespace openlauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BuildService _buildService = new();

        private GameMenuItem? _selectedMenuItem;
        private bool _ready;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            gameListView.SelectedIndex = 0;
            _ready = true;
            var selectedItem = gameListView.SelectedItem as GameMenuItem;
            await SetPageAsync(selectedItem);
        }

        private async Task SetPageAsync(GameMenuItem? item)
        {
            if (!_ready)
                return;

            _selectedMenuItem = item;
            if (item != null)
            {
                titleTextBlock.Text = item.Game!.Name;
                await RefreshInstalledVersionAsync();
                await RefreshAvailableVersionsAsync();
            }
            else
            {
            }
        }

        private async void gameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await SetPageAsync(gameListView.SelectedItem as GameMenuItem);
        }

        private async void downloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMenuItem == null)
                return;

            try
            {
                downloadButton.IsEnabled = false;
                playButton.IsEnabled = false;

                var builds = _selectedMenuItem.Builds;
                var index = versionDropdown.SelectedIndex;
                if (index >= 0 && index < builds.Length)
                {
                    var build = builds[index];
                    var assets = build.Assets
                        .Where(x => x.IsApplicableForCurrentPlatform())
                        .OrderBy(x => x, BuildAssetComparer.Default)
                        .ToArray();
                    var asset = assets.FirstOrDefault();
                    if (asset != null)
                    {
                        var progress = new Progress<float>();
                        progress.ProgressChanged += (s, value) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                downloadButton.Content = $"{value * 100:0.0}%";
                            });
                        };

                        var cts = new CancellationTokenSource();
                        await _selectedMenuItem.InstallService.DownloadVersion(build.Version, asset.Uri, progress, cts.Token);
                        await RefreshInstalledVersionAsync();
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                downloadButton.Content = "Download";
                downloadButton.IsEnabled = true;
                playButton.IsEnabled = _selectedMenuItem.InstallService.CanLaunch();
            }
        }

        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMenuItem == null)
                return;

            try
            {
                _selectedMenuItem.InstallService.Launch();
            }
            catch
            {
            }
        }

        private async Task RefreshInstalledVersionAsync()
        {
            if (_selectedMenuItem == null)
                return;

            try
            {
                var installService = _selectedMenuItem.InstallService;
                var version = await installService.GetCurrentVersionAsync();
                if (version == null && installService.CanLaunch())
                {
                    version = "(Unknown)";
                }
                installedVersionTextBlock.Text = version;
                playButton.IsEnabled = installService.CanLaunch();
                playButton.ToolTip = installService.ExecutablePath;
            }
            catch
            {
            }
        }

        private async Task RefreshAvailableVersionsAsync()
        {
            if (_selectedMenuItem == null)
                return;

            try
            {
                // Clear list
                versionDropdown.Items.Clear();

                // Refresh builds
                var builds = _selectedMenuItem.Builds;
                if (builds.IsDefault)
                {
                    builds = await _buildService.GetBuildsAsync(_selectedMenuItem.Game!);
                }
                _selectedMenuItem.Builds = builds;

                // Populate list
                foreach (var build in builds)
                {
                    if (build.Assets.Any(x => x.IsApplicableForCurrentPlatform()))
                    {
                        var content = build.PublishedAt is DateTime dt ?
                            $"{build.Version} (released {GetAge(dt)})" :
                            build.Version;
                        versionDropdown.Items.Add(new ComboBoxItem() { Content = content });
                    }
                }
                if (versionDropdown.Items.Count != 0)
                {
                    versionDropdown.SelectedIndex = 0;
                    downloadButton.IsEnabled = true;
                }
            }
            catch
            {
            }
        }

        private static string GetAge(DateTime dt)
        {
            const string template = "{0} {1} ago";
            var offset = DateTime.UtcNow - dt.ToUniversalTime();
            if (offset.TotalDays < 1)
            {
                var hours = offset.TotalHours;
                if (hours < 1)
                {
                    var minutes = offset.TotalMinutes;
                    return Pluralise(template, minutes, "minute");
                }
                else
                {
                    return Pluralise(template, hours, "hour");
                }
            }
            else
            {
                var days = offset.TotalDays;
                if (days >= 30)
                {
                    var months = days / 30;
                    if (months >= 24)
                    {
                        var years = months / 12;
                        return Pluralise(template, years, "year");
                    }
                    else
                    {
                        return Pluralise(template, months, "month");
                    }
                }
                else
                {
                    return Pluralise(template, days, "day");
                }
            }

            static string Pluralise(string template, double d, string subject)
            {
                var n = (int)d;
                return string.Format(template, n, n == 1 ? subject : subject + "s");
            }
        }
    }

    public class GameMenuItem
    {
        private InstallService? _installService;

        public Game? Game { get; set; }
        public ImageSource? Image { get; set; }

        public InstallService InstallService
        {
            get
            {
                if (_installService == null)
                    _installService = new InstallService(Game!);
                return _installService;
            }
        }
        public ImmutableArray<Build> Builds { get; set; }
    }
}
