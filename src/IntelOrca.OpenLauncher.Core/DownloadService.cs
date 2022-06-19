using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IntelOrca.OpenLauncher.Core
{
    public class DownloadService
    {
        private const string StatusDownloading = "Downloading";

        public async Task<string> DownloadFileAsync(Uri uri, IProgress<DownloadProgressReport> progress, CancellationToken ct)
        {
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
                progress?.Report(new DownloadProgressReport(StatusDownloading, 1.0f));
                return tempFile;
            }
            catch
            {
                if (tempFile != null)
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                    }
                }
                throw;
            }
        }
    }
}
