using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Markdown2Doc
{
    public record ExecutableInfo(string Path, string Version);

    public static class WkHtmlToPdfDetector
    {
        // 嘗試偵測 wkhtmltopdf（先用 EnvUtils 設定值，再搜尋 PATH）
        public static async Task<ExecutableInfo?> DetectWkhtmlAsync(CancellationToken cancellationToken = default)
        {
            // 1. 先檢查使用者先前設定的路徑（EnvUtils.GetString）
            string? saved = EnvUtils.GetString("wkhtmltopdfPath");
            if (!string.IsNullOrWhiteSpace(saved))
            {
                var v = await TryValidateWkhtmlPathAsync(saved, cancellationToken);
                if (v != null) return v;
            }

            // 2. 檢查可執行檔名稱：wkhtmltopdf (Windows 上尋找 wkhtmltopdf.exe)
            string[] candidates = new[] { "wkhtmltopdf.exe", "wkhtmltopdf" };

            // 搜尋 PATH 中的檔案
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var p in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    foreach (var c in candidates)
                    {
                        var full = Path.Combine(p, c);
                        if (File.Exists(full))
                        {
                            var v = await TryValidateWkhtmlPathAsync(full, cancellationToken);
                            if (v != null) return v;
                        }
                    }
                }
                catch
                {
                    // 忽略單一路徑例外
                }
            }

            // 3. 若找不到，回傳 null
            return null;
        }

        // 驗證給定路徑是否為有效 wkhtmltopdf 可執行檔，並回傳版本資訊
        public static Task<ExecutableInfo?> TryValidateWkhtmlPathAsync(string exePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(exePath)) return (ExecutableInfo?)null;

                // 如果路徑是資料夾，直接返回 null
                if (Directory.Exists(exePath)) return (ExecutableInfo?)null;

                // 嘗試執行 exe 並取得 --version 輸出
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null) return (ExecutableInfo?)null;

                    // 等待結束或取消
                    while (!proc.HasExited)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            try { proc.Kill(true); } catch { }
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        Thread.Sleep(10);
                    }

                    // 讀取輸出
                    string outp = proc.StandardOutput.ReadToEnd().Trim();
                    string errp = proc.StandardError.ReadToEnd().Trim();

                    // wkhtmltopdf 的版本資訊通常在 stdout 或 stderr (視版本)
                    string combined = string.Join(" ", new[] { outp, errp }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (string.IsNullOrWhiteSpace(combined)) return (ExecutableInfo?)null;

                    // 嘗試萃取版本字串（抓第一行）
                    string firstLine = combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? combined;
                    return new ExecutableInfo(Path.GetFullPath(exePath), firstLine);
                }
                catch
                {
                    return (ExecutableInfo?)null;
                }
            }, cancellationToken);
        }

        // 開啟檔案選擇視窗讓使用者指定 wkhtmltopdf.exe
        public static string? AskUserToLocateWkhtmlFile(IWin32Window owner)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "請選擇 wkhtmltopdf 可執行檔",
                Filter = "wkhtmltopdf.exe|wkhtmltopdf.exe|所有檔案|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false
            };

            // 若有先前儲存的路徑，就把初始資料夾指回去
            var saved = EnvUtils.GetString("wkhtmltopdfPath");
            try
            {
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    var dir = Path.GetDirectoryName(saved);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                        ofd.InitialDirectory = dir;
                }
            }
            catch { /* ignore */ }

            var dr = ofd.ShowDialog(owner);
            if (dr == DialogResult.OK)
                return ofd.FileName;
            return null;
        }

        // 儲存使用者選定的路徑（EnvUtils.SetString 假定存在）
        public static void SaveUserSelectedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            // 你可以把這裡改成 Properties.Settings.Default["wkhtmltopdfPath"] = path; Properties.Settings.Default.Save();
            EnvUtils.SetString("wkhtmltopdfPath", path);
        }
    }
}
