using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Markdown2Doc
{
    public partial class SettingForm : Form
    {

        private readonly Serilog.ILogger _logger;

        public SettingForm()
        {
            InitializeComponent();
            _logger = Log.ForContext<SettingForm>();
            _logger.Information("Form1 initialized");

            defaultOutputPath();

        }

        //預設
        private void defaultOutputPath()
        {
            //pandocPath
            txtEnvPath.Text = EnvUtils.GetString("pandocPath");

            //wkhtmltopdfPath
            txtWkPath.Text = EnvUtils.GetString("wkhtmltopdfPath");


            //輸出
            string? outputPath = EnvUtils.GetString("output");
            txtOutputPath.Text = outputPath;

            _logger.Information($"output path:  {outputPath}");

        }



        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string? url = EnvUtils.GetString("downloadUrl");
            _logger.Information("Link clicked. downloadUrl = {Url}", url);


            try
            {
                // 在 modern .NET (Core / 5 / 6 / 7) 使用 UseShellExecute = true 來用預設瀏覽器開啟
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);

                _logger.Information("Opened URL with system shell: {Url}", url);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to open URL: {Url}", url);
                MessageBox.Show(this, $"無法開啟連結：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private async void btnCheckEnv_Click(object sender, EventArgs e)
        {
            btnCheckEnv.Enabled = false;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var info = await PandocDetector.DetectPandocAsync(cts.Token);
                if (info != null)
                {
                    MessageBox.Show($"找到 pandoc：{info.Version}\n位置：{info.Path}");
                    // 可把路徑顯示在 textbox


                    if (string.IsNullOrWhiteSpace(txtEnvPath.Text))
                    {
                        // 尚未儲存過，詢問是否要儲存
                        var r = MessageBox.Show("是否要將此 pandoc 路徑設為預設？", "儲存預設路徑", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r == DialogResult.Yes)
                        {
                            PandocDetector.SaveUserSelectedPath(info.Path);
                            MessageBox.Show("已儲存。");
                        }
                    }
                    txtEnvPath.Text = info.Path;
                }
                else
                {
                    var r = MessageBox.Show("系統找不到 pandoc。是否手動指定 pandoc.exe？", "Pandoc 未找到", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                    {
                        var chosen = PandocDetector.AskUserToLocatePandocFile(this);
                        if (!string.IsNullOrWhiteSpace(chosen))
                        {
                            // 驗證並儲存
                            var validated = await PandocDetector.TryValidatePandocPathAsync(chosen);
                            if (validated != null)
                            {
                                PandocDetector.SaveUserSelectedPath(validated.Path);
                                MessageBox.Show($"已儲存 pandoc：{validated.Version}\n{validated.Path}");
                                txtEnvPath.Text = validated.Path;
                            }
                            else
                            {
                                MessageBox.Show("所選檔案不是有效的 pandoc 可執行檔。");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("檢查逾時，請稍後再試");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"發生例外：{ex.Message}");
            }
            finally
            {
                btnCheckEnv.Enabled = true;
            }
            string? pandocPath = EnvUtils.GetString("pandocPath");
            _logger.Information($"pandocPath = {pandocPath}");

        }

        private void btnSaveOutputPath_Click(object sender, EventArgs e)
        {
            EnvUtils.SetString("output", txtOutputPath.Text);


        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string? url = EnvUtils.GetString("downloadUrl2");
            _logger.Information("Link clicked. wkhtmltopdf  download = {Url}", url);


            try
            {
                // 在 modern .NET (Core / 5 / 6 / 7) 使用 UseShellExecute = true 來用預設瀏覽器開啟
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);

                _logger.Information("Opened URL with system shell: {Url}", url);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to open URL: {Url}", url);
                MessageBox.Show(this, $"無法開啟連結：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        

        private async void btnWkhtmlpdf_Click(object sender, EventArgs e)
        {
            btnWkhtmlpdf.Enabled = false;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var info = await WkHtmlToPdfDetector.DetectWkhtmlAsync(cts.Token);
                if (info != null)
                {
                    MessageBox.Show($"找到 wkhtmltopdf：{info.Version}\n位置：{info.Path}");
                    // 把路徑顯示到 textbox（請把 txtWkPath 換成你實際控制項名稱）
                    txtWkPath.Text = info.Path;

                    if (string.IsNullOrWhiteSpace(txtWkPath.Text) || string.IsNullOrWhiteSpace(EnvUtils.GetString("wkhtmltopdfPath")))
                    {
                        var r = MessageBox.Show("是否要將此 wkhtmltopdf 路徑設為預設？", "儲存預設路徑", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (r == DialogResult.Yes)
                        {
                            WkHtmlToPdfDetector.SaveUserSelectedPath(info.Path);
                            MessageBox.Show("已儲存。");
                        }
                    }
                }
                else
                {
                    var r = MessageBox.Show("系統找不到 wkhtmltopdf。是否手動指定 wkhtmltopdf.exe？", "wkhtmltopdf 未找到", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                    {
                        var chosen = WkHtmlToPdfDetector.AskUserToLocateWkhtmlFile(this);
                        if (!string.IsNullOrWhiteSpace(chosen))
                        {
                            var validated = await WkHtmlToPdfDetector.TryValidateWkhtmlPathAsync(chosen, CancellationToken.None);
                            if (validated != null)
                            {
                                WkHtmlToPdfDetector.SaveUserSelectedPath(validated.Path);
                                MessageBox.Show($"已儲存 wkhtmltopdf：{validated.Version}\n{validated.Path}");
                                txtWkPath.Text = validated.Path;
                            }
                            else
                            {
                                MessageBox.Show("所選檔案不是有效的 wkhtmltopdf 可執行檔。");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("檢查逾時，請稍後再試");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"發生例外：{ex.Message}");
            }
            finally
            {
                btnWkhtmlpdf.Enabled = true;
            }

            string? wkPath = EnvUtils.GetString("wkhtmltopdfPath");
            _logger?.Information($"wkhtmltopdfPath = {wkPath}");
        }


    }
}
