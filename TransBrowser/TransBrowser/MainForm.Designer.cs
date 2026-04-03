
namespace TransBrowser
{
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));

            this.webView21 = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.trayContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.trayShowHideMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.trayClickThroughMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.traySettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.traySepMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.trayExitMenuItem = new System.Windows.Forms.ToolStripMenuItem();

            // Legacy tray items (control panel, old settings, old exit – kept for backward compat)
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.控制器ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.设置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.退出ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();

            this.pageHeader1 = new AntdUI.PageHeader();

            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPageFirst = new System.Windows.Forms.TabPage();
            this.tabPageAdd = new System.Windows.Forms.TabPage();

            ((System.ComponentModel.ISupportInitialize)(this.webView21)).BeginInit();
            this.trayContextMenuStrip.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPageFirst.SuspendLayout();
            this.SuspendLayout();

            // 
            // webView21 – lives inside tabPageFirst
            // 
            this.webView21.AllowExternalDrop = true;
            this.webView21.CreationProperties = null;
            this.webView21.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView21.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webView21.Location = new System.Drawing.Point(0, 0);
            this.webView21.Name = "webView21";
            this.webView21.Size = new System.Drawing.Size(582, 361);
            this.webView21.TabIndex = 0;
            this.webView21.ZoomFactor = 1D;

            // 
            // tabPageFirst
            // 
            this.tabPageFirst.Controls.Add(this.webView21);
            this.tabPageFirst.Location = new System.Drawing.Point(4, 22);
            this.tabPageFirst.Name = "tabPageFirst";
            this.tabPageFirst.Size = new System.Drawing.Size(582, 361);
            this.tabPageFirst.TabIndex = 0;
            this.tabPageFirst.Text = "新标签页";
            this.tabPageFirst.UseVisualStyleBackColor = true;

            // 
            // tabPageAdd – the "+" sentinel tab
            // 
            this.tabPageAdd.Location = new System.Drawing.Point(4, 22);
            this.tabPageAdd.Name = "tabPageAdd";
            this.tabPageAdd.Size = new System.Drawing.Size(582, 361);
            this.tabPageAdd.TabIndex = 1;
            this.tabPageAdd.Text = "+";
            this.tabPageAdd.UseVisualStyleBackColor = true;

            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPageFirst);
            this.tabControl1.Controls.Add(this.tabPageAdd);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 23);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(590, 387);
            this.tabControl1.TabIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            this.tabControl1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tabControl1_MouseDown);

            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.trayContextMenuStrip;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "TransBrowser";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseDoubleClick);

            // 
            // trayContextMenuStrip
            // 
            this.trayContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.trayShowHideMenuItem,
                this.trayClickThroughMenuItem,
                this.traySettingsMenuItem,
                this.traySepMenuItem,
                this.trayExitMenuItem });
            this.trayContextMenuStrip.Name = "trayContextMenuStrip";
            this.trayContextMenuStrip.Size = new System.Drawing.Size(160, 98);

            // 
            // trayShowHideMenuItem
            // 
            this.trayShowHideMenuItem.Name = "trayShowHideMenuItem";
            this.trayShowHideMenuItem.Size = new System.Drawing.Size(159, 22);
            this.trayShowHideMenuItem.Text = "显示 / 隐藏";
            this.trayShowHideMenuItem.Click += new System.EventHandler(this.trayShowHideMenuItem_Click);

            // 
            // trayClickThroughMenuItem
            // 
            this.trayClickThroughMenuItem.CheckOnClick = false;
            this.trayClickThroughMenuItem.Name = "trayClickThroughMenuItem";
            this.trayClickThroughMenuItem.Size = new System.Drawing.Size(159, 22);
            this.trayClickThroughMenuItem.Text = "鼠标穿透模式";
            this.trayClickThroughMenuItem.Click += new System.EventHandler(this.trayClickThroughMenuItem_Click);

            // 
            // traySettingsMenuItem
            // 
            this.traySettingsMenuItem.Name = "traySettingsMenuItem";
            this.traySettingsMenuItem.Size = new System.Drawing.Size(159, 22);
            this.traySettingsMenuItem.Text = "设置";
            this.traySettingsMenuItem.Click += new System.EventHandler(this.traySettingsMenuItem_Click);

            // 
            // traySepMenuItem
            // 
            this.traySepMenuItem.Name = "traySepMenuItem";
            this.traySepMenuItem.Size = new System.Drawing.Size(156, 6);

            // 
            // trayExitMenuItem
            // 
            this.trayExitMenuItem.Name = "trayExitMenuItem";
            this.trayExitMenuItem.Size = new System.Drawing.Size(159, 22);
            this.trayExitMenuItem.Text = "退出";
            this.trayExitMenuItem.Click += new System.EventHandler(this.trayExitMenuItem_Click);

            // 
            // contextMenuStrip1  (legacy – used for form's own ContextMenuStrip property)
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.控制器ToolStripMenuItem,
                this.设置ToolStripMenuItem,
                this.退出ToolStripMenuItem });
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(113, 70);

            this.控制器ToolStripMenuItem.Name = "控制器ToolStripMenuItem";
            this.控制器ToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.控制器ToolStripMenuItem.Text = "控制器";
            this.控制器ToolStripMenuItem.Click += new System.EventHandler(this.控制器ToolStripMenuItem_Click);

            this.设置ToolStripMenuItem.Name = "设置ToolStripMenuItem";
            this.设置ToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.设置ToolStripMenuItem.Text = "设置";
            this.设置ToolStripMenuItem.Click += new System.EventHandler(this.设置ToolStripMenuItem_Click);

            this.退出ToolStripMenuItem.Name = "退出ToolStripMenuItem";
            this.退出ToolStripMenuItem.Size = new System.Drawing.Size(112, 22);
            this.退出ToolStripMenuItem.Text = "退出";
            this.退出ToolStripMenuItem.Click += new System.EventHandler(this.退出ToolStripMenuItem_Click);

            // 
            // pageHeader1
            // 
            this.pageHeader1.Dock = System.Windows.Forms.DockStyle.Top;
            this.pageHeader1.Location = new System.Drawing.Point(0, 0);
            this.pageHeader1.MaximizeBox = false;
            this.pageHeader1.MinimizeBox = false;
            this.pageHeader1.Name = "pageHeader1";
            this.pageHeader1.ShowButton = true;
            this.pageHeader1.ShowIcon = true;
            this.pageHeader1.Size = new System.Drawing.Size(590, 23);
            this.pageHeader1.TabIndex = 2;
            this.pageHeader1.Text = "TransBrowser";

            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(590, 410);
            this.ContextMenuStrip = this.contextMenuStrip1;
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.pageHeader1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "TransBrowser";
            this.Load += new System.EventHandler(this.MainForm_Load);

            ((System.ComponentModel.ISupportInitialize)(this.webView21)).EndInit();
            this.trayContextMenuStrip.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPageFirst.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private Microsoft.Web.WebView2.WinForms.WebView2 webView21;
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip trayContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem trayShowHideMenuItem;
        private System.Windows.Forms.ToolStripMenuItem trayClickThroughMenuItem;
        private System.Windows.Forms.ToolStripMenuItem traySettingsMenuItem;
        private System.Windows.Forms.ToolStripSeparator traySepMenuItem;
        private System.Windows.Forms.ToolStripMenuItem trayExitMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 设置ToolStripMenuItem;
        private AntdUI.PageHeader pageHeader1;
        private System.Windows.Forms.ToolStripMenuItem 退出ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 控制器ToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPageFirst;
        private System.Windows.Forms.TabPage tabPageAdd;
    }
}

