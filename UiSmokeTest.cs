using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Windows.Forms;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LlamaServerManager
{
    internal static class UiSmokeTest
    {
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);

        [STAThread]
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs eventArgs)
            {
                Exception error = eventArgs.ExceptionObject as Exception;
                string details = error == null ? "unknown" : error.GetType().FullName + "\r\n" + error.Message + "\r\n" + error.StackTrace;
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-error.txt"), details); } catch { }
            };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string output = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-v2.png");
            AppConfig config = ConfigStore.Load();
            config.ThemeMode = args.Length > 1 ? args[1] : "Light";
            config.AccentName = "Blue";
            ConfigStore.Save(config);
            float dialogScale = args.Length > 5 ? Single.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 1F;
            if (args.Length > 2 && string.Equals(args[2], "api-keys", StringComparison.OrdinalIgnoreCase))
            {
                RenderApiKeyDialog(output, config, dialogScale);
                Console.WriteLine("AUDIT PASS: " + output);
                return;
            }
            if (args.Length > 2 && string.Equals(args[2], "connection-info", StringComparison.OrdinalIgnoreCase))
            {
                RenderConnectionInfoDialog(output, config, dialogScale);
                Console.WriteLine("AUDIT PASS: " + output);
                return;
            }
            if (args.Length > 2 && string.Equals(args[2], "message-dialog", StringComparison.OrdinalIgnoreCase))
            {
                RenderMessageDialog(output, config, dialogScale);
                Console.WriteLine("AUDIT PASS: " + output);
                return;
            }
            if (args.Length > 2 && string.Equals(args[2], "prompt-dialog", StringComparison.OrdinalIgnoreCase))
            {
                RenderPromptDialog(output, config, dialogScale);
                Console.WriteLine("AUDIT PASS: " + output);
                return;
            }
            using (MainFormV2 form = new MainFormV2())
            {
                if (args.Length > 2)
                {
                    MethodInfo navigate = typeof(MainFormV2).GetMethod("Navigate", BindingFlags.Instance | BindingFlags.NonPublic);
                    navigate.Invoke(form, new object[] { args[2] });
                }
                int width = args.Length > 3 ? Int32.Parse(args[3]) : 1320;
                int height = args.Length > 4 ? Int32.Parse(args[4]) : 840;
                float scale = args.Length > 5 ? Single.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 1F;
                if (Math.Abs(scale - 1F) > 0.01F) form.Scale(new SizeF(scale, scale));
                form.Size = new Size(width, height);
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                form.Location = new Point(-20000, -20000);
                Application.DoEvents();
                form.PerformLayout();
                bool monitoring = args.Length > 2 && string.Equals(args[2], "monitoring", StringComparison.OrdinalIgnoreCase);
                if (monitoring)
                {
                    Stopwatch wait = Stopwatch.StartNew();
                    while (wait.ElapsedMilliseconds < 2600)
                    {
                        Application.DoEvents();
                        Thread.Sleep(40);
                    }
                }
                else
                {
                    Thread.Sleep(350);
                    Application.DoEvents();
                }
                if (args.Length > 6 && string.Equals(args[6], "bottom", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ScrollPageToBottom(form, args.Length > 2 ? args[2] : "dashboard"))
                        throw new InvalidOperationException("scroll audit failed: page did not move");
                    Application.DoEvents();
                }
                List<string> problems = Audit(form);
                if (problems.Count > 0)
                {
                    string message = "UI audit failed:\r\n" + string.Join("\r\n", problems.ToArray());
                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-error.txt"), message, System.Text.Encoding.UTF8); } catch { }
                    Console.Error.WriteLine(message);
                    form.Close();
                    Environment.ExitCode = 2;
                    return;
                }
                CaptureWindow(form, output);
                form.Close();
            }
            Console.WriteLine("AUDIT PASS: " + output);
        }

        private static void RenderApiKeyDialog(string output, AppConfig config, float scale)
        {
            bool dark = string.Equals(config.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase);
            Color accent = ThemeService.GetAccent(config.AccentName);
            if (dark && string.Equals(config.AccentName, "Blue", StringComparison.OrdinalIgnoreCase)) accent = Color.FromArgb(10, 132, 255);
            ApiKeyStore store = new ApiKeyStore(ConfigStore.ApiKeyDirectory);
            ManagedApiKeyFile testKey = store.Save("UI 测试密钥", "test-only-secret-1234");
            try
            {
                using (ApiKeyManagerDialog dialog = new ApiKeyManagerDialog(testKey.FilePath, ThemePalette.Create(dark, accent)))
                {
                    if (Math.Abs(scale - 1F) > 0.01F) dialog.Scale(new SizeF(scale, scale));
                    dialog.ShowInTaskbar = false;
                    dialog.StartPosition = FormStartPosition.Manual;
                    dialog.Location = new Point(-20000, -20000);
                    dialog.Show();
                    dialog.Location = new Point(-20000, -20000);
                    Application.DoEvents();
                    dialog.PerformLayout();
                    Thread.Sleep(250);
                    Application.DoEvents();
                    List<string> problems = new List<string>();
                    AuditVisibleBounds(dialog, problems);
                    AuditDialogChrome(dialog, problems);
                    FieldInfo secretField = typeof(ApiKeyManagerDialog).GetField("txtKeys", BindingFlags.Instance | BindingFlags.NonPublic);
                    AntdUI.Input secretBox = secretField == null ? null : secretField.GetValue(dialog) as AntdUI.Input;
                    if (secretBox == null || !secretBox.ReadOnly || secretBox.Text.Contains("test-only-secret"))
                        problems.Add("API Key dialog does not mask secrets by default");
                    if (problems.Count > 0)
                    {
                        string message = "API Key dialog audit failed:\r\n" + string.Join("\r\n", problems.ToArray());
                        try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-error.txt"), message, System.Text.Encoding.UTF8); } catch { }
                        Console.Error.WriteLine(message);
                        dialog.Close();
                        Environment.ExitCode = 2;
                        return;
                    }
                    dialog.Refresh();
                    CaptureControl(dialog, output);
                    dialog.Close();
                }
            }
            finally { try { store.Delete(testKey.FilePath); } catch { } }
        }

        private static void RenderConnectionInfoDialog(string output, AppConfig config, float scale)
        {
            bool dark = string.Equals(config.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase);
            Color accent = ThemeService.GetAccent(config.AccentName);
            if (dark && string.Equals(config.AccentName, "Blue", StringComparison.OrdinalIgnoreCase)) accent = Color.FromArgb(10, 132, 255);
            ConnectionInfoSnapshot info = new ConnectionInfoSnapshot
            {
                ProviderId = "llamalift-ornith-1-5",
                ApiProtocol = "Responses（原生）",
                ApiAddress = "http://127.0.0.1:8080/v1",
                ApiKey = "sk-llamalift-test-only-secret",
                HasApiKey = true,
                ModelFullName = "Ornith-1.5-35B-A3B-AD-IQ3_XXS-IQ2_S.gguf",
                MaximumContext = "131,072 tokens"
            };
            using (ConnectionInfoDialog dialog = new ConnectionInfoDialog(info, ThemePalette.Create(dark, accent)))
            {
                if (Math.Abs(scale - 1F) > 0.01F) dialog.Scale(new SizeF(scale, scale));
                dialog.ShowInTaskbar = false;
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-20000, -20000);
                dialog.Show();
                dialog.Location = new Point(-20000, -20000);
                Application.DoEvents();
                dialog.PerformLayout();
                Thread.Sleep(250);
                Application.DoEvents();
                List<string> problems = new List<string>();
                AuditVisibleBounds(dialog, problems);
                AuditDialogChrome(dialog, problems);
                Control keyControl = FindControlByName(dialog, "connectionValue3");
                CopyValueCard keyCard = keyControl as CopyValueCard;
                CopyValueCard providerCard = FindControlByName(dialog, "connectionValue0") as CopyValueCard;
                Control eye = FindControlByName(dialog, "toggleApiKeyVisibility");
                if (CountControls<CopyValueCard>(dialog) != 6)
                    problems.Add("connection info dialog does not expose all six copyable values");
                if (keyCard == null || keyCard.SecretRevealed || keyCard.CompleteValue != info.ApiKey)
                    problems.Add("connection info dialog does not keep the complete API Key masked by default");
                if (eye == null || string.IsNullOrWhiteSpace(eye.AccessibleName))
                    problems.Add("connection info dialog has no accessible API Key visibility control");
                if (providerCard == null || providerCard.CopyActionLabel != "点此复制")
                    problems.Add("connection copy action does not use the requested 点此复制 label");
                if (ContainsControlText(dialog, "左键单击任意一行即可复制完整内容，无需选中文本。"))
                    problems.Add("the removed connection helper subtitle is still visible");
                if (keyCard != null)
                {
                    Clipboard.Clear();
                    MethodInfo click = typeof(CopyValueCard).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (click == null) problems.Add("connection copy card does not expose a left-click interaction");
                    else
                    {
                        click.Invoke(keyCard, new object[] { EventArgs.Empty });
                        if (Clipboard.GetText() != info.ApiKey)
                            problems.Add("left-clicking a connection card does not copy the complete value");
                    }
                }
                if (problems.Count > 0)
                {
                    string message = "Connection info dialog audit failed:\r\n" + string.Join("\r\n", problems.ToArray());
                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-error.txt"), message, System.Text.Encoding.UTF8); } catch { }
                    Console.Error.WriteLine(message);
                    dialog.Close();
                    Environment.ExitCode = 2;
                    return;
                }
                dialog.Refresh();
                CaptureControl(dialog, output);
                dialog.Close();
            }
        }

        private static void RenderMessageDialog(string output, AppConfig config, float scale)
        {
            ThemePalette palette = DialogPalette(config);
            using (LlamaLiftMessageDialog dialog = new LlamaLiftMessageDialog(
                "当前端口仍被其他进程占用。请先关闭原 BAT / llama-server，等待显存与端口释放，或改用其他端口。",
                "端口已占用", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, palette))
            {
                if (Math.Abs(scale - 1F) > 0.01F) dialog.Scale(new SizeF(scale, scale));
                PrepareDialogForCapture(dialog);
                List<string> problems = new List<string>();
                AuditVisibleBounds(dialog, problems);
                AuditDialogChrome(dialog, problems);
                if (FindControlByName(dialog, "messageDialogBody") == null || dialog.AcceptButton == null || dialog.CancelButton == null)
                    problems.Add("themed message dialog is missing its body or keyboard actions");
                FinishDialogCapture(dialog, output, "Message dialog", problems);
            }
        }

        private static void RenderPromptDialog(string output, AppConfig config, float scale)
        {
            ThemePalette palette = DialogPalette(config);
            using (LlamaLiftPromptDialog dialog = new LlamaLiftPromptDialog("新配置名称", "新建模型配置", "我的 llama.cpp 服务", palette))
            {
                if (Math.Abs(scale - 1F) > 0.01F) dialog.Scale(new SizeF(scale, scale));
                PrepareDialogForCapture(dialog);
                List<string> problems = new List<string>();
                AuditVisibleBounds(dialog, problems);
                AuditDialogChrome(dialog, problems);
                Control input = FindControlByName(dialog, "promptInput");
                if (!(input is AntdUI.Input) || dialog.AcceptButton == null || dialog.CancelButton == null)
                    problems.Add("themed prompt dialog is missing its accessible input or keyboard actions");
                FinishDialogCapture(dialog, output, "Prompt dialog", problems);
            }
        }

        private static ThemePalette DialogPalette(AppConfig config)
        {
            bool dark = string.Equals(config.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase);
            Color accent = ThemeService.GetAccent(config.AccentName);
            if (dark && string.Equals(config.AccentName, "Blue", StringComparison.OrdinalIgnoreCase)) accent = Color.FromArgb(10, 132, 255);
            return ThemePalette.Create(dark, accent);
        }

        private static void PrepareDialogForCapture(Form dialog)
        {
            dialog.ShowInTaskbar = false;
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(-20000, -20000);
            dialog.Show();
            dialog.Location = new Point(-20000, -20000);
            Application.DoEvents();
            dialog.PerformLayout();
            Thread.Sleep(250);
            Application.DoEvents();
        }

        private static void FinishDialogCapture(Form dialog, string output, string name, List<string> problems)
        {
            if (problems.Count > 0)
            {
                string message = name + " audit failed:\r\n" + string.Join("\r\n", problems.ToArray());
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-error.txt"), message, System.Text.Encoding.UTF8); } catch { }
                Console.Error.WriteLine(message);
                dialog.Close();
                Environment.ExitCode = 2;
                return;
            }
            dialog.Refresh();
            CaptureControl(dialog, output);
            dialog.Close();
        }

        private static void CaptureControl(Control control, string output)
        {
            using (Bitmap bitmap = new Bitmap(control.Width, control.Height))
            {
                control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static void AuditVisibleBounds(Control parent, List<string> problems)
        {
            Rectangle allowed = parent.ClientRectangle;
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                Rectangle bounds = child.Bounds;
                Rectangle relaxed = allowed;
                relaxed.Inflate(2, 2);
                if (!relaxed.Contains(bounds)) problems.Add("control outside parent: " + child.GetType().Name + " '" + child.Text + "' " + bounds + " parent=" + allowed);
                AuditVisibleBounds(child, problems);
            }
        }

        private static void CaptureWindow(Form form, string output)
        {
            using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();
                bool rendered;
                try { rendered = PrintWindow(form.Handle, hdc, 2U); }
                finally { graphics.ReleaseHdc(hdc); }
                if (!rendered) throw new InvalidOperationException("native window capture failed");
                bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static List<string> Audit(MainFormV2 form)
        {
            List<string> problems = new List<string>();
            AuditProtocolAndStatusInteractions(form, problems);
            if (form.FormBorderStyle != FormBorderStyle.Sizable) problems.Add("window is not sizable");
            if (!form.ControlBox || !form.MinimizeBox || !form.MaximizeBox) problems.Add("native window controls are incomplete");
            Rectangle formClient = form.RectangleToScreen(form.ClientRectangle);
            string[] layoutFields = new string[] { "shellLayout", "mainLayout", "pageHost" };
            foreach (string layoutField in layoutFields)
            {
                FieldInfo layoutInfo = typeof(MainFormV2).GetField(layoutField, BindingFlags.Instance | BindingFlags.NonPublic);
                Control layout = layoutInfo == null ? null : layoutInfo.GetValue(form) as Control;
                if (layout != null && !formClient.Contains(layout.RectangleToScreen(layout.ClientRectangle)))
                    problems.Add("root layout exceeds the native client area: " + layoutField + " / layout=" + layout.RectangleToScreen(layout.ClientRectangle) + " / form=" + formClient);
            }
            Color expectedBackground = AntdUI.Config.IsDark ? Color.FromArgb(28, 28, 30) : Color.FromArgb(245, 245, 247);
            if (form.BackColor != expectedBackground) problems.Add("page background does not match the Apple neutral token: " + form.BackColor);

            FieldInfo sidebarField = typeof(MainFormV2).GetField("sidebar", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo brandTitleField = typeof(MainFormV2).GetField("lblBrandTitle", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo brandVersionField = typeof(MainFormV2).GetField("lblBrandVersion", BindingFlags.Instance | BindingFlags.NonPublic);
            Control sidebar = sidebarField.GetValue(form) as Control;
            Label brandTitle = brandTitleField.GetValue(form) as Label;
            Label brandVersion = brandVersionField.GetValue(form) as Label;
            FieldInfo sidebarStatusField = typeof(MainFormV2).GetField("lblSidebarServiceStatus", BindingFlags.Instance | BindingFlags.NonPublic);
            Label sidebarStatus = sidebarStatusField == null ? null : sidebarStatusField.GetValue(form) as Label;
            if (sidebar == null || brandTitle == null || brandVersion == null)
                problems.Add("responsive sidebar brand controls are missing");
            else
            {
                Rectangle sidebarScreen = sidebar.RectangleToScreen(sidebar.ClientRectangle);
                if (!sidebarScreen.Contains(brandTitle.RectangleToScreen(brandTitle.ClientRectangle)) ||
                    !sidebarScreen.Contains(brandVersion.RectangleToScreen(brandVersion.ClientRectangle)))
                    problems.Add("sidebar brand text is clipped by the sidebar: sidebar=" + sidebarScreen + ", title=" + brandTitle.RectangleToScreen(brandTitle.ClientRectangle) + ", version=" + brandVersion.RectangleToScreen(brandVersion.ClientRectangle));
                if (brandTitle.RectangleToScreen(brandTitle.ClientRectangle).IntersectsWith(brandVersion.RectangleToScreen(brandVersion.ClientRectangle)))
                    problems.Add("sidebar brand title overlaps version text");
                if (!string.Equals(brandTitle.Text, "LlamaLift", StringComparison.Ordinal))
                    problems.Add("product brand was not migrated to LlamaLift");
                if (brandVersion.Text.IndexOf("本地模型，一键起飞", StringComparison.Ordinal) < 0)
                    problems.Add("brand slogan is missing from the sidebar");
                if (sidebarStatus == null || sidebarStatus.Text.IndexOf("llama.cpp", StringComparison.OrdinalIgnoreCase) < 0 ||
                    sidebarStatus.Text.Contains("便携") || sidebarStatus.Text.Contains("安装"))
                    problems.Add("sidebar footer does not expose the real llama.cpp service status");
                else if (!sidebarScreen.Contains(sidebarStatus.RectangleToScreen(sidebarStatus.ClientRectangle)))
                    problems.Add("sidebar llama.cpp status is clipped: sidebar=" + sidebarScreen + ", status=" + sidebarStatus.RectangleToScreen(sidebarStatus.ClientRectangle));

                FieldInfo navButtonsField = typeof(MainFormV2).GetField("navButtons", BindingFlags.Instance | BindingFlags.NonPublic);
                IDictionary navButtons = navButtonsField == null ? null : navButtonsField.GetValue(form) as IDictionary;
                if (navButtons == null || navButtons.Count != 7)
                    problems.Add("sidebar navigation controls are missing");
                else
                {
                    int selectedCount = 0;
                    foreach (DictionaryEntry entry in navButtons)
                    {
                        AntdUI.Button navButton = entry.Value as AntdUI.Button;
                        if (navButton == null || navButton.Parent == null)
                        {
                            problems.Add("sidebar navigation is missing: " + entry.Key);
                            continue;
                        }
                        Rectangle navAllowed = navButton.Parent.RectangleToScreen(navButton.Parent.ClientRectangle);
                        navAllowed.Inflate(1, 1);
                        Rectangle navBounds = navButton.RectangleToScreen(navButton.ClientRectangle);
                        if (!navAllowed.Contains(navBounds))
                            problems.Add("sidebar navigation is clipped: " + entry.Key + " / allowed=" + navAllowed + " / button=" + navBounds);
                        if (navButton.Type != AntdUI.TTypeMini.Default)
                            problems.Add("sidebar navigation uses a filled primary style: " + entry.Key);
                        if (navButton.Font != null && navButton.Font.Bold) selectedCount++;
                    }
                    if (selectedCount != 1) problems.Add("expected one restrained selected navigation capsule, found " + selectedCount);
                }
            }

            FieldInfo field = typeof(MainFormV2).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary pages = field.GetValue(form) as IDictionary;
            if (pages == null || pages.Count != 7) problems.Add("expected seven application pages");
            else
            {
                foreach (DictionaryEntry entry in pages)
                {
                    ScrollableControl page = entry.Value as ScrollableControl;
                    if (page == null || (!page.AutoScroll && !HasScrollableChild(page))) problems.Add("page has no scrolling: " + entry.Key);
                    if (page != null && page.Parent != null && page.Width > page.Parent.ClientSize.Width + 3)
                        problems.Add("page exceeds its visible host width: " + entry.Key + " / page=" + page.Width + " / host=" + page.Parent.ClientSize.Width);
                }
            }

            FieldInfo profilePageField = typeof(MainFormV2).GetField("profilePage", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileScrollField = typeof(MainFormV2).GetField("profileScroll", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileCommandField = typeof(MainFormV2).GetField("profileCommandCard", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileColumnsField = typeof(MainFormV2).GetField("profileColumns", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileStackedField = typeof(MainFormV2).GetField("profileCardsStacked", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileFilesScrollField = typeof(MainFormV2).GetField("profileFilesScroll", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileRuntimeScrollField = typeof(MainFormV2).GetField("profileRuntimeScroll", BindingFlags.Instance | BindingFlags.NonPublic);
            Control profilePage = profilePageField.GetValue(form) as Control;
            ScrollableControl profileScroll = profileScrollField.GetValue(form) as ScrollableControl;
            Control profileCommand = profileCommandField.GetValue(form) as Control;
            Control profileColumns = profileColumnsField.GetValue(form) as Control;
            ScrollableControl profileFilesScroll = profileFilesScrollField == null ? null : profileFilesScrollField.GetValue(form) as ScrollableControl;
            ScrollableControl profileRuntimeScroll = profileRuntimeScrollField == null ? null : profileRuntimeScrollField.GetValue(form) as ScrollableControl;
            if (profilePage == null || profileScroll == null || profileCommand == null)
                problems.Add("profile fixed-layout controls are missing");
            else if (profilePage.Visible)
            {
                if (profileScroll.Parent != profilePage || profileCommand.Parent != profilePage)
                    problems.Add("profile scroll and command areas do not share the page container");
                if (profileScroll.Top < 0 || profileScroll.Bottom > profileCommand.Top)
                    problems.Add("profile scroll area overlaps the fixed command area: scroll=" + profileScroll.Bounds + ", command=" + profileCommand.Bounds);
                if (profileCommand.Top < 0 || profileCommand.Bottom > profilePage.ClientSize.Height + 2)
                    problems.Add("profile command area is outside the visible page: page=" + profilePage.ClientSize + ", command=" + profileCommand.Bounds);
                if (profileColumns == null || profileColumns.Width > profileScroll.ClientSize.Width + 4 || profileScroll.HorizontalScroll.Visible)
                    problems.Add("profile inputs overflow horizontally: scroll=" + profileScroll.ClientSize + ", columns=" + (profileColumns == null ? Rectangle.Empty : profileColumns.Bounds));
                bool stacked = profileStackedField != null && Convert.ToBoolean(profileStackedField.GetValue(form));
                if (sidebar != null && sidebar.Width > 300 && !stacked)
                    problems.Add("profile cards did not stack at high DPI");
                if (profileFilesScroll == null || profileRuntimeScroll == null || ReferenceEquals(profileFilesScroll, profileRuntimeScroll))
                    problems.Add("model files and runtime parameters do not have independent scroll regions");
                else
                {
                    if (!profileFilesScroll.AutoScroll || !profileRuntimeScroll.AutoScroll)
                        problems.Add("independent profile scroll regions are disabled");
                    if (!stacked && profileScroll.AutoScroll)
                        problems.Add("wide profile layout still uses the shared outer scrollbar");
                }
            }

            FieldInfo processMetricField = typeof(MainFormV2).GetField("lblProcessMetric", BindingFlags.Instance | BindingFlags.NonPublic);
            Label processMetric = processMetricField == null ? null : processMetricField.GetValue(form) as Label;
            Control metricGrid = processMetric == null || processMetric.Parent == null || processMetric.Parent.Parent == null
                ? null : processMetric.Parent.Parent.Parent;
            if (metricGrid == null || !string.Equals(metricGrid.Tag as string, "background", StringComparison.OrdinalIgnoreCase) || metricGrid.BackColor != expectedBackground)
                problems.Add("rounded dashboard metrics have a square surface-colored backing layer");

            FieldInfo startField = typeof(MainFormV2).GetField("btnStart", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo stopField = typeof(MainFormV2).GetField("btnStop", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo restartField = typeof(MainFormV2).GetField("btnRestart", BindingFlags.Instance | BindingFlags.NonPublic);
            Control startButton = startField == null ? null : startField.GetValue(form) as Control;
            Control stopButton = stopField == null ? null : stopField.GetValue(form) as Control;
            Control restartButton = restartField == null ? null : restartField.GetValue(form) as Control;
            if (startButton == null || stopButton == null || restartButton == null ||
                startButton.Parent == null || !ReferenceEquals(startButton.Parent, stopButton.Parent) || !ReferenceEquals(startButton.Parent, restartButton.Parent))
                problems.Add("dashboard lifecycle action row is incomplete");
            else if (startButton.Parent.Visible)
            {
                Rectangle restartBounds = restartButton.RectangleToScreen(restartButton.ClientRectangle);
                Rectangle stopBounds = stopButton.RectangleToScreen(stopButton.ClientRectangle);
                Rectangle startBounds = startButton.RectangleToScreen(startButton.ClientRectangle);
                Rectangle actionBounds = startButton.Parent.RectangleToScreen(startButton.Parent.ClientRectangle);
                if (Math.Abs(restartBounds.Top - stopBounds.Top) > 2 || Math.Abs(stopBounds.Top - startBounds.Top) > 2)
                    problems.Add("dashboard lifecycle actions wrapped instead of staying on one row");
                if (!(restartBounds.Left < stopBounds.Left && stopBounds.Left < startBounds.Left))
                    problems.Add("dashboard lifecycle actions are not ordered restart, stop, start from left to right");
                if (restartBounds.IntersectsWith(stopBounds) || stopBounds.IntersectsWith(startBounds) || !actionBounds.Contains(restartBounds) || !actionBounds.Contains(stopBounds) || !actionBounds.Contains(startBounds))
                    problems.Add("dashboard lifecycle actions overlap or exceed their container");
            }
            if (FindControl(form, "连接信息") == null)
                problems.Add("dashboard has no way to reopen the connection information dialog");

            FieldInfo commandPageField = typeof(MainFormV2).GetField("commandPage", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo commandEditorField = typeof(MainFormV2).GetField("txtCommandEditor", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo parameterPresetField = typeof(MainFormV2).GetField("cmbParameterPreset", BindingFlags.Instance | BindingFlags.NonPublic);
            Control commandPage = commandPageField.GetValue(form) as Control;
            RichTextBox commandEditor = commandEditorField.GetValue(form) as RichTextBox;
            AntdUI.Select parameterPreset = parameterPresetField.GetValue(form) as AntdUI.Select;
            if (commandPage == null || commandEditor == null || parameterPreset == null)
                problems.Add("parameter workspace controls are missing");
            else
            {
                if (commandEditor.ReadOnly || !commandEditor.AcceptsTab) problems.Add("command editor is not in editable advanced mode");
                if (parameterPreset.Items.Count < 3) problems.Add("expected at least three parameter presets");
                string monoName = commandEditor.Font == null ? string.Empty : commandEditor.Font.Name;
                if (!string.Equals(monoName, "Cascadia Mono", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(monoName, "Cascadia Code", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(monoName, "Consolas", StringComparison.OrdinalIgnoreCase))
                    problems.Add("command editor has no offline monospace font fallback: " + monoName);
                if (FindControl(commandPage, "校验并保存") == null)
                    problems.Add("parameter workspace has no save-time preflight action");
            }

            Control settingsPage = pages == null ? null : pages["settings"] as Control;
            if (settingsPage != null && FindControl(settingsPage, "管理 API Key") == null)
                problems.Add("settings page has no API Key management entry");
            if (settingsPage != null && FindLabel(settingsPage, "\u8fd0\u884c\u6a21\u5f0f") != null)
                problems.Add("settings page still exposes the removed install/portable mode label");

            Control monitoringPage = pages == null ? null : pages["monitoring"] as Control;
            if (monitoringPage == null || CountControls<RealtimeMetricChart>(monitoringPage) < 8)
                problems.Add("performance monitoring page has fewer than eight realtime charts");
            if (monitoringPage != null && FindControl(monitoringPage, "暂停监测") == null)
                problems.Add("performance monitoring page has no pause control");

            int visiblePrimaryActions = CountVisiblePrimaryButtons(form);
            if (visiblePrimaryActions > 1) problems.Add("visible page has more than one primary action: " + visiblePrimaryActions);

            FieldInfo logsPageField = typeof(MainFormV2).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary allPages = logsPageField == null ? null : logsPageField.GetValue(form) as IDictionary;
            Control logsPage = allPages == null ? null : allPages["logs"] as Control;
            if (logsPage != null && logsPage.Visible)
            {
                if (FindLabel(logsPage, "运行日志") != null)
                    problems.Add("log page repeats the global page title inside the content card");
            }

            if (profilePage != null && profilePage.Visible)
            {
                string[] inputFields = new string[] { "txtProfileName", "txtServerExe", "txtModel", "txtMmproj", "txtAlias", "txtApiKeyFile", "txtChatTemplate", "txtHost", "txtAdvertisedHost", "txtGpuLayers", "txtExtraArgs" };
                foreach (string inputField in inputFields)
                {
                    FieldInfo inputInfo = typeof(MainFormV2).GetField(inputField, BindingFlags.Instance | BindingFlags.NonPublic);
                    Control input = inputInfo == null ? null : inputInfo.GetValue(form) as Control;
                    if (input == null || input.Width < 120) problems.Add("profile input is hidden or too narrow: " + inputField);
                }
                if (FindControl(profilePage, "检测") == null)
                    problems.Add("profile page has no automatic llama-server detection action");
                FieldInfo protocolField = typeof(MainFormV2).GetField("cmbApiProtocol", BindingFlags.Instance | BindingFlags.NonPublic);
                AntdUI.Select protocol = protocolField == null ? null : protocolField.GetValue(form) as AntdUI.Select;
                if (protocol == null || protocol.Items.Count != 3 || protocol.Width < 120)
                    problems.Add("profile page does not provide all three API protocol choices");

                FieldInfo toolTipField = typeof(MainFormV2).GetField("parameterToolTip", BindingFlags.Instance | BindingFlags.NonPublic);
                ToolTip parameterToolTip = toolTipField == null ? null : toolTipField.GetValue(form) as ToolTip;
                string[] parameterLabels = new string[]
                {
                    "自适应", "监听地址", "公开地址", "端口", "上下文", "并发数", "GPU 层",
                    "KV Cache K", "KV Cache V", "Fit 余量 MB", "图片 tokens", "CPU 线程", "Batch",
                    "Ubatch", "推理解析", "自动 Fit", "Flash Attention", "启用 Jinja", "禁用 WebUI",
                    "No mmap", "Mlock", "性能指标", "自定义参数"
                };
                if (parameterToolTip == null || profileRuntimeScroll == null)
                    problems.Add("runtime parameter tooltip service is missing");
                else
                    foreach (string parameterLabel in parameterLabels)
                    {
                        Label label = FindLabel(profileRuntimeScroll, parameterLabel);
                        if (label == null || string.IsNullOrWhiteSpace(parameterToolTip.GetToolTip(label)))
                            problems.Add("runtime parameter has no beginner tooltip: " + parameterLabel);
                    }
            }

            AuditChildren(form, problems);
            return problems;
        }

        private static void AuditProtocolAndStatusInteractions(MainFormV2 form, List<string> problems)
        {
            FieldInfo profileField = typeof(MainFormV2).GetField("currentProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo protocolField = typeof(MainFormV2).GetField("cmbApiProtocol", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo commandField = typeof(MainFormV2).GetField("txtCommand", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo loadingField = typeof(MainFormV2).GetField("loadingControls", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo stateField = typeof(MainFormV2).GetField("localModelUiState", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo sidebarStatusField = typeof(MainFormV2).GetField("lblSidebarServiceStatus", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo updatePreview = typeof(MainFormV2).GetMethod("UpdateCommandPreview", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo updateSummary = typeof(MainFormV2).GetMethod("UpdateDashboardSummary", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo setState = typeof(MainFormV2).GetMethod("SetLocalModelState", BindingFlags.Instance | BindingFlags.NonPublic);
            ModelProfile profile = profileField == null ? null : profileField.GetValue(form) as ModelProfile;
            AntdUI.Select protocol = protocolField == null ? null : protocolField.GetValue(form) as AntdUI.Select;
            AntdUI.Input command = commandField == null ? null : commandField.GetValue(form) as AntdUI.Input;
            Label sidebarStatus = sidebarStatusField == null ? null : sidebarStatusField.GetValue(form) as Label;
            if (profile == null || protocol == null || command == null || loadingField == null)
            {
                problems.Add("protocol interaction audit could not access the model controls");
                return;
            }

            bool originalUseCustom = profile.UseCustomCommand;
            string originalCustom = profile.CustomCommand;
            string originalProtocol = profile.ApiProtocol;
            int originalSelectedIndex = protocol.SelectedIndex;
            string originalCommandText = command.Text;
            object originalState = stateField == null ? null : stateField.GetValue(form);
            try
            {
                profile.UseCustomCommand = true;
                profile.CustomCommand = originalCommandText;
                protocol.SelectedIndex = 1;
                Application.DoEvents();
                if (profile.ApiProtocol != ApiProtocolMode.ChatCompletions)
                    problems.Add("switching to Chat Completions does not synchronize the profile");
                if (!profile.UseCustomCommand || command.Text != originalCommandText)
                    problems.Add("switching API protocols discards or rewrites the custom llama-server command");

                protocol.SelectedIndex = 2;
                Application.DoEvents();
                if (profile.ApiProtocol != ApiProtocolMode.AnthropicMessages ||
                    LlamaApiClient.ProtocolEndpointUrl(profile).IndexOf("/v1/messages", StringComparison.Ordinal) < 0)
                    problems.Add("switching to Anthropic Messages does not produce the correct endpoint");

                if (setState != null && stateField != null && sidebarStatus != null)
                {
                    object generating = Enum.Parse(stateField.FieldType, "Generating");
                    setState.Invoke(form, new object[] { generating });
                    if (sidebarStatus.Text.IndexOf("输出中", StringComparison.Ordinal) < 0 ||
                        sidebarStatus.AccessibleName.IndexOf("输出中", StringComparison.Ordinal) < 0)
                        problems.Add("the sidebar does not expose the live generating state accessibly");
                }
            }
            finally
            {
                loadingField.SetValue(form, true);
                profile.UseCustomCommand = originalUseCustom;
                profile.CustomCommand = originalCustom;
                profile.ApiProtocol = originalProtocol;
                protocol.SelectedIndex = originalSelectedIndex;
                loadingField.SetValue(form, false);
                if (updatePreview != null) updatePreview.Invoke(form, null);
                if (updateSummary != null) updateSummary.Invoke(form, null);
                if (setState != null && originalState != null) setState.Invoke(form, new object[] { originalState });
                Application.DoEvents();
            }
        }

        private static bool ScrollPageToBottom(MainFormV2 form, string pageKey)
        {
            if (string.Equals(pageKey, "logs", StringComparison.OrdinalIgnoreCase))
            {
                FieldInfo logsField = typeof(MainFormV2).GetField("txtLogs", BindingFlags.Instance | BindingFlags.NonPublic);
                RichTextBox logs = logsField == null ? null : logsField.GetValue(form) as RichTextBox;
                if (logs == null) return false;
                logs.SelectionStart = logs.TextLength;
                logs.ScrollToCaret();
                return true;
            }
            FieldInfo field = typeof(MainFormV2).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary pages = field.GetValue(form) as IDictionary;
            ScrollableControl page = pages == null ? null : pages[pageKey] as ScrollableControl;
            if (string.Equals(pageKey, "profiles", StringComparison.OrdinalIgnoreCase))
            {
                FieldInfo scrollField = typeof(MainFormV2).GetField("profileScroll", BindingFlags.Instance | BindingFlags.NonPublic);
                ScrollableControl inner = scrollField.GetValue(form) as ScrollableControl;
                if (inner != null) page = inner;
            }
            if (page == null || !page.AutoScroll) return false;
            int before = page.AutoScrollPosition.Y;
            for (int i = 0; i < 3; i++)
            {
                page.AutoScrollPosition = new Point(0, Int32.MaxValue);
                page.PerformLayout();
                Application.DoEvents();
            }
            return page.AutoScrollPosition.Y != before;
        }

        private static bool HasScrollableChild(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                RichTextBox richText = child as RichTextBox;
                if (richText != null && richText.ScrollBars != RichTextBoxScrollBars.None) return true;
                ScrollableControl scrollable = child as ScrollableControl;
                if (scrollable != null && scrollable.AutoScroll) return true;
                if (child.HasChildren && HasScrollableChild(child)) return true;
            }
            return false;
        }

        private static void AuditChildren(Control parent, List<string> problems)
        {
            AntdUI.Panel roundedPanel = parent as AntdUI.Panel;
            if (roundedPanel != null && roundedPanel.Radius > 0 && roundedPanel.BackColor.A != 0)
                problems.Add("rounded panel has an opaque rectangular backing: " + Trim(parent.Text));
            ScrollableControl parentScrollable = parent as ScrollableControl;
            if (parentScrollable != null && parentScrollable.Visible && parentScrollable.HorizontalScroll.Visible)
                problems.Add("horizontal scrollbar is visible: " + parent.GetType().Name + " / " + Trim(parent.Text));
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                ScrollableControl scrollable = parent as ScrollableControl;
                bool canScroll = scrollable != null && scrollable.AutoScroll;
                if (!canScroll && child.Dock == DockStyle.None)
                {
                    Rectangle allowed = new Rectangle(-3, -3, parent.ClientSize.Width + 6, parent.ClientSize.Height + 6);
                    if (!allowed.Contains(child.Bounds))
                        problems.Add("control clipped by parent: " + child.GetType().Name + " / " + Trim(child.Text) + " / parent=" + parent.ClientRectangle + " / child=" + child.Bounds);
                }
                Label label = child as Label;
                if (label != null && !label.AutoSize && label.Height > 0)
                {
                    Size preferred = label.GetPreferredSize(new Size(Math.Max(1, label.Width), 0));
                    if (preferred.Height > label.Height + 3)
                        problems.Add("label text clipped: " + Trim(label.Text));
                }
                if (child.HasChildren) AuditChildren(child, problems);
            }
        }

        private static Label FindLabel(Control parent, string text)
        {
            foreach (Control child in parent.Controls)
            {
                Label label = child as Label;
                if (label != null && string.Equals(label.Text, text, StringComparison.Ordinal)) return label;
                if (child.HasChildren)
                {
                    Label nested = FindLabel(child, text);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static Control FindControl(Control parent, string text)
        {
            if (parent == null) return null;
            foreach (Control child in parent.Controls)
            {
                if (string.Equals(child.Text, text, StringComparison.Ordinal)) return child;
                Control nested = FindControl(child, text);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void AuditDialogChrome(Form dialog, List<string> problems)
        {
            Control bar = FindControlByName(dialog, "dialogCustomTitleBar");
            Control title = FindControlByName(dialog, "dialogCustomTitle");
            Control close = FindControlByName(dialog, "dialogCustomClose");
            Control logo = FindControlByName(dialog, "dialogBrandLogo");
            if (dialog.FormBorderStyle != FormBorderStyle.None)
                problems.Add("dialog still uses the Windows default title bar");
            if (bar == null || bar.Height < 40 || title == null || close == null)
                problems.Add("dialog custom title bar is incomplete");
            if (!(logo is DialogBrandMark) || !DialogBrandMark.OfficialLogoAvailable)
                problems.Add("dialog title bar does not use the embedded official LlamaLift logo");
            if (close != null && (string.IsNullOrWhiteSpace(close.AccessibleName) || close.Cursor != Cursors.Hand))
                problems.Add("dialog custom close button is not accessible or visibly interactive");
            if (dialog.Region == null)
                problems.Add("borderless dialog has no rounded window region");
        }

        private static bool ContainsControlText(Control parent, string value)
        {
            if (string.Equals(parent.Text, value, StringComparison.Ordinal)) return true;
            foreach (Control child in parent.Controls)
                if (ContainsControlText(child, value)) return true;
            return false;
        }

        private static Control FindControlByName(Control parent, string name)
        {
            if (parent == null) return null;
            if (string.Equals(parent.Name, name, StringComparison.Ordinal)) return parent;
            foreach (Control child in parent.Controls)
            {
                Control nested = FindControlByName(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        private static int CountVisiblePrimaryButtons(Control parent)
        {
            int count = 0;
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                AntdUI.Button button = child as AntdUI.Button;
                if (button != null && button.Type == AntdUI.TTypeMini.Primary) count++;
                if (child.HasChildren) count += CountVisiblePrimaryButtons(child);
            }
            return count;
        }

        private static int CountControls<T>(Control parent) where T : Control
        {
            int count = parent is T ? 1 : 0;
            foreach (Control child in parent.Controls) count += CountControls<T>(child);
            return count;
        }

        private static string Trim(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "(no text)";
            value = value.Replace("\r", " ").Replace("\n", " ");
            return value.Length <= 40 ? value : value.Substring(0, 40);
        }
    }
}
