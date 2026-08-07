using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace IntelOrca.OpenLauncher.Core
{
    public class UpdateService
    {
        private static readonly RepositoryName Repository = new RepositoryName("OpenRCT2", "OpenLauncher");

        public async Task<UpdateCheckResult?> CheckUpdateAsync(BuildService buildService, Version currentVersion)
        {
            var builds = await buildService.GetBuildsAsync(Repository, isRelease: true);
            var latestBuild = builds.Sort().FirstOrDefault();
            if (latestBuild != null)
            {
                var updateAvailable = latestBuild.ParsedVersion > currentVersion;
                var assets = latestBuild.Assets
                    .Where(x => x.IsApplicableForCurrentPlatform())
                    .OrderBy(x => x, BuildAssetComparer.Default)
                    .ToArray();

                var asset = assets.FirstOrDefault();
                if (asset == null)
                    return null;

                return new UpdateCheckResult(updateAvailable, latestBuild.Version, asset.Uri);
            }
            return null;
        }

        public async Task DownloadAndUpdateAsync(
            DownloadService downloadService,
            Shell shell,
            string processPath,
            Uri uri,
            IProgress<DownloadProgressReport> progress,
            CancellationToken ct)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await DownloadAndUpdateMacAsync(downloadService, shell, processPath, uri, progress, ct)
                    .ConfigureAwait(false);
                return;
            }

            var currentBinaryTempPath = processPath + ".backup";
            try
            {
                shell.DeleteFile(currentBinaryTempPath);
            }
            catch
            {
                throw new Exception($"Failed to delete \"{currentBinaryTempPath}\"");
            }

            var downloadPath = await downloadService.DownloadFileAsync(uri, progress, ct).ConfigureAwait(false);

            // Rename current process binary (we can't delete it on Windows, but we can rename it)
            shell.MoveFile(processPath, currentBinaryTempPath);
            try
            {
                shell.MoveFile(downloadPath, processPath);
            }
            catch
            {
                // Undo the rename of current process binary
                shell.MoveFile(currentBinaryTempPath, processPath);
                throw new Exception("Unable to move downloaded file to current launcher location.");
            }
            shell.SetExecutable(processPath);

            RestartAndExit(shell, processPath);
        }

        // On macOS the launcher is an .app bundle inside a zip archive, and processPath points at the
        // executable a few directories inside it. The bundle has to be replaced as a whole, since the
        // resources next to the executable change between versions.
        private async Task DownloadAndUpdateMacAsync(
            DownloadService downloadService,
            Shell shell,
            string processPath,
            Uri uri,
            IProgress<DownloadProgressReport> progress,
            CancellationToken ct)
        {
            var bundlePath = GetAppBundlePath(processPath) ??
                throw new Exception($"\"{processPath}\" is not inside an .app bundle.");

            var downloadPath = await downloadService.DownloadFileAsync(uri, progress, ct).ConfigureAwait(false);

            // Staged next to the bundle so that swapping them is a rename, not a copy across volumes
            var extractPath = bundlePath + ".update";
            var backupPath = bundlePath + ".backup";
            try
            {
                // Either may have been left behind by a previous failed update
                shell.DeleteDirectory(extractPath);
                shell.DeleteDirectory(backupPath);
                shell.ExtractMacArchive(downloadPath, extractPath);

                var newBundlePath = shell.GetFileSystemEntries(extractPath)
                    .FirstOrDefault(x => x.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) ??
                    throw new Exception("Update archive did not contain an .app bundle.");

                // Keep the old bundle around until the new one is in place
                shell.MoveDirectory(bundlePath, backupPath);
                try
                {
                    shell.MoveDirectory(newBundlePath, bundlePath);
                }
                catch
                {
                    shell.MoveDirectory(backupPath, bundlePath);
                    throw new Exception("Unable to move updated bundle to current launcher location.");
                }
                shell.DeleteDirectory(backupPath);
            }
            finally
            {
                shell.DeleteDirectory(extractPath);
                shell.TryDeleteFile(downloadPath);
            }

            // -n forces a new instance, as this one is still running until RestartAndExit exits it
            RestartAndExit(shell, "/usr/bin/open", "-n", bundlePath);
        }

        // Whether an update can overwrite what it needs to: the directory holding the .app bundle on
        // macOS, the launcher binary itself elsewhere.
        public bool CanUpdateInPlace(Shell shell, string processPath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var bundleParent = Path.GetDirectoryName(GetAppBundlePath(processPath));
                    return bundleParent != null && shell.CanWriteToDirectory(bundleParent);
                }
                return !File.GetAttributes(processPath).HasFlag(FileAttributes.ReadOnly);
            }
            catch
            {
                return false;
            }
        }

        private static void RestartAndExit(Shell shell, string path, params string[] args)
        {
            try
            {
                shell.StartProcess(path, args);
            }
            catch
            {
                throw new Exception("Launcher updated, but failed to start");
            }
            Environment.Exit(0);
        }

        // "OpenLauncher.app/Contents/MacOS/openlauncher" to "OpenLauncher.app"
        private static string? GetAppBundlePath(string processPath)
        {
            var contentsDirectory = Path.GetDirectoryName(Path.GetDirectoryName(processPath));
            var bundleDirectory = Path.GetDirectoryName(contentsDirectory);
            return bundleDirectory?.EndsWith(".app", StringComparison.OrdinalIgnoreCase) == true
                ? bundleDirectory
                : null;
        }
    }

    public class UpdateCheckResult
    {
        public bool UpdateAvailable { get; }
        public string LatestVersion { get; }
        public Uri DownloadUri { get; }

        public UpdateCheckResult(bool updateAvailable, string latestVersion, Uri downloadUri)
        {
            UpdateAvailable = updateAvailable;
            LatestVersion = latestVersion;
            DownloadUri = downloadUri;
        }
    }
}
