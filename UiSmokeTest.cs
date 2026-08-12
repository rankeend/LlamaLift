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
            if (args.Length > 2 && string.Equals(args[2], "api-keys", StringComparison.OrdinalIgnoreCase))
            {
                RenderApiKeyDialog(output, config);
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
                    throw new InvalidOperationException("UI audit failed:\r\n" + string.Join("\r\n", problems.ToArray()));
                CaptureWindow(form, output);
                form.Close();
            }
            Console.WriteLine("AUDIT PASS: " + output);
        }

        private static void RenderApiKeyDialog(string output, AppConfig config)
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
                    FieldInfo secretField = typeof(ApiKeyManagerDialog).GetField("txtKeys", BindingFlags.Instance | BindingFlags.NonPublic);
                    TextBox secretBox = secretField == null ? null : secretField.GetValue(dialog) as TextBox;
                    if (secretBox == null || !secretBox.ReadOnly || secretBox.Text.Contains("test-only-secret"))
                        problems.Add("API Key dialog does not mask secrets by default");
                    if (problems.Count > 0)
                        throw new InvalidOperationException("API Key dialog audit failed:\r\n" + string.Join("\r\n", problems.ToArray()));
                    dialog.Refresh();
                    CaptureControl(dialog, output);
                    dialog.Close();
                }
            }
            finally { try { store.Delete(testKey.FilePath); } catch { } }
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
                string[] inputFields = new string[] { "txtProfileName", "txtServerExe", "txtModel", "txtMmproj", "txtAlias", "txtApiKeyFile", "txtHost", "txtAdvertisedHost", "txtGpuLayers", "txtExtraArgs" };
                foreach (string inputField in inputFields)
                {
                    FieldInfo inputInfo = typeof(MainFormV2).GetField(inputField, BindingFlags.Instance | BindingFlags.NonPublic);
                    Control input = inputInfo == null ? null : inputInfo.GetValue(form) as Control;
                    if (input == null || input.Width < 120) problems.Add("profile input is hidden or too narrow: " + inputField);
                }
                if (FindControl(profilePage, "检测") == null)
                    problems.Add("profile page has no automatic llama-server detection action");
            }

            AuditChildren(form, problems);
            return problems;
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
