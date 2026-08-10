using System;
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
            ConfigStore.Save(config);
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
                Thread.Sleep(350);
                Application.DoEvents();
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

            FieldInfo field = typeof(MainFormV2).GetField("pages", BindingFlags.Instance | BindingFlags.NonPublic);
            IDictionary pages = field.GetValue(form) as IDictionary;
            if (pages == null || pages.Count != 5) problems.Add("expected five application pages");
            else
            {
                foreach (DictionaryEntry entry in pages)
                {
                    ScrollableControl page = entry.Value as ScrollableControl;
                    if (page == null || (!page.AutoScroll && !HasScrollableChild(page))) problems.Add("page has no scrolling: " + entry.Key);
                }
            }

            FieldInfo profilePageField = typeof(MainFormV2).GetField("profilePage", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileScrollField = typeof(MainFormV2).GetField("profileScroll", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo profileCommandField = typeof(MainFormV2).GetField("profileCommandCard", BindingFlags.Instance | BindingFlags.NonPublic);
            Control profilePage = profilePageField.GetValue(form) as Control;
            Control profileScroll = profileScrollField.GetValue(form) as Control;
            Control profileCommand = profileCommandField.GetValue(form) as Control;
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
            }

            AuditChildren(form, problems);
            return problems;
        }

        private static bool ScrollPageToBottom(MainFormV2 form, string pageKey)
        {
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
                ScrollableControl scrollable = child as ScrollableControl;
                if (scrollable != null && scrollable.AutoScroll) return true;
                if (child.HasChildren && HasScrollableChild(child)) return true;
            }
            return false;
        }

        private static void AuditChildren(Control parent, List<string> problems)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible) continue;
                ScrollableControl scrollable = parent as ScrollableControl;
                bool canScroll = scrollable != null && scrollable.AutoScroll;
                if (!canScroll && child.Dock == DockStyle.None)
                {
                    Rectangle allowed = new Rectangle(-3, -3, parent.ClientSize.Width + 6, parent.ClientSize.Height + 6);
                    if (!allowed.Contains(child.Bounds))
                        problems.Add("control clipped by parent: " + child.GetType().Name + " / " + Trim(child.Text));
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

        private static string Trim(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "(no text)";
            value = value.Replace("\r", " ").Replace("\n", " ");
            return value.Length <= 40 ? value : value.Substring(0, 40);
        }
    }
}
