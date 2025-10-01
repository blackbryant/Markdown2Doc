using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Markdown2Doc
{
    public sealed class PandocInfo
    {
        public string Path { get; }
        public string Version { get; }

        public PandocInfo(string path, string version)
        {
            Path = path;
            Version = version;
        }

        public override string ToString() => $"{Version} @ {Path}";
    }

    public static class PandocDetector
    {
        // 預設用來儲存/讀取的 key（你可以改）
        public const string EnvKey = "pandocPath";

        /// <summary>
        /// 嘗試以多種方式偵測 pandoc（順序：EnvUtils 保存的自訂路徑 -> 環境變數 -> PATH 搜尋 -> 常見安裝路徑）。
        /// 回傳 PandocInfo 或 null（找不到或不可執行）。
        /// </summary>
        public static async Task<PandocInfo?> DetectPandocAsync(CancellationToken ct = default)
        {
            // 1) 檢查 EnvUtils 是否有使用者先前存的路徑
            try
            {
                var saved = EnvUtils.GetString(EnvKey);
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    var p = await TryValidatePandocPathAsync(saved, ct).ConfigureAwait(false);
                    if (p != null) return p;
                }
            }
            catch { /* ignore */ }

            // 2) 檢查常見環境變數（如果你希望支援自訂 env var，可檢查 PANDOC_HOME / PANDOC_PATH）
            try
            {
                var candidatesFromEnv = new[] { "PANDOC_HOME", "PANDOC_PATH", "PANDOC" };
                foreach (var envName in candidatesFromEnv)
                {
                    try
                    {
                        var v = Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.Process)
                                ?? Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.User)
                                ?? Environment.GetEnvironmentVariable(envName, EnvironmentVariableTarget.Machine);
                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            // env 可能是目錄或是完整可執行檔
                            var path = v;
                            if (Directory.Exists(v))
                            {
                                // 常見：PANDOC_HOME 指向安裝目錄，組合 pandoc(.exe)
                                path = Path.Combine(v, ExecutableFileName());
                            }
                            var p = await TryValidatePandocPathAsync(path, ct).ConfigureAwait(false);
                            if (p != null) return p;
                        }
                    }
                    catch { /* ignore per env */ }
                }
            }
            catch { /* ignore */ }

            // 3) 檢查 PATH（嘗試直接執行 "pandoc"）
            try
            {
                // 直接用可執行檔名測試（若 PATH 有設定）
                var p = await TryValidatePandocPathAsync(ExecutableFileName(), ct).ConfigureAwait(false);
                if (p != null) return p;
            }
            catch { /* ignore */ }

            // 4) 檢查 OS 特有的常見安裝路徑
            try
            {
                foreach (var candidate in CommonInstallPaths())
                {
                    var p = await TryValidatePandocPathAsync(candidate, ct).ConfigureAwait(false);
                    if (p != null) return p;
                }
            }
            catch { /* ignore */ }

            // 找不到
            return null;
        }

        /// <summary>
        /// 驗證指定路徑是否為可執行的 pandoc（會執行 --version 取得版本字串）。
        /// 支援：完整可執行檔路徑，或在 PATH 中的可執行檔名 (e.g. "pandoc")。
        /// </summary>
        public static async Task<PandocInfo?> TryValidatePandocPathAsync(string candidatePathOrName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(candidatePathOrName)) return null;

            // 若 candidatePathOrName 是目錄，組合成執行檔
            if (Directory.Exists(candidatePathOrName))
            {
                candidatePathOrName = System.IO.Path.Combine(candidatePathOrName, ExecutableFileName());
            }

            // 若是相對路徑或單純檔名，我們仍嘗試直接執行（讓 OS 去靠 PATH 找）
            var executable = candidatePathOrName;

            // 如果有像 "C:\..." (根路徑) 或 絕對路徑存在，先檢查檔案是否存在
            if (Path.IsPathRooted(executable) && !File.Exists(executable))
                return null;

            // 若 .exe 缺漏副檔名（Windows 的情況），使用 ExecutableFileName() 可幫忙
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !Path.IsPathRooted(executable))
            {
                // leave it — we will call process with the name (Windows will find pandoc.exe via PATH)
            }

            try
            {
                var version = await GetPandocVersionAsync(executable, ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    // 若執行成功，但使用者傳入的是純檔名 (pandoc)，嘗試解析實際 full path via where/which
                    string fullPath = executable;
                    try
                    {
                        var resolved = await ResolveFullPathAsync(executable, ct).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(resolved))
                            fullPath = resolved;
                    }
                    catch { /* ignore */ }

                    return new PandocInfo(fullPath, version.Trim());
                }
            }
            catch
            {
                // 執行失敗則視為不可用
            }

            return null;
        }

        /// <summary>
        /// 呼叫 pandoc --version 並回傳第一行（版本相關）。
        /// </summary>
        public static async Task<string?> GetPandocVersionAsync(string executableOrName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(executableOrName)) return null;

            var psi = new ProcessStartInfo
            {
                FileName = executableOrName,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.EnableRaisingEvents = true;
            proc.Exited += (s, e) => tcs.TrySetResult(true);

            try
            {
                if (!proc.Start())
                    throw new InvalidOperationException("Failed to start process");

                // 讀取輸出（不會阻塞）
                var outputTask = proc.StandardOutput.ReadToEndAsync();
                var errorTask = proc.StandardError.ReadToEndAsync();

                using (ct.Register(() =>
                {
                    try { if (!proc.HasExited) proc.Kill(); } catch { }
                    tcs.TrySetCanceled();
                }))
                {
                    await tcs.Task.ConfigureAwait(false); // 等 process exit 或 cancellation
                }

                var output = await outputTask.ConfigureAwait(false);
                var err = await errorTask.ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    // pandoc --version 的第一行範例："pandoc 2.19.2"
                    var firstLine = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    return firstLine;
                }

                // 有些情況寫到 stderr
                if (!string.IsNullOrWhiteSpace(err))
                {
                    var firstLine = err.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    return firstLine;
                }

                return null;
            }
            finally
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
            }
        }

        /// <summary>
        /// 嘗試解析可執行檔實際 full path：Windows 用 where, Unix 用 which
        /// </summary>
        private static async Task<string?> ResolveFullPathAsync(string exeName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(exeName)) return null;

            string cmd, args;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cmd = "where";
                args = exeName;
            }
            else
            {
                cmd = "which";
                args = exeName;
            }

            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            try
            {
                proc.Start();
                var outp = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                proc.WaitForExit(2000);
                var first = outp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return first;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 依作業系統回傳 pandoc 可執行檔名稱
        /// </summary>
        private static string ExecutableFileName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "pandoc.exe";
            return "pandoc";
        }

        /// <summary>
        /// 常見的安裝路徑（含 Windows / macOS / Linux 常見位置）
        /// </summary>
        private static string[] CommonInstallPaths()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                return new[]
                {
                    Path.Combine(programFiles, "Pandoc", "pandoc.exe"),
                    Path.Combine(programFilesX86, "Pandoc", "pandoc.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "pandoc", "pandoc.exe"),
                    // msys / chocolatey common locations
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "pandoc.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "bin", "pandoc.exe")
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new[]
                {
                    "/usr/local/bin/pandoc",
                    "/opt/homebrew/bin/pandoc",
                    "/usr/bin/pandoc"
                };
            }
            else // linux
            {
                return new[]
                {
                    "/usr/bin/pandoc",
                    "/usr/local/bin/pandoc",
                    "/snap/bin/pandoc",
                    "/home/" + Environment.UserName + "/.local/bin/pandoc"
                };
            }
        }

        /// <summary>
        /// 如果偵測不到，讓 user 選擇 pandoc.exe（WinForms 範例 helper）
        /// </summary>
        public static string? AskUserToLocatePandocFile(System.Windows.Forms.IWin32Window owner = null)
        {
            using var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Title = "請選擇 pandoc 可執行檔 (pandoc.exe)",
                Filter = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pandoc.exe|pandoc.exe|All files|*.*" : "pandoc|pandoc|All files|*.*",
                CheckFileExists = true,
                CheckPathExists = true
            };

            var dr = owner == null ? ofd.ShowDialog() : ofd.ShowDialog(owner);
            if (dr == System.Windows.Forms.DialogResult.OK)
                return ofd.FileName;
            return null;
        }

        /// <summary>
        /// 如果使用者指定路徑，將路徑寫到 EnvUtils（或其他設定）以供未來重用。
        /// </summary>
        public static void SaveUserSelectedPath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;
            // 儲存在你的 EnvUtils（json config）以後續自動載入
            EnvUtils.SetString(EnvKey, fullPath);
        }
    }

}
