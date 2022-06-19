using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using IntelOrca.OpenLauncher.Core;

namespace openlauncher
{
    public partial class MainWindow : Window
    {
        private readonly BuildService _buildService = new();

        private GameMenuItem? _selectedMenuItem;
        private bool _ready;
        private UpdateCheckResult? _updateCheckResult;
        private bool _isBusy;

        public MainWindow()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                downloadProgress.IsVisible = false;
                errorBox.Opacity = 0;
                updateBoxContainer.Opacity = 0;
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

            if (!Design.IsDesignMode)
            {
                await DoUpdateCheck();
            }
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

        private async Task DoUpdateCheck()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null)
                return;

            var updateService = new UpdateService();
            var checkResult = await updateService.CheckUpdateAsync(_buildService, version);
            if (checkResult != null && checkResult.UpdateAvailable)
            {
                updateBoxContainer.Opacity = 1;
                _updateCheckResult = checkResult;
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
                downloadProgress.IsVisible = true;
                SetAllInteractionEnabled(false);

                var selectedItem = versionDropdown.SelectedItem as ComboBoxItem;
                if (selectedItem?.Tag is Build build)
                {
                    var assets = build.Assets
                        .Where(x => x.IsPortable)
                        .Where(x => x.IsApplicableForCurrentPlatform())
                        .OrderBy(x => x, BuildAssetComparer.Default)
                        .ToArray();
                    var asset = assets.FirstOrDefault();
                    if (asset != null)
                    {
                        var progress = new Progress<DownloadProgressReport>();
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
                        await _selectedMenuItem.InstallService.DownloadVersion(
                            new DownloadService(),
                            new Shell(),
                            build.Version,
                            asset.Uri,
                            progress,
                            cts.Token);
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
                downloadProgress.IsVisible = false;
                SetAllInteractionEnabled(true);
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
                if (!_isBusy)
                {
                    playButton.IsEnabled = installService.CanLaunch();
                }
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

                    if (build.Assets.Any(x => x.IsPortable && x.IsApplicableForCurrentPlatform()))
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
                    if (!_isBusy)
                    {
                        downloadButton.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to obtain builds", ex);
            }
        }

        private void ShowError(string caption, Exception ex)
        {
            ShowError(caption, ex.Message);
        }

        private void ShowError(string caption, string message)
        {
            errorBox.Opacity = 1;
            errorBox.Title = caption;
            errorBox.Message = message;
        }

        private async void update_Click(object sender, RoutedEventArgs e)
        {
            if (_updateCheckResult == null)
                return;

            var processPath = Environment.ProcessPath;
            if (processPath == null)
            {
                ShowError("Launcher update failed", "Unable to obtain path to running process.");
                return;
            }

            try
            {
                SetAllInteractionEnabled(false);

                var progress = new Progress<DownloadProgressReport>(report =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        updateButton.Content = $"{report.Value * 100:0}%";
                    });
                });

                var updateService = new UpdateService();
                await updateService.DownloadAndUpdateAsync(
                    new DownloadService(),
                    new Shell(),
                    processPath,
                    _updateCheckResult.DownloadUri,
                    progress,
                    default);
            }
            catch (Exception ex)
            {
                ShowError("Launcher update failed", ex);
            }
            finally
            {
                updateButton.Content = "Update";
                SetAllInteractionEnabled(true);
            }
        }

        private void SetAllInteractionEnabled(bool value)
        {
            _isBusy = !value;

            gameListView.IsEnabled = value;
            updateButton.IsEnabled = value;
            versionDropdown.IsHitTestVisible = value;
            showPreReleaseCheckbox.IsEnabled = value;

            if (value)
            {
                playButton.IsEnabled = _selectedMenuItem?.InstallService.CanLaunch() ?? false;
                downloadButton.IsEnabled = versionDropdown.Items.GetEnumerator().MoveNext();
            }
            else
            {
                playButton.IsEnabled = value;
                downloadButton.IsEnabled = value;
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
