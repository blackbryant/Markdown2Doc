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
            // ���V x64 �� x86 �l��Ƨ��]���A���ظm/�B�z�Ǧ줸�ơ^
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
                 rollingInterval: RollingInterval.Day,           // �C�Ѥ@����
                 retainedFileCountLimit: 30,                    // �O�d�̪� 30 ��
                 outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {Message:lj}{NewLine}{Exception}",
                 fileSizeLimitBytes: 10_000_000,                // �C�ɤW�� 10MB (�i��)
                 rollOnFileSizeLimit: true
             ))
             .WriteTo.Console()
             .CreateLogger();

            try
            {
                Log.Information("���αҰ�");
                Application.EnableVisualStyles();
                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "���εo�ͭP�R���~�òפ�");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}