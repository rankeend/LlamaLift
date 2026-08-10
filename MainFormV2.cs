using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using AButton = AntdUI.Button;
using AInput = AntdUI.Input;
using AInputNumber = AntdUI.InputNumber;
using APanel = AntdUI.Panel;
using ASelect = AntdUI.Select;
using ASwitch = AntdUI.Switch;

namespace LlamaServerManager
{
    public sealed class MainFormV2 : Form
    {
        private readonly AppConfig config;
        private readonly ServerProcessManager processManager;
        private readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
        private readonly Dictionary<string, AButton> navButtons = new Dictionary<string, AButton>();
        private readonly List<Control> configurationControls = new List<Control>();

        private ModelProfile currentProfile;
        private ThemePalette palette;
        private bool loadingControls;
        private bool bindingProfiles;
        private bool healthCheckBusy;
        private bool externalServiceDetected;
        private bool forceExit;
        private string currentPage = "dashboard";

        private APanel sidebar;
        private APanel pageHost;
        private TableLayoutPanel profilePage;
        private TableLayoutPanel profileColumns;
        private System.Windows.Forms.Panel profileScroll;
        private AntdUI.Panel profileCommandCard;
        private APanel profileFilesCard;
        private APanel profileRuntimeCard;
        private bool profileCardsStacked;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private Label lblHeroStatus;
        private Label lblEndpoint;
        private Label lblProfileSummary;
        private Label lblProcessMetric;
        private Label lblApiMetric;
        private Label lblPromptMetric;
        private Label lblGenerationMetric;
        private RichTextBox txtDashboardLog;
        private RichTextBox txtLogs;

        private ComboBox cmbProfiles;
        private AInput txtProfileName;
        private AInput txtServerExe;
        private AInput txtModel;
        private AInput txtMmproj;
        private AInput txtAlias;
        private AInput txtApiKeyFile;
        private AInput txtHost;
        private AInput txtAdvertisedHost;
        private AInputNumber numPort;
        private AInputNumber numContext;
        private AInputNumber numParallel;
        private AInput txtGpuLayers;
        private AInputNumber numFitTarget;
        private AInputNumber numImageTokens;
        private ASelect cmbCacheK;
        private ASelect cmbCacheV;
        private ASelect cmbReasoning;
        private ASwitch swFit;
        private ASwitch swFlash;
        private ASwitch swJinja;
        private ASwitch swNoWebUi;
        private ASwitch swNoMmap;
        private ASwitch swMlock;
        private AInput txtExtraArgs;
        private AInput txtCommand;

        private AButton btnStart;
        private AButton btnStop;
        private AButton btnRestart;
        private AButton btnDetect;
        private AButton btnTest;
        private ASelect cmbTheme;
        private ASelect cmbAccent;
        private System.Windows.Forms.Timer healthTimer;
        private NotifyIcon trayIcon;

        public MainFormV2()
        {
            config = ConfigStore.Load();
            processManager = new ServerProcessManager();
            processManager.LogReceived += ProcessManagerLogReceived;
            processManager.RunningChanged += ProcessManagerRunningChanged;

            InitializeWindow();
            BuildInterface();
            BindProfiles();
            ApplyTheme();
            SetupTray();

            healthTimer = new System.Windows.Forms.Timer();
            healthTimer.Interval = 2500;
            healthTimer.Tick += async delegate { await RefreshHealthAsync(); };
            healthTimer.Start();

            AppendLog("Llama Server Manager " + AppVersion.DisplayVersion + " 已启动。", false);
            AppendLog("运行模式：" + (ConfigStore.IsPortable ? "便携版" : "安装版") + "；配置目录：" + ConfigStore.DataDirectory, false);
            UpdateDashboardSummary();
        }

        private void InitializeWindow()
        {
            Text = "Llama Server Manager " + AppVersion.DisplayVersion;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(940, 600);
            Size = new Size(1320, 840);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.Sizable;
            ControlBox = true;
            MinimizeBox = true;
            MaximizeBox = true;
            ShowInTaskbar = true;
            Shown += delegate { EnsureWindowFitsScreen(); UpdateProfileResponsiveLayout(); };
            FormClosing += MainFormClosing;
        }

        private void EnsureWindowFitsScreen()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int maximumWidth = Math.Max(MinimumSize.Width, area.Width - 40);
            int maximumHeight = Math.Max(MinimumSize.Height, area.Height - 40);
            if (Width > maximumWidth || Height > maximumHeight)
                Size = new Size(Math.Min(Width, maximumWidth), Math.Min(Height, maximumHeight));
            Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
            Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
        }

        private void BuildInterface()
        {
            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 2;
            shell.RowCount = 1;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224F));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Controls.Add(shell);

            sidebar = BuildSidebar();
            shell.Controls.Add(sidebar, 0, 0);

            TableLayoutPanel main = new TableLayoutPanel();
            main.Dock = DockStyle.Fill;
            main.Tag = "background";
            main.Padding = new Padding(18, 0, 18, 16);
            main.RowCount = 2;
            main.ColumnCount = 1;
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shell.Controls.Add(main, 1, 0);

            main.Controls.Add(BuildTopBar(), 0, 0);
            pageHost = new APanel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.Radius = 14;
            pageHost.Shadow = 2;
            pageHost.Padding = new Padding(0);
            pageHost.Tag = "surface";
            main.Controls.Add(pageHost, 0, 1);

            AddPage("dashboard", BuildDashboardPage());
            AddPage("profiles", BuildProfilesPage());
            AddPage("logs", BuildLogsPage());
            AddPage("settings", BuildSettingsPage());
            Navigate("dashboard");
        }

        private APanel BuildSidebar()
        {
            APanel panel = new APanel();
            panel.Dock = DockStyle.Fill;
            panel.Radius = 0;
            panel.Tag = "sidebar";
            panel.Padding = new Padding(16, 20, 16, 16);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 7;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 98F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            panel.Controls.Add(layout);

            System.Windows.Forms.Panel brand = new System.Windows.Forms.Panel();
            brand.Dock = DockStyle.Fill;
            brand.Tag = "sidebar";
            Label logo = MakeLabel("LLAMA\nCONTROL", 18F, FontStyle.Bold);
            logo.Location = new Point(6, 5);
            logo.AutoSize = true;
            Label version = MakeMutedLabel("SERVER MANAGER  ·  V" + AppVersion.ProductVersion, 8.5F);
            version.Location = new Point(7, 61);
            version.AutoSize = true;
            brand.Controls.Add(logo);
            brand.Controls.Add(version);
            layout.Controls.Add(brand, 0, 0);

            layout.Controls.Add(MakeNavButton("dashboard", "总览", "▦"), 0, 1);
            layout.Controls.Add(MakeNavButton("profiles", "模型配置", "◫"), 0, 2);
            layout.Controls.Add(MakeNavButton("logs", "运行日志", "≡"), 0, 3);
            layout.Controls.Add(MakeNavButton("settings", "外观与设置", "⚙"), 0, 4);

            System.Windows.Forms.Panel footer = new System.Windows.Forms.Panel();
            footer.Dock = DockStyle.Fill;
            footer.Tag = "sidebar";
            Label machine = MakeLabel(Environment.MachineName, 9F, FontStyle.Bold);
            machine.Location = new Point(6, 13);
            machine.AutoSize = true;
            Label mode = MakeMutedLabel(ConfigStore.IsPortable ? "便携模式" : "安装模式", 8.5F);
            mode.Location = new Point(6, 37);
            mode.AutoSize = true;
            footer.Controls.Add(machine);
            footer.Controls.Add(mode);
            layout.Controls.Add(footer, 0, 6);
            return panel;
        }

        private Control BuildTopBar()
        {
            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.Tag = "background";
            bar.ColumnCount = 1;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            System.Windows.Forms.Panel titles = new System.Windows.Forms.Panel();
            titles.Dock = DockStyle.Fill;
            titles.Tag = "background";
            lblHeaderTitle = MakeLabel("服务总览", 18F, FontStyle.Bold);
            lblHeaderTitle.Location = new Point(2, 15);
            lblHeaderTitle.AutoSize = true;
            lblHeaderSubtitle = MakeMutedLabel("管理 llama.cpp 后端、模型和 API", 9F);
            lblHeaderSubtitle.Location = new Point(4, 46);
            lblHeaderSubtitle.AutoSize = true;
            titles.Controls.Add(lblHeaderTitle);
            titles.Controls.Add(lblHeaderSubtitle);
            bar.Controls.Add(titles, 0, 0);

            cmbAccent = MakeSelect(96);
            cmbAccent.Items.AddRange(new object[] { "翡翠", "蓝色", "紫罗兰", "橙色", "玫红" });
            cmbAccent.SelectedIndexChanged += delegate { QuickAccentChanged(); };
            cmbTheme = MakeSelect(96);
            cmbTheme.Items.AddRange(new object[] { "跟随系统", "浅色", "深色" });
            cmbTheme.SelectedIndexChanged += delegate { QuickThemeChanged(); };
            return bar;
        }

        private Control BuildDashboardPage()
        {
            TableLayoutPanel page = NewPage();
            page.Padding = new Padding(22);
            page.RowCount = 4;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 280F));
            page.AutoScrollMinSize = new Size(920, 704);

            APanel hero = NewCard();
            hero.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel heroLayout = new TableLayoutPanel();
            heroLayout.Dock = DockStyle.Fill;
            heroLayout.Padding = new Padding(22, 16, 22, 16);
            heroLayout.ColumnCount = 2;
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
            System.Windows.Forms.Panel heroText = new System.Windows.Forms.Panel();
            heroText.Dock = DockStyle.Fill;
            heroText.Tag = "surface";
            lblHeroStatus = MakeLabel("服务未运行", 19F, FontStyle.Bold);
            lblHeroStatus.Location = new Point(0, 8);
            lblHeroStatus.AutoSize = true;
            lblProfileSummary = MakeMutedLabel("请选择并配置一个 llama.cpp 模型", 9.5F);
            lblProfileSummary.Location = new Point(2, 45);
            lblProfileSummary.AutoSize = true;
            lblEndpoint = MakeMutedLabel("API 地址尚未配置", 9F);
            lblEndpoint.Location = new Point(2, 72);
            lblEndpoint.AutoSize = true;
            heroText.Controls.Add(lblHeroStatus);
            heroText.Controls.Add(lblProfileSummary);
            heroText.Controls.Add(lblEndpoint);
            heroLayout.Controls.Add(heroText, 0, 0);

            FlowLayoutPanel heroActions = new FlowLayoutPanel();
            heroActions.Dock = DockStyle.Fill;
            heroActions.FlowDirection = FlowDirection.RightToLeft;
            heroActions.WrapContents = true;
            heroActions.Padding = new Padding(0, 21, 0, 0);
            heroActions.Tag = "surface";
            btnStart = MakeButton("启动服务", 108, AntdUI.TTypeMini.Primary, StartClicked);
            btnStop = MakeButton("停止", 78, AntdUI.TTypeMini.Error, StopClicked);
            btnRestart = MakeButton("重启", 78, AntdUI.TTypeMini.Warn, RestartClicked);
            heroActions.Controls.Add(btnStart);
            heroActions.Controls.Add(btnStop);
            heroActions.Controls.Add(btnRestart);
            heroLayout.Controls.Add(heroActions, 1, 0);
            hero.Controls.Add(heroLayout);
            page.Controls.Add(hero, 0, 0);

            TableLayoutPanel metrics = new TableLayoutPanel();
            metrics.Dock = DockStyle.Fill;
            metrics.Tag = "surface";
            metrics.ColumnCount = 4;
            for (int i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            lblProcessMetric = AddMetricCard(metrics, 0, "进程", "未运行", "PID 与进程状态");
            lblApiMetric = AddMetricCard(metrics, 1, "API", "离线", "本机健康检查");
            lblPromptMetric = AddMetricCard(metrics, 2, "预填充", "—", "Prompt tokens/s");
            lblGenerationMetric = AddMetricCard(metrics, 3, "生成", "—", "Generation tokens/s");
            page.Controls.Add(metrics, 0, 1);

            APanel actions = NewCard();
            actions.Margin = new Padding(0, 14, 0, 14);
            TableLayoutPanel actionLayout = new TableLayoutPanel();
            actionLayout.Dock = DockStyle.Fill;
            actionLayout.Padding = new Padding(18, 12, 18, 12);
            actionLayout.RowCount = 2;
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label quickTitle = MakeLabel("快捷操作", 11F, FontStyle.Bold);
            quickTitle.Dock = DockStyle.Fill;
            actionLayout.Controls.Add(quickTitle, 0, 0);
            FlowLayoutPanel quick = new FlowLayoutPanel();
            quick.Dock = DockStyle.Fill;
            quick.WrapContents = false;
            quick.Tag = "surface";
            btnDetect = MakeButton("检测后端", 104, AntdUI.TTypeMini.Default, DetectBackendClicked);
            btnTest = MakeButton("测试双协议", 112, AntdUI.TTypeMini.Default, TestApiClicked);
            quick.Controls.Add(btnDetect);
            quick.Controls.Add(btnTest);
            quick.Controls.Add(MakeButton("复制 API 地址", 124, AntdUI.TTypeMini.Default, CopyEndpointClicked));
            quick.Controls.Add(MakeButton("打开 WebUI", 108, AntdUI.TTypeMini.Default, OpenWebUiClicked));
            quick.Controls.Add(MakeButton("编辑模型配置", 124, AntdUI.TTypeMini.Default, delegate { Navigate("profiles"); }));
            actionLayout.Controls.Add(quick, 0, 1);
            actions.Controls.Add(actionLayout);
            page.Controls.Add(actions, 0, 2);

            APanel recent = NewCard();
            recent.Padding = new Padding(16);
            TableLayoutPanel recentLayout = new TableLayoutPanel();
            recentLayout.Dock = DockStyle.Fill;
            recentLayout.RowCount = 2;
            recentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            recentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label recentTitle = MakeLabel("实时日志", 11F, FontStyle.Bold);
            recentTitle.Dock = DockStyle.Fill;
            recentLayout.Controls.Add(recentTitle, 0, 0);
            txtDashboardLog = MakeLogBox();
            recentLayout.Controls.Add(txtDashboardLog, 0, 1);
            recent.Controls.Add(recentLayout);
            page.Controls.Add(recent, 0, 3);
            return page;
        }

        private Control BuildProfilesPage()
        {
            TableLayoutPanel page = NewPage();
            profilePage = page;
            page.AutoScroll = false;
            page.Padding = new Padding(22);
            page.RowCount = 3;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            page.AutoScrollMinSize = Size.Empty;

            APanel toolbar = NewCard();
            toolbar.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel toolLayout = new TableLayoutPanel();
            toolLayout.Dock = DockStyle.Fill;
            toolLayout.Padding = new Padding(14, 10, 14, 10);
            toolLayout.ColumnCount = 1;
            toolLayout.RowCount = 2;
            toolLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            toolLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            cmbProfiles = new ComboBox();
            cmbProfiles.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbProfiles.FlatStyle = FlatStyle.Flat;
            cmbProfiles.Font = new Font("Microsoft YaHei UI", 9F);
            cmbProfiles.DrawMode = DrawMode.OwnerDrawFixed;
            cmbProfiles.ItemHeight = 28;
            cmbProfiles.DrawItem += ProfileComboDrawItem;
            cmbProfiles.Margin = new Padding(2, 5, 24, 5);
            cmbProfiles.Dock = DockStyle.Fill;
            cmbProfiles.SelectedIndexChanged += ProfileSelectedIndexChanged;
            toolLayout.Controls.Add(cmbProfiles, 0, 0);
            TableLayoutPanel profileActions = new TableLayoutPanel();
            profileActions.Dock = DockStyle.Fill;
            profileActions.Width = 410;
            profileActions.Dock = DockStyle.Left;
            profileActions.ColumnCount = 4;
            profileActions.RowCount = 1;
            for (int i = 0; i < 4; i++) profileActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            profileActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            profileActions.Tag = "surface";
            AButton newButton = MakeButton("新建", 70, AntdUI.TTypeMini.Default, NewProfileClicked);
            AButton cloneButton = MakeButton("复制", 70, AntdUI.TTypeMini.Default, CloneProfileClicked);
            AButton deleteButton = MakeButton("删除", 70, AntdUI.TTypeMini.Error, DeleteProfileClicked);
            AButton saveButton = MakeButton("保存配置", 94, AntdUI.TTypeMini.Primary, SaveProfileClicked);
            newButton.Dock = cloneButton.Dock = deleteButton.Dock = saveButton.Dock = DockStyle.Fill;
            profileActions.Controls.Add(newButton, 0, 0);
            profileActions.Controls.Add(cloneButton, 1, 0);
            profileActions.Controls.Add(deleteButton, 2, 0);
            profileActions.Controls.Add(saveButton, 3, 0);
            toolLayout.Controls.Add(profileActions, 0, 1);
            toolbar.Controls.Add(toolLayout);
            page.Controls.Add(toolbar, 0, 0);

            System.Windows.Forms.Panel scroll = new System.Windows.Forms.Panel();
            profileScroll = scroll;
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.Tag = "background";
            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Top;
            columns.AutoSize = false;
            columns.Height = 470;
            columns.ColumnCount = 2;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            columns.RowStyles.Add(new RowStyle(SizeType.Absolute, 470F));
            profileColumns = columns;
            profileFilesCard = BuildFilesCard();
            profileRuntimeCard = BuildRuntimeCard();
            columns.Controls.Add(profileFilesCard, 0, 0);
            columns.Controls.Add(profileRuntimeCard, 1, 0);
            scroll.Controls.Add(columns);
            page.Controls.Add(scroll, 0, 1);
            page.SizeChanged += delegate { UpdateProfileResponsiveLayout(); };

            APanel commandCard = NewCard();
            profileCommandCard = commandCard;
            commandCard.Margin = new Padding(0, 14, 0, 0);
            commandCard.Padding = new Padding(14, 10, 14, 10);
            TableLayoutPanel commandLayout = new TableLayoutPanel();
            commandLayout.Dock = DockStyle.Fill;
            commandLayout.RowCount = 2;
            commandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));
            commandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label commandTitle = MakeLabel("实际启动命令 · 只读", 9.5F, FontStyle.Bold);
            commandTitle.Dock = DockStyle.Fill;
            commandLayout.Controls.Add(commandTitle, 0, 0);
            txtCommand = MakeInput("生成的 llama-server 命令");
            txtCommand.ReadOnly = true;
            txtCommand.Font = new Font("Consolas", 8.5F);
            txtCommand.Dock = DockStyle.Fill;
            commandLayout.Controls.Add(txtCommand, 0, 1);
            commandCard.Controls.Add(commandLayout);
            page.Controls.Add(commandCard, 0, 2);
            return page;
        }

        private void UpdateProfileResponsiveLayout()
        {
            if (profilePage == null || profileColumns == null || profileFilesCard == null || profileRuntimeCard == null) return;
            float scale = Math.Max(1F, sidebar == null ? Font.Height / 15F : sidebar.Width / 224F);
            int availableWidth = Math.Max(0, ClientSize.Width - (sidebar == null ? 0 : sidebar.Width) - Convert.ToInt32(70F * scale));
            bool stack = availableWidth < Convert.ToInt32(950F * scale);
            int cardHeight = Convert.ToInt32(470F * scale);

            profileColumns.SuspendLayout();
            profilePage.SuspendLayout();
            try
            {
                profileColumns.ColumnStyles.Clear();
                profileColumns.RowStyles.Clear();
                if (stack)
                {
                    profileColumns.ColumnCount = 1;
                    profileColumns.RowCount = 2;
                    profileColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    profileColumns.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
                    profileColumns.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
                    profileColumns.SetCellPosition(profileFilesCard, new TableLayoutPanelCellPosition(0, 0));
                    profileColumns.SetCellPosition(profileRuntimeCard, new TableLayoutPanelCellPosition(0, 1));
                    profileFilesCard.Margin = new Padding(0, 0, 0, Convert.ToInt32(8F * scale));
                    profileRuntimeCard.Margin = new Padding(0, Convert.ToInt32(8F * scale), 0, 0);
                    profileColumns.Height = cardHeight * 2 + Convert.ToInt32(16F * scale);
                }
                else
                {
                    profileColumns.ColumnCount = 2;
                    profileColumns.RowCount = 1;
                    profileColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    profileColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    profileColumns.RowStyles.Add(new RowStyle(SizeType.Absolute, cardHeight));
                    profileColumns.SetCellPosition(profileFilesCard, new TableLayoutPanelCellPosition(0, 0));
                    profileColumns.SetCellPosition(profileRuntimeCard, new TableLayoutPanelCellPosition(1, 0));
                    profileFilesCard.Margin = new Padding(0, 0, Convert.ToInt32(8F * scale), 0);
                    profileRuntimeCard.Margin = new Padding(Convert.ToInt32(8F * scale), 0, 0, 0);
                    profileColumns.Height = cardHeight;
                }
                profileScroll.AutoScrollMinSize = new Size(Convert.ToInt32(720F * scale), profileColumns.Height + Convert.ToInt32(20F * scale));
                profileCardsStacked = stack;
            }
            finally
            {
                profilePage.ResumeLayout(true);
                profileColumns.ResumeLayout(true);
            }
        }

        private APanel BuildFilesCard()
        {
            APanel card = NewCard();
            card.Margin = new Padding(0, 0, 8, 0);
            card.Padding = new Padding(18, 14, 18, 16);
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.AutoSize = true;
            table.ColumnCount = 3;
            table.RowCount = 8;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            for (int i = 1; i < 8; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

            Label title = MakeLabel("模型与程序", 11F, FontStyle.Bold);
            title.Dock = DockStyle.Fill;
            table.Controls.Add(title, 0, 0);
            table.SetColumnSpan(title, 3);

            txtProfileName = AddInputRow(table, 1, "配置名称", "例如：Qwen 35B · 主服务", null);
            txtServerExe = AddInputRow(table, 2, "llama-server", "选择 llama-server.exe", delegate { BrowseFile(txtServerExe, "llama-server.exe|llama-server.exe|可执行文件|*.exe|所有文件|*.*"); });
            txtModel = AddInputRow(table, 3, "主模型", "选择 GGUF 模型", delegate { BrowseFile(txtModel, "GGUF 模型|*.gguf|所有文件|*.*"); });
            txtMmproj = AddInputRow(table, 4, "视觉模型", "可选：mmproj-*.gguf", delegate { BrowseFile(txtMmproj, "GGUF 视觉模型|*.gguf|所有文件|*.*"); });
            txtAlias = AddInputRow(table, 5, "模型别名", "API 请求中的 model 名称", null);
            txtApiKeyFile = AddInputRow(table, 6, "API Key", "可选：每行一个 Key 的文本文件", delegate { BrowseFile(txtApiKeyFile, "文本文件|*.txt|所有文件|*.*"); });

            Label note = MakeMutedLabel("管理器只保存文件路径，不复制或删除模型。API Key 为空时将不启用鉴权。", 8.5F);
            note.Dock = DockStyle.Fill;
            note.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(note, 0, 7);
            table.SetColumnSpan(note, 3);
            card.Controls.Add(table);
            return card;
        }

        private APanel BuildRuntimeCard()
        {
            APanel card = NewCard();
            card.Margin = new Padding(8, 0, 0, 0);
            card.Padding = new Padding(18, 14, 18, 16);
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.AutoSize = true;
            table.ColumnCount = 4;
            table.RowCount = 8;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            for (int i = 1; i < 8; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            table.RowStyles[6].Height = 82F;

            Label title = MakeLabel("运行参数", 11F, FontStyle.Bold);
            title.Dock = DockStyle.Fill;
            table.Controls.Add(title, 0, 0);
            table.SetColumnSpan(title, 4);

            txtHost = MakeInput("127.0.0.1 或 0.0.0.0");
            txtAdvertisedHost = MakeInput("客户端访问的 IP/主机名");
            AddPair(table, 1, "监听地址", txtHost, "公开地址", txtAdvertisedHost);

            numPort = MakeNumber(1, 65535, 8080);
            numContext = MakeNumber(0, 1048576, 8192);
            AddPair(table, 2, "端口", numPort, "上下文", numContext);

            numParallel = MakeNumber(1, 128, 1);
            txtGpuLayers = MakeInput("auto、-1 或层数");
            AddPair(table, 3, "并发数", numParallel, "GPU 层", txtGpuLayers);

            cmbCacheK = MakeSelect(160);
            AddCacheOptions(cmbCacheK);
            cmbCacheV = MakeSelect(160);
            AddCacheOptions(cmbCacheV);
            AddPair(table, 4, "KV Cache K", cmbCacheK, "KV Cache V", cmbCacheV);

            numFitTarget = MakeNumber(0, 1048576, 1024);
            numImageTokens = MakeNumber(0, 1048576, 0);
            AddPair(table, 5, "Fit 余量 MB", numFitTarget, "图片 tokens", numImageTokens);

            FlowLayoutPanel switches = new FlowLayoutPanel();
            switches.Dock = DockStyle.Fill;
            switches.WrapContents = true;
            switches.Tag = "surface";
            swFit = MakeSwitch(string.Empty);
            swFlash = MakeSwitch(string.Empty);
            swJinja = MakeSwitch(string.Empty);
            swNoWebUi = MakeSwitch(string.Empty);
            swNoMmap = MakeSwitch(string.Empty);
            swMlock = MakeSwitch(string.Empty);
            switches.Controls.Add(MakeSwitchItem("自动 Fit", swFit));
            switches.Controls.Add(MakeSwitchItem("Flash Attention", swFlash));
            switches.Controls.Add(MakeSwitchItem("Jinja 工具调用", swJinja));
            switches.Controls.Add(MakeSwitchItem("禁用 WebUI", swNoWebUi));
            switches.Controls.Add(MakeSwitchItem("No mmap", swNoMmap));
            switches.Controls.Add(MakeSwitchItem("Mlock", swMlock));
            configurationControls.AddRange(new Control[] { swFit, swFlash, swJinja, swNoWebUi, swNoMmap, swMlock });
            table.Controls.Add(switches, 0, 6);
            table.SetColumnSpan(switches, 4);

            cmbReasoning = MakeSelect(150);
            cmbReasoning.Items.AddRange(new object[] { "默认", "none", "auto", "deepseek", "deepseek-legacy" });
            txtExtraArgs = MakeInput("其余 llama-server 参数（高级）");
            AddPair(table, 7, "推理解析", cmbReasoning, "自定义参数", txtExtraArgs);

            card.Controls.Add(table);
            return card;
        }

        private Control BuildLogsPage()
        {
            TableLayoutPanel page = NewPage();
            page.Padding = new Padding(22);
            page.RowCount = 2;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            page.AutoScrollMinSize = new Size(640, 560);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Fill;
            tools.FlowDirection = FlowDirection.RightToLeft;
            tools.WrapContents = false;
            tools.Tag = "surface";
            tools.Controls.Add(MakeButton("打开日志目录", 120, AntdUI.TTypeMini.Default, OpenLogDirectoryClicked));
            tools.Controls.Add(MakeButton("清空显示", 94, AntdUI.TTypeMini.Default, delegate { txtLogs.Clear(); txtDashboardLog.Clear(); }));
            Label title = MakeLabel("运行日志", 15F, FontStyle.Bold);
            title.Dock = DockStyle.Left;
            title.Width = 180;
            tools.Controls.Add(title);
            page.Controls.Add(tools, 0, 0);

            APanel card = NewCard();
            card.Padding = new Padding(14);
            txtLogs = MakeLogBox();
            card.Controls.Add(txtLogs);
            page.Controls.Add(card, 0, 1);
            return page;
        }

        private Control BuildSettingsPage()
        {
            TableLayoutPanel page = NewPage();
            page.Padding = new Padding(22);
            page.RowCount = 3;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 270F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
            page.AutoScrollMinSize = new Size(640, 704);

            APanel appearance = NewCard();
            appearance.Margin = new Padding(0, 0, 0, 14);
            appearance.Padding = new Padding(20);
            TableLayoutPanel app = new TableLayoutPanel();
            app.Dock = DockStyle.Fill;
            app.ColumnCount = 2;
            app.RowCount = 3;
            app.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            app.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            app.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            app.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            app.RowStyles.Add(new RowStyle(SizeType.Absolute, 94F));
            Label appearanceTitle = MakeLabel("外观", 14F, FontStyle.Bold);
            appearanceTitle.Dock = DockStyle.Fill;
            appearanceTitle.TextAlign = ContentAlignment.MiddleLeft;
            app.Controls.Add(appearanceTitle, 0, 0);
            app.SetColumnSpan(appearanceTitle, 2);
            ASelect settingsTheme = MakeSelect(240);
            settingsTheme.Items.AddRange(new object[] { "跟随系统", "浅色", "深色" });
            settingsTheme.SelectedIndex = ThemeIndex(config.ThemeMode);
            settingsTheme.SelectedIndexChanged += delegate { if (settingsTheme.SelectedIndex >= 0) { cmbTheme.SelectedIndex = settingsTheme.SelectedIndex; QuickThemeChanged(); } };
            AddSettingRow(app, 1, "主题模式", settingsTheme);
            FlowLayoutPanel accentButtons = new FlowLayoutPanel();
            accentButtons.Dock = DockStyle.Fill;
            accentButtons.Tag = "surface";
            string[] accents = ThemeService.AccentNames;
            for (int i = 0; i < accents.Length; i++)
            {
                string captured = accents[i];
                AButton button = MakeButton(AccentDisplayName(captured), 84, AntdUI.TTypeMini.Default, delegate { config.AccentName = captured; ApplyTheme(); ConfigStore.Save(config); });
                accentButtons.Controls.Add(button);
            }
            AddSettingRow(app, 2, "强调色", accentButtons);
            appearance.Controls.Add(app);
            page.Controls.Add(appearance, 0, 0);

            APanel storage = NewCard();
            storage.Margin = new Padding(0, 0, 0, 14);
            storage.Padding = new Padding(20);
            TableLayoutPanel storageLayout = new TableLayoutPanel();
            storageLayout.Dock = DockStyle.Fill;
            storageLayout.ColumnCount = 2;
            storageLayout.RowCount = 3;
            storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            Label storageTitle = MakeLabel("应用与数据", 14F, FontStyle.Bold);
            storageTitle.Dock = DockStyle.Fill;
            storageTitle.TextAlign = ContentAlignment.MiddleLeft;
            storageLayout.Controls.Add(storageTitle, 0, 0);
            storageLayout.SetColumnSpan(storageTitle, 2);
            AddSettingText(storageLayout, 1, "运行模式", ConfigStore.IsPortable ? "便携版 · 配置保存在程序旁的 data 目录" : "安装版 · 配置保存在当前 Windows 用户目录");
            AddSettingText(storageLayout, 2, "配置目录", ConfigStore.DataDirectory);
            storage.Controls.Add(storageLayout);
            page.Controls.Add(storage, 0, 1);

            APanel about = NewCard();
            about.Padding = new Padding(20);
            TableLayoutPanel aboutLayout = new TableLayoutPanel();
            aboutLayout.Dock = DockStyle.Fill;
            aboutLayout.ColumnCount = 1;
            aboutLayout.RowCount = 2;
            aboutLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            aboutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label aboutTitle = MakeLabel("Llama Server Manager  " + AppVersion.DisplayVersion, 14F, FontStyle.Bold);
            aboutTitle.Dock = DockStyle.Fill;
            aboutTitle.TextAlign = ContentAlignment.MiddleLeft;
            Label aboutBody = MakeMutedLabel("通用 Windows llama.cpp 服务管理器。软件不捆绑 llama.cpp、模型或 CUDA；请在模型配置中选择您自己的 llama-server.exe 与 GGUF 文件。\n界面基于 AntdUI（Apache-2.0）；安装包由 Inno Setup 构建。", 9.5F);
            aboutBody.Dock = DockStyle.Fill;
            aboutBody.AutoSize = false;
            aboutBody.TextAlign = ContentAlignment.TopLeft;
            aboutLayout.Controls.Add(aboutTitle, 0, 0);
            aboutLayout.Controls.Add(aboutBody, 0, 1);
            about.Controls.Add(aboutLayout);
            page.Controls.Add(about, 0, 2);
            return page;
        }

        private TableLayoutPanel NewPage()
        {
            TableLayoutPanel page = new TableLayoutPanel();
            page.Dock = DockStyle.Fill;
            page.AutoScroll = true;
            page.Tag = "surface";
            page.ColumnCount = 1;
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return page;
        }

        private APanel NewCard()
        {
            APanel card = new APanel();
            card.Dock = DockStyle.Fill;
            card.Radius = 12;
            card.BorderWidth = 1F;
            card.Shadow = 1;
            card.Tag = "surface";
            return card;
        }

        private void AddPage(string key, Control page)
        {
            page.Visible = false;
            pages[key] = page;
            pageHost.Controls.Add(page);
        }

        private void Navigate(string key)
        {
            if (!pages.ContainsKey(key)) return;
            currentPage = key;
            foreach (KeyValuePair<string, Control> item in pages)
            {
                item.Value.Visible = item.Key == key;
                if (item.Key == key) item.Value.BringToFront();
            }
            foreach (KeyValuePair<string, AButton> item in navButtons)
                item.Value.Type = item.Key == key ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;

            if (key == "profiles")
            {
                lblHeaderTitle.Text = "模型配置";
                lblHeaderSubtitle.Text = "配置任意 Windows llama.cpp 后端、模型和启动参数";
            }
            else if (key == "logs")
            {
                lblHeaderTitle.Text = "运行日志";
                lblHeaderSubtitle.Text = "查看 llama-server 输出、性能与错误信息";
            }
            else if (key == "settings")
            {
                lblHeaderTitle.Text = "外观与设置";
                lblHeaderSubtitle.Text = "切换主题、查看数据位置和开源组件";
            }
            else
            {
                lblHeaderTitle.Text = "服务总览";
                lblHeaderSubtitle.Text = "管理 llama.cpp 后端、模型和 API";
            }
        }

        private AButton MakeNavButton(string key, string textValue, string glyph)
        {
            AButton button = MakeButton(glyph + "   " + textValue, 184, AntdUI.TTypeMini.Default, delegate { Navigate(key); });
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 5, 0, 5);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(14, 0, 0, 0);
            navButtons[key] = button;
            return button;
        }

        private Label AddMetricCard(TableLayoutPanel parent, int column, string title, string value, string hint)
        {
            APanel card = NewCard();
            card.Margin = new Padding(column == 0 ? 0 : 7, 0, column == 3 ? 0 : 7, 0);
            System.Windows.Forms.Panel content = new System.Windows.Forms.Panel();
            content.Dock = DockStyle.Fill;
            content.Tag = "surface";
            Label caption = MakeMutedLabel(title.ToUpperInvariant(), 8.5F);
            caption.Location = new Point(18, 14);
            caption.AutoSize = true;
            Label metric = MakeLabel(value, 16F, FontStyle.Bold);
            metric.Location = new Point(17, 40);
            metric.AutoSize = true;
            Label description = MakeMutedLabel(hint, 8.5F);
            description.Location = new Point(18, 72);
            description.AutoSize = true;
            content.Controls.Add(caption);
            content.Controls.Add(metric);
            content.Controls.Add(description);
            card.Controls.Add(content);
            parent.Controls.Add(card, column, 0);
            return metric;
        }

        private static Label MakeLabel(string value, float size, FontStyle style)
        {
            Label label = new Label();
            label.Text = value;
            label.Font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
            label.AutoEllipsis = true;
            return label;
        }

        private static Label MakeMutedLabel(string value, float size)
        {
            Label label = MakeLabel(value, size, FontStyle.Regular);
            label.Tag = "muted";
            return label;
        }

        private static AButton MakeButton(string value, int width, AntdUI.TTypeMini type, EventHandler handler)
        {
            AButton button = new AButton();
            button.Text = value;
            button.Width = width;
            button.Height = 34;
            button.Radius = 7;
            button.Type = type;
            button.BorderWidth = 1F;
            button.AutoEllipsis = true;
            button.Margin = new Padding(4, 0, 4, 0);
            if (handler != null) button.Click += handler;
            return button;
        }

        private static AInput MakeInput(string placeholder)
        {
            AInput input = new AInput();
            input.PlaceholderText = placeholder;
            input.Radius = 6;
            input.Margin = new Padding(2, 5, 2, 5);
            input.Dock = DockStyle.Fill;
            return input;
        }

        private static ASelect MakeSelect(int width)
        {
            ASelect select = new ASelect();
            select.Width = width;
            select.Height = 34;
            select.Radius = 6;
            select.Margin = new Padding(4, 0, 4, 0);
            return select;
        }

        private static AInputNumber MakeNumber(decimal min, decimal max, decimal value)
        {
            AInputNumber number = new AInputNumber();
            number.Minimum = min;
            number.Maximum = max;
            number.Value = value;
            number.Radius = 6;
            number.Dock = DockStyle.Fill;
            number.Margin = new Padding(2, 5, 2, 5);
            return number;
        }

        private static ASwitch MakeSwitch(string textValue)
        {
            ASwitch value = new ASwitch();
            value.Text = textValue;
            value.AutoSize = false;
            value.Width = 132;
            value.Height = 30;
            value.Margin = new Padding(3, 7, 13, 3);
            return value;
        }

        private static Control MakeSwitchItem(string labelText, ASwitch toggle)
        {
            System.Windows.Forms.Panel item = new System.Windows.Forms.Panel();
            item.Width = 135;
            item.Height = 32;
            item.Margin = new Padding(0, 2, 4, 2);
            item.Tag = "surface";
            Label label = MakeMutedLabel(labelText, 8.25F);
            label.Location = new Point(0, 7);
            label.Width = 90;
            label.Height = 22;
            label.TextAlign = ContentAlignment.MiddleLeft;
            toggle.Location = new Point(93, 4);
            toggle.Width = 40;
            toggle.Height = 24;
            item.Controls.Add(label);
            item.Controls.Add(toggle);
            return item;
        }

        private static RichTextBox MakeLogBox()
        {
            RichTextBox log = new RichTextBox();
            log.Dock = DockStyle.Fill;
            log.ReadOnly = true;
            log.BorderStyle = BorderStyle.None;
            log.Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
            log.DetectUrls = false;
            log.WordWrap = false;
            return log;
        }

        private AInput AddInputRow(TableLayoutPanel table, int row, string label, string placeholder, EventHandler browse)
        {
            Label caption = MakeMutedLabel(label, 9F);
            caption.Dock = DockStyle.Fill;
            caption.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(caption, 0, row);
            AInput input = MakeInput(placeholder);
            input.TextChanged += AnySettingChanged;
            table.Controls.Add(input, 1, row);
            if (browse != null)
            {
                AButton button = MakeButton("…", 34, AntdUI.TTypeMini.Default, browse);
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(6, 7, 0, 7);
                table.Controls.Add(button, 2, row);
            }
            else table.SetColumnSpan(input, 2);
            configurationControls.Add(input);
            return input;
        }

        private void AddPair(TableLayoutPanel table, int row, string label1, Control control1, string label2, Control control2)
        {
            Label first = MakeMutedLabel(label1, 8.5F);
            first.Dock = DockStyle.Fill;
            first.TextAlign = ContentAlignment.MiddleLeft;
            Label second = MakeMutedLabel(label2, 8.5F);
            second.Dock = DockStyle.Fill;
            second.TextAlign = ContentAlignment.MiddleLeft;
            control1.Dock = DockStyle.Fill;
            control2.Dock = DockStyle.Fill;
            table.Controls.Add(first, 0, row);
            table.Controls.Add(control1, 1, row);
            table.Controls.Add(second, 2, row);
            table.Controls.Add(control2, 3, row);
            WireSettingControl(control1);
            WireSettingControl(control2);
            configurationControls.Add(control1);
            configurationControls.Add(control2);
        }

        private static void AddSettingRow(TableLayoutPanel table, int row, string label, Control control)
        {
            Label caption = MakeMutedLabel(label, 9F);
            caption.Dock = DockStyle.Fill;
            caption.TextAlign = ContentAlignment.MiddleLeft;
            control.Dock = DockStyle.Left;
            table.Controls.Add(caption, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private static void AddSettingText(TableLayoutPanel table, int row, string label, string value)
        {
            Label caption = MakeMutedLabel(label, 9F);
            caption.Dock = DockStyle.Fill;
            caption.TextAlign = ContentAlignment.MiddleLeft;
            Label content = MakeLabel(value, 9F, FontStyle.Regular);
            content.Dock = DockStyle.Fill;
            content.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(caption, 0, row);
            table.Controls.Add(content, 1, row);
        }

        private void WireSettingControl(Control control)
        {
            AInput input = control as AInput;
            if (input != null) input.TextChanged += AnySettingChanged;
            AInputNumber number = control as AInputNumber;
            if (number != null) number.ValueChanged += AnySettingChanged;
            ASelect select = control as ASelect;
            if (select != null) select.SelectedIndexChanged += AnySettingChanged;
            ASwitch toggle = control as ASwitch;
            if (toggle != null) toggle.CheckedChanged += AnySettingChanged;
        }

        private static void AddCacheOptions(ASelect select)
        {
            select.Items.AddRange(new object[] { "f32", "f16", "bf16", "q8_0", "q4_0", "q4_1", "iq4_nl" });
        }

        private void BindProfiles()
        {
            bindingProfiles = true;
            cmbProfiles.Items.Clear();
            int selected = 0;
            for (int i = 0; i < config.Profiles.Count; i++)
            {
                cmbProfiles.Items.Add(config.Profiles[i].Name);
                if (config.Profiles[i].Id == config.SelectedProfileId) selected = i;
            }
            if (cmbProfiles.Items.Count > 0) cmbProfiles.SelectedIndex = selected;
            if (config.Profiles.Count > 0) cmbProfiles.Text = config.Profiles[selected].Name;
            bindingProfiles = false;
            if (config.Profiles.Count > 0) currentProfile = config.Profiles[selected];
            if (currentProfile != null) LoadProfileToControls(currentProfile);
        }

        private void ProfileSelectedIndexChanged(object sender, EventArgs e)
        {
            if (bindingProfiles) return;
            if (processManager.IsRunning)
            {
                MessageBox.Show(this, "服务器运行时不能切换配置，请先停止服务。", "配置正在使用", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SelectCurrentProfileInCombo();
                return;
            }
            if (cmbProfiles.SelectedIndex < 0 || cmbProfiles.SelectedIndex >= config.Profiles.Count) return;
            ModelProfile selected = config.Profiles[cmbProfiles.SelectedIndex];
            currentProfile = selected;
            config.SelectedProfileId = selected.Id;
            ConfigStore.Save(config);
            LoadProfileToControls(selected);
        }

        private void ProfileComboDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= cmbProfiles.Items.Count) return;
            ThemePalette colors = palette ?? ThemePalette.Create(false, ThemeService.GetAccent(config.AccentName));
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selected ? colors.Accent : colors.SurfaceAlt;
            Color foreground = selected ? Color.White : colors.Text;
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            Rectangle textBounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(cmbProfiles.Items[e.Index]), cmbProfiles.Font, textBounds, foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
        }

        private void LoadProfileToControls(ModelProfile profile)
        {
            loadingControls = true;
            try
            {
                txtProfileName.Text = profile.Name;
                txtServerExe.Text = profile.ServerExecutable;
                txtModel.Text = profile.ModelPath;
                txtMmproj.Text = profile.MmprojPath;
                txtAlias.Text = profile.Alias;
                txtApiKeyFile.Text = profile.ApiKeyFile;
                txtHost.Text = profile.Host;
                txtAdvertisedHost.Text = profile.AdvertisedHost;
                SetNumber(numPort, profile.Port);
                SetNumber(numContext, profile.ContextSize);
                SetNumber(numParallel, profile.Parallel);
                txtGpuLayers.Text = profile.GpuLayers;
                swFit.Checked = profile.FitEnabled;
                SetNumber(numFitTarget, profile.FitTarget);
                swFlash.Checked = profile.FlashAttention;
                SelectValue(cmbCacheK, profile.CacheTypeK, "f16");
                SelectValue(cmbCacheV, profile.CacheTypeV, "f16");
                SetNumber(numImageTokens, profile.ImageMinTokens);
                swJinja.Checked = profile.Jinja;
                swNoWebUi.Checked = profile.DisableWebUi;
                swNoMmap.Checked = profile.NoMmap;
                swMlock.Checked = profile.Mlock;
                SelectValue(cmbReasoning, string.IsNullOrWhiteSpace(profile.Reasoning) ? "默认" : profile.Reasoning, "默认");
                txtExtraArgs.Text = profile.ExtraArguments;
            }
            finally { loadingControls = false; }
            UpdateDashboardSummary();
            UpdateCommandPreview();
        }

        private void UpdateProfileFromControls()
        {
            if (currentProfile == null || loadingControls) return;
            currentProfile.Name = string.IsNullOrWhiteSpace(txtProfileName.Text) ? "未命名模型" : txtProfileName.Text.Trim();
            currentProfile.ServerExecutable = txtServerExe.Text.Trim();
            currentProfile.ModelPath = txtModel.Text.Trim();
            currentProfile.MmprojPath = txtMmproj.Text.Trim();
            currentProfile.Alias = string.IsNullOrWhiteSpace(txtAlias.Text) ? "local-model" : txtAlias.Text.Trim();
            currentProfile.ApiKeyFile = txtApiKeyFile.Text.Trim();
            currentProfile.Host = string.IsNullOrWhiteSpace(txtHost.Text) ? "127.0.0.1" : txtHost.Text.Trim();
            currentProfile.AdvertisedHost = string.IsNullOrWhiteSpace(txtAdvertisedHost.Text) ? "127.0.0.1" : txtAdvertisedHost.Text.Trim();
            currentProfile.Port = Decimal.ToInt32(numPort.Value);
            currentProfile.ContextSize = Decimal.ToInt32(numContext.Value);
            currentProfile.Parallel = Decimal.ToInt32(numParallel.Value);
            currentProfile.GpuLayers = string.IsNullOrWhiteSpace(txtGpuLayers.Text) ? "auto" : txtGpuLayers.Text.Trim();
            currentProfile.FitEnabled = swFit.Checked;
            currentProfile.FitTarget = Decimal.ToInt32(numFitTarget.Value);
            currentProfile.FlashAttention = swFlash.Checked;
            currentProfile.CacheTypeK = SelectText(cmbCacheK, "f16");
            currentProfile.CacheTypeV = SelectText(cmbCacheV, "f16");
            currentProfile.ImageMinTokens = Decimal.ToInt32(numImageTokens.Value);
            currentProfile.Jinja = swJinja.Checked;
            currentProfile.DisableWebUi = swNoWebUi.Checked;
            currentProfile.NoMmap = swNoMmap.Checked;
            currentProfile.Mlock = swMlock.Checked;
            string reasoning = SelectText(cmbReasoning, "默认");
            currentProfile.Reasoning = reasoning == "默认" ? string.Empty : reasoning;
            currentProfile.ExtraArguments = txtExtraArgs.Text.Trim();
        }

        private void SaveProfileClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            UpdateProfileFromControls();
            config.SelectedProfileId = currentProfile.Id;
            config.FirstRunCompleted = !string.IsNullOrWhiteSpace(currentProfile.ServerExecutable) && !string.IsNullOrWhiteSpace(currentProfile.ModelPath);
            ConfigStore.Save(config);
            RefreshProfileComboText();
            AppendLog("配置已保存：" + currentProfile.Name, false);
            UpdateDashboardSummary();
        }

        private void NewProfileClicked(object sender, EventArgs e)
        {
            if (processManager.IsRunning) return;
            string name = PromptDialogV2.Show(this, "新配置名称", "新建模型配置", "我的 llama.cpp 服务");
            if (string.IsNullOrWhiteSpace(name)) return;
            ModelProfile profile = ModelProfile.CreateGenericProfile();
            profile.Name = name.Trim();
            config.Profiles.Add(profile);
            config.SelectedProfileId = profile.Id;
            ConfigStore.Save(config);
            BindProfiles();
        }

        private void CloneProfileClicked(object sender, EventArgs e)
        {
            if (processManager.IsRunning || currentProfile == null) return;
            UpdateProfileFromControls();
            string name = PromptDialogV2.Show(this, "新配置名称", "复制模型配置", currentProfile.Name + " - 副本");
            if (string.IsNullOrWhiteSpace(name)) return;
            ModelProfile copy = currentProfile.CloneAs(name.Trim());
            config.Profiles.Add(copy);
            config.SelectedProfileId = copy.Id;
            ConfigStore.Save(config);
            BindProfiles();
        }

        private void DeleteProfileClicked(object sender, EventArgs e)
        {
            if (processManager.IsRunning || currentProfile == null) return;
            if (config.Profiles.Count <= 1)
            {
                MessageBox.Show(this, "至少保留一个模型配置。", "不能删除", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "确定删除配置“" + currentProfile.Name + "”吗？\n不会删除 llama.cpp 或模型文件。", "删除配置", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            config.Profiles.Remove(currentProfile);
            config.SelectedProfileId = config.Profiles[0].Id;
            ConfigStore.Save(config);
            BindProfiles();
        }

        private void StartClicked(object sender, EventArgs e)
        {
            if (processManager.IsRunning || currentProfile == null) return;
            if (externalServiceDetected)
            {
                MessageBox.Show(this, "当前端口已经存在其他服务。请先关闭原 BAT/llama-server，或改用其他端口。", "端口已占用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            UpdateProfileFromControls();
            List<string> errors = CommandBuilder.ValidateForStart(currentProfile);
            if (errors.Count > 0)
            {
                MessageBox.Show(this, string.Join("\n", errors.ToArray()), "无法启动", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Navigate("profiles");
                return;
            }
            if (IsLanBinding(currentProfile.Host) && string.IsNullOrWhiteSpace(currentProfile.ApiKeyFile))
            {
                DialogResult answer = MessageBox.Show(this,
                    "当前监听地址会向局域网开放 API，但没有配置 API Key。\n\n同一网络中的设备可能无需密码即可调用模型。是否仍然启动？",
                    "未启用 API 鉴权", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }
            config.SelectedProfileId = currentProfile.Id;
            config.FirstRunCompleted = true;
            ConfigStore.Save(config);
            try
            {
                processManager.Start(currentProfile);
                LockConfiguration(true);
            }
            catch (Exception ex)
            {
                AppendLog("启动失败：" + ex.Message, true);
                MessageBox.Show(this, ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopClicked(object sender, EventArgs e)
        {
            processManager.Stop();
            LockConfiguration(false);
        }

        private async void RestartClicked(object sender, EventArgs e)
        {
            if (!processManager.IsRunning) { StartClicked(sender, e); return; }
            processManager.Stop();
            await Task.Delay(700);
            externalServiceDetected = false;
            StartClicked(sender, e);
        }

        private async void DetectBackendClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            UpdateProfileFromControls();
            AppendLog("正在检测 llama.cpp 后端……", false);
            string result = await processManager.ProbeBackendAsync(currentProfile.ServerExecutable);
            bool error = result.IndexOf("找不到", StringComparison.OrdinalIgnoreCase) >= 0 || result.IndexOf("超时", StringComparison.OrdinalIgnoreCase) >= 0;
            AppendLog(result, error);
            MessageBox.Show(this, result, "后端检测结果", MessageBoxButtons.OK, error ? MessageBoxIcon.Error : MessageBoxIcon.Information);
        }

        private async void TestApiClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            UpdateProfileFromControls();
            btnTest.Loading = true;
            try
            {
                AppendLog("开始测试 /v1/responses……", false);
                ApiCheckResult responses = await LlamaApiClient.TestResponsesAsync(currentProfile);
                AppendLog("Responses：" + responses.Summary, !responses.Success);
                if (!string.IsNullOrWhiteSpace(responses.Body)) AppendLog(TrimForLog(responses.Body, 1600), !responses.Success);

                AppendLog("开始测试 /v1/chat/completions……", false);
                ApiCheckResult chat = await LlamaApiClient.TestChatCompletionsAsync(currentProfile);
                AppendLog("Chat Completions：" + chat.Summary, !chat.Success);
                if (!string.IsNullOrWhiteSpace(chat.Body)) AppendLog(TrimForLog(chat.Body, 1600), !chat.Success);

                bool success = responses.Success && chat.Success;
                MessageBox.Show(this, "Responses：" + responses.Summary + "\nChat Completions：" + chat.Summary,
                    "双协议 API 测试", MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            finally { btnTest.Loading = false; }
        }

        private void CopyEndpointClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            UpdateProfileFromControls();
            string endpoint = LlamaApiClient.LanBaseUrl(currentProfile) + "/v1";
            Clipboard.SetText(endpoint);
            AppendLog("已复制 API Base URL：" + endpoint, false);
        }

        private void OpenWebUiClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            UpdateProfileFromControls();
            if (currentProfile.DisableWebUi)
            {
                MessageBox.Show(this, "当前配置启用了 --no-webui。请取消“禁用 WebUI”并重启服务。", "WebUI 已禁用", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start(LlamaApiClient.LocalBaseUrl(currentProfile)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法打开浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void OpenLogDirectoryClicked(object sender, EventArgs e)
        {
            Directory.CreateDirectory(ConfigStore.LogDirectory);
            try { Process.Start("explorer.exe", ConfigStore.LogDirectory); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法打开目录", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private async Task RefreshHealthAsync()
        {
            if (healthCheckBusy || currentProfile == null) return;
            healthCheckBusy = true;
            try
            {
                ApiCheckResult health = await LlamaApiClient.CheckHealthAsync(currentProfile);
                externalServiceDetected = health.Success && !processManager.IsRunning;
                if (health.Success)
                {
                    lblApiMetric.Text = "就绪";
                    lblApiMetric.ForeColor = palette.Success;
                    lblHeroStatus.Text = processManager.IsRunning ? "服务已就绪" : "检测到外部服务";
                    lblHeroStatus.ForeColor = processManager.IsRunning ? palette.Success : palette.Warning;
                }
                else if (health.StatusCode == 503)
                {
                    lblApiMetric.Text = "模型加载中";
                    lblApiMetric.ForeColor = palette.Warning;
                    lblHeroStatus.Text = "正在加载模型";
                    lblHeroStatus.ForeColor = palette.Warning;
                }
                else
                {
                    lblApiMetric.Text = "离线";
                    lblApiMetric.ForeColor = palette.Muted;
                    if (!processManager.IsRunning)
                    {
                        lblHeroStatus.Text = "服务未运行";
                        lblHeroStatus.ForeColor = palette.Text;
                    }
                }
                UpdateActionButtons();
            }
            catch { }
            finally { healthCheckBusy = false; }
        }

        private void ProcessManagerLogReceived(string message, bool error)
        {
            if (IsDisposed) return;
            try { BeginInvoke((MethodInvoker)delegate { AppendLog(message, error); }); }
            catch { }
        }

        private void ProcessManagerRunningChanged(bool running, int pid)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    lblProcessMetric.Text = running ? "PID " + pid : "未运行";
                    lblProcessMetric.ForeColor = running ? palette.Success : palette.Muted;
                    if (running)
                    {
                        lblHeroStatus.Text = "模型加载中";
                        lblHeroStatus.ForeColor = palette.Warning;
                    }
                    else LockConfiguration(false);
                    UpdateActionButtons();
                });
            }
            catch { }
        }

        private void AppendLog(string message, bool error)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message.TrimEnd() + Environment.NewLine;
            AppendLogToBox(txtLogs, line, error);
            AppendLogToBox(txtDashboardLog, line, error);
            if (txtDashboardLog != null && txtDashboardLog.TextLength > 16000)
                txtDashboardLog.Text = txtDashboardLog.Text.Substring(txtDashboardLog.TextLength - 12000);
            ParsePerformance(message);
            TryWriteLogFile(line);
        }

        private void AppendLogToBox(RichTextBox box, string line, bool error)
        {
            if (box == null) return;
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = error ? palette.Danger : palette.LogText;
            box.AppendText(line);
            box.SelectionColor = palette.LogText;
            box.ScrollToCaret();
        }

        private void ParsePerformance(string message)
        {
            Match prompt = Regex.Match(message, @"prompt eval time\s*=.*?([0-9]+(?:\.[0-9]+)?)\s+tokens per second", RegexOptions.IgnoreCase);
            if (prompt.Success)
            {
                lblPromptMetric.Text = prompt.Groups[1].Value + " tok/s";
                lblPromptMetric.ForeColor = palette.Accent;
            }
            Match generation = Regex.Match(message, @"(?<!prompt )eval time\s*=.*?([0-9]+(?:\.[0-9]+)?)\s+tokens per second", RegexOptions.IgnoreCase);
            if (generation.Success)
            {
                lblGenerationMetric.Text = generation.Groups[1].Value + " tok/s";
                lblGenerationMetric.ForeColor = palette.Accent;
            }
            Match live = Regex.Match(message, @"\btg(?:_3s)?\s*=\s*([0-9]+(?:\.[0-9]+)?)\s*t/s", RegexOptions.IgnoreCase);
            if (live.Success)
            {
                lblGenerationMetric.Text = live.Groups[1].Value + " tok/s";
                lblGenerationMetric.ForeColor = palette.Accent;
            }
        }

        private void TryWriteLogFile(string line)
        {
            try
            {
                Directory.CreateDirectory(ConfigStore.LogDirectory);
                string name = currentProfile == null ? "manager" : SanitizeFileName(currentProfile.Name);
                string path = Path.Combine(ConfigStore.LogDirectory, DateTime.Now.ToString("yyyyMMdd") + "-" + name + ".log");
                File.AppendAllText(path, line, new UTF8Encoding(false));
            }
            catch { }
        }

        private void AnySettingChanged(object sender, EventArgs e)
        {
            if (loadingControls) return;
            UpdateCommandPreview();
            UpdateDashboardSummary();
        }

        private void UpdateCommandPreview()
        {
            if (txtCommand == null || currentProfile == null || loadingControls) return;
            UpdateProfileFromControls();
            txtCommand.Text = CommandBuilder.BuildDisplayCommand(currentProfile);
        }

        private void UpdateDashboardSummary()
        {
            if (currentProfile == null || lblEndpoint == null) return;
            if (!loadingControls) UpdateProfileFromControls();
            lblProfileSummary.Text = currentProfile.Name + "   ·   " + (string.IsNullOrWhiteSpace(currentProfile.ModelPath) ? "尚未选择模型" : Path.GetFileName(currentProfile.ModelPath));
            lblEndpoint.Text = LlamaApiClient.LanBaseUrl(currentProfile) + "/v1   ·   model: " + currentProfile.Alias;
        }

        private void UpdateActionButtons()
        {
            if (btnStart == null) return;
            bool running = processManager.IsRunning;
            btnStart.Enabled = !running && !externalServiceDetected;
            btnStop.Enabled = running;
            btnRestart.Enabled = running;
        }

        private void LockConfiguration(bool locked)
        {
            if (cmbProfiles != null) cmbProfiles.Enabled = !locked;
            foreach (Control control in configurationControls)
                if (control != null) control.Enabled = !locked;
        }

        private void ApplyTheme()
        {
            loadingControls = true;
            try
            {
                if (cmbTheme != null) cmbTheme.SelectedIndex = ThemeIndex(config.ThemeMode);
                if (cmbAccent != null) cmbAccent.SelectedIndex = AccentIndex(config.AccentName);
            }
            finally { loadingControls = false; }
            palette = ThemeService.Apply(config, this);
            if (txtLogs != null) { txtLogs.BackColor = palette.LogBackground; txtLogs.ForeColor = palette.LogText; }
            if (txtDashboardLog != null) { txtDashboardLog.BackColor = palette.LogBackground; txtDashboardLog.ForeColor = palette.LogText; }
            Navigate(currentPage);
            if (lblProcessMetric != null) lblProcessMetric.ForeColor = processManager.IsRunning ? palette.Success : palette.Muted;
            ConfigStore.Save(config);
        }

        private void QuickThemeChanged()
        {
            if (loadingControls || cmbTheme == null || cmbTheme.SelectedIndex < 0) return;
            config.ThemeMode = cmbTheme.SelectedIndex == 1 ? "Light" : cmbTheme.SelectedIndex == 2 ? "Dark" : "System";
            ApplyTheme();
        }

        private void QuickAccentChanged()
        {
            if (loadingControls || cmbAccent == null || cmbAccent.SelectedIndex < 0) return;
            string[] names = ThemeService.AccentNames;
            if (cmbAccent.SelectedIndex < names.Length) config.AccentName = names[cmbAccent.SelectedIndex];
            ApplyTheme();
        }

        private void SetupTray()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Text = "Llama Server Manager " + AppVersion.DisplayVersion;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += delegate { ShowFromTray(); };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示窗口", null, delegate { ShowFromTray(); });
            menu.Items.Add("启动服务器", null, StartClicked);
            menu.Items.Add("停止服务器", null, StopClicked);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { forceExit = true; Close(); });
            trayIcon.ContextMenuStrip = menu;
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    trayIcon.ShowBalloonTip(1000, "Llama Server Manager", "程序仍在托盘运行。", ToolTipIcon.Info);
                }
            };
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!forceExit && e.CloseReason == CloseReason.UserClosing && processManager.IsRunning)
            {
                DialogResult result = MessageBox.Show(this,
                    "退出管理器会同时停止由它启动的 llama-server。\n\n选择“否”将最小化到托盘并保持服务运行。",
                    "服务器正在运行", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel) { e.Cancel = true; return; }
                if (result == DialogResult.No) { e.Cancel = true; WindowState = FormWindowState.Minimized; return; }
            }
            if (processManager.IsRunning) processManager.Stop();
            if (currentProfile != null) UpdateProfileFromControls();
            ConfigStore.Save(config);
            if (healthTimer != null) healthTimer.Stop();
            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            processManager.Dispose();
        }

        private void BrowseFile(AInput target, string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = filter;
                dialog.CheckFileExists = true;
                dialog.RestoreDirectory = true;
                try { if (File.Exists(target.Text)) dialog.InitialDirectory = Path.GetDirectoryName(target.Text); }
                catch { }
                if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.FileName;
            }
        }

        private void RefreshProfileComboText()
        {
            bindingProfiles = true;
            int selected = cmbProfiles.SelectedIndex;
            cmbProfiles.Items.Clear();
            foreach (ModelProfile profile in config.Profiles) cmbProfiles.Items.Add(profile.Name);
            if (selected >= 0 && selected < cmbProfiles.Items.Count) cmbProfiles.SelectedIndex = selected;
            if (selected >= 0 && selected < config.Profiles.Count) cmbProfiles.Text = config.Profiles[selected].Name;
            bindingProfiles = false;
        }

        private void SelectCurrentProfileInCombo()
        {
            bindingProfiles = true;
            for (int i = 0; i < cmbProfiles.Items.Count; i++)
            {
                if (currentProfile != null && i < config.Profiles.Count && config.Profiles[i].Id == currentProfile.Id) { cmbProfiles.SelectedIndex = i; cmbProfiles.Text = config.Profiles[i].Name; break; }
            }
            bindingProfiles = false;
        }

        private static void SelectValue(ASelect select, string value, string fallback)
        {
            int index = -1;
            for (int i = 0; i < select.Items.Count; i++)
                if (string.Equals(Convert.ToString(select.Items[i]), value, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            if (index < 0 && !string.IsNullOrWhiteSpace(value))
            {
                select.Items.Add(value);
                index = select.Items.Count - 1;
            }
            if (index < 0)
            {
                for (int i = 0; i < select.Items.Count; i++)
                    if (string.Equals(Convert.ToString(select.Items[i]), fallback, StringComparison.OrdinalIgnoreCase)) { index = i; break; }
            }
            if (index >= 0) select.SelectedIndex = index;
        }

        private static string SelectText(ASelect select, string fallback)
        {
            string value = Convert.ToString(select.SelectedValue);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void SetNumber(AInputNumber number, int value)
        {
            decimal minimum = number.Minimum.HasValue ? number.Minimum.Value : Decimal.MinValue;
            decimal maximum = number.Maximum.HasValue ? number.Maximum.Value : Decimal.MaxValue;
            number.Value = Math.Max(minimum, Math.Min(maximum, Convert.ToDecimal(value)));
        }

        private static int ThemeIndex(string value)
        {
            if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase)) return 2;
            return 0;
        }

        private static int AccentIndex(string value)
        {
            string[] names = ThemeService.AccentNames;
            for (int i = 0; i < names.Length; i++) if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        private static string AccentDisplayName(string name)
        {
            if (name == "Blue") return "蓝色";
            if (name == "Violet") return "紫罗兰";
            if (name == "Orange") return "橙色";
            if (name == "Rose") return "玫红";
            return "翡翠";
        }

        private static bool IsLanBinding(string host)
        {
            return string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) || string.Equals(host, "::", StringComparison.OrdinalIgnoreCase) || string.Equals(host, "*", StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimForLog(string value, int max)
        {
            if (value == null) return string.Empty;
            return value.Length <= max ? value : value.Substring(0, max) + "…";
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char ch in Path.GetInvalidFileNameChars()) value = value.Replace(ch, '_');
            return value;
        }
    }

    internal static class PromptDialogV2
    {
        public static string Show(IWin32Window owner, string labelText, string title, string initial)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(450, 150);
                form.Font = new Font("Microsoft YaHei UI", 9F);
                Label label = new Label();
                label.Text = labelText;
                label.AutoSize = true;
                label.Location = new Point(18, 18);
                TextBox input = new TextBox();
                input.Text = initial;
                input.Location = new Point(20, 48);
                input.Width = 410;
                Button ok = new Button();
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(274, 100);
                ok.Width = 74;
                Button cancel = new Button();
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(356, 100);
                cancel.Width = 74;
                form.Controls.Add(label);
                form.Controls.Add(input);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                input.SelectAll();
                return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
            }
        }
    }
}
