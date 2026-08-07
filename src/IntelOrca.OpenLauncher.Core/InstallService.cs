using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IntelOrca.OpenLauncher.Core
{
    public class InstallService
    {
        private const int BAD_ACCESS = unchecked((int)0x80070020);

        private readonly Game _game;

        private string VersionFilePath => Path.Combine(_game.BinPath, ".version");


        public InstallService(Game game)
        {
            _game = game;
        }

        public string ExecutablePath
        {
            get
            {
                string binaryName;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    binaryName = $"{_game.BinaryName}.exe";
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    // We need to use Name and not BinaryName since BinaryName isn't capitalized
                    binaryName = $"{_game.Name}.app/Contents/MacOS/{_game.Name}";
                } else {
                    binaryName = _game.BinaryName;
                }
                return Path.Combine(_game.BinPath, binaryName);
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
                var path = ExecutablePath;
                if (!File.Exists(path))
                    return false;

                // A half extracted archive can leave the file in place without its executable bit,
                // in which case launching it only fails once the user presses play
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return true;

                const UnixFileMode executeBits =
                    UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                return (File.GetUnixFileMode(path) & executeBits) != 0;
            }
            catch
            {
                return false;
            }
        }

        public Task Launch()
        {
            return Task.Run(async () =>
            {
                var psi = new ProcessStartInfo(ExecutablePath)
                {
                    RedirectStandardError = true
                };

                var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start process '{psi}'");

                var outputBuilder = new StringBuilder();
                var sw = Stopwatch.StartNew();

                while (sw.ElapsedMilliseconds < 2000)
                {
                    string? s = process.StandardError.ReadToEnd();
                    if (s != null)
                        outputBuilder.Append(s);

                    await Task.Delay(10);

                    if (process.HasExited && process.ExitCode != 0)
                    {
                        outputBuilder.Append(process.StandardError.ReadToEnd());
                        throw new Exception(outputBuilder.ToString());
                    }
                }
            });
        }

        public async Task DownloadVersion(
            DownloadService downloadService,
            Shell shell,
            string version,
            Uri uri,
            IProgress<DownloadProgressReport> progress,
            CancellationToken ct)
        {
            const string StatusExtracting = "StatusExtracting";

            var tempFile = await downloadService.DownloadFileAsync(uri, progress, ct).ConfigureAwait(false);
            try
            {
                progress?.Report(new DownloadProgressReport(StatusExtracting, 1.0f));
                ct.ThrowIfCancellationRequested();

                // Backup old bin directory
                var binDirectory = _game.BinPath;
                var backupDirectory = _game.BinPath + ".backup";
                
                // The old dir may not have been cleaned up properly. (Function checks for existence);
                shell.DeleteDirectory(backupDirectory);
                
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
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    shell.ExtractMacArchive(archivePath, outDirectory);
                } else {
                    ZipFile.ExtractToDirectory(archivePath, outDirectory, overwriteFiles: true);
                }
            }
            else if (uri.LocalPath.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
            {
                shell.CreateDirectory(outDirectory);
                var binaryPath = Path.Combine(outDirectory, _game.BinaryName);
                shell.MoveFile(archivePath, binaryPath);
                shell.SetExecutable(binaryPath);
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
