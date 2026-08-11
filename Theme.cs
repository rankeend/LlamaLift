using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace LlamaServerManager
{
    public sealed class ThemePalette
    {
        public Color Background { get; private set; }
        public Color Surface { get; private set; }
        public Color SurfaceAlt { get; private set; }
        public Color Sidebar { get; private set; }
        public Color SidebarSelected { get; private set; }
        public Color SidebarHover { get; private set; }
        public Color Border { get; private set; }
        public Color Text { get; private set; }
        public Color Muted { get; private set; }
        public Color LogBackground { get; private set; }
        public Color LogText { get; private set; }
        public Color Accent { get; private set; }
        public Color Success { get; private set; }
        public Color Warning { get; private set; }
        public Color Danger { get; private set; }
        public bool IsDark { get; private set; }

        public static ThemePalette Create(bool dark, Color accent)
        {
            ThemePalette value = new ThemePalette();
            value.IsDark = dark;
            value.Accent = accent;
            value.Success = Color.FromArgb(48, 184, 90);
            value.Warning = Color.FromArgb(255, 159, 10);
            value.Danger = Color.FromArgb(255, 69, 58);
            if (dark)
            {
                value.Background = Color.FromArgb(28, 28, 30);
                value.Surface = Color.FromArgb(36, 36, 38);
                value.SurfaceAlt = Color.FromArgb(44, 44, 46);
                value.Sidebar = Color.FromArgb(32, 32, 34);
                value.SidebarSelected = Color.FromArgb(58, 58, 60);
                value.SidebarHover = Color.FromArgb(48, 48, 50);
                value.Border = Color.FromArgb(58, 58, 60);
                value.Text = Color.FromArgb(245, 245, 247);
                value.Muted = Color.FromArgb(161, 161, 166);
                value.LogBackground = Color.FromArgb(20, 20, 22);
                value.LogText = Color.FromArgb(222, 222, 227);
            }
            else
            {
                value.Background = Color.FromArgb(245, 245, 247);
                value.Surface = Color.White;
                value.SurfaceAlt = Color.FromArgb(250, 250, 252);
                value.Sidebar = Color.FromArgb(238, 238, 242);
                value.SidebarSelected = Color.FromArgb(255, 255, 255);
                value.SidebarHover = Color.FromArgb(246, 246, 248);
                value.Border = Color.FromArgb(210, 210, 215);
                value.Text = Color.FromArgb(29, 29, 31);
                value.Muted = Color.FromArgb(110, 110, 115);
                value.LogBackground = Color.FromArgb(28, 28, 30);
                value.LogText = Color.FromArgb(235, 235, 240);
            }
            return value;
        }
    }

    public static class ThemeService
    {
        private static readonly Dictionary<string, Color> Accents = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Blue", Color.FromArgb(0, 122, 255) },
            { "Emerald", Color.FromArgb(48, 184, 90) },
            { "Violet", Color.FromArgb(175, 82, 222) },
            { "Orange", Color.FromArgb(255, 149, 0) },
            { "Rose", Color.FromArgb(255, 55, 95) }
        };

        public static string[] AccentNames
        {
            get { return new string[] { "Blue", "Emerald", "Violet", "Orange", "Rose" }; }
        }

        public static Color GetAccent(string name)
        {
            Color value;
            return !string.IsNullOrWhiteSpace(name) && Accents.TryGetValue(name, out value)
                ? value
                : Accents["Blue"];
        }

        public static ThemePalette Apply(AppConfig config, Form form)
        {
            AntdUI.TMode mode = IsSystemDark() ? AntdUI.TMode.Dark : AntdUI.TMode.Light;
            if (string.Equals(config.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase)) mode = AntdUI.TMode.Dark;
            else if (string.Equals(config.ThemeMode, "Light", StringComparison.OrdinalIgnoreCase)) mode = AntdUI.TMode.Light;

            AntdUI.Config.Mode = mode;
            Color accent = GetAccent(config.AccentName);
            if (mode == AntdUI.TMode.Dark && string.Equals(config.AccentName, "Blue", StringComparison.OrdinalIgnoreCase))
                accent = Color.FromArgb(10, 132, 255);
            AntdUI.Style.SetPrimary(accent);
            AntdUI.Style.SetInfo(accent);

            ThemePalette palette = ThemePalette.Create(AntdUI.Config.IsDark, accent);
            form.BackColor = palette.Background;
            form.ForeColor = palette.Text;
            ApplyControl(form, palette);
            ApplyNativeTitleBar(form, palette.IsDark);
            form.Invalidate(true);
            return palette;
        }

        private static bool IsSystemDark()
        {
            try
            {
                object value = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", 1);
                return Convert.ToInt32(value) == 0;
            }
            catch { return false; }
        }

        private static void ApplyControl(Control control, ThemePalette palette)
        {
            string role = control.Tag as string;

            AntdUI.Button antButton = control as AntdUI.Button;
            if (antButton != null && antButton.Type == AntdUI.TTypeMini.Default)
            {
                antButton.DefaultBack = palette.SurfaceAlt;
                antButton.DefaultBorderColor = palette.Border;
                antButton.ForeColor = palette.Text;
                antButton.ForeHover = palette.Text;
                antButton.ForeActive = palette.Text;
                antButton.BackHover = Blend(palette.SurfaceAlt, palette.Accent, 0.08F);
                antButton.BackActive = Blend(palette.SurfaceAlt, palette.Accent, 0.15F);
                antButton.WaveSize = 2;
                if (role == "danger-action")
                {
                    antButton.ForeColor = palette.Danger;
                    antButton.ForeHover = palette.Danger;
                    antButton.ForeActive = palette.Danger;
                    antButton.DefaultBorderColor = Blend(palette.Border, palette.Danger, 0.35F);
                    antButton.BackHover = Blend(palette.SurfaceAlt, palette.Danger, 0.08F);
                    antButton.BackActive = Blend(palette.SurfaceAlt, palette.Danger, 0.15F);
                }
                else if (role == "warning-action")
                {
                    antButton.ForeColor = palette.Warning;
                    antButton.ForeHover = palette.Warning;
                    antButton.ForeActive = palette.Warning;
                    antButton.DefaultBorderColor = Blend(palette.Border, palette.Warning, 0.35F);
                    antButton.BackHover = Blend(palette.SurfaceAlt, palette.Warning, 0.08F);
                    antButton.BackActive = Blend(palette.SurfaceAlt, palette.Warning, 0.15F);
                }
            }

            AntdUI.Input input = control as AntdUI.Input;
            if (input != null)
            {
                input.BackColor = palette.SurfaceAlt;
                input.ForeColor = palette.Text;
                input.BorderColor = palette.Border;
                input.BorderHover = Blend(palette.Border, palette.Accent, 0.45F);
                input.BorderActive = palette.Accent;
                input.PlaceholderColor = palette.Muted;
                input.CaretColor = palette.Accent;
                input.SelectionColor = Blend(palette.SurfaceAlt, palette.Accent, 0.28F);
                input.WaveSize = 2;
            }

            Label label = control as Label;
            if (label != null)
            {
                label.ForeColor = role == "muted" ? palette.Muted : palette.Text;
                label.BackColor = Color.Transparent;
            }

            RichTextBox log = control as RichTextBox;
            if (log != null)
            {
                log.BackColor = palette.LogBackground;
                log.ForeColor = palette.LogText;
                log.BorderStyle = BorderStyle.None;
            }

            ProgressBar progress = control as ProgressBar;
            if (progress != null)
            {
                progress.BackColor = palette.SurfaceAlt;
                progress.ForeColor = palette.Accent;
            }

            ComboBox combo = control as ComboBox;
            if (combo != null)
            {
                combo.BackColor = palette.SurfaceAlt;
                combo.ForeColor = palette.Text;
                combo.FlatStyle = FlatStyle.Flat;
            }

            AntdUI.Panel antPanel = control as AntdUI.Panel;
            if (antPanel != null)
            {
                Color panelBack = role == "sidebar" ? palette.Sidebar : role == "surface-alt" ? palette.SurfaceAlt : role == "background" ? palette.Background : palette.Surface;
                antPanel.Back = panelBack;
                // AntdUI paints the rounded surface through Back. Keeping the native
                // WinForms backing transparent prevents square pixels behind the radius.
                antPanel.BackColor = Color.Transparent;
                antPanel.BorderColor = palette.Border;
            }
            else
            {
                System.Windows.Forms.Panel panel = control as System.Windows.Forms.Panel;
                TableLayoutPanel table = control as TableLayoutPanel;
                FlowLayoutPanel flow = control as FlowLayoutPanel;
                if (panel != null || table != null || flow != null)
                {
                    if (role == "sidebar") control.BackColor = palette.Sidebar;
                    else if (role == "surface") control.BackColor = palette.Surface;
                    else if (role == "surface-alt") control.BackColor = palette.SurfaceAlt;
                    else if (role == "background" || control.Parent == null) control.BackColor = palette.Background;
                    else if (control.Parent is AntdUI.Panel) control.BackColor = Color.Transparent;
                    else control.BackColor = control.Parent.BackColor;
                }
            }

            foreach (Control child in control.Controls)
                ApplyControl(child, palette);
        }

        private static Color Blend(Color background, Color foreground, float amount)
        {
            amount = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                Convert.ToInt32(background.R + (foreground.R - background.R) * amount),
                Convert.ToInt32(background.G + (foreground.G - background.G) * amount),
                Convert.ToInt32(background.B + (foreground.B - background.B) * amount));
        }

        public static Color Mix(Color background, Color foreground, float amount)
        {
            return Blend(background, foreground, amount);
        }

        internal static void ApplyNativeTitleBar(Form form, bool dark)
        {
            if (Environment.OSVersion.Version.Major < 10) return;
            try
            {
                int enabled = dark ? 1 : 0;
                int result = DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
                if (result != 0) DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
