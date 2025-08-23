using System.Drawing.Drawing2D;

namespace Chinatsuservices_localAPI_GUI
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            OutputConsole = new RichTextBox();
            label2 = new Label();
            proccessBar = new ProgressBar();
            Run_API = new Button();
            Rebuld_Config = new Button();
            Open_Config = new Button();
            TimeTillNextCall = new Label();
            Open_Cache = new Button();
            Open_Manga_Storage = new Button();
            Open_Current_Log = new Button();
            Open_Backup = new Button();
            SuspendLayout();
            // 
            // OutputConsole
            // 
            OutputConsole.BackColor = Color.FromArgb(64, 64, 64);
            OutputConsole.ForeColor = Color.White;
            OutputConsole.Location = new Point(12, 41);
            OutputConsole.Name = "OutputConsole";
            OutputConsole.Size = new Size(682, 311);
            OutputConsole.TabIndex = 0;
            OutputConsole.Text = "";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(13, 358);
            label2.Name = "label2";
            label2.Size = new Size(165, 15);
            label2.TabIndex = 2;
            label2.Text = "Current API Proccess Progress";
            // 
            // proccessBar
            // 
            proccessBar.ForeColor = Color.FromArgb(47, 41, 237);
            proccessBar.Location = new Point(189, 358);
            proccessBar.Name = "proccessBar";
            proccessBar.Size = new Size(505, 15);
            proccessBar.TabIndex = 3;
            proccessBar.ForeColorChanged += proccessBar_ForeColorChanged;
            // 
            // Run_API
            // 
            Run_API.BackColor = Color.FromArgb(0, 192, 0);
            Run_API.ForeColor = SystemColors.Control;
            Run_API.Location = new Point(12, 12);
            Run_API.Name = "Run_API";
            Run_API.Size = new Size(75, 23);
            Run_API.TabIndex = 5;
            Run_API.Text = "Run API";
            Run_API.UseVisualStyleBackColor = false;
            Run_API.Click += Run_API_Click;
            // 
            // Rebuld_Config
            // 
            Rebuld_Config.Location = new Point(13, 390);
            Rebuld_Config.Name = "Rebuld_Config";
            Rebuld_Config.Size = new Size(137, 23);
            Rebuld_Config.TabIndex = 6;
            Rebuld_Config.Text = "Rebuild Config";
            Rebuld_Config.UseVisualStyleBackColor = true;
            Rebuld_Config.Click += Rebuld_Config_Click;
            // 
            // Open_Config
            // 
            Open_Config.Location = new Point(13, 419);
            Open_Config.Name = "Open_Config";
            Open_Config.Size = new Size(137, 23);
            Open_Config.TabIndex = 7;
            Open_Config.Text = "Open Config";
            Open_Config.UseVisualStyleBackColor = true;
            Open_Config.Click += Open_Config_Click;
            // 
            // TimeTillNextCall
            // 
            TimeTillNextCall.AutoSize = true;
            TimeTillNextCall.Location = new Point(491, 16);
            TimeTillNextCall.Name = "TimeTillNextCall";
            TimeTillNextCall.Size = new Size(155, 15);
            TimeTillNextCall.TabIndex = 8;
            TimeTillNextCall.Text = "Time till next Auto API Run: ";
            // 
            // Open_Cache
            // 
            Open_Cache.Location = new Point(189, 390);
            Open_Cache.Name = "Open_Cache";
            Open_Cache.Size = new Size(85, 23);
            Open_Cache.TabIndex = 9;
            Open_Cache.Text = "Open Cache";
            Open_Cache.UseVisualStyleBackColor = true;
            Open_Cache.Click += Open_Cache_Click;
            // 
            // Open_Manga_Storage
            // 
            Open_Manga_Storage.Location = new Point(280, 390);
            Open_Manga_Storage.Name = "Open_Manga_Storage";
            Open_Manga_Storage.Size = new Size(130, 23);
            Open_Manga_Storage.TabIndex = 10;
            Open_Manga_Storage.Text = "Open Manga Storage";
            Open_Manga_Storage.UseVisualStyleBackColor = true;
            Open_Manga_Storage.Click += Open_Manga_Storage_Click;
            // 
            // Open_Current_Log
            // 
            Open_Current_Log.Location = new Point(189, 419);
            Open_Current_Log.Name = "Open_Current_Log";
            Open_Current_Log.Size = new Size(111, 23);
            Open_Current_Log.TabIndex = 11;
            Open_Current_Log.Text = "Open Current Log";
            Open_Current_Log.UseVisualStyleBackColor = true;
            Open_Current_Log.Click += Open_Current_Log_Click;
            // 
            // Open_Backup
            // 
            Open_Backup.Location = new Point(306, 419);
            Open_Backup.Name = "Open_Backup";
            Open_Backup.Size = new Size(104, 23);
            Open_Backup.TabIndex = 12;
            Open_Backup.Text = "Open Backup";
            Open_Backup.UseVisualStyleBackColor = true;
            Open_Backup.Click += Open_Backup_Click;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(714, 449);
            Controls.Add(Open_Backup);
            Controls.Add(Open_Current_Log);
            Controls.Add(Open_Manga_Storage);
            Controls.Add(Open_Cache);
            Controls.Add(TimeTillNextCall);
            Controls.Add(Open_Config);
            Controls.Add(Rebuld_Config);
            Controls.Add(Run_API);
            Controls.Add(proccessBar);
            Controls.Add(label2);
            Controls.Add(OutputConsole);
            Name = "Main";
            Text = "API Controller";
            FormClosing += AppClosing_FormClosing;
            Load += Main_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private RichTextBox OutputConsole;
        private Label label2;
        private ProgressBar proccessBar;
        private Button Run_API;
        private Button Rebuld_Config;
        private Button Open_Config;

        public static void RoundControl(Control ctrl, int radius)
        {
            var path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(new Rectangle(0, 0, radius, radius), 180, 90);
            path.AddArc(new Rectangle(ctrl.Width - radius, 0, radius, radius), 270, 90);
            path.AddArc(new Rectangle(ctrl.Width - radius, ctrl.Height - radius, radius, radius), 0, 90);
            path.AddArc(new Rectangle(0, ctrl.Height - radius, radius, radius), 90, 90);
            path.CloseFigure();

            ctrl.Region = new Region(path);
        }

        private void AppClosing_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Main.runningAPICount > 0)
            {
                var closeWarningMsg = MessageBox.Show("The application cannot currently be closed due to an API proccess currently in operation, Please wait for this operation to finish \n\nIf the application were to be closed this may result in: \n\nPartial OR Full Cache File Corruption, or missing Data", "Error");

                e.Cancel = true;

                return;
            }
            else
            {
                var closeWarningMsg = MessageBox.Show("If you close this, No API Proccesses will run and Cache will not be updated, Untill this Aplication is ran again. \n\nDo you wish to procced?", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (closeWarningMsg != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    
                }
            }
        }

        private Label TimeTillNextCall;
        private Button Open_Cache;
        private Button Open_Manga_Storage;
        private Button Open_Current_Log;
        private Button Open_Backup;
    }
}
