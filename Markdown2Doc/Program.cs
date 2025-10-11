using Serilog;
using System.Runtime.InteropServices;

namespace Markdown2Doc
{
    internal static class Program
    {

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.


            //
            // 指向 x64 或 x86 子資料夾（視你的建置/處理序位元數）
            var baseDir = AppContext.BaseDirectory;
            var archDir = Path.Combine(baseDir, Environment.Is64BitProcess ? "x64" : "x86");
            if (Directory.Exists(archDir))
            {
                SetDllDirectory(archDir);
            }


            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Debug()
             .Enrich.FromLogContext()
             .WriteTo.Async(a => a.File(
                 path: "logs\\app-.log",
                 rollingInterval: RollingInterval.Day,           // 每天一個檔
                 retainedFileCountLimit: 30,                    // 保留最近 30 天
                 outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message:lj}{NewLine}{Exception}",
                 fileSizeLimitBytes: 10_000_000,                // 每檔上限 10MB (可選)
                 rollOnFileSizeLimit: true
             ))
             .WriteTo.Console()
             .CreateLogger();

            try
            {
                Log.Information("應用啟動");
                Application.EnableVisualStyles();
                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "應用發生致命錯誤並終止");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}