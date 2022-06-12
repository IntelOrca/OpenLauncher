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

        public async Task DownloadVersion(string version, Uri uri, IProgress<float> progress, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            string? tempFile = null;
            try
            {
                progress?.Report(0.0f);

                var client = new HttpClient();
                var response = await client.GetAsync(uri).ConfigureAwait(false);
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                tempFile = Path.GetTempFileName();
                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    var totalBytes = stream.Length;
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
                        progress?.Report((float)downloadedBytes / totalBytes);
                    }
                }
                progress?.Report(1.0f);
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
                    ZipFile.ExtractToDirectory(tempFile, binDirectory, overwriteFiles: true);
                    await File.WriteAllTextAsync(VersionFilePath, version).ConfigureAwait(false);

                    // Delete backup bin directory
                    if (Directory.Exists(backupDirectory))
                    {
                        Directory.Delete(backupDirectory, recursive: true);
                    }
                }
                catch
                {
                    // Restore backup bin directory
                    if (Directory.Exists(binDirectory))
                    {
                        Directory.Delete(binDirectory, recursive: true);
                    }
                    if (Directory.Exists(backupDirectory))
                    {
                        Directory.Move(backupDirectory, binDirectory);
                    }
                }
            }
            catch (IOException ex) when (ex.HResult == BAD_ACCESS)
            {
                throw new Exception("Failed to extract zip archive.", ex);
            }
            finally
            {
                if (tempFile != null && File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
