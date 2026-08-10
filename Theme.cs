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
            value.Success = Color.FromArgb(34, 197, 94);
            value.Warning = Color.FromArgb(245, 158, 11);
            value.Danger = Color.FromArgb(239, 68, 68);
            if (dark)
            {
                value.Background = Color.FromArgb(15, 18, 25);
                value.Surface = Color.FromArgb(24, 28, 38);
                value.SurfaceAlt = Color.FromArgb(31, 36, 48);
                value.Sidebar = Color.FromArgb(18, 22, 31);
                value.Border = Color.FromArgb(48, 55, 70);
                value.Text = Color.FromArgb(239, 242, 248);
                value.Muted = Color.FromArgb(151, 162, 181);
                value.LogBackground = Color.FromArgb(10, 13, 19);
                value.LogText = Color.FromArgb(197, 209, 226);
            }
            else
            {
                value.Background = Color.FromArgb(244, 247, 251);
                value.Surface = Color.White;
                value.SurfaceAlt = Color.FromArgb(248, 250, 253);
                value.Sidebar = Color.FromArgb(250, 252, 255);
                value.Border = Color.FromArgb(220, 226, 236);
                value.Text = Color.FromArgb(24, 31, 43);
                value.Muted = Color.FromArgb(101, 113, 132);
                value.LogBackground = Color.FromArgb(249, 251, 254);
                value.LogText = Color.FromArgb(45, 57, 73);
            }
            return value;
        }
    }

    public static class ThemeService
    {
        private static readonly Dictionary<string, Color> Accents = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Emerald", Color.FromArgb(16, 185, 129) },
            { "Blue", Color.FromArgb(59, 130, 246) },
            { "Violet", Color.FromArgb(139, 92, 246) },
            { "Orange", Color.FromArgb(249, 115, 22) },
            { "Rose", Color.FromArgb(244, 63, 94) }
        };

        public static string[] AccentNames
        {
            get { return new string[] { "Emerald", "Blue", "Violet", "Orange", "Rose" }; }
        }

        public static Color GetAccent(string name)
        {
            Color value;
            return !string.IsNullOrWhiteSpace(name) && Accents.TryGetValue(name, out value)
                ? value
                : Accents["Emerald"];
        }

        public static ThemePalette Apply(AppConfig config, Form form)
        {
            AntdUI.TMode mode = IsSystemDark() ? AntdUI.TMode.Dark : AntdUI.TMode.Light;
            if (string.Equals(config.ThemeMode, "Dark", StringComparison.OrdinalIgnoreCase)) mode = AntdUI.TMode.Dark;
            else if (string.Equals(config.ThemeMode, "Light", StringComparison.OrdinalIgnoreCase)) mode = AntdUI.TMode.Light;

            AntdUI.Config.Mode = mode;
            Color accent = GetAccent(config.AccentName);
            AntdUI.Style.SetPrimary(accent);
            AntdUI.Style.SetInfo(accent);

            ThemePalette palette = ThemePalette.Create(AntdUI.Config.IsDark, accent);
            ApplyControl(form, palette);
            form.BackColor = palette.Background;
            form.ForeColor = palette.Text;
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
                antButton.BackHover = Blend(palette.SurfaceAlt, palette.Accent, 0.16F);
                antButton.BackActive = Blend(palette.SurfaceAlt, palette.Accent, 0.25F);
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
                antPanel.Back = role == "sidebar" ? palette.Sidebar : role == "surface-alt" ? palette.SurfaceAlt : palette.Surface;
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
                    else control.BackColor = palette.Background;
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

        private static void ApplyNativeTitleBar(Form form, bool dark)
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
