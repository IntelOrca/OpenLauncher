using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace IntelOrca.OpenLauncher.Core
{
    public class InstallService
    {
        private const int BAD_ACCESS = unchecked((int)0x80070020);

        private readonly Game _game;


        private string BinPath => Path.Combine(_game.DefaultLocation, "bin");
        private string VersionFilePath => Path.Combine(_game.DefaultLocation, "bin", ".version");

        public InstallService(Game game)
        {
            _game = game;
        }

        public string ExecutablePath
        {
            get
            {
                var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                var binaryName = isWindows ? $"{_game.BinaryName}.exe" : _game.BinaryName;
                return Path.Combine(BinPath, binaryName);
            }
        }

        public async Task<string?> GetCurrentVersionAsync()
        {
            try
            {
                var versionFile = VersionFilePath;
                if (File.Exists(versionFile))
                {
                    var version = (await File.ReadAllTextAsync(versionFile)).Trim();
                    // TODO validate text
                    return version;
                }
            }
            catch
            {
            }
            return null;
        }

        public bool CanLaunch()
        {
            try
            {
                return File.Exists(ExecutablePath);
            }
            catch
            {
                return false;
            }
        }

        public void Launch()
        {
            var exePath = ExecutablePath;
            Process.Start(exePath);
        }

        public async Task DownloadVersion(
            DownloadService downloadService,
            Shell shell,
            string version,
            Uri uri,
            IProgress<DownloadProgressReport> progress,
            CancellationToken ct)
        {
            const string StatusExtracting = "Extracting";

            var tempFile = await downloadService.DownloadFileAsync(uri, progress, ct).ConfigureAwait(false);
            try
            {
                progress?.Report(new DownloadProgressReport(StatusExtracting, 1.0f));
                ct.ThrowIfCancellationRequested();

                // Backup old bin directory
                var binDirectory = BinPath;
                var backupDirectory = BinPath + ".backup";
                if (shell.DirectoryExists(binDirectory))
                {
                    shell.MoveDirectory(binDirectory, backupDirectory);
                }

                try
                {
                    // Create new bin directory
                    ExtractArchive(shell, uri, tempFile, binDirectory);
                    await shell.WriteAllTextAsync(VersionFilePath, version).ConfigureAwait(false);

                    // Delete backup bin directory
                    shell.DeleteDirectory(backupDirectory);
                }
                catch
                {
                    // Restore backup bin directory
                    shell.DeleteDirectory(binDirectory);
                    if (shell.DirectoryExists(backupDirectory))
                    {
                        shell.MoveDirectory(backupDirectory, binDirectory);
                    }
                    throw;
                }
            }
            catch (IOException ex) when (ex.HResult == BAD_ACCESS)
            {
                throw new Exception("Failed to extract zip archive.", ex);
            }
            finally
            {
                if (tempFile != null)
                {
                    shell.TryDeleteFile(tempFile);
                }
            }
        }

        private void ExtractArchive(Shell shell, Uri uri, string archivePath, string outDirectory)
        {
            if (uri.LocalPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, outDirectory, overwriteFiles: true);
            }
            else if (uri.LocalPath.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
            {
                shell.CreateDirectory(outDirectory);
                var binaryPath = Path.Combine(outDirectory, _game.BinaryName);
                shell.MoveFile(archivePath, binaryPath);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var exitCode = shell.RunProcess("chmod", "+x", binaryPath);
                    if (exitCode != 0)
                    {
                        throw new Exception($"Failed to run chmod on '{binaryPath}'");
                    }
                }
            }
            else if (uri.LocalPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                shell.CreateDirectory(outDirectory);
                var exitCode = shell.RunProcess("tar", "-C", outDirectory, "-xf", archivePath);
                if (exitCode != 0)
                {
                    throw new Exception($"tar operation failed, exit code = {exitCode}");
                }

                var extractedFiles = shell.GetFileSystemEntries(outDirectory);
                if (extractedFiles.Length == 1)
                {
                    // tar contained a single folder, move everything in that down
                    var tempDirectory = outDirectory + "-temp";
                    shell.DeleteDirectory(tempDirectory);
                    shell.MoveDirectory(outDirectory, tempDirectory);
                    shell.MoveDirectory(Path.Combine(tempDirectory, Path.GetFileName(extractedFiles[0])), outDirectory);
                    shell.DeleteDirectory(tempDirectory);
                }
            }
            else
            {
                throw new Exception("Unknown file format to extract.");
            }
        }
    }
}
