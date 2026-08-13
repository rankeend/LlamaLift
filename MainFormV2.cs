using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
        private enum LocalModelUiState
        {
            Closed,
            Loading,
            Ready,
            Generating,
            Stopping,
            External,
            Failed
        }

        private static readonly string MonospaceFontFamily = ResolveMonospaceFontFamily();
        private readonly AppConfig config;
        private readonly ServerProcessManager processManager;
        private readonly SystemPerformanceMonitor systemPerformanceMonitor;
        private readonly LlamaMetricsClient llamaMetricsClient;
        private readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
        private readonly Dictionary<string, AButton> navButtons = new Dictionary<string, AButton>();
        private readonly Dictionary<AButton, string> accentChoiceButtons = new Dictionary<AButton, string>();
        private readonly List<Control> configurationControls = new List<Control>();
        private readonly ConcurrentQueue<PendingLog> processLogQueue = new ConcurrentQueue<PendingLog>();

        private ModelProfile currentProfile;
        private ThemePalette palette;
        private bool loadingControls;
        private bool bindingProfiles;
        private bool healthCheckBusy;
        private bool externalServiceDetected;
        private bool forceExit;
        private bool bindingPresets;
        private bool loadingCommandEditor;
        private bool commandEditorDirty;
        private bool monitoringBusy;
        private bool monitoringPaused;
        private int queuedProcessLogCount;
        private int droppedProcessLogCount;
        private bool lifecycleBusy;
        private bool closingAfterStop;
        private bool closingInProgress;
        private bool shutdownFinalized;
        private DateTime serviceStartedUtc = DateTime.MinValue;
        private DateTime lastGenerationActivityUtc = DateTime.MinValue;
        private LocalModelUiState localModelUiState = LocalModelUiState.Closed;
        private string currentPage = "dashboard";

        private APanel sidebar;
        private APanel pageHost;
        private TableLayoutPanel shellLayout;
        private TableLayoutPanel mainLayout;
        private Label lblBrandTitle;
        private Label lblBrandVersion;
        private Label lblSidebarServiceStatus;
        private TableLayoutPanel profilePage;
        private TableLayoutPanel profileColumns;
        private System.Windows.Forms.Panel profileScroll;
        private System.Windows.Forms.Panel profileFilesScroll;
        private System.Windows.Forms.Panel profileRuntimeScroll;
        private AntdUI.Panel profileCommandCard;
        private APanel profileFilesCard;
        private APanel profileRuntimeCard;
        private bool profileCardsStacked;
        private TableLayoutPanel monitoringPageLayout;
        private TableLayoutPanel systemMetricsGrid;
        private TableLayoutPanel modelMetricsGrid;
        private TableLayoutPanel systemChartsGrid;
        private TableLayoutPanel modelChartsGrid;
        private bool monitoringMetricsStacked;
        private bool monitoringChartsStacked;
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
        private ASelect cmbApiProtocol;
        private Label lblProtocolHint;
        private AInput txtHost;
        private AInput txtAdvertisedHost;
        private AInputNumber numPort;
        private AInputNumber numContext;
        private AInputNumber numParallel;
        private AInputNumber numThreads;
        private AInputNumber numBatch;
        private AInputNumber numUbatch;
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
        private ASwitch swMetrics;
        private AInput txtExtraArgs;
        private AInput txtCommand;
        private ASelect cmbTuningPreset;
        private AButton btnAutoTune;

        private TableLayoutPanel commandPage;
        private RichTextBox txtCommandEditor;
        private ASelect cmbParameterPreset;
        private AButton btnApplyParameterPreset;
        private AButton btnSaveParameterPreset;
        private AButton btnRenameParameterPreset;
        private AButton btnParseCommand;
        private AButton btnGenerateCommand;
        private Label lblCommandEditorState;
        private Label lblCommandParseSummary;
        private Label lblPresetSummary;

        private ASelect cmbRuntimeAsset;
        private ASelect cmbInstalledRuntime;
        private AButton btnRefreshRuntimes;
        private AButton btnInstallRuntime;
        private AButton btnUseRuntime;
        private Label lblHardwareSummary;
        private Label lblRuntimeStatus;
        private ProgressBar runtimeProgress;
        private HardwareProfile detectedHardware;

        private AButton btnStart;
        private AButton btnStop;
        private AButton btnRestart;
        private AButton btnDetect;
        private AButton btnTest;
        private AButton btnTestAllProtocols;
        private ASelect cmbTheme;
        private ASelect cmbAccent;
        private System.Windows.Forms.Timer healthTimer;
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer monitorTimer;
        private System.Windows.Forms.Timer logFlushTimer;
        private AButton btnPauseMonitoring;
        private Label lblMonitoringStatus;
        private Label lblMonitoringUpdated;
        private Label lblSystemCpu;
        private Label lblSystemMemory;
        private Label lblSystemGpu;
        private Label lblSystemVram;
        private Label lblServerCpu;
        private Label lblServerMemory;
        private Label lblSystemDisk;
        private Label lblSystemNetwork;
        private Label lblPromptSpeed;
        private Label lblGenerationSpeed;
        private Label lblActiveRequests;
        private Label lblDeferredRequests;
        private Label lblContextUsage;
        private Label lblTokenTotals;
        private Label lblSlotUsage;
        private Label lblServerUptime;
        private Label lblSystemDetails;
        private Label lblModelDetails;
        private RealtimeMetricChart chartCpu;
        private RealtimeMetricChart chartMemory;
        private RealtimeMetricChart chartGpu;
        private RealtimeMetricChart chartServerMemory;
        private RealtimeMetricChart chartPromptSpeed;
        private RealtimeMetricChart chartGenerationSpeed;
        private RealtimeMetricChart chartRequests;
        private RealtimeMetricChart chartContext;

        public MainFormV2()
        {
            config = ConfigStore.Load();
            processManager = new ServerProcessManager();
            systemPerformanceMonitor = new SystemPerformanceMonitor();
            llamaMetricsClient = new LlamaMetricsClient();
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

            monitorTimer = new System.Windows.Forms.Timer();
            monitorTimer.Interval = 5000;
            monitorTimer.Tick += async delegate { await RefreshMonitoringAsync(); };
            monitorTimer.Start();

            logFlushTimer = new System.Windows.Forms.Timer();
            logFlushTimer.Interval = 120;
            logFlushTimer.Tick += delegate { FlushProcessLogs(); };
            logFlushTimer.Start();

            AppendLog("LlamaLift " + AppVersion.DisplayVersion + " 已启动。", false);
            AppendLog("配置目录：" + ConfigStore.DataDirectory, false);
            UpdateDashboardSummary();
        }

        private void InitializeWindow()
        {
            Text = "LlamaLift " + AppVersion.DisplayVersion;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
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
            Shown += delegate { EnsureWindowFitsScreen(); UpdateResponsiveLayouts(); };
            DpiChanged += delegate { BeginInvoke(new Action(UpdateResponsiveLayouts)); };
            SizeChanged += delegate { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(UpdateResponsiveLayouts)); };
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
            shellLayout = shell;
            shell.Dock = DockStyle.Fill;
            shell.Margin = new Padding(0);
            shell.ColumnCount = 2;
            shell.RowCount = 1;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216F));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Controls.Add(shell);

            sidebar = BuildSidebar();
            sidebar.Margin = new Padding(0);
            shell.Controls.Add(sidebar, 0, 0);

            TableLayoutPanel main = new TableLayoutPanel();
            mainLayout = main;
            main.Dock = DockStyle.Fill;
            main.Margin = new Padding(0);
            main.Tag = "background";
            main.Padding = new Padding(24, 0, 24, 24);
            main.RowCount = 2;
            main.ColumnCount = 1;
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shell.Controls.Add(main, 1, 0);

            main.Controls.Add(BuildTopBar(), 0, 0);
            pageHost = new APanel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.Margin = new Padding(0);
            pageHost.Radius = 0;
            pageHost.Shadow = 0;
            pageHost.Padding = new Padding(0);
            pageHost.Tag = "background";
            main.Controls.Add(pageHost, 0, 1);

            AddPage("dashboard", BuildDashboardPage());
            AddPage("monitoring", BuildMonitoringPage());
            AddPage("profiles", BuildProfilesPage());
            AddPage("parameters", BuildParametersPage());
            AddPage("runtimes", BuildRuntimeManagerPage());
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
            panel.Padding = new Padding(14, 18, 12, 18);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Tag = "sidebar";
            layout.ColumnCount = 1;
            layout.RowCount = 10;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            panel.Controls.Add(layout);

            TableLayoutPanel brand = new TableLayoutPanel();
            brand.Dock = DockStyle.Fill;
            brand.Tag = "sidebar";
            brand.ColumnCount = 1;
            brand.RowCount = 2;
            brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 66F));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
            brand.Padding = new Padding(6, 2, 6, 6);
            lblBrandTitle = MakeLabel("LlamaLift", 18F, FontStyle.Bold);
            lblBrandTitle.Dock = DockStyle.Fill;
            lblBrandTitle.AutoSize = false;
            lblBrandTitle.AutoEllipsis = false;
            lblBrandTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblBrandVersion = MakeMutedLabel("本地模型，一键起飞。 · v" + AppVersion.ProductVersion, 8.25F);
            lblBrandVersion.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            lblBrandVersion.Dock = DockStyle.Fill;
            lblBrandVersion.AutoSize = false;
            lblBrandVersion.AutoEllipsis = false;
            lblBrandVersion.TextAlign = ContentAlignment.MiddleLeft;
            brand.Controls.Add(lblBrandTitle, 0, 0);
            brand.Controls.Add(lblBrandVersion, 0, 1);
            layout.Controls.Add(brand, 0, 0);

            layout.Controls.Add(MakeNavButton("dashboard", "服务总览"), 0, 1);
            layout.Controls.Add(MakeNavButton("monitoring", "性能监测"), 0, 2);
            layout.Controls.Add(MakeNavButton("profiles", "模型配置"), 0, 3);
            layout.Controls.Add(MakeNavButton("parameters", "参数工作台"), 0, 4);
            layout.Controls.Add(MakeNavButton("runtimes", "运行环境"), 0, 5);
            layout.Controls.Add(MakeNavButton("logs", "运行日志"), 0, 6);
            layout.Controls.Add(MakeNavButton("settings", "外观与设置"), 0, 7);

            System.Windows.Forms.Panel footer = new System.Windows.Forms.Panel();
            footer.Dock = DockStyle.Fill;
            footer.Tag = "sidebar";
            Label machine = MakeLabel(Environment.MachineName, 9F, FontStyle.Bold);
            machine.Location = new Point(6, 13);
            machine.AutoSize = true;
            lblSidebarServiceStatus = MakeMutedLabel("llama.cpp · 已关闭", 8.5F);
            lblSidebarServiceStatus.Location = new Point(6, 37);
            lblSidebarServiceStatus.AutoSize = true;
            lblSidebarServiceStatus.AccessibleName = "本地大模型状态：已关闭";
            footer.Controls.Add(machine);
            footer.Controls.Add(lblSidebarServiceStatus);
            layout.Controls.Add(footer, 0, 9);
            return panel;
        }

        private Control BuildTopBar()
        {
            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.Tag = "background";
            bar.ColumnCount = 1;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            TableLayoutPanel titles = new TableLayoutPanel();
            titles.Dock = DockStyle.Fill;
            titles.Tag = "background";
            titles.ColumnCount = 1;
            titles.RowCount = 2;
            titles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            titles.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            titles.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            titles.Padding = new Padding(2, 8, 0, 7);
            lblHeaderTitle = MakeLabel("服务总览", 18F, FontStyle.Bold);
            lblHeaderTitle.Dock = DockStyle.Fill;
            lblHeaderTitle.AutoSize = false;
            lblHeaderTitle.AutoEllipsis = false;
            lblHeaderTitle.TextAlign = ContentAlignment.BottomLeft;
            lblHeaderSubtitle = MakeMutedLabel("管理 llama.cpp 后端、模型和 API", 9F);
            lblHeaderSubtitle.Dock = DockStyle.Fill;
            lblHeaderSubtitle.AutoSize = false;
            lblHeaderSubtitle.AutoEllipsis = false;
            lblHeaderSubtitle.TextAlign = ContentAlignment.TopLeft;
            titles.Controls.Add(lblHeaderTitle, 0, 0);
            titles.Controls.Add(lblHeaderSubtitle, 0, 1);
            bar.Controls.Add(titles, 0, 0);

            cmbAccent = MakeSelect(96);
            cmbAccent.Items.AddRange(new object[] { "蓝色", "翡翠", "紫罗兰", "橙色", "玫红" });
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
            page.AutoScrollMinSize = new Size(0, 704);

            APanel hero = NewCard();
            hero.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel heroLayout = new TableLayoutPanel();
            heroLayout.Dock = DockStyle.Fill;
            heroLayout.Padding = new Padding(22, 16, 22, 16);
            heroLayout.ColumnCount = 2;
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            // Keep the three lifecycle actions on one predictable row, including
            // at the minimum window size and under DPI scaling.
            heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            System.Windows.Forms.Panel heroText = new System.Windows.Forms.Panel();
            heroText.Dock = DockStyle.Fill;
            heroText.Tag = "surface";
            lblHeroStatus = MakeLabel("服务未运行", 19F, FontStyle.Bold);
            lblHeroStatus.Location = new Point(0, 8);
            lblHeroStatus.AutoSize = true;
            lblProfileSummary = MakeMutedLabel("请选择并配置一个 llama.cpp 模型", 9.5F);
            lblProfileSummary.Location = new Point(2, 45);
            lblProfileSummary.AutoSize = false;
            lblProfileSummary.AutoEllipsis = true;
            lblProfileSummary.Size = new Size(10, 22);
            lblProfileSummary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblEndpoint = MakeMutedLabel("API 地址尚未配置", 9F);
            lblEndpoint.Location = new Point(2, 72);
            lblEndpoint.AutoSize = false;
            lblEndpoint.AutoEllipsis = true;
            lblEndpoint.Size = new Size(10, 22);
            lblEndpoint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            heroText.Controls.Add(lblHeroStatus);
            heroText.Controls.Add(lblProfileSummary);
            heroText.Controls.Add(lblEndpoint);
            heroText.SizeChanged += delegate { if (currentProfile != null) UpdateDashboardSummary(); };
            heroLayout.Controls.Add(heroText, 0, 0);

            FlowLayoutPanel heroActions = new FlowLayoutPanel();
            heroActions.Dock = DockStyle.Fill;
            heroActions.FlowDirection = FlowDirection.LeftToRight;
            heroActions.WrapContents = false;
            heroActions.Padding = new Padding(4, 21, 0, 0);
            heroActions.Tag = "surface";
            btnStart = MakeButton("启动服务", 96, AntdUI.TTypeMini.Primary, StartClicked);
            btnStop = MakeButton("停止", 64, AntdUI.TTypeMini.Default, StopClicked);
            btnStop.Tag = "danger-action";
            btnRestart = MakeButton("重启", 64, AntdUI.TTypeMini.Default, RestartClicked);
            btnRestart.Tag = "warning-action";
            heroActions.Controls.Add(btnRestart);
            heroActions.Controls.Add(btnStop);
            heroActions.Controls.Add(btnStart);
            heroLayout.Controls.Add(heroActions, 1, 0);
            hero.Controls.Add(heroLayout);
            page.Controls.Add(hero, 0, 0);

            TableLayoutPanel metrics = new TableLayoutPanel();
            metrics.Dock = DockStyle.Fill;
            // The grid sits outside the rounded metric cards. It must use the page
            // background; a surface-colored grid otherwise appears as square tips
            // behind every rounded corner.
            metrics.Tag = "background";
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
            btnDetect = MakeButton("检测后端", 84, AntdUI.TTypeMini.Default, DetectBackendClicked);
            btnTest = MakeButton("测试当前", 92, AntdUI.TTypeMini.Default, TestApiClicked);
            btnTestAllProtocols = MakeButton("测试全部", 92, AntdUI.TTypeMini.Default, TestAllProtocolsClicked);
            quick.Controls.Add(btnDetect);
            quick.Controls.Add(btnTest);
            quick.Controls.Add(btnTestAllProtocols);
            quick.Controls.Add(MakeButton("复制地址", 92, AntdUI.TTypeMini.Default, CopyEndpointClicked));
            quick.Controls.Add(MakeButton("打开 WebUI", 92, AntdUI.TTypeMini.Default, OpenWebUiClicked));
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

        private Control BuildMonitoringPage()
        {
            TableLayoutPanel page = NewPage();
            monitoringPageLayout = page;
            page.Padding = new Padding(22);
            page.RowCount = 9;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 98F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 218F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 486F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 218F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 218F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 486F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            page.AutoScrollMinSize = new Size(0, 2020);

            APanel liveCard = NewCard();
            liveCard.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel live = new TableLayoutPanel();
            live.Dock = DockStyle.Fill;
            live.Padding = new Padding(20, 12, 18, 12);
            live.ColumnCount = 3;
            live.RowCount = 2;
            live.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            live.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
            live.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            live.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            live.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            Label liveTitle = MakeLabel("实时性能中心", 14F, FontStyle.Bold);
            liveTitle.Dock = DockStyle.Fill;
            liveTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblMonitoringStatus = MakeLabel("LIVE · 2 秒刷新", 9F, FontStyle.Bold);
            lblMonitoringStatus.Dock = DockStyle.Fill;
            lblMonitoringStatus.TextAlign = ContentAlignment.MiddleRight;
            btnPauseMonitoring = MakeButton("暂停监测", 106, AntdUI.TTypeMini.Default, ToggleMonitoringClicked);
            btnPauseMonitoring.Dock = DockStyle.Fill;
            btnPauseMonitoring.Margin = new Padding(12, 4, 0, 4);
            lblMonitoringUpdated = MakeMutedLabel("正在建立本机与 llama-server 指标基线……", 8.5F);
            lblMonitoringUpdated.Dock = DockStyle.Fill;
            lblMonitoringUpdated.TextAlign = ContentAlignment.MiddleLeft;
            live.Controls.Add(liveTitle, 0, 0);
            live.Controls.Add(lblMonitoringStatus, 1, 0);
            live.Controls.Add(btnPauseMonitoring, 2, 0);
            live.Controls.Add(lblMonitoringUpdated, 0, 1);
            live.SetColumnSpan(lblMonitoringUpdated, 3);
            liveCard.Controls.Add(live);
            page.Controls.Add(liveCard, 0, 0);

            page.Controls.Add(BuildMonitoringSectionHeader("系统性能", "Windows 全局资源与 llama-server 进程消耗，所有采样仅在本机处理。"), 0, 1);

            TableLayoutPanel systemMetrics = NewMonitoringMetricGrid();
            systemMetricsGrid = systemMetrics;
            lblSystemCpu = AddMonitoringMetric(systemMetrics, 0, 0, "CPU", "—", "全机利用率");
            lblSystemMemory = AddMonitoringMetric(systemMetrics, 1, 0, "内存", "—", "已用比例");
            lblSystemGpu = AddMonitoringMetric(systemMetrics, 2, 0, "GPU", "—", "最繁忙图形引擎");
            lblSystemVram = AddMonitoringMetric(systemMetrics, 3, 0, "显存", "—", "独占显存用量");
            lblServerCpu = AddMonitoringMetric(systemMetrics, 0, 1, "服务 CPU", "—", "llama-server 进程");
            lblServerMemory = AddMonitoringMetric(systemMetrics, 1, 1, "服务内存", "—", "工作集 / 私有内存");
            lblSystemDisk = AddMonitoringMetric(systemMetrics, 2, 1, "磁盘吞吐", "—", "全机读取 + 写入");
            lblSystemNetwork = AddMonitoringMetric(systemMetrics, 3, 1, "网络吞吐", "—", "全机接收 + 发送");
            page.Controls.Add(systemMetrics, 0, 2);

            TableLayoutPanel systemCharts = NewChartGrid();
            systemChartsGrid = systemCharts;
            chartCpu = AddMonitoringChart(systemCharts, 0, 0, "CPU 使用率", "%", 100D);
            chartMemory = AddMonitoringChart(systemCharts, 1, 0, "内存使用率", "%", 100D);
            chartGpu = AddMonitoringChart(systemCharts, 0, 1, "GPU 使用率", "%", 100D);
            chartServerMemory = AddMonitoringChart(systemCharts, 1, 1, "llama-server 工作集", " GB", 0D);
            page.Controls.Add(systemCharts, 0, 3);

            APanel systemDetailsCard = NewCard();
            systemDetailsCard.Margin = new Padding(0, 14, 0, 14);
            systemDetailsCard.Padding = new Padding(20, 14, 20, 14);
            lblSystemDetails = MakeMutedLabel("正在读取处理器、显卡、磁盘、网络与服务进程明细……", 8.75F);
            lblSystemDetails.Dock = DockStyle.Fill;
            lblSystemDetails.AutoSize = false;
            lblSystemDetails.TextAlign = ContentAlignment.MiddleLeft;
            systemDetailsCard.Controls.Add(lblSystemDetails);
            page.Controls.Add(systemDetailsCard, 0, 4);

            page.Controls.Add(BuildMonitoringSectionHeader("大模型性能", "来自 llama-server /metrics 与 /slots：速度、吞吐、并发、排队和上下文。"), 0, 5);

            TableLayoutPanel modelMetrics = NewMonitoringMetricGrid();
            modelMetricsGrid = modelMetrics;
            lblPromptSpeed = AddMonitoringMetric(modelMetrics, 0, 0, "预填充速度", "—", "Prompt tokens/s");
            lblGenerationSpeed = AddMonitoringMetric(modelMetrics, 1, 0, "生成速度", "—", "生成 tokens/s");
            lblActiveRequests = AddMonitoringMetric(modelMetrics, 2, 0, "处理中", "—", "正在推理的请求");
            lblDeferredRequests = AddMonitoringMetric(modelMetrics, 3, 0, "排队", "—", "等待可用槽位");
            lblContextUsage = AddMonitoringMetric(modelMetrics, 0, 1, "上下文占用", "—", "当前槽位使用率");
            lblTokenTotals = AddMonitoringMetric(modelMetrics, 1, 1, "累计 tokens", "—", "输入 + 输出");
            lblSlotUsage = AddMonitoringMetric(modelMetrics, 2, 1, "并发槽位", "—", "活跃 / 总数");
            lblServerUptime = AddMonitoringMetric(modelMetrics, 3, 1, "运行时长", "—", "管理器启动的服务");
            page.Controls.Add(modelMetrics, 0, 6);

            TableLayoutPanel modelCharts = NewChartGrid();
            modelChartsGrid = modelCharts;
            chartPromptSpeed = AddMonitoringChart(modelCharts, 0, 0, "预填充速度", " tok/s", 0D);
            chartGenerationSpeed = AddMonitoringChart(modelCharts, 1, 0, "生成速度", " tok/s", 0D);
            chartRequests = AddMonitoringChart(modelCharts, 0, 1, "活跃请求", "", 0D);
            chartContext = AddMonitoringChart(modelCharts, 1, 1, "上下文占用", "%", 100D);
            page.Controls.Add(modelCharts, 0, 7);

            APanel modelDetailsCard = NewCard();
            modelDetailsCard.Margin = new Padding(0, 14, 0, 0);
            modelDetailsCard.Padding = new Padding(20, 14, 20, 14);
            lblModelDetails = MakeMutedLabel("等待 llama-server。使用简易配置启动时会默认加入 --metrics；自定义命令也可手动加入。", 8.75F);
            lblModelDetails.Dock = DockStyle.Fill;
            lblModelDetails.AutoSize = false;
            lblModelDetails.TextAlign = ContentAlignment.MiddleLeft;
            modelDetailsCard.Controls.Add(lblModelDetails);
            page.Controls.Add(modelDetailsCard, 0, 8);
            return page;
        }

        private static Control BuildMonitoringSectionHeader(string title, string subtitle)
        {
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.Tag = "background";
            header.Padding = new Padding(2, 8, 0, 4);
            header.ColumnCount = 2;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Label heading = MakeLabel(title, 13F, FontStyle.Bold);
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            Label description = MakeMutedLabel(subtitle, 8.5F);
            description.Dock = DockStyle.Fill;
            description.TextAlign = ContentAlignment.MiddleLeft;
            header.Controls.Add(heading, 0, 0);
            header.Controls.Add(description, 1, 0);
            return header;
        }

        private static TableLayoutPanel NewMonitoringMetricGrid()
        {
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Tag = "background";
            grid.ColumnCount = 4;
            grid.RowCount = 2;
            for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            return grid;
        }

        private Label AddMonitoringMetric(TableLayoutPanel grid, int column, int row, string title, string value, string hint)
        {
            APanel card = NewCard();
            card.Margin = new Padding(column == 0 ? 0 : 7, row == 0 ? 0 : 7, column == 3 ? 0 : 7, row == 1 ? 0 : 7);
            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(16, 10, 12, 8);
            content.RowCount = 3;
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            Label caption = MakeMutedLabel(title.ToUpperInvariant(), 8F);
            caption.Dock = DockStyle.Fill;
            caption.TextAlign = ContentAlignment.MiddleLeft;
            Label metric = MakeLabel(value, 13F, FontStyle.Bold);
            metric.Font = new Font("Segoe UI", 13F, FontStyle.Bold, GraphicsUnit.Point);
            metric.Dock = DockStyle.Fill;
            metric.AutoSize = false;
            metric.TextAlign = ContentAlignment.MiddleLeft;
            Label description = MakeMutedLabel(hint, 7.75F);
            description.Dock = DockStyle.Fill;
            description.TextAlign = ContentAlignment.MiddleLeft;
            content.Controls.Add(caption, 0, 0);
            content.Controls.Add(metric, 0, 1);
            content.Controls.Add(description, 0, 2);
            card.Controls.Add(content);
            grid.Controls.Add(card, column, row);
            return metric;
        }

        private static TableLayoutPanel NewChartGrid()
        {
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.Tag = "background";
            grid.ColumnCount = 2;
            grid.RowCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            return grid;
        }

        private RealtimeMetricChart AddMonitoringChart(TableLayoutPanel grid, int column, int row, string title, string unit, double maximum)
        {
            APanel card = NewCard();
            card.Margin = new Padding(column == 0 ? 0 : 7, row == 0 ? 0 : 7, column == 1 ? 0 : 7, row == 1 ? 0 : 7);
            card.Padding = new Padding(2);
            RealtimeMetricChart chart = new RealtimeMetricChart();
            chart.Dock = DockStyle.Fill;
            chart.ChartTitle = title;
            chart.Unit = unit;
            chart.FixedMaximum = maximum;
            card.Controls.Add(chart);
            grid.Controls.Add(card, column, row);
            return chart;
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
            AButton deleteButton = MakeButton("删除", 70, AntdUI.TTypeMini.Default, DeleteProfileClicked);
            deleteButton.Tag = "danger-action";
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
            scroll.AutoScroll = false;
            scroll.Tag = "background";
            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Fill;
            columns.AutoSize = false;
            columns.ColumnCount = 2;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
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
            Label commandTitle = MakeLabel("启动命令预览 · 前往“参数工作台”可编辑并反向同步", 9.5F, FontStyle.Bold);
            commandTitle.Dock = DockStyle.Fill;
            commandLayout.Controls.Add(commandTitle, 0, 0);
            txtCommand = MakeInput("生成的 llama-server 命令");
            txtCommand.ReadOnly = true;
            txtCommand.Font = MakeMonospaceFont(8.5F);
            txtCommand.Dock = DockStyle.Fill;
            commandLayout.Controls.Add(txtCommand, 0, 1);
            commandCard.Controls.Add(commandLayout);
            page.Controls.Add(commandCard, 0, 2);
            return page;
        }

        private Control BuildParametersPage()
        {
            TableLayoutPanel page = NewPage();
            commandPage = page;
            page.Padding = new Padding(22);
            page.RowCount = 4;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 154F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 382F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 142F));
            page.AutoScrollMinSize = new Size(0, 830);

            APanel intro = NewCard();
            intro.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel introLayout = new TableLayoutPanel();
            introLayout.Dock = DockStyle.Fill;
            introLayout.Padding = new Padding(20, 14, 20, 14);
            introLayout.ColumnCount = 2;
            introLayout.RowCount = 2;
            introLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            introLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144F));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label introTitle = MakeLabel("进阶参数模式", 14F, FontStyle.Bold);
            introTitle.Dock = DockStyle.Fill;
            introLayout.Controls.Add(introTitle, 0, 0);
            Label introBody = MakeMutedLabel("直接编辑完整 llama-server 命令。保存时会自动静态预检并给出修改建议；风险提示不会强制阻止保存，未知参数也会原样保留。", 9F);
            introBody.Dock = DockStyle.Fill;
            introBody.AutoSize = false;
            introBody.AutoEllipsis = false;
            introBody.TextAlign = ContentAlignment.MiddleLeft;
            introLayout.Controls.Add(introBody, 0, 1);
            AButton backToForm = MakeButton("返回简易配置", 136, AntdUI.TTypeMini.Default, delegate { Navigate("profiles"); });
            backToForm.Dock = DockStyle.Fill;
            backToForm.Margin = new Padding(8, 15, 0, 15);
            introLayout.Controls.Add(backToForm, 1, 0);
            introLayout.SetRowSpan(backToForm, 2);
            intro.Controls.Add(introLayout);
            page.Controls.Add(intro, 0, 0);

            APanel presetCard = NewCard();
            presetCard.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel presetLayout = new TableLayoutPanel();
            presetLayout.Dock = DockStyle.Fill;
            presetLayout.Padding = new Padding(20, 12, 20, 12);
            presetLayout.ColumnCount = 4;
            presetLayout.RowCount = 3;
            presetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            presetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116F));
            presetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126F));
            presetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            presetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            presetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            presetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label presetTitle = MakeLabel("参数预设", 11F, FontStyle.Bold);
            presetTitle.Dock = DockStyle.Fill;
            presetLayout.Controls.Add(presetTitle, 0, 0);
            presetLayout.SetColumnSpan(presetTitle, 4);
            cmbParameterPreset = MakeSelect(320);
            cmbParameterPreset.Dock = DockStyle.Fill;
            cmbParameterPreset.Margin = new Padding(0, 5, 10, 5);
            cmbParameterPreset.SelectedIndexChanged += ParameterPresetSelectedIndexChanged;
            presetLayout.Controls.Add(cmbParameterPreset, 0, 1);
            btnApplyParameterPreset = MakeButton("应用到当前配置", 108, AntdUI.TTypeMini.Default, ApplyParameterPresetClicked);
            btnSaveParameterPreset = MakeButton("保存当前参数", 118, AntdUI.TTypeMini.Default, SaveParameterPresetClicked);
            btnRenameParameterPreset = MakeButton("重命名", 96, AntdUI.TTypeMini.Default, RenameParameterPresetClicked);
            btnApplyParameterPreset.Dock = btnSaveParameterPreset.Dock = btnRenameParameterPreset.Dock = DockStyle.Fill;
            btnApplyParameterPreset.Margin = btnSaveParameterPreset.Margin = btnRenameParameterPreset.Margin = new Padding(6, 5, 0, 5);
            presetLayout.Controls.Add(btnApplyParameterPreset, 1, 1);
            presetLayout.Controls.Add(btnSaveParameterPreset, 2, 1);
            presetLayout.Controls.Add(btnRenameParameterPreset, 3, 1);
            lblPresetSummary = MakeMutedLabel("预设只保存性能与高级参数，不会替换模型路径、程序路径、端口或监听地址。", 8.5F);
            lblPresetSummary.Dock = DockStyle.Fill;
            lblPresetSummary.AutoSize = false;
            lblPresetSummary.TextAlign = ContentAlignment.MiddleLeft;
            presetLayout.Controls.Add(lblPresetSummary, 0, 2);
            presetLayout.SetColumnSpan(lblPresetSummary, 4);
            presetCard.Controls.Add(presetLayout);
            page.Controls.Add(presetCard, 0, 1);

            APanel editorCard = NewCard();
            editorCard.Margin = new Padding(0, 0, 0, 14);
            TableLayoutPanel editorLayout = new TableLayoutPanel();
            editorLayout.Dock = DockStyle.Fill;
            editorLayout.Padding = new Padding(20, 14, 20, 14);
            editorLayout.ColumnCount = 1;
            editorLayout.RowCount = 3;
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            TableLayoutPanel editorHeader = new TableLayoutPanel();
            editorHeader.Dock = DockStyle.Fill;
            editorHeader.ColumnCount = 2;
            editorHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            editorHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
            Label editorTitle = MakeLabel("完整启动命令", 11F, FontStyle.Bold);
            editorTitle.Dock = DockStyle.Fill;
            lblCommandEditorState = MakeMutedLabel("已与简易表单同步", 8.5F);
            lblCommandEditorState.Dock = DockStyle.Fill;
            lblCommandEditorState.TextAlign = ContentAlignment.MiddleRight;
            editorHeader.Controls.Add(editorTitle, 0, 0);
            editorHeader.Controls.Add(lblCommandEditorState, 1, 0);
            editorLayout.Controls.Add(editorHeader, 0, 0);

            txtCommandEditor = new RichTextBox();
            txtCommandEditor.Dock = DockStyle.Fill;
            txtCommandEditor.BorderStyle = BorderStyle.None;
            txtCommandEditor.Font = MakeMonospaceFont(9.25F);
            txtCommandEditor.AcceptsTab = true;
            txtCommandEditor.DetectUrls = false;
            txtCommandEditor.WordWrap = true;
            txtCommandEditor.Margin = new Padding(0, 2, 0, 8);
            txtCommandEditor.TextChanged += CommandEditorTextChanged;
            editorLayout.Controls.Add(txtCommandEditor, 0, 1);

            TableLayoutPanel editorActions = new TableLayoutPanel();
            editorActions.Dock = DockStyle.Fill;
            editorActions.ColumnCount = 3;
            editorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            editorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156F));
            editorActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156F));
            Label editorHint = MakeMutedLabel("支持短参数、--参数=值、换行与带空格路径。Ctrl+Z 可撤销编辑。", 8.25F);
            editorHint.Dock = DockStyle.Fill;
            editorHint.TextAlign = ContentAlignment.MiddleLeft;
            editorActions.Controls.Add(editorHint, 0, 0);
            btnGenerateCommand = MakeButton("从简易表单重新生成", 148, AntdUI.TTypeMini.Default, GenerateCommandFromFormClicked);
            btnParseCommand = MakeButton("校验并保存", 148, AntdUI.TTypeMini.Primary, ParseCommandClicked);
            btnGenerateCommand.Dock = btnParseCommand.Dock = DockStyle.Fill;
            btnGenerateCommand.Margin = btnParseCommand.Margin = new Padding(8, 7, 0, 7);
            editorActions.Controls.Add(btnGenerateCommand, 1, 0);
            editorActions.Controls.Add(btnParseCommand, 2, 0);
            editorLayout.Controls.Add(editorActions, 0, 2);
            editorCard.Controls.Add(editorLayout);
            page.Controls.Add(editorCard, 0, 2);

            APanel resultCard = NewCard();
            resultCard.Padding = new Padding(20, 14, 20, 14);
            TableLayoutPanel resultLayout = new TableLayoutPanel();
            resultLayout.Dock = DockStyle.Fill;
            resultLayout.RowCount = 2;
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label resultTitle = MakeLabel("参数预检与同步结果", 11F, FontStyle.Bold);
            resultTitle.Dock = DockStyle.Fill;
            lblCommandParseSummary = MakeMutedLabel("等待编辑。保存前会检查语法、文件、端口、参数组合与鉴权风险。", 9F);
            lblCommandParseSummary.Dock = DockStyle.Fill;
            lblCommandParseSummary.AutoSize = false;
            lblCommandParseSummary.TextAlign = ContentAlignment.TopLeft;
            resultLayout.Controls.Add(resultTitle, 0, 0);
            resultLayout.Controls.Add(lblCommandParseSummary, 0, 1);
            resultCard.Controls.Add(resultLayout);
            page.Controls.Add(resultCard, 0, 3);

            configurationControls.AddRange(new Control[] {
                txtCommandEditor, cmbParameterPreset, btnApplyParameterPreset, btnSaveParameterPreset,
                btnRenameParameterPreset, btnGenerateCommand, btnParseCommand
            });
            return page;
        }

        private void UpdateProfileResponsiveLayout()
        {
            if (profilePage == null || profileColumns == null || profileFilesCard == null || profileRuntimeCard == null ||
                profileFilesScroll == null || profileRuntimeScroll == null) return;
            float scale = Math.Max(1F, Math.Max(DeviceDpi / 96F, sidebar == null ? Font.Height / 15F : sidebar.Width / 216F));
            int availableWidth = Math.Max(0, ClientSize.Width - (sidebar == null ? 0 : sidebar.Width) - Convert.ToInt32(70F * scale));
            bool stack = availableWidth < Convert.ToInt32(980F * scale);
            int sectionHeight = Math.Max(Convert.ToInt32(360F * scale), Math.Min(Convert.ToInt32(590F * scale), Math.Max(0, profileScroll.ClientSize.Height)));

            profileColumns.SuspendLayout();
            profilePage.SuspendLayout();
            try
            {
                profileColumns.ColumnStyles.Clear();
                profileColumns.RowStyles.Clear();
                if (stack)
                {
                    profileScroll.AutoScroll = true;
                    profileColumns.Dock = DockStyle.Top;
                    profileColumns.ColumnCount = 1;
                    profileColumns.RowCount = 2;
                    profileColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    profileColumns.RowStyles.Add(new RowStyle(SizeType.Absolute, sectionHeight));
                    profileColumns.RowStyles.Add(new RowStyle(SizeType.Absolute, sectionHeight));
                    profileColumns.SetCellPosition(profileFilesCard, new TableLayoutPanelCellPosition(0, 0));
                    profileColumns.SetCellPosition(profileRuntimeCard, new TableLayoutPanelCellPosition(0, 1));
                    profileFilesCard.Margin = new Padding(0, 0, 0, Convert.ToInt32(8F * scale));
                    profileRuntimeCard.Margin = new Padding(0, Convert.ToInt32(8F * scale), 0, 0);
                    profileColumns.Height = sectionHeight * 2 + Convert.ToInt32(16F * scale);
                    profileScroll.AutoScrollMinSize = new Size(0, profileColumns.Height + Convert.ToInt32(8F * scale));
                }
                else
                {
                    profileScroll.AutoScroll = false;
                    profileScroll.AutoScrollMinSize = Size.Empty;
                    profileColumns.Dock = DockStyle.Fill;
                    profileColumns.ColumnCount = 2;
                    profileColumns.RowCount = 1;
                    profileColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    profileColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    profileColumns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                    profileColumns.SetCellPosition(profileFilesCard, new TableLayoutPanelCellPosition(0, 0));
                    profileColumns.SetCellPosition(profileRuntimeCard, new TableLayoutPanelCellPosition(1, 0));
                    profileFilesCard.Margin = new Padding(0, 0, Convert.ToInt32(8F * scale), 0);
                    profileRuntimeCard.Margin = new Padding(Convert.ToInt32(8F * scale), 0, 0, 0);
                }
                profileCardsStacked = stack;
            }
            finally
            {
                profilePage.ResumeLayout(true);
                profileColumns.ResumeLayout(true);
            }
        }

        private void UpdateResponsiveLayouts()
        {
            FitPagesToHost();
            UpdateProfileResponsiveLayout();
            UpdateMonitoringResponsiveLayout();
            if (sidebar != null) sidebar.PerformLayout();
            if (pageHost != null) pageHost.PerformLayout();
        }

        private void FitPagesToHost()
        {
            if (shellLayout != null && ClientSize.Width > 0)
            {
                shellLayout.MaximumSize = ClientSize;
                shellLayout.Size = ClientSize;
                shellLayout.PerformLayout();
            }
            if (mainLayout != null && sidebar != null)
            {
                int mainWidth = Math.Max(0, ClientSize.Width - sidebar.Width);
                mainLayout.MaximumSize = new Size(mainWidth, ClientSize.Height);
                mainLayout.Size = new Size(mainWidth, ClientSize.Height);
                mainLayout.PerformLayout();
            }
            if (pageHost == null || mainLayout == null) return;
            int headerHeight = mainLayout.RowStyles.Count > 0 ? Convert.ToInt32(mainLayout.RowStyles[0].Height) : 0;
            Size hostSize = new Size(
                Math.Max(0, mainLayout.ClientSize.Width - mainLayout.Padding.Horizontal),
                Math.Max(0, mainLayout.ClientSize.Height - mainLayout.Padding.Vertical - headerHeight));
            pageHost.MaximumSize = hostSize;
            pageHost.Size = hostSize;
            pageHost.PerformLayout();
            if (pageHost.ClientSize.Width <= 0) return;
            int width = pageHost.ClientSize.Width;
            foreach (Control page in pages.Values)
            {
                page.MaximumSize = new Size(width, 0);
                page.Width = width;
            }
        }

        private void UpdateMonitoringResponsiveLayout()
        {
            if (monitoringPageLayout == null || systemMetricsGrid == null || modelMetricsGrid == null ||
                systemChartsGrid == null || modelChartsGrid == null) return;
            float scale = Math.Max(1F, Math.Max(DeviceDpi / 96F, sidebar == null ? 1F : sidebar.Width / 216F));
            int availableWidth = Math.Max(0, ClientSize.Width - (sidebar == null ? 0 : sidebar.Width) - Convert.ToInt32(110F * scale));
            bool stackMetrics = availableWidth < Convert.ToInt32(900F * scale);
            bool stackCharts = availableWidth < Convert.ToInt32(760F * scale);
            if (stackMetrics != monitoringMetricsStacked)
            {
                ReflowMonitoringGrid(systemMetricsGrid, stackMetrics ? 2 : 4);
                ReflowMonitoringGrid(modelMetricsGrid, stackMetrics ? 2 : 4);
                monitoringMetricsStacked = stackMetrics;
            }
            if (stackCharts != monitoringChartsStacked)
            {
                ReflowMonitoringGrid(systemChartsGrid, stackCharts ? 1 : 2);
                ReflowMonitoringGrid(modelChartsGrid, stackCharts ? 1 : 2);
                monitoringChartsStacked = stackCharts;
            }

            float metricHeight = (stackMetrics ? 410F : 218F) * scale;
            float chartHeight = (stackCharts ? 900F : 486F) * scale;
            monitoringPageLayout.RowStyles[2].Height = metricHeight;
            monitoringPageLayout.RowStyles[3].Height = chartHeight;
            monitoringPageLayout.RowStyles[6].Height = metricHeight;
            monitoringPageLayout.RowStyles[7].Height = chartHeight;
            int total = Convert.ToInt32((98F + 58F + 136F + 72F + 150F) * scale + metricHeight * 2F + chartHeight * 2F + 44F * scale);
            monitoringPageLayout.AutoScrollMinSize = new Size(0, total);
        }

        private static void ReflowMonitoringGrid(TableLayoutPanel grid, int columns)
        {
            List<Control> children = new List<Control>();
            foreach (Control child in grid.Controls) children.Add(child);
            int rows = Convert.ToInt32(Math.Ceiling(children.Count / Math.Max(1D, columns)));
            grid.SuspendLayout();
            try
            {
                grid.ColumnCount = columns;
                grid.RowCount = rows;
                grid.ColumnStyles.Clear();
                grid.RowStyles.Clear();
                for (int column = 0; column < columns; column++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
                for (int row = 0; row < rows; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
                for (int i = 0; i < children.Count; i++)
                {
                    int column = i % columns;
                    int row = i / columns;
                    grid.SetCellPosition(children[i], new TableLayoutPanelCellPosition(column, row));
                    children[i].Margin = new Padding(column == 0 ? 0 : 7, row == 0 ? 0 : 7,
                        column == columns - 1 ? 0 : 7, row == rows - 1 ? 0 : 7);
                }
            }
            finally { grid.ResumeLayout(true); }
        }

        private static System.Windows.Forms.Panel NewIndependentScrollRegion()
        {
            System.Windows.Forms.Panel region = new System.Windows.Forms.Panel();
            region.Dock = DockStyle.Fill;
            region.AutoScroll = true;
            region.Tag = "surface";
            region.TabStop = true;
            return region;
        }

        private APanel BuildFilesCard()
        {
            APanel card = NewCard();
            card.Margin = new Padding(0, 0, 8, 0);
            card.Padding = new Padding(18, 12, 10, 14);
            TableLayoutPanel frame = new TableLayoutPanel();
            frame.Dock = DockStyle.Fill;
            frame.ColumnCount = 1;
            frame.RowCount = 2;
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label title = MakeLabel("模型与程序", 11F, FontStyle.Bold);
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            frame.Controls.Add(title, 0, 0);
            System.Windows.Forms.Panel body = NewIndependentScrollRegion();
            profileFilesScroll = body;
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.ColumnCount = 3;
            table.RowCount = 8;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
            for (int i = 0; i < 7; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));

            txtProfileName = AddInputRow(table, 0, "配置名称", "例如：Qwen 35B · 主服务", null);
            txtServerExe = AddInputRow(table, 1, "llama-server", "自动检测或手动选择 llama-server.exe", DetectServerExecutableClicked, "检测");
            txtModel = AddInputRow(table, 2, "主模型", "选择 GGUF 模型", delegate { BrowseFile(txtModel, "GGUF 模型|*.gguf|所有文件|*.*"); });
            txtMmproj = AddInputRow(table, 3, "视觉模型", "可选：mmproj-*.gguf", delegate { BrowseFile(txtMmproj, "GGUF 视觉模型|*.gguf|所有文件|*.*"); });
            txtAlias = AddInputRow(table, 4, "模型别名", "API 请求中的 model 名称", null);
            txtApiKeyFile = AddInputRow(table, 5, "API Key", "可选：选择托管或外部 Key 文件", delegate { OpenApiKeyManager(); }, "管理");

            Label protocolLabel = MakeMutedLabel("API 协议", 8.5F);
            protocolLabel.Dock = DockStyle.Fill;
            protocolLabel.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(protocolLabel, 0, 6);
            cmbApiProtocol = MakeSelect(260);
            cmbApiProtocol.Items.AddRange(new object[] { "Responses（原生）", "Chat Completions", "Anthropic Messages" });
            cmbApiProtocol.Dock = DockStyle.Fill;
            table.Controls.Add(cmbApiProtocol, 1, 6);
            table.SetColumnSpan(cmbApiProtocol, 2);
            WireSettingControl(cmbApiProtocol);
            configurationControls.Add(cmbApiProtocol);

            lblProtocolHint = MakeMutedLabel("选择客户端接入地址、鉴权头和测试方式；不会修改 llama-server 启动参数。", 8.25F);
            lblProtocolHint.Dock = DockStyle.Fill;
            lblProtocolHint.AutoSize = false;
            lblProtocolHint.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(lblProtocolHint, 0, 7);
            table.SetColumnSpan(lblProtocolHint, 3);
            body.Controls.Add(table);
            frame.Controls.Add(body, 0, 1);
            card.Controls.Add(frame);
            return card;
        }

        private APanel BuildRuntimeCard()
        {
            APanel card = NewCard();
            card.Margin = new Padding(8, 0, 0, 0);
            card.Padding = new Padding(18, 12, 10, 14);
            TableLayoutPanel frame = new TableLayoutPanel();
            frame.Dock = DockStyle.Fill;
            frame.ColumnCount = 1;
            frame.RowCount = 2;
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label title = MakeLabel("运行参数", 11F, FontStyle.Bold);
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            frame.Controls.Add(title, 0, 0);
            System.Windows.Forms.Panel body = NewIndependentScrollRegion();
            profileRuntimeScroll = body;
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.ColumnCount = 4;
            table.RowCount = 10;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (int i = 0; i < 10; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            table.RowStyles[8].Height = 112F;

            cmbTuningPreset = MakeSelect(160);
            cmbTuningPreset.Items.AddRange(new object[] { "快速", "均衡", "极限", "自定义" });
            Label tuningLabel = MakeMutedLabel("自适应", 8.5F);
            tuningLabel.Dock = DockStyle.Fill;
            tuningLabel.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(tuningLabel, 0, 0);
            cmbTuningPreset.Dock = DockStyle.Fill;
            table.Controls.Add(cmbTuningPreset, 1, 0);
            btnAutoTune = MakeButton("检测并生成方案", 170, AntdUI.TTypeMini.Default, AutoTuneClicked);
            btnAutoTune.Dock = DockStyle.Fill;
            btnAutoTune.Margin = new Padding(6, 7, 0, 7);
            table.Controls.Add(btnAutoTune, 2, 0);
            table.SetColumnSpan(btnAutoTune, 2);
            cmbTuningPreset.SelectedIndexChanged += delegate
            {
                if (!loadingControls && btnAutoTune != null)
                {
                    string selected = SelectText(cmbTuningPreset, "均衡");
                    btnAutoTune.Text = selected == "自定义" ? "请选择性能档位" : "检测并应用“" + selected + "”方案";
                }
            };
            configurationControls.Add(cmbTuningPreset);
            configurationControls.Add(btnAutoTune);

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

            numThreads = MakeNumber(0, 512, 0);
            numBatch = MakeNumber(1, 65536, 2048);
            AddPair(table, 6, "CPU 线程", numThreads, "Batch", numBatch);

            numUbatch = MakeNumber(1, 65536, 512);
            cmbReasoning = MakeSelect(150);
            cmbReasoning.Items.AddRange(new object[] { "默认", "none", "auto", "deepseek", "deepseek-legacy" });
            AddPair(table, 7, "Ubatch", numUbatch, "推理解析", cmbReasoning);

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
            swMetrics = MakeSwitch(string.Empty);
            switches.Controls.Add(MakeSwitchItem("自动 Fit", swFit));
            switches.Controls.Add(MakeSwitchItem("Flash Attention", swFlash));
            switches.Controls.Add(MakeSwitchItem("Jinja 工具调用", swJinja));
            switches.Controls.Add(MakeSwitchItem("禁用 WebUI", swNoWebUi));
            switches.Controls.Add(MakeSwitchItem("No mmap", swNoMmap));
            switches.Controls.Add(MakeSwitchItem("Mlock", swMlock));
            switches.Controls.Add(MakeSwitchItem("性能指标", swMetrics));
            configurationControls.AddRange(new Control[] { swFit, swFlash, swJinja, swNoWebUi, swNoMmap, swMlock, swMetrics });
            table.Controls.Add(switches, 0, 8);
            table.SetColumnSpan(switches, 4);

            txtExtraArgs = MakeInput("其余 llama-server 参数（高级）");
            Label extraLabel = MakeMutedLabel("自定义参数", 8.5F);
            extraLabel.Dock = DockStyle.Fill;
            extraLabel.TextAlign = ContentAlignment.MiddleLeft;
            table.Controls.Add(extraLabel, 0, 9);
            table.Controls.Add(txtExtraArgs, 1, 9);
            table.SetColumnSpan(txtExtraArgs, 3);
            WireSettingControl(txtExtraArgs);
            configurationControls.Add(txtExtraArgs);

            body.Controls.Add(table);
            frame.Controls.Add(body, 0, 1);
            card.Controls.Add(frame);
            return card;
        }

        private Control BuildRuntimeManagerPage()
        {
            TableLayoutPanel page = NewPage();
            page.Padding = new Padding(22);
            page.RowCount = 3;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 162F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 260F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 210F));
            page.AutoScrollMinSize = new Size(0, 676);

            APanel hardwareCard = NewCard();
            hardwareCard.Margin = new Padding(0, 0, 0, 14);
            hardwareCard.Padding = new Padding(20, 16, 20, 16);
            TableLayoutPanel hardwareLayout = new TableLayoutPanel();
            hardwareLayout.Dock = DockStyle.Fill;
            hardwareLayout.ColumnCount = 2;
            hardwareLayout.RowCount = 2;
            hardwareLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            hardwareLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            hardwareLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            hardwareLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label hardwareTitle = MakeLabel("本机运行能力", 14F, FontStyle.Bold);
            hardwareTitle.Dock = DockStyle.Fill;
            hardwareLayout.Controls.Add(hardwareTitle, 0, 0);
            btnRefreshRuntimes = MakeButton("检测并刷新版本", 142, AntdUI.TTypeMini.Default, RefreshRuntimesClicked);
            btnRefreshRuntimes.Dock = DockStyle.Fill;
            hardwareLayout.Controls.Add(btnRefreshRuntimes, 1, 0);
            lblHardwareSummary = MakeMutedLabel("尚未检测。点击右上角后，软件会识别 CPU、内存、GPU，并从 llama.cpp 官方仓库读取可用版本。", 9F);
            lblHardwareSummary.Dock = DockStyle.Fill;
            lblHardwareSummary.AutoSize = false;
            lblHardwareSummary.TextAlign = ContentAlignment.MiddleLeft;
            hardwareLayout.Controls.Add(lblHardwareSummary, 0, 1);
            hardwareLayout.SetColumnSpan(lblHardwareSummary, 2);
            hardwareCard.Controls.Add(hardwareLayout);
            page.Controls.Add(hardwareCard, 0, 0);

            APanel releasesCard = NewCard();
            releasesCard.Margin = new Padding(0, 0, 0, 14);
            releasesCard.Padding = new Padding(20, 14, 20, 16);
            TableLayoutPanel releasesLayout = new TableLayoutPanel();
            releasesLayout.Dock = DockStyle.Fill;
            releasesLayout.ColumnCount = 2;
            releasesLayout.RowCount = 5;
            releasesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            releasesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            releasesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            releasesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            releasesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            releasesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            releasesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label releasesTitle = MakeLabel("llama.cpp 官方 Windows 运行时", 13F, FontStyle.Bold);
            releasesTitle.Dock = DockStyle.Fill;
            releasesLayout.Controls.Add(releasesTitle, 0, 0);
            releasesLayout.SetColumnSpan(releasesTitle, 2);
            cmbRuntimeAsset = MakeSelect(520);
            cmbRuntimeAsset.Dock = DockStyle.Fill;
            releasesLayout.Controls.Add(cmbRuntimeAsset, 0, 1);
            btnInstallRuntime = MakeButton("下载并安装", 138, AntdUI.TTypeMini.Primary, InstallRuntimeClicked);
            btnInstallRuntime.Dock = DockStyle.Fill;
            btnInstallRuntime.Enabled = false;
            releasesLayout.Controls.Add(btnInstallRuntime, 1, 1);
            Label releasesNote = MakeMutedLabel("仅使用 ggml-org/llama.cpp 官方 Release；CUDA 构建会自动合并同版本 cudart 包。官方提供摘要时会强制校验 SHA-256。", 8.5F);
            releasesNote.Dock = DockStyle.Fill;
            releasesNote.AutoSize = false;
            releasesNote.TextAlign = ContentAlignment.MiddleLeft;
            releasesLayout.Controls.Add(releasesNote, 0, 2);
            releasesLayout.SetColumnSpan(releasesNote, 2);
            runtimeProgress = new ProgressBar();
            runtimeProgress.Dock = DockStyle.Fill;
            runtimeProgress.Minimum = 0;
            runtimeProgress.Maximum = 100;
            runtimeProgress.Style = ProgressBarStyle.Continuous;
            runtimeProgress.Margin = new Padding(2, 8, 2, 8);
            releasesLayout.Controls.Add(runtimeProgress, 0, 3);
            releasesLayout.SetColumnSpan(runtimeProgress, 2);
            lblRuntimeStatus = MakeMutedLabel("等待刷新官方版本。", 8.75F);
            lblRuntimeStatus.Dock = DockStyle.Fill;
            lblRuntimeStatus.AutoSize = false;
            lblRuntimeStatus.TextAlign = ContentAlignment.MiddleLeft;
            releasesLayout.Controls.Add(lblRuntimeStatus, 0, 4);
            releasesLayout.SetColumnSpan(lblRuntimeStatus, 2);
            releasesCard.Controls.Add(releasesLayout);
            page.Controls.Add(releasesCard, 0, 1);

            APanel installedCard = NewCard();
            installedCard.Padding = new Padding(20, 14, 20, 16);
            TableLayoutPanel installedLayout = new TableLayoutPanel();
            installedLayout.Dock = DockStyle.Fill;
            installedLayout.ColumnCount = 3;
            installedLayout.RowCount = 3;
            installedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            installedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142F));
            installedLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            installedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            installedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            installedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label installedTitle = MakeLabel("已安装运行时", 13F, FontStyle.Bold);
            installedTitle.Dock = DockStyle.Fill;
            installedLayout.Controls.Add(installedTitle, 0, 0);
            installedLayout.SetColumnSpan(installedTitle, 3);
            cmbInstalledRuntime = MakeSelect(480);
            cmbInstalledRuntime.Dock = DockStyle.Fill;
            installedLayout.Controls.Add(cmbInstalledRuntime, 0, 1);
            btnUseRuntime = MakeButton("用于当前配置", 134, AntdUI.TTypeMini.Default, UseRuntimeClicked);
            btnUseRuntime.Dock = DockStyle.Fill;
            installedLayout.Controls.Add(btnUseRuntime, 1, 1);
            AButton openRuntime = MakeButton("打开目录", 112, AntdUI.TTypeMini.Default, OpenRuntimeDirectoryClicked);
            openRuntime.Dock = DockStyle.Fill;
            installedLayout.Controls.Add(openRuntime, 2, 1);
            Label installedNote = MakeMutedLabel("运行时保存在应用数据目录，可安装多个版本并为不同模型配置分别切换；模型文件仍由用户自行选择。", 8.75F);
            installedNote.Dock = DockStyle.Fill;
            installedNote.AutoSize = false;
            installedNote.TextAlign = ContentAlignment.MiddleLeft;
            installedLayout.Controls.Add(installedNote, 0, 2);
            installedLayout.SetColumnSpan(installedNote, 3);
            installedCard.Controls.Add(installedLayout);
            page.Controls.Add(installedCard, 0, 2);
            RefreshInstalledRuntimeOptions();
            return page;
        }

        private Control BuildLogsPage()
        {
            TableLayoutPanel page = NewPage();
            page.AutoScroll = false;
            page.Padding = new Padding(22);
            page.RowCount = 1;
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            APanel card = NewCard();
            card.Padding = new Padding(14);
            TableLayoutPanel consoleLayout = new TableLayoutPanel();
            consoleLayout.Dock = DockStyle.Fill;
            consoleLayout.ColumnCount = 1;
            consoleLayout.RowCount = 2;
            consoleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            consoleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            FlowLayoutPanel header = new FlowLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.FlowDirection = FlowDirection.RightToLeft;
            header.WrapContents = false;
            header.Padding = new Padding(0, 7, 0, 7);
            header.Controls.Add(MakeButton("打开日志目录", 112, AntdUI.TTypeMini.Default, OpenLogDirectoryClicked));
            header.Controls.Add(MakeButton("清空显示", 88, AntdUI.TTypeMini.Default, delegate { txtLogs.Clear(); txtDashboardLog.Clear(); }));
            consoleLayout.Controls.Add(header, 0, 0);
            txtLogs = MakeLogBox();
            consoleLayout.Controls.Add(txtLogs, 0, 1);
            card.Controls.Add(consoleLayout);
            page.Controls.Add(card, 0, 0);
            return page;
        }

        private Control BuildSettingsPage()
        {
            TableLayoutPanel page = NewPage();
            page.Padding = new Padding(22);
            page.RowCount = 4;
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 270F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
            page.AutoScrollMinSize = new Size(0, 824);

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
            FlowLayoutPanel accentChoices = new FlowLayoutPanel();
            accentChoices.Dock = DockStyle.Fill;
            accentChoices.Tag = "surface";
            string[] accents = ThemeService.AccentNames;
            for (int i = 0; i < accents.Length; i++)
            {
                string captured = accents[i];
                AButton button = MakeButton(AccentDisplayName(captured), 84, AntdUI.TTypeMini.Default, delegate { config.AccentName = captured; ApplyTheme(); ConfigStore.Save(config); });
                accentChoiceButtons[button] = captured;
                accentChoices.Controls.Add(button);
            }
            AddSettingRow(app, 2, "强调色", accentChoices);
            appearance.Controls.Add(app);
            page.Controls.Add(appearance, 0, 0);

            APanel apiKeys = NewCard();
            apiKeys.Margin = new Padding(0, 0, 0, 14);
            apiKeys.Padding = new Padding(20);
            TableLayoutPanel apiKeyLayout = new TableLayoutPanel();
            apiKeyLayout.Dock = DockStyle.Fill;
            apiKeyLayout.ColumnCount = 2;
            apiKeyLayout.RowCount = 2;
            apiKeyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            apiKeyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154F));
            apiKeyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            apiKeyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label apiKeyTitle = MakeLabel("API Key 管理", 14F, FontStyle.Bold);
            apiKeyTitle.Dock = DockStyle.Fill;
            apiKeyTitle.TextAlign = ContentAlignment.MiddleLeft;
            apiKeyLayout.Controls.Add(apiKeyTitle, 0, 0);
            apiKeyLayout.SetColumnSpan(apiKeyTitle, 2);
            Label apiKeyBody = MakeMutedLabel("集中创建、生成、导入和删除 llama.cpp 鉴权密钥。列表只显示脱敏摘要，选择后可直接用于当前模型配置。", 9F);
            apiKeyBody.Dock = DockStyle.Fill;
            apiKeyBody.AutoSize = false;
            apiKeyBody.TextAlign = ContentAlignment.MiddleLeft;
            apiKeyLayout.Controls.Add(apiKeyBody, 0, 1);
            AButton manageApiKeys = MakeButton("管理 API Key", 138, AntdUI.TTypeMini.Default, delegate { OpenApiKeyManager(); });
            manageApiKeys.Dock = DockStyle.Fill;
            manageApiKeys.Margin = new Padding(12, 12, 0, 12);
            apiKeyLayout.Controls.Add(manageApiKeys, 1, 1);
            apiKeys.Controls.Add(apiKeyLayout);
            page.Controls.Add(apiKeys, 0, 1);

            APanel storage = NewCard();
            storage.Margin = new Padding(0, 0, 0, 14);
            storage.Padding = new Padding(20);
            TableLayoutPanel storageLayout = new TableLayoutPanel();
            storageLayout.Dock = DockStyle.Fill;
            storageLayout.ColumnCount = 2;
            storageLayout.RowCount = 2;
            storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
            storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            storageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            Label storageTitle = MakeLabel("应用与数据", 14F, FontStyle.Bold);
            storageTitle.Dock = DockStyle.Fill;
            storageTitle.TextAlign = ContentAlignment.MiddleLeft;
            storageLayout.Controls.Add(storageTitle, 0, 0);
            storageLayout.SetColumnSpan(storageTitle, 2);
            AddSettingText(storageLayout, 1, "配置目录", ConfigStore.DataDirectory);
            storage.Controls.Add(storageLayout);
            page.Controls.Add(storage, 0, 2);

            APanel about = NewCard();
            about.Padding = new Padding(20);
            TableLayoutPanel aboutLayout = new TableLayoutPanel();
            aboutLayout.Dock = DockStyle.Fill;
            aboutLayout.ColumnCount = 1;
            aboutLayout.RowCount = 2;
            aboutLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            aboutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label aboutTitle = MakeLabel("LlamaLift  " + AppVersion.DisplayVersion, 14F, FontStyle.Bold);
            aboutTitle.Dock = DockStyle.Fill;
            aboutTitle.TextAlign = ContentAlignment.MiddleLeft;
            Label aboutBody = MakeMutedLabel("本地模型，一键起飞。LlamaLift 可安装 llama.cpp 运行时、自适应本机参数、管理模型服务与 API Key，并实时观察系统和推理性能。软件不捆绑模型文件或用户密钥。\n界面基于 AntdUI（Apache-2.0）；安装包由 Inno Setup 构建。", 9.5F);
            aboutBody.Dock = DockStyle.Fill;
            aboutBody.AutoSize = false;
            aboutBody.TextAlign = ContentAlignment.TopLeft;
            aboutLayout.Controls.Add(aboutTitle, 0, 0);
            aboutLayout.Controls.Add(aboutBody, 0, 1);
            about.Controls.Add(aboutLayout);
            page.Controls.Add(about, 0, 3);
            return page;
        }

        private TableLayoutPanel NewPage()
        {
            TableLayoutPanel page = new TableLayoutPanel();
            page.Dock = DockStyle.Fill;
            page.AutoScroll = true;
            page.Tag = "background";
            page.ColumnCount = 1;
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return page;
        }

        private APanel NewCard()
        {
            APanel card = new APanel();
            card.Dock = DockStyle.Fill;
            card.Radius = 16;
            card.BorderWidth = 1F;
            card.Shadow = 0;
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
            if (monitorTimer != null) monitorTimer.Interval = key == "monitoring" ? 2000 : 5000;
            foreach (KeyValuePair<string, Control> item in pages)
            {
                item.Value.Visible = item.Key == key;
                if (item.Key == key) item.Value.BringToFront();
            }
            foreach (KeyValuePair<string, AButton> item in navButtons)
                StyleNavigationButton(item.Value, item.Key == key);

            if (key == "monitoring")
            {
                lblHeaderTitle.Text = "性能监测";
                lblHeaderSubtitle.Text = "实时查看系统资源与本地大模型推理状态";
            }
            else if (key == "profiles")
            {
                lblHeaderTitle.Text = "模型配置";
                lblHeaderSubtitle.Text = "用简易表单配置后端、模型和常用参数";
            }
            else if (key == "parameters")
            {
                lblHeaderTitle.Text = "参数工作台";
                lblHeaderSubtitle.Text = "编辑完整命令、反向识别字段，并管理可复用的参数预设";
            }
            else if (key == "runtimes")
            {
                lblHeaderTitle.Text = "运行环境";
                lblHeaderSubtitle.Text = "检测硬件，从官方 Release 安装和切换 llama.cpp";
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
                lblHeaderSubtitle.Text = "让本地模型从安装、调优到服务一步起飞";
            }
        }

        private AButton MakeNavButton(string key, string textValue)
        {
            AButton button = MakeButton(textValue, 0, AntdUI.TTypeMini.Default, delegate { Navigate(key); });
            button.AutoSize = false;
            button.MinimumSize = Size.Empty;
            button.MaximumSize = Size.Empty;
            button.Dock = DockStyle.Fill;
            // AntdUI's drawable bounds extend slightly past a dock-filled TableLayout cell.
            // Reserve a right safe area so the rounded edge is never clipped at any DPI.
            button.Margin = new Padding(0, 4, 22, 4);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(16, 0, 0, 0);
            button.Radius = 10;
            navButtons[key] = button;
            return button;
        }

        private void StyleNavigationButton(AButton button, bool selected)
        {
            button.Type = AntdUI.TTypeMini.Default;
            if (palette == null) return;
            button.DefaultBack = selected ? palette.SidebarSelected : palette.Sidebar;
            button.DefaultBorderColor = selected ? palette.SidebarSelected : palette.Sidebar;
            button.ForeColor = palette.Text;
            button.ForeHover = palette.Text;
            button.ForeActive = palette.Text;
            button.BackHover = palette.SidebarHover;
            button.BackActive = palette.SidebarSelected;
            button.Font = new Font("Microsoft YaHei UI", 9F, selected ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
        }

        private Label AddMetricCard(TableLayoutPanel parent, int column, string title, string value, string hint)
        {
            APanel card = NewCard();
            card.Margin = new Padding(column == 0 ? 0 : 7, 0, column == 3 ? 0 : 7, 0);
            System.Windows.Forms.Panel content = new System.Windows.Forms.Panel();
            content.Dock = DockStyle.Fill;
            Label caption = MakeMutedLabel(title.ToUpperInvariant(), 8.5F);
            caption.Location = new Point(18, 14);
            caption.AutoSize = true;
            Label metric = MakeLabel(value, 16F, FontStyle.Bold);
            metric.Font = new Font("Segoe UI", 16F, FontStyle.Bold, GraphicsUnit.Point);
            metric.Location = new Point(17, 40);
            metric.AutoSize = true;
            Label description = MakeMutedLabel(hint, 8.5F);
            description.Dock = DockStyle.Bottom;
            description.Height = 34;
            description.Padding = new Padding(18, 0, 8, 0);
            description.AutoSize = false;
            description.TextAlign = ContentAlignment.TopLeft;
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
            if (width > 0) button.Width = width;
            button.Height = 38;
            button.Radius = 10;
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
            input.Radius = 10;
            input.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            input.Margin = new Padding(2, 5, 2, 5);
            input.Dock = DockStyle.Fill;
            return input;
        }

        private static ASelect MakeSelect(int width)
        {
            ASelect select = new ASelect();
            select.Width = width;
            select.Height = 38;
            select.Radius = 10;
            select.Margin = new Padding(4, 0, 4, 0);
            return select;
        }

        private static AInputNumber MakeNumber(decimal min, decimal max, decimal value)
        {
            AInputNumber number = new AInputNumber();
            number.Minimum = min;
            number.Maximum = max;
            number.Value = value;
            number.Radius = 10;
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
            log.Font = MakeMonospaceFont(9F);
            log.DetectUrls = false;
            log.WordWrap = true;
            log.ScrollBars = RichTextBoxScrollBars.Vertical;
            return log;
        }

        private static Font MakeMonospaceFont(float size)
        {
            return new Font(MonospaceFontFamily, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        private static string ResolveMonospaceFontFamily()
        {
            using (InstalledFontCollection fonts = new InstalledFontCollection())
            {
                string[] preferred = new string[] { "Cascadia Mono", "Cascadia Code", "Consolas" };
                foreach (string candidate in preferred)
                    foreach (FontFamily family in fonts.Families)
                        if (string.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase)) return family.Name;
            }
            return FontFamily.GenericMonospace.Name;
        }

        private AInput AddInputRow(TableLayoutPanel table, int row, string label, string placeholder, EventHandler browse)
        {
            return AddInputRow(table, row, label, placeholder, browse, "…");
        }

        private AInput AddInputRow(TableLayoutPanel table, int row, string label, string placeholder, EventHandler browse, string actionText)
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
                AButton button = MakeButton(string.IsNullOrWhiteSpace(actionText) ? "…" : actionText, 34, AntdUI.TTypeMini.Default, browse);
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(6, 7, 0, 7);
                table.Controls.Add(button, 2, row);
                configurationControls.Add(button);
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
            select.Items.AddRange(new object[]
            {
                "f32", "f16", "bf16", "q8_0", "q5_0", "q5_1", "q4_0", "q4_1", "iq4_nl",
                "turbo2", "turbo3", "turbo4"
            });
        }

        private void BindParameterPresets()
        {
            if (cmbParameterPreset == null) return;
            bindingPresets = true;
            try
            {
                cmbParameterPreset.Items.Clear();
                int selected = 0;
                for (int i = 0; i < config.ParameterPresets.Count; i++)
                {
                    cmbParameterPreset.Items.Add(config.ParameterPresets[i]);
                    if (config.ParameterPresets[i].Id == config.SelectedParameterPresetId) selected = i;
                }
                if (cmbParameterPreset.Items.Count > 0) cmbParameterPreset.SelectedIndex = selected;
            }
            finally { bindingPresets = false; }
            UpdateParameterPresetSummary();
        }

        private ParameterPreset SelectedParameterPreset()
        {
            if (cmbParameterPreset == null) return null;
            int index = cmbParameterPreset.SelectedIndex;
            return index >= 0 && index < config.ParameterPresets.Count ? config.ParameterPresets[index] : null;
        }

        private void ParameterPresetSelectedIndexChanged(object sender, EventArgs e)
        {
            if (bindingPresets) return;
            ParameterPreset preset = SelectedParameterPreset();
            if (preset == null) return;
            config.SelectedParameterPresetId = preset.Id;
            ConfigStore.Save(config);
            UpdateParameterPresetSummary();
        }

        private void UpdateParameterPresetSummary()
        {
            if (lblPresetSummary == null) return;
            ParameterPreset preset = SelectedParameterPreset();
            if (preset == null)
            {
                lblPresetSummary.Text = "请选择一个参数预设。";
                return;
            }
            lblPresetSummary.Text = preset.Name + "  ·  上下文 " + preset.ContextSize + "  ·  并发 " + preset.Parallel +
                "  ·  GPU 层 " + preset.GpuLayers + "  ·  KV " + preset.CacheTypeK + "/" + preset.CacheTypeV +
                "\r\n只保存性能与高级参数，不会替换模型路径、程序路径、端口或监听地址。";
        }

        private void ApplyParameterPresetClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            if (commandEditorDirty)
            {
                SetCommandParseSummary("命令编辑器还有未同步修改。请先解析同步，或从简易表单重新生成后再应用预设。", true);
                return;
            }
            ParameterPreset preset = SelectedParameterPreset();
            if (preset == null) return;
            bool customMode;
            if (!ApplyPerformanceSettings(delegate(ModelProfile profile)
            {
                preset.ApplyTo(profile);
                profile.TuningPreset = string.IsNullOrWhiteSpace(preset.BuiltInKey) ? "Custom" : preset.BuiltInKey;
            }, out customMode)) return;
            SetCommandParseSummary("已应用“" + preset.Name + "”并同步到简易表单。模型路径、监听地址和端口保持不变。", false);
            AppendLog("已应用参数预设：" + preset.Name + (customMode ? "；原自定义命令已无损转换为表单模式，未知参数继续保留" : string.Empty), false);
        }

        private bool ApplyPerformanceSettings(Action<ModelProfile> apply, out bool convertedCustomCommand)
        {
            convertedCustomCommand = false;
            if (currentProfile == null || apply == null) return false;
            if (commandEditorDirty)
            {
                SetCommandParseSummary("参数工作台还有未同步修改。请先点击“解析并同步”，再应用自适应方案或参数预设。", true);
                Navigate("parameters");
                return false;
            }
            UpdateProfileFromControls();
            convertedCustomCommand = currentProfile.UseCustomCommand;
            string preservedUnknownArguments = convertedCustomCommand ? currentProfile.ExtraArguments : string.Empty;
            apply(currentProfile);
            if (convertedCustomCommand) currentProfile.ExtraArguments = ModelProfile.MergeExtraArguments(preservedUnknownArguments, currentProfile.ExtraArguments);
            currentProfile.SwitchToGeneratedCommand();
            commandEditorDirty = false;
            LoadProfileToControls(currentProfile);
            ConfigStore.Save(config);
            return true;
        }

        private void SaveParameterPresetClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            if (commandEditorDirty)
            {
                SetCommandParseSummary("命令编辑器还有未同步修改。请先点击“解析并同步”，再保存预设。", true);
                return;
            }
            ParameterPreset preset = SelectedParameterPreset();
            if (preset == null) return;
            UpdateProfileFromControls();
            preset.Capture(currentProfile);
            ConfigStore.Save(config);
            UpdateParameterPresetSummary();
            SetCommandParseSummary("当前性能与高级参数已保存到“" + preset.Name + "”。", false);
        }

        private void RenameParameterPresetClicked(object sender, EventArgs e)
        {
            ParameterPreset preset = SelectedParameterPreset();
            if (preset == null) return;
            string name = PromptDialogV2.Show(this, "预设名称", "重命名参数预设", preset.Name);
            if (string.IsNullOrWhiteSpace(name)) return;
            preset.Name = name.Trim();
            config.SelectedParameterPresetId = preset.Id;
            ConfigStore.Save(config);
            BindParameterPresets();
            SetCommandParseSummary("预设已重命名为“" + preset.Name + "”。", false);
        }

        private void CommandEditorTextChanged(object sender, EventArgs e)
        {
            if (loadingCommandEditor) return;
            commandEditorDirty = true;
            if (lblCommandEditorState != null)
            {
                lblCommandEditorState.Text = "有未同步修改";
                if (palette != null) lblCommandEditorState.ForeColor = palette.Warning;
            }
            SetCommandParseSummary("命令已修改。点击“校验并保存”后会先检查风险，再同步简易填写处。", false);
        }

        private void GenerateCommandFromFormClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            UpdateProfileFromControls();
            currentProfile.UseCustomCommand = false;
            currentProfile.CustomCommand = string.Empty;
            currentProfile.LastCommandValidationSummary = string.Empty;
            currentProfile.LastCommandValidatedAtUtc = string.Empty;
            SetCommandEditorText(CommandBuilder.BuildDisplayCommand(currentProfile));
            SetCommandParseSummary("已切换为简易表单生成模式，并重新生成启动命令。", false);
        }

        private void ParseCommandClicked(object sender, EventArgs e)
        {
            ValidateAndSaveCustomCommand();
        }

        private bool ValidateAndSaveCustomCommand()
        {
            if (currentProfile == null || txtCommandEditor == null) return false;
            UpdateProfileFromControls();
            string customCommand = txtCommandEditor.Text == null ? string.Empty : txtCommandEditor.Text.Trim();
            CommandPreflightResult preflight = CommandPreflightValidator.Validate(customCommand, currentProfile, true);
            bool needsReview = preflight.ErrorCount > 0 || preflight.WarningCount > 0;
            if (needsReview)
            {
                DialogResult answer = MessageBox.Show(this,
                    preflight.BuildReviewText(7) + "\n\n这些提示不会强制阻止保存。是否仍然保存并使用这段自定义命令？",
                    "启动参数预检", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes)
                {
                    string suggestion = FirstDiagnosticSuggestion(preflight);
                    SetCommandParseSummary("尚未保存：" + preflight.StatusText + (string.IsNullOrWhiteSpace(suggestion) ? string.Empty : "。建议：" + suggestion), false, true);
                    return false;
                }
            }

            currentProfile.CopyCommandSettingsFrom(preflight.ParseResult.Profile);
            currentProfile.TuningPreset = "Custom";
            currentProfile.UseCustomCommand = true;
            currentProfile.CustomCommand = customCommand;
            currentProfile.LastCommandValidationSummary = preflight.StatusText + "；错误风险 " + preflight.ErrorCount + "，提醒 " + preflight.WarningCount;
            currentProfile.LastCommandValidatedAtUtc = DateTime.UtcNow.ToString("o");
            commandEditorDirty = false;
            LoadProfileToControls(currentProfile);
            config.SelectedProfileId = currentProfile.Id;
            ConfigStore.Save(config);
            SetCommandEditorText(customCommand);

            string summary = "已保存自定义命令：识别 " + preflight.ParseResult.RecognizedCount + " 项，原样保留未映射项 " +
                preflight.ParseResult.UnknownCount + " 个。" + preflight.StatusText + "。";
            string firstSuggestion = FirstDiagnosticSuggestion(preflight);
            if (!string.IsNullOrWhiteSpace(firstSuggestion)) summary += " 建议：" + firstSuggestion;
            SetCommandParseSummary(summary, false, needsReview);
            AppendLog("启动命令已预检并保存：" + currentProfile.Name + " · " + preflight.StatusText, preflight.ErrorCount > 0);
            UpdateDashboardSummary();
            return true;
        }

        private static string FirstDiagnosticSuggestion(CommandPreflightResult result)
        {
            if (result == null) return string.Empty;
            foreach (CommandDiagnosticIssue issue in result.Issues)
                if (!string.IsNullOrWhiteSpace(issue.Suggestion)) return issue.Suggestion;
            return string.Empty;
        }

        private void SetCommandEditorText(string value)
        {
            if (txtCommandEditor == null) return;
            loadingCommandEditor = true;
            try { txtCommandEditor.Text = value ?? string.Empty; }
            finally { loadingCommandEditor = false; }
            commandEditorDirty = false;
            if (lblCommandEditorState != null)
            {
                lblCommandEditorState.Text = currentProfile != null && currentProfile.UseCustomCommand ? "已保存自定义命令" : "已与简易表单同步";
                if (palette != null) lblCommandEditorState.ForeColor = palette.Success;
            }
        }

        private void SetCommandParseSummary(string value, bool error)
        {
            SetCommandParseSummary(value, error, false);
        }

        private void SetCommandParseSummary(string value, bool error, bool warning)
        {
            if (lblCommandParseSummary == null) return;
            lblCommandParseSummary.Text = value;
            if (palette != null) lblCommandParseSummary.ForeColor = error ? palette.Danger : warning ? palette.Warning : palette.Muted;
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
            RefreshInstalledRuntimeOptions();
            BindParameterPresets();
        }

        private void ProfileSelectedIndexChanged(object sender, EventArgs e)
        {
            if (bindingProfiles) return;
            if (commandEditorDirty)
            {
                DialogResult discard = MessageBox.Show(this, "参数工作台中还有未同步的修改。是否舍弃这些修改并切换配置？", "未同步的启动参数", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (discard != DialogResult.Yes) { SelectCurrentProfileInCombo(); return; }
                commandEditorDirty = false;
            }
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
            commandEditorDirty = false;
            loadingControls = true;
            try
            {
                txtProfileName.Text = profile.Name;
                txtServerExe.Text = profile.ServerExecutable;
                txtModel.Text = profile.ModelPath;
                txtMmproj.Text = profile.MmprojPath;
                txtAlias.Text = profile.Alias;
                txtApiKeyFile.Text = profile.ApiKeyFile;
                SelectValue(cmbApiProtocol, ApiProtocolMode.DisplayName(profile.ApiProtocol), "Responses（原生）");
                txtHost.Text = profile.Host;
                txtAdvertisedHost.Text = profile.AdvertisedHost;
                SetNumber(numPort, profile.Port);
                SetNumber(numContext, profile.ContextSize);
                SetNumber(numParallel, profile.Parallel);
                SetNumber(numThreads, profile.Threads);
                SetNumber(numBatch, profile.BatchSize);
                SetNumber(numUbatch, profile.UbatchSize);
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
                swMetrics.Checked = profile.EnableMetrics;
                SelectValue(cmbReasoning, string.IsNullOrWhiteSpace(profile.Reasoning) ? "默认" : profile.Reasoning, "默认");
                txtExtraArgs.Text = profile.ExtraArguments;
                SelectValue(cmbTuningPreset, AdaptiveTuner.DisplayPreset(profile.TuningPreset), "均衡");
            }
            finally { loadingControls = false; }
            if (btnAutoTune != null)
            {
                string selectedMode = SelectText(cmbTuningPreset, "均衡");
                btnAutoTune.Text = selectedMode == "自定义" ? "请选择性能档位" : "检测并应用“" + selectedMode + "”方案";
            }
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
            currentProfile.ApiProtocol = ApiProtocolMode.FromDisplayName(SelectText(cmbApiProtocol, "Responses（原生）"));
            currentProfile.Host = string.IsNullOrWhiteSpace(txtHost.Text) ? "127.0.0.1" : txtHost.Text.Trim();
            currentProfile.AdvertisedHost = string.IsNullOrWhiteSpace(txtAdvertisedHost.Text) ? "127.0.0.1" : txtAdvertisedHost.Text.Trim();
            currentProfile.Port = Decimal.ToInt32(numPort.Value);
            currentProfile.ContextSize = Decimal.ToInt32(numContext.Value);
            currentProfile.Parallel = Decimal.ToInt32(numParallel.Value);
            currentProfile.Threads = Decimal.ToInt32(numThreads.Value);
            currentProfile.BatchSize = Decimal.ToInt32(numBatch.Value);
            currentProfile.UbatchSize = Decimal.ToInt32(numUbatch.Value);
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
            currentProfile.EnableMetrics = swMetrics.Checked;
            string reasoning = SelectText(cmbReasoning, "默认");
            currentProfile.Reasoning = reasoning == "默认" ? string.Empty : reasoning;
            currentProfile.ExtraArguments = txtExtraArgs.Text.Trim();
        }

        private void SaveProfileClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            if (commandEditorDirty || currentProfile.UseCustomCommand)
            {
                if (!ValidateAndSaveCustomCommand())
                {
                    if (commandEditorDirty) Navigate("parameters");
                    return;
                }
            }
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

        private async void AutoTuneClicked(object sender, EventArgs e)
        {
            if (currentProfile == null || processManager.IsRunning) return;
            if (commandEditorDirty)
            {
                SetCommandParseSummary("参数工作台还有未同步修改。请先点击“解析并同步”，再运行参数自适应。", true);
                Navigate("parameters");
                return;
            }
            string selectedPreset = SelectText(cmbTuningPreset, "均衡");
            if (selectedPreset == "自定义")
            {
                MessageBox.Show(this, "当前参数已被手工修改。请先选择“快速”“均衡”或“极限”作为目标档位，再运行自适应检测。", "请选择目标档位", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            UpdateProfileFromControls();
            if (string.IsNullOrWhiteSpace(currentProfile.ModelPath) || !File.Exists(currentProfile.ModelPath))
            {
                MessageBox.Show(this, "请先选择一个本地 GGUF 模型，再运行参数自适应。", "缺少模型", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string profileId = currentProfile.Id;
            string modelPath = currentProfile.ModelPath;
            string executable = currentProfile.ServerExecutable;
            btnAutoTune.Loading = true;
            btnAutoTune.Enabled = false;
            cmbTuningPreset.Enabled = false;
            cmbProfiles.Enabled = false;
            try
            {
                if (detectedHardware == null)
                    detectedHardware = await Task.Factory.StartNew<HardwareProfile>(delegate { return HardwareDetector.Detect(); });
                GgufModelInfo model = await Task.Factory.StartNew<GgufModelInfo>(delegate { return GgufMetadataReader.Read(modelPath); });
                ICollection<string> supportedCacheTypes = await Task.Factory.StartNew<ICollection<string>>(delegate
                {
                    return RuntimeCapabilityDetector.DetectCacheTypes(executable);
                });
                AdaptivePlan plan = AdaptiveTuner.Recommend(detectedHardware, model, selectedPreset, supportedCacheTypes);
                string message = plan.Summary;
                bool turboSelected = plan.CacheTypeK.StartsWith("turbo", StringComparison.OrdinalIgnoreCase) ||
                    plan.CacheTypeV.StartsWith("turbo", StringComparison.OrdinalIgnoreCase);
                message += "\n运行时能力：" + (turboSelected ? "已按当前运行时启用 TurboQuant KV Cache" : "已按当前运行时使用标准 KV Cache");
                if (plan.Warnings.Count > 0)
                    message += "\n\n注意：\n- " + string.Join("\n- ", plan.Warnings.ToArray());
                message += "\n\n是否将这组参数应用到当前模型配置？";
                DialogResult answer = MessageBox.Show(this, message, "参数自适应方案 · " + AdaptiveTuner.DisplayPreset(plan.Preset), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (answer != DialogResult.Yes) return;
                if (currentProfile == null || currentProfile.Id != profileId)
                {
                    MessageBox.Show(this, "检测期间当前配置发生了变化，本次结果未应用。请重新检测。", "配置已变化", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool customMode;
                if (!ApplyPerformanceSettings(delegate(ModelProfile profile) { plan.ApplyTo(profile); }, out customMode)) return;
                SetCommandParseSummary("自适应方案已同步到运行参数、启动命令和当前配置。" +
                    (customMode ? "原自定义命令已转换为表单模式，未识别参数仍保留在自定义参数中。" : string.Empty), false);
                AppendLog("已应用参数自适应方案：" + AdaptiveTuner.DisplayPreset(plan.Preset) + "；上下文 " + plan.ContextSize + "；KV " + plan.CacheTypeK + "/" + plan.CacheTypeV +
                    (customMode ? "；已同步替换旧自定义命令中的性能参数" : string.Empty), false);
            }
            catch (Exception ex)
            {
                AppendLog("参数自适应失败：" + ex.Message, true);
                MessageBox.Show(this, ex.Message, "参数自适应失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAutoTune.Loading = false;
                bool unlocked = !processManager.IsRunning && !processManager.IsStopping;
                btnAutoTune.Enabled = unlocked;
                cmbTuningPreset.Enabled = unlocked;
                cmbProfiles.Enabled = unlocked;
            }
        }

        private async void RefreshRuntimesClicked(object sender, EventArgs e)
        {
            if (btnRefreshRuntimes == null) return;
            btnRefreshRuntimes.Loading = true;
            btnInstallRuntime.Enabled = false;
            runtimeProgress.Value = 0;
            lblRuntimeStatus.Text = "正在检测本机硬件……";
            try
            {
                detectedHardware = await Task.Factory.StartNew<HardwareProfile>(delegate { return HardwareDetector.Detect(); });
                lblHardwareSummary.Text = detectedHardware.Summary;
                lblRuntimeStatus.Text = "正在读取 ggml-org/llama.cpp 官方 Release……";
                List<LlamaReleaseAsset> assets = await LlamaReleaseClient.GetWindowsAssetsAsync();
                cmbRuntimeAsset.Items.Clear();
                foreach (LlamaReleaseAsset asset in assets) cmbRuntimeAsset.Items.Add(asset);
                if (assets.Count == 0)
                {
                    lblRuntimeStatus.Text = "官方 Release 中没有识别到 Windows x64 运行时。可稍后重试或继续手工选择 llama-server.exe。";
                    return;
                }

                int selected = 0;
                string newestTag = assets[0].ReleaseTag;
                for (int i = 0; i < assets.Count; i++)
                {
                    if (assets[i].ReleaseTag == newestTag && string.Equals(assets[i].Backend, detectedHardware.RecommendedBackend, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = i;
                        break;
                    }
                }
                cmbRuntimeAsset.SelectedIndex = selected;
                btnInstallRuntime.Enabled = true;
                lblRuntimeStatus.Text = "已读取 " + assets.Count + " 个 Windows x64 构建；已按本机硬件优先选择 " + assets[selected].Backend + "。";
                runtimeProgress.Value = 100;
            }
            catch (Exception ex)
            {
                lblRuntimeStatus.Text = "刷新失败：" + ex.Message + "。现有已安装运行时和手工选择方式不受影响。";
                AppendLog("刷新 llama.cpp 官方版本失败：" + ex.Message, true);
            }
            finally { btnRefreshRuntimes.Loading = false; }
        }

        private async void InstallRuntimeClicked(object sender, EventArgs e)
        {
            LlamaReleaseAsset asset = SelectedRuntimeAsset();
            if (asset == null || processManager.IsRunning) return;
            btnInstallRuntime.Loading = true;
            btnInstallRuntime.Enabled = false;
            btnRefreshRuntimes.Enabled = false;
            runtimeProgress.Value = 0;
            lblRuntimeStatus.Text = "准备下载 " + asset.ReleaseTag + " · " + asset.Backend + "……";
            try
            {
                Progress<int> progress = new Progress<int>(delegate(int value)
                {
                    runtimeProgress.Value = Math.Max(runtimeProgress.Minimum, Math.Min(runtimeProgress.Maximum, value));
                    lblRuntimeStatus.Text = "正在下载、校验并安装…… " + value + "%";
                });
                InstalledRuntime installed = await RuntimeInstaller.InstallAsync(asset, progress);
                for (int i = config.InstalledRuntimes.Count - 1; i >= 0; i--)
                    if (string.Equals(config.InstalledRuntimes[i].InstallDirectory, installed.InstallDirectory, StringComparison.OrdinalIgnoreCase))
                        config.InstalledRuntimes.RemoveAt(i);
                config.InstalledRuntimes.Add(installed);
                if (currentProfile != null)
                {
                    currentProfile.ServerExecutable = installed.ServerExecutable;
                    txtServerExe.Text = installed.ServerExecutable;
                }
                ConfigStore.Save(config);
                RefreshInstalledRuntimeOptions();
                lblRuntimeStatus.Text = "安装完成：" + installed.ReleaseTag + " · " + installed.Backend + "；已用于当前模型配置。";
                AppendLog(lblRuntimeStatus.Text, false);
                MessageBox.Show(this, "llama.cpp 运行时安装完成，并已绑定到当前模型配置。\n\n" + installed.ServerExecutable, "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblRuntimeStatus.Text = "安装失败：" + ex.Message;
                AppendLog(lblRuntimeStatus.Text, true);
                MessageBox.Show(this, ex.Message, "llama.cpp 安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnInstallRuntime.Loading = false;
                btnInstallRuntime.Enabled = cmbRuntimeAsset.Items.Count > 0;
                btnRefreshRuntimes.Enabled = true;
            }
        }

        private void UseRuntimeClicked(object sender, EventArgs e)
        {
            if (processManager.IsRunning)
            {
                MessageBox.Show(this, "请先停止正在运行的服务，再切换 llama.cpp 版本。", "服务正在运行", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            InstalledRuntime runtime = SelectedInstalledRuntime();
            if (runtime == null || currentProfile == null) return;
            if (!File.Exists(runtime.ServerExecutable))
            {
                MessageBox.Show(this, "该运行时的 llama-server.exe 已不存在，请重新安装。", "运行时文件缺失", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            currentProfile.ServerExecutable = runtime.ServerExecutable;
            txtServerExe.Text = runtime.ServerExecutable;
            ConfigStore.Save(config);
            AppendLog("当前配置已切换到 " + runtime.ReleaseTag + " · " + runtime.Backend, false);
            Navigate("profiles");
        }

        private void OpenRuntimeDirectoryClicked(object sender, EventArgs e)
        {
            InstalledRuntime runtime = SelectedInstalledRuntime();
            string directory = runtime == null ? ConfigStore.RuntimeDirectory : runtime.InstallDirectory;
            try
            {
                Directory.CreateDirectory(directory);
                Process.Start("explorer.exe", directory);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法打开目录", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RefreshInstalledRuntimeOptions()
        {
            if (cmbInstalledRuntime == null) return;
            cmbInstalledRuntime.Items.Clear();
            int selected = 0;
            for (int i = 0; i < config.InstalledRuntimes.Count; i++)
            {
                InstalledRuntime runtime = config.InstalledRuntimes[i];
                cmbInstalledRuntime.Items.Add(runtime);
                if (currentProfile != null && string.Equals(currentProfile.ServerExecutable, runtime.ServerExecutable, StringComparison.OrdinalIgnoreCase)) selected = i;
            }
            if (cmbInstalledRuntime.Items.Count > 0) cmbInstalledRuntime.SelectedIndex = selected;
            if (btnUseRuntime != null) btnUseRuntime.Enabled = cmbInstalledRuntime.Items.Count > 0;
        }

        private LlamaReleaseAsset SelectedRuntimeAsset()
        {
            int index = cmbRuntimeAsset == null ? -1 : cmbRuntimeAsset.SelectedIndex;
            return index >= 0 && index < cmbRuntimeAsset.Items.Count ? cmbRuntimeAsset.Items[index] as LlamaReleaseAsset : null;
        }

        private InstalledRuntime SelectedInstalledRuntime()
        {
            int index = cmbInstalledRuntime == null ? -1 : cmbInstalledRuntime.SelectedIndex;
            return index >= 0 && index < cmbInstalledRuntime.Items.Count ? cmbInstalledRuntime.Items[index] as InstalledRuntime : null;
        }

        private void StartClicked(object sender, EventArgs e)
        {
            if (lifecycleBusy || processManager.IsStopping || processManager.IsRunning || currentProfile == null) return;
            UpdateProfileFromControls();
            if (NetworkHelper.IsTcpPortInUse(currentProfile.Port))
            {
                externalServiceDetected = true;
                SetLocalModelState(LocalModelUiState.External);
                UpdateActionButtons();
                MessageBox.Show(this, "端口 " + currentProfile.Port + " 仍被其他进程占用。请先关闭原 BAT/llama-server，等待显存与端口释放，或改用其他端口。", "端口已占用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            externalServiceDetected = false;
            if (!string.IsNullOrWhiteSpace(currentProfile.ApiKeyFile))
            {
                string apiKeyError;
                if (!ApiKeyFileSupport.TryOpenForRead(currentProfile.ApiKeyFile, out apiKeyError))
                {
                    string replacement = ApiKeyFileSupport.FindReadableReplacement(currentProfile.ApiKeyFile);
                    if (!string.IsNullOrWhiteSpace(replacement))
                    {
                        currentProfile.ApiKeyFile = replacement;
                        txtApiKeyFile.Text = replacement;
                        ConfigStore.Save(config);
                        UpdateCommandPreview();
                        AppendLog("原 API Key 路径已失效，已自动重定位到同名托管密钥：" + replacement, false);
                    }
                    else
                    {
                        DialogResult repair = MessageBox.Show(this,
                            "当前 API Key 文件存在但无法读取，llama-server 因此会立即退出。\n\n" + apiKeyError +
                            "\n\n是否现在打开“API Key 管理”重新选择或新建密钥？",
                            "API Key 无法读取", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (repair == DialogResult.Yes) OpenApiKeyManager();
                        Navigate("profiles");
                        return;
                    }
                }
            }
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
                serviceStartedUtc = DateTime.UtcNow;
                lastGenerationActivityUtc = DateTime.MinValue;
                SetLocalModelState(LocalModelUiState.Loading);
                LockConfiguration(true);
            }
            catch (Exception ex)
            {
                AppendLog("启动失败：" + ex.Message, true);
                MessageBox.Show(this, ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void StopClicked(object sender, EventArgs e)
        {
            if (lifecycleBusy) return;
            lifecycleBusy = true;
            btnStop.Loading = true;
            UpdateActionButtons();
            try { await StopManagedServerAsync(true); }
            finally
            {
                lifecycleBusy = false;
                btnStop.Loading = false;
                UpdateActionButtons();
            }
        }

        private async void RestartClicked(object sender, EventArgs e)
        {
            if (lifecycleBusy) return;
            if (!processManager.IsRunning) { StartClicked(sender, e); return; }
            lifecycleBusy = true;
            btnRestart.Loading = true;
            UpdateActionButtons();
            try
            {
                if (!await StopManagedServerAsync(true)) return;
                await Task.Delay(1000);
            }
            finally
            {
                lifecycleBusy = false;
                btnRestart.Loading = false;
                UpdateActionButtons();
            }
            StartClicked(sender, e);
        }

        private async Task<bool> StopManagedServerAsync(bool notifyFailure)
        {
            if (!processManager.IsRunning)
            {
                LockConfiguration(false);
                return true;
            }
            int port = currentProfile == null ? 0 : currentProfile.Port;
            lblHeroStatus.Text = "正在停止服务…";
            lblHeroStatus.ForeColor = palette.Warning;
            SetLocalModelState(LocalModelUiState.Stopping);
            AppendLog("正在停止服务；确认进程和端口释放前不会允许重新启动。", false);
            bool exited = await processManager.StopAsync(15000);
            bool portReleased = exited && (port <= 0 || await NetworkHelper.WaitForTcpPortReleaseAsync(port, 15000));
            if (exited && portReleased)
            {
                externalServiceDetected = false;
                serviceStartedUtc = DateTime.MinValue;
                lastGenerationActivityUtc = DateTime.MinValue;
                SetLocalModelState(LocalModelUiState.Closed);
                LockConfiguration(false);
                lblHeroStatus.Text = "服务已停止";
                lblHeroStatus.ForeColor = palette.Text;
                AppendLog("服务进程与端口均已释放。", false);
                return true;
            }

            if (exited)
            {
                externalServiceDetected = true;
                SetLocalModelState(LocalModelUiState.External);
                LockConfiguration(false);
            }
            else
            {
                SetLocalModelState(LocalModelUiState.Failed);
                LockConfiguration(true);
            }
            lblHeroStatus.Text = exited ? "端口仍未释放" : "停止未完成";
            lblHeroStatus.ForeColor = palette.Danger;
            if (notifyFailure)
                MessageBox.Show(this,
                    exited ? "llama-server 已退出，但端口仍未释放。为避免显存或端口冲突，本次不会自动重启。请稍后重试。" :
                    "llama-server 未能在 15 秒内确认退出。为避免旧进程占用显存，本次不会继续启动新实例。请在任务管理器确认旧 PID。",
                    "停止未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
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
                string protocol = ApiProtocolMode.Normalize(currentProfile.ApiProtocol);
                ModelProfile snapshot = currentProfile.Clone();
                ApiCheckResult result = await TestProtocolAndLogAsync(snapshot, protocol);
                MessageBox.Show(this,
                    ApiProtocolMode.DisplayName(protocol) + "：" + result.Summary +
                    (result.Success ? "\n\n当前接入协议可用。" : "\n\n请确认 llama.cpp 版本、模型别名、API Key 和服务日志。"),
                    "当前协议测试", MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                AppendLog("协议测试异常：" + ex.Message, true);
                MessageBox.Show(this, ex.Message, "协议测试异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { btnTest.Loading = false; }
        }

        private async void TestAllProtocolsClicked(object sender, EventArgs e)
        {
            if (currentProfile == null || btnTestAllProtocols == null) return;
            UpdateProfileFromControls();
            btnTestAllProtocols.Loading = true;
            bool testButtonWasEnabled = btnTest.Enabled;
            btnTest.Enabled = false;
            try
            {
                ModelProfile snapshot = currentProfile.Clone();
                StringBuilder summary = new StringBuilder();
                bool allSucceeded = true;
                foreach (string protocol in ApiProtocolMode.Values())
                {
                    ApiCheckResult result = await TestProtocolAndLogAsync(snapshot, protocol);
                    if (summary.Length > 0) summary.AppendLine();
                    summary.Append(ApiProtocolMode.DisplayName(protocol)).Append("：").Append(result.Summary);
                    allSucceeded = allSucceeded && result.Success;
                }
                summary.AppendLine().AppendLine();
                summary.Append(allSucceeded ? "三个兼容端点均可用。" : "部分协议不可用；这通常与 llama.cpp 版本或分支能力有关，可继续使用测试成功的协议。");
                MessageBox.Show(this, summary.ToString(), "全部协议测试", MessageBoxButtons.OK,
                    allSucceeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppendLog("全部协议测试异常：" + ex.Message, true);
                MessageBox.Show(this, ex.Message, "协议测试异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTest.Enabled = testButtonWasEnabled;
                btnTestAllProtocols.Loading = false;
            }
        }

        private async Task<ApiCheckResult> TestProtocolAndLogAsync(ModelProfile profile, string protocol)
        {
            string normalized = ApiProtocolMode.Normalize(protocol);
            string display = ApiProtocolMode.DisplayName(normalized);
            string endpoint = LlamaApiClient.LocalBaseUrl(profile) + ApiProtocolMode.EndpointPath(normalized);
            AppendLog("开始测试 " + display + " · " + endpoint + "……", false);
            ApiCheckResult result = await LlamaApiClient.TestProtocolAsync(profile, normalized);
            AppendLog(display + "：" + result.Summary, !result.Success);
            if (!string.IsNullOrWhiteSpace(result.Body)) AppendLog(TrimForLog(result.Body, 1600), !result.Success);
            return result;
        }

        private void CopyEndpointClicked(object sender, EventArgs e)
        {
            if (currentProfile == null) return;
            UpdateProfileFromControls();
            string endpoint = LlamaApiClient.ProtocolClientBaseUrl(currentProfile);
            Clipboard.SetText(endpoint);
            AppendLog("已复制 " + ApiProtocolMode.DisplayName(currentProfile.ApiProtocol) + " Base URL：" + endpoint, false);
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
            if (healthCheckBusy || currentProfile == null || lifecycleBusy || processManager.IsStopping) return;
            healthCheckBusy = true;
            try
            {
                ApiCheckResult health = await LlamaApiClient.CheckHealthAsync(currentProfile);
                if (lifecycleBusy || processManager.IsStopping) return;
                bool managed = processManager.IsRunning;
                externalServiceDetected = !managed && (health.Success || health.StatusCode == 503 || NetworkHelper.IsTcpPortInUse(currentProfile.Port));
                if (health.Success)
                {
                    lblApiMetric.Text = "就绪";
                    lblApiMetric.ForeColor = palette.Success;
                    lblHeroStatus.Text = processManager.IsRunning ? "服务已就绪" : "检测到外部服务";
                    lblHeroStatus.ForeColor = processManager.IsRunning ? palette.Success : palette.Warning;
                    SetLocalModelState(processManager.IsRunning ? LocalModelUiState.Ready : LocalModelUiState.External);
                }
                else if (health.StatusCode == 503)
                {
                    lblApiMetric.Text = "模型加载中";
                    lblApiMetric.ForeColor = palette.Warning;
                    lblHeroStatus.Text = "正在加载模型";
                    lblHeroStatus.ForeColor = palette.Warning;
                    SetLocalModelState(LocalModelUiState.Loading);
                }
                else
                {
                    lblApiMetric.Text = "离线";
                    lblApiMetric.ForeColor = palette.Muted;
                    if (!processManager.IsRunning)
                    {
                        lblHeroStatus.Text = "服务未运行";
                        lblHeroStatus.ForeColor = palette.Text;
                        SetLocalModelState(externalServiceDetected ? LocalModelUiState.External : LocalModelUiState.Closed);
                    }
                }
                if (managed && !health.Success && serviceStartedUtc != DateTime.MinValue)
                {
                    TimeSpan loading = DateTime.UtcNow - serviceStartedUtc;
                    if (loading.TotalMinutes >= 3D)
                    {
                        lblHeroStatus.Text = "仍在加载 " + ((int)loading.TotalMinutes) + " 分 " + loading.Seconds + " 秒";
                        lblHeroStatus.ForeColor = palette.Warning;
                        lblApiMetric.Text = "进程仍在运行";
                        SetLocalModelState(LocalModelUiState.Loading);
                    }
                }
                UpdateActionButtons();
            }
            catch { }
            finally { healthCheckBusy = false; }
        }

        private async Task RefreshMonitoringAsync()
        {
            if (monitoringBusy || monitoringPaused || IsDisposed || currentProfile == null) return;
            monitoringBusy = true;
            try
            {
                int processId = processManager.ProcessId;
                ModelProfile snapshot = currentProfile.Clone();
                Task<SystemPerformanceSample> systemTask = Task.Factory.StartNew(delegate { return systemPerformanceMonitor.Sample(processId); });
                Task<LlamaPerformanceSample> modelTask = Task.Factory.StartNew(delegate { return llamaMetricsClient.Sample(snapshot); });
                await Task.WhenAll(systemTask, modelTask);
                if (IsDisposed) return;
                UpdateSystemMonitoring(systemTask.Result);
                UpdateModelMonitoring(modelTask.Result, snapshot, processId);
                lblMonitoringUpdated.Text = "最后更新 " + DateTime.Now.ToString("HH:mm:ss") + " · 悬停图表可查看历史采样点";
            }
            catch (Exception ex)
            {
                if (lblMonitoringUpdated != null) lblMonitoringUpdated.Text = "部分计数器暂不可用：" + TrimForLog(ex.Message, 120);
            }
            finally { monitoringBusy = false; }
        }

        private async void ToggleMonitoringClicked(object sender, EventArgs e)
        {
            monitoringPaused = !monitoringPaused;
            btnPauseMonitoring.Text = monitoringPaused ? "继续监测" : "暂停监测";
            lblMonitoringStatus.Text = monitoringPaused ? "PAUSED · 数据已冻结" : "LIVE · 2 秒刷新";
            lblMonitoringStatus.ForeColor = monitoringPaused ? palette.Warning : palette.Success;
            foreach (RealtimeMetricChart chart in MonitoringCharts()) chart.Paused = monitoringPaused;
            if (!monitoringPaused) await RefreshMonitoringAsync();
        }

        private void UpdateSystemMonitoring(SystemPerformanceSample sample)
        {
            if (sample == null || lblSystemCpu == null) return;
            double memoryPercent = sample.MemoryTotalBytes == 0 ? 0D : sample.MemoryUsedBytes * 100D / sample.MemoryTotalBytes;
            lblSystemCpu.Text = FormatPercent(sample.CpuUsage);
            lblSystemMemory.Text = FormatPercent(memoryPercent);
            lblSystemGpu.Text = FormatPercent(sample.GpuUsage);
            lblSystemVram.Text = FormatBytes(sample.GpuDedicatedBytes);
            lblServerCpu.Text = processManager.IsRunning ? FormatPercent(sample.ProcessCpuUsage) : "未运行";
            lblServerMemory.Text = processManager.IsRunning ? FormatBytes(sample.ProcessWorkingSetBytes) + " / " + FormatBytes(sample.ProcessPrivateBytes) : "未运行";
            lblSystemDisk.Text = FormatRate(sample.DiskReadBytesPerSecond + sample.DiskWriteBytesPerSecond);
            lblSystemNetwork.Text = FormatRate(sample.NetworkReceiveBytesPerSecond + sample.NetworkSendBytesPerSecond);

            chartCpu.AddValue(sample.CpuUsage);
            chartMemory.AddValue(memoryPercent);
            chartGpu.AddValue(sample.GpuUsage);
            chartServerMemory.AddValue(sample.ProcessWorkingSetBytes / 1073741824D);

            string vramTotal = systemPerformanceMonitor.GpuMemoryTotalBytes > 0
                ? FormatBytes(systemPerformanceMonitor.GpuMemoryTotalBytes)
                : "驱动未公开总量";
            lblSystemDetails.Text =
                "CPU  " + systemPerformanceMonitor.CpuName + " · " + systemPerformanceMonitor.LogicalProcessors + " 逻辑处理器" +
                (systemPerformanceMonitor.CpuMaxClockMhz > 0 ? " · 最高 " + systemPerformanceMonitor.CpuMaxClockMhz + " MHz" : string.Empty) + Environment.NewLine +
                "GPU  " + systemPerformanceMonitor.GpuName + " · 独占显存 " + FormatBytes(sample.GpuDedicatedBytes) + " / " + vramTotal +
                " · 共享显存 " + FormatBytes(sample.GpuSharedBytes) + " · 驱动 " + systemPerformanceMonitor.GpuDriverVersion + Environment.NewLine +
                "llama-server  工作集 " + FormatBytes(sample.ProcessWorkingSetBytes) + " · 私有内存 " + FormatBytes(sample.ProcessPrivateBytes) +
                " · GPU " + FormatPercent(sample.ProcessGpuUsage) + " · 进程显存 " + FormatBytes(sample.ProcessGpuDedicatedBytes) +
                " · 线程 " + sample.ProcessThreads + " · 句柄 " + sample.ProcessHandles + Environment.NewLine +
                "I/O  磁盘读 " + FormatRate(sample.DiskReadBytesPerSecond) + " · 写 " + FormatRate(sample.DiskWriteBytesPerSecond) +
                " · 服务读 " + FormatRate(sample.ProcessReadBytesPerSecond) + " · 服务写 " + FormatRate(sample.ProcessWriteBytesPerSecond) +
                " · 网络收 " + FormatRate(sample.NetworkReceiveBytesPerSecond) + " · 发 " + FormatRate(sample.NetworkSendBytesPerSecond);
        }

        private void UpdateModelMonitoring(LlamaPerformanceSample sample, ModelProfile profile, int processId)
        {
            if (sample == null || lblPromptSpeed == null) return;
            if (!lifecycleBusy && !processManager.IsStopping)
            {
                if (sample.ServerReachable && sample.RequestsProcessing > 0)
                {
                    lastGenerationActivityUtc = DateTime.UtcNow;
                    SetLocalModelState(LocalModelUiState.Generating);
                }
                else if (sample.ServerReachable)
                    SetLocalModelState(processManager.IsRunning ? LocalModelUiState.Ready : LocalModelUiState.External);
                else if (processManager.IsRunning)
                    SetLocalModelState(LocalModelUiState.Loading);
                else if (!externalServiceDetected)
                    SetLocalModelState(LocalModelUiState.Closed);
            }
            double contextPercent = sample.ContextUsagePercent;
            if (contextPercent <= 0D && sample.ContextHighWatermark > 0 && profile.ContextSize > 0)
                contextPercent = Math.Min(100D, sample.ContextHighWatermark * 100D / profile.ContextSize);
            lblPromptSpeed.Text = sample.ServerReachable ? FormatNumber(sample.PromptTokensPerSecond) + " tok/s" : "离线";
            lblGenerationSpeed.Text = sample.ServerReachable ? FormatNumber(sample.GenerationTokensPerSecond) + " tok/s" : "离线";
            lblActiveRequests.Text = sample.ServerReachable ? sample.RequestsProcessing.ToString() : "—";
            lblDeferredRequests.Text = sample.ServerReachable ? sample.RequestsDeferred.ToString() : "—";
            lblContextUsage.Text = sample.ServerReachable ? FormatPercent(contextPercent) : "—";
            lblTokenTotals.Text = sample.MetricsAvailable ? FormatCompactNumber(sample.PromptTokensTotal + sample.GeneratedTokensTotal) : "待启用";
            lblSlotUsage.Text = sample.SlotsAvailable ? sample.SlotsActive + " / " + sample.SlotsTotal : "不可用";
            lblServerUptime.Text = processId > 0 ? FormatProcessUptime(processId) : "未托管";

            chartPromptSpeed.AddValue(sample.PromptTokensPerSecond);
            chartGenerationSpeed.AddValue(sample.GenerationTokensPerSecond);
            chartRequests.AddValue(sample.RequestsProcessing);
            chartContext.AddValue(contextPercent);

            lblMonitoringStatus.Text = monitoringPaused ? "PAUSED · 数据已冻结" : (sample.ServerReachable ? "LIVE · 指标在线" : "LIVE · 等待服务");
            lblMonitoringStatus.ForeColor = monitoringPaused ? palette.Warning : sample.ServerReachable ? palette.Success : palette.Muted;
            if (sample.PromptTokensPerSecond > 0D)
            {
                lblPromptMetric.Text = FormatNumber(sample.PromptTokensPerSecond) + " tok/s";
                lblPromptMetric.ForeColor = palette.Accent;
            }
            if (sample.GenerationTokensPerSecond > 0D)
            {
                lblGenerationMetric.Text = FormatNumber(sample.GenerationTokensPerSecond) + " tok/s";
                lblGenerationMetric.ForeColor = palette.Accent;
            }

            bool metricsRequested = profile.UseCustomCommand
                ? Regex.IsMatch(profile.CustomCommand ?? string.Empty, @"(?i)(?:^|\s)--metrics(?:\s|$|=)")
                : profile.EnableMetrics;
            string endpointHint = sample.MetricsAvailable ? "Prometheus 指标已启用" :
                metricsRequested ? "当前服务未提供 /metrics，可能需要更新或重启 llama.cpp" : "在模型配置中开启“性能指标”并重启服务，可获得累计 tokens 与完整吞吐";
            lblModelDetails.Text =
                sample.Status + " · " + endpointHint + Environment.NewLine +
                "吞吐  Prompt " + FormatNumber(sample.PromptTokensPerSecond) + " tok/s · Generation " + FormatNumber(sample.GenerationTokensPerSecond) +
                " tok/s · Prompt 用时 " + FormatDurationSeconds(sample.PromptSecondsTotal) + " · Generation 用时 " + FormatDurationSeconds(sample.GeneratedSecondsTotal) + Environment.NewLine +
                "请求  处理中 " + sample.RequestsProcessing + " · 排队 " + sample.RequestsDeferred + " · 槽位 " + sample.SlotsActive + "/" + sample.SlotsTotal +
                " · 推测解码槽 " + sample.SlotsSpeculative + Environment.NewLine +
                "上下文  当前 " + sample.ContextTokensUsed + " / " + sample.ContextTokensTotal + " tokens · 使用率 " + FormatPercent(contextPercent) +
                " · 历史高水位 " + sample.ContextHighWatermark + " tokens · 累计 Prompt " + FormatCompactNumber(sample.PromptTokensTotal) +
                " / Generation " + FormatCompactNumber(sample.GeneratedTokensTotal);
        }

        private IEnumerable<RealtimeMetricChart> MonitoringCharts()
        {
            RealtimeMetricChart[] values = new RealtimeMetricChart[] {
                chartCpu, chartMemory, chartGpu, chartServerMemory,
                chartPromptSpeed, chartGenerationSpeed, chartRequests, chartContext
            };
            foreach (RealtimeMetricChart chart in values) if (chart != null) yield return chart;
        }

        private static string FormatPercent(double value)
        {
            return Math.Max(0D, value).ToString("0.0") + "%";
        }

        private static string FormatNumber(double value)
        {
            if (value <= 0D) return "0.0";
            return value >= 100D ? value.ToString("0") : value.ToString("0.0");
        }

        private static string FormatCompactNumber(double value)
        {
            if (value >= 1000000000D) return (value / 1000000000D).ToString("0.0") + "B";
            if (value >= 1000000D) return (value / 1000000D).ToString("0.0") + "M";
            if (value >= 1000D) return (value / 1000D).ToString("0.0") + "K";
            return value.ToString("0");
        }

        private static string FormatBytes(double bytes)
        {
            if (bytes <= 0D) return "0 MB";
            if (bytes >= 1073741824D) return (bytes / 1073741824D).ToString("0.00") + " GB";
            if (bytes >= 1048576D) return (bytes / 1048576D).ToString("0") + " MB";
            if (bytes >= 1024D) return (bytes / 1024D).ToString("0") + " KB";
            return bytes.ToString("0") + " B";
        }

        private static string FormatUsedTotal(double used, double total)
        {
            if (total <= 0D) return FormatBytes(used);
            if (total >= 1073741824D)
                return (used / 1073741824D).ToString("0.0") + "/" + (total / 1073741824D).ToString("0.0") + " GB";
            return FormatBytes(used) + " / " + FormatBytes(total);
        }

        private static string FormatRate(double bytesPerSecond)
        {
            return FormatBytes(bytesPerSecond) + "/s";
        }

        private static string FormatDurationSeconds(double seconds)
        {
            if (seconds <= 0D) return "0s";
            TimeSpan value = TimeSpan.FromSeconds(seconds);
            if (value.TotalHours >= 1D) return value.ToString(@"h\:mm\:ss");
            if (value.TotalMinutes >= 1D) return value.ToString(@"m\:ss");
            return value.TotalSeconds.ToString("0.0") + "s";
        }

        private static string FormatProcessUptime(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    TimeSpan uptime = DateTime.Now - process.StartTime;
                    return uptime.TotalHours >= 1D ? uptime.ToString(@"h\:mm\:ss") : uptime.ToString(@"m\:ss");
                }
            }
            catch { return "—"; }
        }

        private void ProcessManagerLogReceived(string message, bool error)
        {
            if (IsDisposed || string.IsNullOrWhiteSpace(message)) return;
            processLogQueue.Enqueue(new PendingLog(message, error));
            int count = Interlocked.Increment(ref queuedProcessLogCount);
            while (count > 5000)
            {
                PendingLog discarded;
                if (!processLogQueue.TryDequeue(out discarded)) break;
                count = Interlocked.Decrement(ref queuedProcessLogCount);
                Interlocked.Increment(ref droppedProcessLogCount);
            }
        }

        private void FlushProcessLogs()
        {
            if (IsDisposed) return;
            StringBuilder diskBatch = new StringBuilder();
            int dropped = Interlocked.Exchange(ref droppedProcessLogCount, 0);
            if (dropped > 0)
                diskBatch.Append(AppendLogCore("日志洪峰期间已丢弃 " + dropped + " 行界面日志；请降低 verbose 级别。", false, false));
            PendingLog item;
            int flushed = 0;
            Stopwatch budget = Stopwatch.StartNew();
            while (flushed < 300 && budget.ElapsedMilliseconds < 12 && processLogQueue.TryDequeue(out item))
            {
                Interlocked.Decrement(ref queuedProcessLogCount);
                diskBatch.Append(AppendLogCore(item.Message, item.Error, false));
                flushed++;
            }
            if (diskBatch.Length > 0) TryWriteLogFile(diskBatch.ToString());
        }

        private void DrainProcessLogsForShutdown()
        {
            StringBuilder diskBatch = new StringBuilder();
            int dropped = Interlocked.Exchange(ref droppedProcessLogCount, 0);
            if (dropped > 0) diskBatch.Append(FormatLogLine("日志洪峰期间已丢弃 " + dropped + " 行界面日志。", false, true));
            PendingLog item;
            while (processLogQueue.TryDequeue(out item))
            {
                Interlocked.Decrement(ref queuedProcessLogCount);
                diskBatch.Append(FormatLogLine(item.Message, item.Error, false));
            }
            if (diskBatch.Length > 0) TryWriteLogFile(diskBatch.ToString());
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
                        SetLocalModelState(LocalModelUiState.Loading);
                    }
                    else if (!lifecycleBusy && !processManager.IsStopping)
                    {
                        SetLocalModelState(externalServiceDetected ? LocalModelUiState.External : LocalModelUiState.Closed);
                        LockConfiguration(false);
                    }
                    UpdateActionButtons();
                });
            }
            catch { }
        }

        private void AppendLog(string message, bool error)
        {
            AppendLogCore(message, error, true);
        }

        private string AppendLogCore(string message, bool error, bool writeFile)
        {
            if (string.IsNullOrWhiteSpace(message)) return string.Empty;
            bool warning = !error && Regex.IsMatch(message, "警告|warning|注意|不足|重试", RegexOptions.IgnoreCase);
            string line = FormatLogLine(message, error, warning);
            AppendLogToBox(txtLogs, line, error, warning);
            AppendLogToBox(txtDashboardLog, line, error, warning);
            if (txtLogs != null && txtLogs.TextLength > 240000)
                txtLogs.Text = txtLogs.Text.Substring(txtLogs.TextLength - 180000);
            if (txtDashboardLog != null && txtDashboardLog.TextLength > 16000)
                txtDashboardLog.Text = txtDashboardLog.Text.Substring(txtDashboardLog.TextLength - 12000);
            ParsePerformance(message);
            if (writeFile) TryWriteLogFile(line);
            return line;
        }

        private static string FormatLogLine(string message, bool error, bool warning)
        {
            string level = error ? "ERROR" : warning ? "WARN " : "INFO ";
            return "[" + DateTime.Now.ToString("HH:mm:ss") + "] [" + level + "] " + (message ?? string.Empty).TrimEnd() + Environment.NewLine;
        }

        private void AppendLogToBox(RichTextBox box, string line, bool error, bool warning)
        {
            if (box == null) return;
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = error ? palette.Danger : warning ? palette.Warning : palette.LogText;
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
            if (currentProfile != null && IsPerformanceSettingControl(sender))
            {
                currentProfile.TuningPreset = "Custom";
                loadingControls = true;
                try { SelectValue(cmbTuningPreset, "自定义", "自定义"); }
                finally { loadingControls = false; }
                if (btnAutoTune != null) btnAutoTune.Text = "请选择性能档位";
            }
            if (!commandEditorDirty && currentProfile != null && currentProfile.UseCustomCommand &&
                !ReferenceEquals(sender, txtProfileName) && !ReferenceEquals(sender, txtAdvertisedHost) &&
                !ReferenceEquals(sender, cmbApiProtocol))
            {
                currentProfile.UseCustomCommand = false;
                currentProfile.CustomCommand = string.Empty;
                currentProfile.LastCommandValidationSummary = string.Empty;
                currentProfile.LastCommandValidatedAtUtc = string.Empty;
                SetCommandParseSummary("简易表单已修改，当前配置已切换回表单生成模式。", false);
            }
            UpdateCommandPreview();
            UpdateDashboardSummary();
            if (commandEditorDirty && lblCommandEditorState != null)
            {
                lblCommandEditorState.Text = "编辑器与简易表单均有修改";
                if (palette != null) lblCommandEditorState.ForeColor = palette.Warning;
            }
        }

        private bool IsPerformanceSettingControl(object sender)
        {
            return ReferenceEquals(sender, numContext) || ReferenceEquals(sender, numParallel) || ReferenceEquals(sender, numThreads) ||
                ReferenceEquals(sender, numBatch) || ReferenceEquals(sender, numUbatch) || ReferenceEquals(sender, txtGpuLayers) ||
                ReferenceEquals(sender, swFit) || ReferenceEquals(sender, numFitTarget) || ReferenceEquals(sender, swFlash) ||
                ReferenceEquals(sender, cmbCacheK) || ReferenceEquals(sender, cmbCacheV) || ReferenceEquals(sender, numImageTokens) ||
                ReferenceEquals(sender, swJinja) || ReferenceEquals(sender, swNoWebUi) || ReferenceEquals(sender, swNoMmap) ||
                ReferenceEquals(sender, swMlock) || ReferenceEquals(sender, swMetrics) || ReferenceEquals(sender, cmbReasoning) ||
                ReferenceEquals(sender, txtExtraArgs);
        }

        private void UpdateCommandPreview()
        {
            if (txtCommand == null || currentProfile == null || loadingControls) return;
            UpdateProfileFromControls();
            string command = CommandBuilder.BuildDisplayCommand(currentProfile);
            txtCommand.Text = command;
            if (!commandEditorDirty) SetCommandEditorText(command);
        }

        private void UpdateDashboardSummary()
        {
            if (currentProfile == null || lblEndpoint == null) return;
            if (!loadingControls) UpdateProfileFromControls();
            lblProfileSummary.Text = currentProfile.Name + "   ·   " + (string.IsNullOrWhiteSpace(currentProfile.ModelPath) ? "尚未选择模型" : Path.GetFileName(currentProfile.ModelPath));
            string protocol = ApiProtocolMode.Normalize(currentProfile.ApiProtocol);
            if (lblEndpoint.Width > 0 && lblEndpoint.Width < 500)
            {
                string shortProtocol = protocol == ApiProtocolMode.ChatCompletions ? "Chat" :
                    protocol == ApiProtocolMode.AnthropicMessages ? "Anthropic" : "Responses";
                string shortAuth = protocol == ApiProtocolMode.AnthropicMessages ? "x-api-key" : "Bearer";
                lblEndpoint.Text = shortProtocol + "   ·   " + ApiProtocolMode.EndpointPath(protocol) + "   ·   " + shortAuth;
            }
            else
            {
                lblEndpoint.Text = ApiProtocolMode.DisplayName(protocol) + "   ·   " + LlamaApiClient.ProtocolEndpointUrl(currentProfile) +
                    "   ·   " + ApiProtocolMode.AuthenticationLabel(protocol) + "   ·   model: " + currentProfile.Alias;
            }
            if (lblProtocolHint != null)
                lblProtocolHint.Text = ApiProtocolMode.Description(protocol) + " 此选择不改启动参数；所选 llama.cpp 版本支持时，其他兼容端点仍可同时使用。";
        }

        private void SetLocalModelState(LocalModelUiState state)
        {
            if (state == LocalModelUiState.Ready && lastGenerationActivityUtc != DateTime.MinValue &&
                (DateTime.UtcNow - lastGenerationActivityUtc).TotalSeconds < 5D)
                state = LocalModelUiState.Generating;
            localModelUiState = state;
            ApplyLocalModelState();
        }

        private void ApplyLocalModelState()
        {
            if (lblSidebarServiceStatus == null) return;
            string text;
            Color color = palette == null ? ForeColor : palette.Muted;
            switch (localModelUiState)
            {
                case LocalModelUiState.Loading:
                    text = "正在加载";
                    if (palette != null) color = palette.Warning;
                    break;
                case LocalModelUiState.Ready:
                    text = "已就绪";
                    if (palette != null) color = palette.Success;
                    break;
                case LocalModelUiState.Generating:
                    text = "输出中";
                    if (palette != null) color = palette.Accent;
                    break;
                case LocalModelUiState.Stopping:
                    text = "正在停止";
                    if (palette != null) color = palette.Warning;
                    break;
                case LocalModelUiState.External:
                    text = "外部服务";
                    if (palette != null) color = palette.Warning;
                    break;
                case LocalModelUiState.Failed:
                    text = "异常";
                    if (palette != null) color = palette.Danger;
                    break;
                default:
                    text = "已关闭";
                    break;
            }
            lblSidebarServiceStatus.Text = "llama.cpp · " + text;
            lblSidebarServiceStatus.ForeColor = color;
            lblSidebarServiceStatus.AccessibleName = "本地大模型状态：" + text;
        }

        private void UpdateActionButtons()
        {
            if (btnStart == null) return;
            bool running = processManager.IsRunning;
            bool busy = lifecycleBusy || processManager.IsStopping;
            btnStart.Enabled = !busy && !running && !externalServiceDetected;
            btnStop.Enabled = !busy && running;
            btnRestart.Enabled = !busy && running;
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
            if (txtCommandEditor != null) { txtCommandEditor.BackColor = palette.LogBackground; txtCommandEditor.ForeColor = palette.LogText; }
            foreach (RealtimeMetricChart chart in MonitoringCharts()) chart.ApplyPalette(palette);
            ApplyLocalModelState();
            foreach (KeyValuePair<AButton, string> choice in accentChoiceButtons)
            {
                bool selected = string.Equals(choice.Value, config.AccentName, StringComparison.OrdinalIgnoreCase);
                choice.Key.Type = selected ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
                if (!selected)
                {
                    choice.Key.DefaultBack = palette.SurfaceAlt;
                    choice.Key.DefaultBorderColor = palette.Border;
                    choice.Key.ForeColor = palette.Text;
                    choice.Key.BackHover = ThemeService.Mix(palette.SurfaceAlt, palette.Accent, 0.08F);
                    choice.Key.BackActive = ThemeService.Mix(palette.SurfaceAlt, palette.Accent, 0.15F);
                }
            }
            if (lblCommandEditorState != null) lblCommandEditorState.ForeColor = commandEditorDirty ? palette.Warning : palette.Success;
            if (lblMonitoringStatus != null) lblMonitoringStatus.ForeColor = monitoringPaused ? palette.Warning : processManager.IsRunning ? palette.Success : palette.Muted;
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
            trayIcon.Icon = Icon ?? SystemIcons.Application;
            trayIcon.Text = "LlamaLift " + AppVersion.DisplayVersion;
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
                    trayIcon.ShowBalloonTip(1000, "LlamaLift", "本地模型服务仍在托盘运行。", ToolTipIcon.Info);
                }
            };
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private async void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (closingAfterStop)
            {
                FinalizeShutdown();
                return;
            }
            if (closingInProgress)
            {
                e.Cancel = true;
                return;
            }
            if (e.CloseReason == CloseReason.UserClosing && commandEditorDirty)
            {
                DialogResult pending = MessageBox.Show(this,
                    "参数工作台中还有未同步修改。\n\n选择“是”将先解析、同步并保存；选择“否”将舍弃这些修改。",
                    "保存启动参数", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (pending == DialogResult.Cancel) { e.Cancel = true; return; }
                if (pending == DialogResult.Yes)
                {
                    ParseCommandClicked(this, EventArgs.Empty);
                    if (commandEditorDirty)
                    {
                        e.Cancel = true;
                        Navigate("parameters");
                        return;
                    }
                }
            }
            if (!forceExit && e.CloseReason == CloseReason.UserClosing && processManager.IsRunning)
            {
                DialogResult result = MessageBox.Show(this,
                    "退出管理器会同时停止由它启动的 llama-server。\n\n选择“否”将最小化到托盘并保持服务运行。",
                    "服务器正在运行", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel) { e.Cancel = true; return; }
                if (result == DialogResult.No) { e.Cancel = true; WindowState = FormWindowState.Minimized; return; }
            }
            if (processManager.IsRunning)
            {
                e.Cancel = true;
                closingInProgress = true;
                lifecycleBusy = true;
                UpdateActionButtons();
                bool stopped;
                try { stopped = await StopManagedServerAsync(!forceExit); }
                finally { lifecycleBusy = false; }
                if (!stopped) { closingInProgress = false; UpdateActionButtons(); return; }
                closingAfterStop = true;
                BeginInvoke((MethodInvoker)Close);
                return;
            }
            FinalizeShutdown();
        }

        private void FinalizeShutdown()
        {
            if (shutdownFinalized) return;
            shutdownFinalized = true;
            if (currentProfile != null) UpdateProfileFromControls();
            ConfigStore.Save(config);
            if (healthTimer != null) healthTimer.Stop();
            if (monitorTimer != null) monitorTimer.Stop();
            if (logFlushTimer != null) { logFlushTimer.Stop(); DrainProcessLogsForShutdown(); logFlushTimer.Dispose(); }
            if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            systemPerformanceMonitor.Dispose();
            processManager.Dispose();
        }

        private sealed class PendingLog
        {
            public string Message { get; private set; }
            public bool Error { get; private set; }
            public PendingLog(string message, bool error) { Message = message; Error = error; }
        }

        private async void DetectServerExecutableClicked(object sender, EventArgs e)
        {
            AButton button = sender as AButton;
            if (button != null) { button.Loading = true; button.Enabled = false; }
            if (txtServerExe != null) txtServerExe.Enabled = false;
            Exception detectionError = null;
            try
            {
                List<LlamaServerCandidate> candidates = await Task.Run(delegate { return LlamaServerLocator.FindCandidates(config); });
                if (IsDisposed) return;
                if (candidates.Count == 0)
                {
                    MessageBox.Show(this,
                        "没有在已登记运行时、系统 PATH、LlamaLift 数据目录和常见安装目录中找到 llama-server.exe。\n\n接下来请手动选择 llama.cpp 安装目录。",
                        "未自动找到 llama-server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await BrowseServerDirectoryManuallyAsync();
                    return;
                }

                LlamaServerCandidate best = candidates[0];
                string additional = candidates.Count > 1 ? "\n\n另外还检测到 " + (candidates.Count - 1) + " 个候选位置。" : string.Empty;
                DialogResult answer = MessageBox.Show(this,
                    "检测到一个可用的 llama-server：\n\n安装目录：" + best.InstallDirectory +
                    "\n程序文件：" + best.ExecutablePath +
                    "\n发现来源：" + best.Source + additional +
                    "\n\n是否使用这个程序？\n选择“否”可改为手动选择。",
                    "确认 llama-server 位置", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    ApplyDetectedServer(best.ExecutablePath, "自动识别");
                }
                else await BrowseServerDirectoryManuallyAsync();
            }
            catch (Exception ex)
            {
                detectionError = ex;
            }
            finally
            {
                if (!IsDisposed)
                {
                    bool unlocked = !processManager.IsRunning && !processManager.IsStopping;
                    if (button != null) { button.Loading = false; button.Enabled = unlocked; }
                    if (txtServerExe != null) txtServerExe.Enabled = unlocked;
                }
            }
            if (detectionError != null)
            {
                MessageBox.Show(this, "自动检测未能完成：" + detectionError.Message + "\n\n接下来请手动选择 llama.cpp 安装目录。",
                    "检测失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                await BrowseServerDirectoryManuallyAsync();
            }
        }

        private async Task BrowseServerDirectoryManuallyAsync()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择 llama.cpp 的安装目录；LlamaLift 会在目录中查找 llama-server.exe。";
                dialog.ShowNewFolderButton = false;
                try
                {
                    if (txtServerExe != null && File.Exists(txtServerExe.Text)) dialog.SelectedPath = Path.GetDirectoryName(txtServerExe.Text);
                    else if (Directory.Exists(ConfigStore.RuntimeDirectory)) dialog.SelectedPath = ConfigStore.RuntimeDirectory;
                }
                catch { }
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string selectedDirectory = dialog.SelectedPath;
                List<LlamaServerCandidate> candidates = await Task.Run(delegate
                {
                    return LlamaServerLocator.FindCandidates(null, new string[] { selectedDirectory }, false);
                });
                if (candidates.Count > 0)
                {
                    LlamaServerCandidate best = candidates[0];
                    DialogResult use = MessageBox.Show(this,
                        "在所选目录中找到了 llama-server.exe：\n\n" + best.ExecutablePath +
                        (candidates.Count > 1 ? "\n\n该目录还有 " + (candidates.Count - 1) + " 个候选程序。" : string.Empty) +
                        "\n\n是否使用这个程序？",
                        "确认手动选择结果", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (use == DialogResult.Yes) { ApplyDetectedServer(best.ExecutablePath, "手动目录识别"); return; }
                }

                DialogResult precise = MessageBox.Show(this,
                    "没有在所选目录中确认到正确的 llama-server.exe。\n\n是否改为精确选择程序文件？",
                    "目录中未找到程序", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (precise == DialogResult.Yes) BrowseFile(txtServerExe, "llama-server.exe|llama-server.exe|可执行文件|*.exe|所有文件|*.*");
            }
        }

        private void ApplyDetectedServer(string executablePath, string source)
        {
            if (txtServerExe == null || string.IsNullOrWhiteSpace(executablePath)) return;
            txtServerExe.Text = executablePath;
            if (currentProfile != null) currentProfile.ServerExecutable = executablePath;
            ConfigStore.Save(config);
            UpdateCommandPreview();
            AppendLog("已通过" + source + "配置 llama-server：" + executablePath, false);
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

        private void OpenApiKeyManager()
        {
            string current = txtApiKeyFile == null ? string.Empty : txtApiKeyFile.Text;
            using (ApiKeyManagerDialog dialog = new ApiKeyManagerDialog(current, palette))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (txtApiKeyFile != null) txtApiKeyFile.Text = dialog.SelectedPath ?? string.Empty;
                UpdateCommandPreview();
                UpdateDashboardSummary();
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
