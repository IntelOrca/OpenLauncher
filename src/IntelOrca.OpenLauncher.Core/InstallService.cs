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

        public async Task DownloadVersion(string version, Uri uri, IProgress<DownloadProgressReport> progress, CancellationToken ct)
        {
            const string StatusDownloading = "Downloading";
            const string StatusExtracting = "Extracting";

            ct.ThrowIfCancellationRequested();

            string? tempFile = null;
            try
            {
                progress?.Report(new DownloadProgressReport(StatusDownloading, 0.0f));

                var client = new HttpClient();
                var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                tempFile = Path.GetTempFileName();
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    var totalBytes = response.Content.Headers.ContentLength;
                    if (totalBytes == null)
                    {
                        progress?.Report(new DownloadProgressReport(StatusDownloading, null));
                    }

                    var downloadedBytes = 0;
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();

                        var buffer = new byte[4096];
                        var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                        if (read == 0)
                            break;

                        await fs.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                        downloadedBytes += read;
                        if (totalBytes != null)
                        {
                            progress?.Report(new DownloadProgressReport(StatusDownloading, (float)downloadedBytes / totalBytes.Value));
                        }
                    }
                }
                progress?.Report(new DownloadProgressReport(StatusExtracting, 1.0f));
                ct.ThrowIfCancellationRequested();

                // Backup old bin directory
                var binDirectory = BinPath;
                var backupDirectory = BinPath + ".backup";
                if (Directory.Exists(binDirectory))
                {
                    Directory.Move(binDirectory, backupDirectory);
                }

                try
                {
                    // Create new bin directory
                    ExtractArchive(uri, tempFile, binDirectory);
                    await File.WriteAllTextAsync(VersionFilePath, version).ConfigureAwait(false);

                    // Delete backup bin directory
                    DeleteDirectory(backupDirectory);
                }
                catch
                {
                    // Restore backup bin directory
                    DeleteDirectory(binDirectory);
                    if (Directory.Exists(backupDirectory))
                    {
                        Directory.Move(backupDirectory, binDirectory);
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
                    TryDeleteFile(tempFile);
                }
            }
        }

        private void ExtractArchive(Uri uri, string archivePath, string outDirectory)
        {
            if (uri.LocalPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, outDirectory, overwriteFiles: true);
            }
            else if (uri.LocalPath.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
            {
                CreateDirectory(outDirectory);
                var binaryPath = Path.Combine(outDirectory, _game.BinaryName);
                File.Move(archivePath, binaryPath);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var exitCode = StartProcess("chmod", "+x", binaryPath);
                    if (exitCode != 0)
                    {
                        throw new Exception($"Failed to run chmod on '{binaryPath}'");
                    }
                }
            }
            else if (uri.LocalPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                CreateDirectory(outDirectory);
                var exitCode = StartProcess("tar", "-C", outDirectory, "-xf", archivePath);
                if (exitCode != 0)
                {
                    throw new Exception($"tar operation failed, exit code = {exitCode}");
                }

                var extractedFiles = Directory.GetFileSystemEntries(outDirectory);
                if (extractedFiles.Length == 1)
                {
                    // tar contained a single folder, move everything in that down
                    var tempDirectory = outDirectory + "-temp";
                    DeleteDirectory(tempDirectory);
                    Directory.Move(outDirectory, tempDirectory);
                    Directory.Move(Path.Combine(tempDirectory, Path.GetFileName(extractedFiles[0])), outDirectory);
                    DeleteDirectory(tempDirectory);
                }
            }
            else
            {
                throw new Exception("Unknown file format to extract.");
            }
        }

        private static int StartProcess(string name, params string[] args)
        {
            var psi = new ProcessStartInfo(name);
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }
            var p = Process.Start(psi);
            p.WaitForExit();
            return p.ExitCode;
        }

        private static void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        public struct DownloadProgressReport
        {
            public string Status { get; }
            public float? Value { get; }

            public DownloadProgressReport(string status, float? value)
            {
                Status = status;
                Value = value;
            }
        }
    }
}
