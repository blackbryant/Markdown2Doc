namespace Markdown2Doc
{
    partial class SettingForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            txtEnvPath = new TextBox();
            btnCheckEnv = new Button();
            linkLabel1 = new LinkLabel();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            txtOutputPath = new TextBox();
            btnSaveOutputPath = new Button();
            SuspendLayout();
            // 
            // txtEnvPath
            // 
            txtEnvPath.Location = new Point(11, 120);
            txtEnvPath.Name = "txtEnvPath";
            txtEnvPath.Size = new Size(400, 27);
            txtEnvPath.TabIndex = 5;
            // 
            // btnCheckEnv
            // 
            btnCheckEnv.Location = new Point(428, 114);
            btnCheckEnv.Name = "btnCheckEnv";
            btnCheckEnv.Size = new Size(120, 37);
            btnCheckEnv.TabIndex = 4;
            btnCheckEnv.Text = "檢查環境變數";
            btnCheckEnv.UseVisualStyleBackColor = true;
            btnCheckEnv.Click += btnCheckEnv_Click;
            // 
            // linkLabel1
            // 
            linkLabel1.AutoSize = true;
            linkLabel1.Location = new Point(12, 43);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(137, 19);
            linkLabel1.TabIndex = 3;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "Download Pandoc";
            linkLabel1.LinkClicked += linkLabel1_LinkClicked;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(11, 15);
            label1.Name = "label1";
            label1.Size = new Size(146, 19);
            label1.TabIndex = 6;
            label1.Text = "Step1. 下載轉檔程式";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 83);
            label2.Name = "label2";
            label2.Size = new Size(161, 19);
            label2.TabIndex = 7;
            label2.Text = "Step2. 設定執行擋路徑";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 170);
            label3.Name = "label3";
            label3.Size = new Size(146, 19);
            label3.TabIndex = 8;
            label3.Text = "Step3. 設定輸出目錄";
            // 
            // txtOutputPath
            // 
            txtOutputPath.Location = new Point(12, 208);
            txtOutputPath.Name = "txtOutputPath";
            txtOutputPath.Size = new Size(400, 27);
            txtOutputPath.TabIndex = 9;
            // 
            // btnSaveOutputPath
            // 
            btnSaveOutputPath.Location = new Point(428, 202);
            btnSaveOutputPath.Name = "btnSaveOutputPath";
            btnSaveOutputPath.Size = new Size(120, 36);
            btnSaveOutputPath.TabIndex = 10;
            btnSaveOutputPath.Text = "儲存輸出路徑";
            btnSaveOutputPath.UseVisualStyleBackColor = true;
            btnSaveOutputPath.Click += btnSaveOutputPath_Click;
            // 
            // SettingForm
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(btnSaveOutputPath);
            Controls.Add(txtOutputPath);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(txtEnvPath);
            Controls.Add(btnCheckEnv);
            Controls.Add(linkLabel1);
            Name = "SettingForm";
            Text = "SettingForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtEnvPath;
        private Button btnCheckEnv;
        private LinkLabel linkLabel1;
        private Label label1;
        private Label label2;
        private Label label3;
        private TextBox txtOutputPath;
        private Button btnSaveOutputPath;
    }
}