using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using IntelOrca.OpenLauncher.Core;

namespace openlauncher
{
    public partial class MainWindow : Window
    {
        private readonly BuildService _buildService = new();

        private GameMenuItem? _selectedMenuItem;
        private bool _ready;

        public MainWindow()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                downloadProgress.IsVisible = false;
                errorBox.Opacity = 0;
            }

            gameListView.Items = new[] {
                new GameMenuItem(Game.OpenRCT2, "avares://openlauncher/resources/icon-openrct2.png"),
                new GameMenuItem(Game.OpenLoco, "avares://openlauncher/resources/icon-openloco.png")
            };
        }

        private async void Window_Opened(object sender, EventArgs e)
        {
            gameListView.SelectedIndex = 1;
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
                if (!Design.IsDesignMode)
                {
                    await RefreshInstalledVersionAsync();
                    await RefreshAvailableVersionsAsync();
                }
            }
            else
            {
            }
        }

        private async void gameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await SetPageAsync(gameListView.SelectedItem as GameMenuItem);
        }

        private async void showPreReleaseCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            await RefreshAvailableVersionsAsync();
        }

        private async void downloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMenuItem == null)
                return;

            try
            {
                showPreReleaseCheckbox.IsEnabled = false;
                versionDropdown.IsHitTestVisible = false;
                downloadProgress.IsVisible = true;
                downloadButton.IsEnabled = false;
                playButton.IsEnabled = false;

                var selectedItem = versionDropdown.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag is Build build)
                {
                    var assets = build.Assets
                        .Where(x => x.IsApplicableForCurrentPlatform())
                        .OrderBy(x => x, BuildAssetComparer.Default)
                        .ToArray();
                    var asset = assets.FirstOrDefault();
                    if (asset != null)
                    {
                        var progress = new Progress<InstallService.DownloadProgressReport>();
                        progress.ProgressChanged += (s, report) =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                downloadButton.Content = report.Status;
                                if (report.Value is float value)
                                {
                                    downloadProgress.IsIndeterminate = false;
                                    downloadProgress.Value = value;
                                }
                                else
                                {
                                    downloadProgress.IsIndeterminate = true;
                                }
                            });
                        };

                        var cts = new CancellationTokenSource();
                        await _selectedMenuItem.InstallService.DownloadVersion(build.Version, asset.Uri, progress, cts.Token);
                        await RefreshInstalledVersionAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to download build", ex);
            }
            finally
            {
                downloadButton.Content = "Download";
                downloadButton.IsEnabled = true;
                playButton.IsEnabled = _selectedMenuItem.InstallService.CanLaunch();
                downloadProgress.IsVisible = false;
                versionDropdown.IsHitTestVisible = true;
                showPreReleaseCheckbox.IsEnabled = true;
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
            catch (Exception ex)
            {
                ShowError($"Failed to launch {_selectedMenuItem.Game!.Name}", ex);
            }
        }

        private async Task RefreshInstalledVersionAsync()
        {
            if (_selectedMenuItem == null)
                return;

            var installService = _selectedMenuItem.InstallService;
            try
            {
                var version = await installService.GetCurrentVersionAsync();
                if (version == null && installService.CanLaunch())
                {
                    version = "(Unknown)";
                }
                installedVersionTextBlock.Text = version;
            }
            catch
            {
                installedVersionTextBlock.Text = string.Empty;
            }
            finally
            {
                playButton.IsEnabled = installService.CanLaunch();
                ToolTip.SetTip(playButton, installService.ExecutablePath);
            }
        }

        private async Task RefreshAvailableVersionsAsync()
        {
            if (_selectedMenuItem == null)
                return;

            try
            {
                // Clear list
                versionDropdown.Items = new ComboBoxItem[0];

                // Refresh builds
                var showDevelop = showPreReleaseCheckbox.IsChecked ?? false;
                var builds = _selectedMenuItem.Builds;
                if (builds.IsDefault || (showDevelop && !_selectedMenuItem.BuildsIncludeDevelop))
                {
                    builds = await _buildService.GetBuildsAsync(_selectedMenuItem.Game!, showDevelop);
                    _selectedMenuItem.BuildsIncludeDevelop = showDevelop;
                }
                _selectedMenuItem.Builds = builds;

                // Populate list
                var items = new List<ComboBoxItem>();
                foreach (var build in builds)
                {
                    if (!showDevelop && !build.IsRelease)
                        continue;

                    if (build.Assets.Any(x => x.IsApplicableForCurrentPlatform()))
                    {
                        var content = build.PublishedAt is DateTime dt ?
                            $"{build.Version} (released {GetAge(dt)})" :
                            build.Version;
                        items.Add(new ComboBoxItem() { Content = content, Tag = build });
                    }
                }
                if (items.Count != 0)
                {
                    versionDropdown.Items = items;
                    versionDropdown.SelectedIndex = 0;
                    downloadButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to obtain builds", ex);
            }
        }

        private void ShowError(string caption, Exception ex)
        {
            errorBox.Opacity = 1;
            errorTitle.Text = caption;
            errorMessage.Text = ex.Message;
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

        public Game Game { get; set; }
        public Bitmap Image { get; }
        public ImmutableArray<Build> Builds { get; set; }
        public bool BuildsIncludeDevelop { get; set; }

        public GameMenuItem(Game game, string imagePath)
        {
            Game = game;
            Image = App.GetImage(imagePath);
        }

        public InstallService InstallService
        {
            get
            {
                if (_installService == null)
                    _installService = new InstallService(Game!);
                return _installService;
            }
        }
    }
}
