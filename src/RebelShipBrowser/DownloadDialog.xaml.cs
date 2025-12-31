using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace RebelShipBrowser
{
    public sealed partial class DownloadDialog : Window, IDisposable
    {
        private readonly Uri _downloadUrl;
        private readonly long _totalBytes;
        private readonly CancellationTokenSource _cts = new();
        private bool _downloadComplete;
        private string? _downloadedFilePath;

        public bool DownloadSuccessful => _downloadComplete && _downloadedFilePath != null;
        public string? DownloadedFilePath => _downloadedFilePath;

        public DownloadDialog(Uri downloadUrl, long totalBytes)
        {
            InitializeComponent();
            _downloadUrl = downloadUrl;
            _totalBytes = totalBytes;

            var totalMb = totalBytes / 1024.0 / 1024.0;
            ProgressText.Text = $"0 MB / {totalMb:F1} MB";

            Loaded += async (s, e) => await StartDownloadAsync();
        }

        private async Task StartDownloadAsync()
        {
            try
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
                response.EnsureSuccessStatusCode();

                // Use unique filename to avoid file locking issues
                var tempPath = Path.Combine(Path.GetTempPath(), $"RebelShipBrowser_Setup_{DateTime.Now:yyyyMMddHHmmss}.exe");
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                await using var contentStream = await response.Content.ReadAsStreamAsync(_cts.Token);

                var buffer = new byte[81920];
                long totalRead = 0;
                var stopwatch = Stopwatch.StartNew();
                long lastSpeedUpdate = 0;
                long bytesAtLastUpdate = 0;

                while (true)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, _cts.Token);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cts.Token);
                    totalRead += bytesRead;

                    // Update UI
                    var progress = (double)totalRead / _totalBytes * 100;
                    var downloadedMb = totalRead / 1024.0 / 1024.0;
                    var totalMb = _totalBytes / 1024.0 / 1024.0;

                    // Calculate speed every 500ms
                    var elapsed = stopwatch.ElapsedMilliseconds;
                    double speedMbps = 0;
                    if (elapsed - lastSpeedUpdate >= 500)
                    {
                        var bytesSinceLastUpdate = totalRead - bytesAtLastUpdate;
                        var secondsSinceLastUpdate = (elapsed - lastSpeedUpdate) / 1000.0;
                        if (secondsSinceLastUpdate > 0)
                        {
                            speedMbps = (bytesSinceLastUpdate / 1024.0 / 1024.0) / secondsSinceLastUpdate;
                        }
                        lastSpeedUpdate = elapsed;
                        bytesAtLastUpdate = totalRead;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgress.Value = progress;
                        ProgressText.Text = $"{downloadedMb:F1} MB / {totalMb:F1} MB";
                        if (speedMbps > 0)
                        {
                            SpeedText.Text = $"{speedMbps:F1} MB/s";
                        }
                    });
                }

                _downloadedFilePath = tempPath;
                _downloadComplete = true;

                // Don't start installer here - let MainWindow do it after exiting
                // This avoids file locking issues
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Download complete!";
                    DialogResult = true;
                    Close();
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Download cancelled.";
                    DialogResult = false;
                    Close();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Download failed: {ex.Message}";
                    CancelButton.Content = "Close";
                });
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_downloadComplete)
            {
                _cts.Cancel();
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_downloadComplete && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Dispose();
            }
        }
    }
}
