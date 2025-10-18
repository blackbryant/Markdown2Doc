using Markdig;
using Pandoc;
using ScintillaNet.Abstractions.Enumerations;
using ScintillaNET;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Log = Serilog.Log;

namespace Markdown2Doc
{
    public partial class Form1 : Form
    {
        private readonly Serilog.ILogger _logger;
        private SplitContainer split;
        private Scintilla scintilla;
        private WebBrowser preview;
        private ToolStrip toolStrip;

        // toolbar buttons
        private ToolStripButton btnSave;
        private ToolStripButton btnOpen;
        private ToolStripButton btnExportHtml;
        private ToolStripButton btnWord;
        private ToolStripButton btnPdf;
        private ToolStripButton btnExcel;
        private ToolStripButton btnSetting;

        // pandoc parameters
        private string pandocPath = "pandoc"; // default to system path
        private string outputFolder = "c:\\" ; 
        private string wkhtmltopdfPath = ""; // default to system path


        private System.Timers.Timer debounceTimer;
        private MarkdownPipeline mdPipeline;



        public Form1()
        {
            InitializeComponent();

          
            BuildUi();            // 建立 UI 元件
            InitScintilla();      // 初始化 Scintilla 設定
            InitMarkdown();       // 初始化 Markdig pipeline
            HookEvents();          // 綁定事件
            // sample content
            scintilla.Text = "# Welcome\n\nType Markdown here...";
            
            UpdatePreview();

                
            _logger = Log.ForContext<Form1>();
            _logger.Information("Form1 initialized");

        }
 

        private void BuildUi()
        {
            var ver = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;


            this.Text = $"Markdown2Doc - {ver}";
            this.ClientSize = new Size(1100, 700);

            // toolbar
            toolStrip = new ToolStrip();
            //toolStrip.SuspendLayout();
            btnSetting  = new ToolStripButton("Settings");
            btnOpen = new ToolStripButton("Open");
            btnSave = new ToolStripButton("Save");
            btnExportHtml = new ToolStripButton("Export HTML");
            btnWord = new ToolStripButton("Export Word");
            btnPdf = new ToolStripButton("Export PDF");

            toolStrip.Items.AddRange(new ToolStripItem[] 
                {btnSetting, btnOpen, btnSave, new ToolStripSeparator(), btnExportHtml ,new ToolStripSeparator(), btnWord , new ToolStripSeparator(), btnPdf});
            toolStrip.Dock = DockStyle.Top;
            
            

            // split container
            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 400,
                //Top = toolStrip.Height
            };
            this.Controls.Add(split);
            this.Controls.Add(toolStrip);

            // Scintilla editor
            scintilla = new Scintilla
            {
                Dock = DockStyle.Fill,
                WrapMode = ScintillaNET.WrapMode.None,
                Font = new Font("Consolas", 12),
                AccessibilityObject = { Name = "Markdown Editor" },
                 
            };
            scintilla.FirstVisibleLine = 0;

            // line numbers margin
            scintilla.Margins[0].Type = ScintillaNET.MarginType.Number;
            scintilla.Margins[0].Width = 100;
            split.Panel1.Controls.Add(scintilla);

            // preview
            preview = new WebBrowser
            {
                Dock = DockStyle.Fill,
                AllowWebBrowserDrop = false,
                IsWebBrowserContextMenuEnabled = false,
                ScriptErrorsSuppressed = true
            };
            split.Panel2.Controls.Add(preview);

            split.SplitterDistance = (int)(this.ClientSize.Width * 0.65);
        }
        private void MainForm_Resize(object sender, EventArgs e)
        {
            // 維持左側 65% 寬度
            if (split != null)
                split.SplitterDistance = (int)(this.ClientSize.Width * 0.65);
        }

        private void InitScintilla()
        {
            scintilla.StyleResetDefault();
            //scintilla.Styles[Style.Font = "Consolas";
            //scintilla.Styles[Style.Default].Size = 11;
            scintilla.StyleClearAll();

            // example styling for simple markdown highlights (optional)
            scintilla.Styles[32].ForeColor = Color.FromArgb(75, 110, 175); // heading
            scintilla.Styles[33].BackColor = Color.FromArgb(250, 250, 250); // code block
            scintilla.Styles[34].ForeColor = Color.DarkBlue; // inline code

            // optional: enable brace matching, autocompletion etc.
            //scintilla.CharAdded += Scintilla_CharAdded;

            // debounce timer for preview updates
            debounceTimer = new System.Timers.Timer(250) { AutoReset = false };
            debounceTimer.Elapsed += (s, e) => {
                if (this.InvokeRequired) this.Invoke(new Action(UpdatePreview));
                else UpdatePreview();
            };
        }

        private void InitMarkdown()
        {
            mdPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();
        }

        private void HookEvents()
        {
            scintilla.TextChanged += (s, e) =>
            {
                // debounce heavy op
                debounceTimer.Stop();
                debounceTimer.Start();

                // optional: do quick regex-based highlighting (lightweight)
                ApplySimpleHighlighting();
            };

            btnSetting.Click += (s, e) =>
            {
                using var settingForm = new SettingForm();
                settingForm.ShowDialog(this);
            };

            btnOpen.Click += async (s, e) =>
            {
                using var ofd = new OpenFileDialog { Filter = "Markdown|*.md;*.markdown|All files|*.*" };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    var txt = await System.IO.File.ReadAllTextAsync(ofd.FileName);
                    scintilla.Text = txt;
                }
            };

            btnSave.Click += async (s, e) =>
            {
                using var sfd = new SaveFileDialog { Filter = "Markdown|*.md;*.markdown|All files|*.*", FileName = "document.md" };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    await System.IO.File.WriteAllTextAsync(sfd.FileName, scintilla.Text);
                    MessageBox.Show(this, "Saved", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnExportHtml.Click += (s, e) =>
            {
                var html = Markdown.ToHtml(scintilla.Text ?? string.Empty, mdPipeline);
                using var sfd = new SaveFileDialog { Filter = "HTML|*.html", FileName = "document.html" };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(sfd.FileName, WrapHtml(html));
                    MessageBox.Show(this, "HTML exported", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            btnWord.Click += (s, e) =>
            {
                if (!CheckPandocPath()) 
                {
                    MessageBox.Show(this, "Word export not implemented yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!CheckOutputPath())
                {
                    MessageBox.Show(this, "Output Folder Path not implemented yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string md = scintilla.Text;
                string tempMd = Path.Combine(Path.GetTempPath(), "temp.md");
                File.WriteAllText(tempMd, md, Encoding.UTF8);

                string outDocx = Path.Combine(outputFolder, "output.docx");
                // 建立 Process 啟動 pandoc
                var psi = new ProcessStartInfo
                {
                    FileName = pandocPath,
                    Arguments = $"\"{tempMd}\" -o \"{outDocx}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    string stdErr = p.StandardError.ReadToEnd();
                    string stdOut = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        // log stdErr
                        MessageBox.Show("轉換失敗: " + stdErr);
                    }
                    else
                    {
                        // 成功
                        Process.Start(new ProcessStartInfo(outDocx) { UseShellExecute = true });
                    }
                }

            };

            btnPdf.Click += (s, e) =>
            {
                if (!CheckPandocPath())
                {
                    MessageBox.Show(this, "Word export not implemented yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!CheckWkhtmlPath())
                {
                    MessageBox.Show(this, "PDF export not implemented yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (!CheckOutputPath())
                {
                    MessageBox.Show(this, "Output Folder Path not implemented yet.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string md = scintilla.Text;
                string tempMd = Path.Combine(Path.GetTempPath(), "temp.md");
                File.WriteAllText(tempMd, md, Encoding.UTF8);
                string outPdf= Path.Combine(outputFolder, "output.pdf");

                // 建立 Process 啟動 pandoc
                var psi = new ProcessStartInfo
                {
                    FileName = pandocPath,
                    // Arguments = $"\"{tempMd}\" -o \"{outPdf}\"   --pdf-engine=\"{wkhtmltopdfPath}\" --metadata-file=emoji.yaml",
                    //  Arguments = $"\"{tempMd}\"  --pdf-engine=\"{wkhtmltopdfPath}\" --metadata-file=emoji.yaml "+
                    //  "--pdf-engine-opt=--enable-local-file-access  --pdf-engine-opt=--javascript-delay --pdf-engine-opt=1500  "
                    //  + " -o \"{outPdf}\"   ",

                    Arguments = $"\"{tempMd}\"  -f gfm+raw_html -t html5  --pdf-engine=\"{wkhtmltopdfPath}\"  --css \"twemoji\\fonts.css\"  " +
                                          $"  --lua-filter=twemoji\\twemoji.lua  --embed-resources  --resource-path=\"twemoji\\72x72;css\"" +
                                          $" -o \"{outPdf}\"   " +
                                          $"   -V wkhtmltopdf_args=\"--enable-local-file-access --print-media-type --dpi 300\"  " 
                                        ,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    string stdErr = p.StandardError.ReadToEnd();
                    string stdOut = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        // log stdErr
                        MessageBox.Show("轉換失敗: " + stdErr);
                    }
                    else
                    {
                        // 成功
                        Process.Start(new ProcessStartInfo(outPdf) { UseShellExecute = true });
                    }
                }

                 
            };


        }

        private bool CheckPandocPath() 
        {
            pandocPath = EnvUtils.GetString("pandocPath")?? "" ;
            if(string.IsNullOrEmpty(pandocPath)) 
            {
                MessageBox.Show(this, "Please Set Pandoc's Execute Path.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            _logger.Information($"pandocPath = {pandocPath}");
            return true; 
        }

        private bool CheckWkhtmlPath()
        {
            wkhtmltopdfPath = EnvUtils.GetString("wkhtmltopdfPath") ?? "";
            if (string.IsNullOrEmpty(wkhtmltopdfPath))
            {
                MessageBox.Show(this, "Please Set wkhtmltopdf's Execute Path.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            _logger.Information($"wkhtmltopdfPath = {wkhtmltopdfPath}");
            return true;
        }


        private bool CheckOutputPath()
        {
            outputFolder = EnvUtils.GetString(EnvUtils.ENV_OUTPUT) ?? "";
            if (string.IsNullOrEmpty(outputFolder))
            {
                MessageBox.Show(this, "Please Set Output Folder Path.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            _logger.Information($"output folder = {outputFolder}");
            return true;
        }



        private void Scintilla_CharAdded(object? sender, CharAddedEventArgs e)
        {
            // simple auto-pair
            if (e.Char == '(') scintilla.InsertText(scintilla.CurrentPosition, ")");
            if (e.Char == '[') scintilla.InsertText(scintilla.CurrentPosition, "]");
            if (e.Char == '{') scintilla.InsertText(scintilla.CurrentPosition, "}");
            if (e.Char == '`') scintilla.InsertText(scintilla.CurrentPosition, "`");
        }

        private void ApplySimpleHighlighting()
        {
            // Very light-weight: only highlight headings lines starting with #
            // Keep it simple so it's fast for moderate file sizes.
            try
            {
                var text = scintilla.Text ?? string.Empty;
                int len = text.Length;
                scintilla.StartStyling(0);
                scintilla.SetStyling(len, 0); // reset

                // headings
                var headingPattern = new Regex(@"(^|\r\n)(#{1,6}\s.*?)(?=\r\n|$)", RegexOptions.Multiline);
                foreach (Match m in headingPattern.Matches(text))
                {
                    scintilla.StartStyling(m.Index);
                    scintilla.SetStyling(m.Length, 32); // style index we used above
                }

                // code fences ```
                var codePattern = new Regex("```.*?```", RegexOptions.Singleline);
                foreach (Match m in codePattern.Matches(text))
                {
                    scintilla.StartStyling(m.Index);
                    scintilla.SetStyling(m.Length, 33); // code block style
                }

                // inline code `..`
                var inline = new Regex(@"`([^`]+?)`");
                foreach (Match m in inline.Matches(text))
                {
                    scintilla.StartStyling(m.Index);
                    scintilla.SetStyling(m.Length, 34);
                }
            }
            catch
            {
                // ignore highlighting errors
            }
        }

        private void UpdatePreview()
        {
            try
            {
                var md = scintilla.Text ?? string.Empty;
                var html = Markdown.ToHtml(md, mdPipeline);
                preview.DocumentText = WrapHtml(html);
            }
            catch (Exception ex)
            {
                preview.DocumentText = $"<pre style='color:red'>{ex}</pre>";
            }
        }

        private string WrapHtml(string bodyHtml)
        {
            return $@"<!doctype html>
            <html>
            <head><meta charset='utf-8'>
            <style>
            body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial; padding: 18px; line-height:1.6; }}
            pre {{ background:#f6f8fa; padding:10px; border-radius:6px; overflow:auto; }}
            code {{ font-family: Consolas, monospace; }}
            h1,h2,h3 {{ border-bottom: 1px solid #eaecef; padding-bottom: .3em; }}
            a {{ color: #0366d6; text-decoration: none; }}
            </style>
            </head>
            <body>{bodyHtml}</body></html>";
        }


    }
}
