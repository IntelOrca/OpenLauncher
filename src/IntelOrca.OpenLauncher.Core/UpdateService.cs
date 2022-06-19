using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IntelOrca.OpenLauncher.Core
{
    public class UpdateService
    {
        private static readonly RepositoryName Repository = new RepositoryName("IntelOrca", "OpenLauncher");

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

            // Update successful, now we just need to launch the new process and quit this one
            try
            {
                shell.StartProcess(processPath);
            }
            catch
            {
                throw new Exception("Launcher updated, but failed to start");
            }
            Environment.Exit(0);
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
