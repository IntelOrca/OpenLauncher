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
using StringResources = openlauncher.Properties.Resources;

namespace openlauncher
{
    public partial class MainWindow : Window
    {
        private readonly BuildService _buildService = new();
        private readonly ConfigService _configService = new();

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
            showPreReleaseCheckbox.IsChecked = _configService.PreReleaseChecked;
            gameListView.SelectedIndex = _configService.SelectingGame;
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
            try
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
            catch
            {
            }
        }

        private async void gameListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready)
                return;
            _configService.SelectingGame = gameListView.SelectedIndex;
            await SetPageAsync(gameListView.SelectedItem as GameMenuItem);
        }

        private async void showPreReleaseCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_ready)
                return;
            _configService.PreReleaseChecked = showPreReleaseCheckbox.IsChecked ?? false;
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
                ShowError(StringResources.DownloadBuildFailedTitle, ex);
            }
            finally
            {
                downloadButton.Content = StringResources.Download;
                downloadProgress.IsVisible = false;
                SetAllInteractionEnabled(true);
            }
        }

        private async void playButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMenuItem == null)
                return;

            try
            {
                await _selectedMenuItem.InstallService.Launch();
            }
            catch (Exception ex)
            {
                ShowError(string.Format(StringResources.FailedToLaunchGame, _selectedMenuItem.Game!.Name), ex);
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
                    version = StringResources.Unknown;
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
                versionDropdown.IsHitTestVisible = false;

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
                        versionDropdown.IsHitTestVisible = true;
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
            var buildsAvailable = versionDropdown.Items.GetEnumerator().MoveNext();

            _isBusy = !value;
            gameListView.IsEnabled = value;
            updateButton.IsEnabled = value;
            versionDropdown.IsHitTestVisible = value && buildsAvailable;
            showPreReleaseCheckbox.IsEnabled = value;

            if (value)
            {
                playButton.IsEnabled = _selectedMenuItem?.InstallService.CanLaunch() ?? false;
                downloadButton.IsEnabled = buildsAvailable;
            }
            else
            {
                playButton.IsEnabled = value;
                downloadButton.IsEnabled = value;
            }
        }

        private static string GetAge(DateTime dt)
        {
            var offset = DateTime.UtcNow - dt.ToUniversalTime();
            if (offset.TotalDays < 1)
            {
                var hours = offset.TotalHours;
                if (hours < 1)
                {
                    return Pluralise(offset.TotalMinutes, StringResources.Minute, StringResources.Minutes);
                }
                else
                {
                    return Pluralise(hours, StringResources.Hour, StringResources.Hours);
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
                        return Pluralise(years, StringResources.Year, StringResources.Years);
                    }
                    else
                    {
                        return Pluralise(months, StringResources.Month, StringResources.Months);
                    }
                }
                else
                {
                    return Pluralise(days, StringResources.Day, StringResources.Days);
                }
            }

            static string Pluralise(double d, string singular, string plural)
            {
                var n = (int)d;
                return string.Format(n == 1 ? singular : plural, n);
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
