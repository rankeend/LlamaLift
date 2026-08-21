using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AButton = AntdUI.Button;
using AInput = AntdUI.Input;

namespace LlamaServerManager
{
    public class LlamaLiftDialogForm : Form
    {
        private const int WmNcLButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;
        private bool chromeInstalled;
        private int cornerRadius = 14;

        protected void InstallDialogChrome(Control content, string title, ThemePalette palette)
        {
            if (content == null) throw new ArgumentNullException("content");
            if (chromeInstalled) throw new InvalidOperationException("Dialog chrome is already installed.");
            chromeInstalled = true;
            Text = string.IsNullOrWhiteSpace(title) ? "LlamaLift" : title;
            FormBorderStyle = FormBorderStyle.None;
            ControlBox = false;
            int chromeHeight = 48;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + chromeHeight);
            if (MinimumSize.Width > 0 || MinimumSize.Height > 0)
                MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + chromeHeight);

            DialogChromePanel shell = new DialogChromePanel(this, Text, palette, chromeHeight);
            shell.Dock = DockStyle.Fill;
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);
            shell.Controls.Add(content, 0, 1);
            Controls.Clear();
            Controls.Add(shell);
            UpdateDialogRegion();
        }

        internal void BeginWindowDrag()
        {
            if (WindowState != FormWindowState.Normal) return;
            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, new IntPtr(HtCaption), IntPtr.Zero);
        }

        internal void CloseFromChrome()
        {
            IButtonControl cancel = CancelButton;
            DialogResult = cancel == null ? DialogResult.Cancel : cancel.DialogResult;
            Close();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateDialogRegion();
        }

        private void UpdateDialogRegion()
        {
            if (!chromeInstalled || Width <= 0 || Height <= 0) return;
            int scaledRadius = Math.Max(10, Convert.ToInt32(cornerRadius * DeviceDpi / 96F));
            using (GraphicsPath path = DialogVisuals.RoundRect(new Rectangle(0, 0, Width, Height), scaledRadius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;
                return cp;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    }

    internal sealed class DialogChromePanel : TableLayoutPanel
    {
        private readonly ThemePalette palette;

        internal DialogChromePanel(LlamaLiftDialogForm owner, string title, ThemePalette colors, int titleHeight)
        {
            palette = colors;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Padding = new Padding(1);
            ColumnCount = 1;
            RowCount = 2;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            RowStyles.Add(new RowStyle(SizeType.Absolute, titleHeight));
            RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            BackColor = palette.Background;

            TableLayoutPanel bar = new TableLayoutPanel();
            bar.Name = "dialogCustomTitleBar";
            bar.Dock = DockStyle.Fill;
            bar.Margin = new Padding(0);
            bar.Padding = new Padding(16, 6, 10, 6);
            bar.ColumnCount = 3;
            bar.RowCount = 1;
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40F));
            bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bar.BackColor = palette.Surface;
            MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) owner.BeginWindowDrag(); };
            bar.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) owner.BeginWindowDrag(); };

            DialogBrandMark mark = new DialogBrandMark(palette);
            mark.Name = "dialogBrandLogo";
            mark.Dock = DockStyle.Fill;
            mark.Margin = new Padding(0, 3, 8, 3);
            mark.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) owner.BeginWindowDrag(); };
            bar.Controls.Add(mark, 0, 0);

            Label titleLabel = new Label();
            titleLabel.Name = "dialogCustomTitle";
            titleLabel.Text = title;
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Margin = new Padding(0);
            titleLabel.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = palette.Text;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.AutoEllipsis = true;
            titleLabel.AccessibleName = title;
            titleLabel.MouseDown += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) owner.BeginWindowDrag(); };
            bar.Controls.Add(titleLabel, 1, 0);

            DialogCloseButton close = new DialogCloseButton(palette);
            close.Name = "dialogCustomClose";
            close.Dock = DockStyle.Fill;
            close.Margin = new Padding(4, 0, 0, 0);
            close.AccessibleName = "关闭" + title;
            close.Click += delegate { owner.CloseFromChrome(); };
            bar.Controls.Add(close, 2, 0);
            Controls.Add(bar, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen border = new Pen(palette.Border, 1F))
                e.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        }
    }

    internal sealed class DialogBrandMark : Control
    {
        internal static bool OfficialLogoAvailable { get { return DialogBrandLogo.Image != null; } }

        internal DialogBrandMark(ThemePalette colors)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            TabStop = false;
            AccessibleName = "LlamaLift";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int side = Math.Max(18, Math.Min(28, Math.Min(Width, Height) - 2));
            Rectangle bounds = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);
            Image logo = DialogBrandLogo.Image;
            if (logo == null) return;
            InterpolationMode oldInterpolation = e.Graphics.InterpolationMode;
            PixelOffsetMode oldPixelOffset = e.Graphics.PixelOffsetMode;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(logo, bounds);
            e.Graphics.InterpolationMode = oldInterpolation;
            e.Graphics.PixelOffsetMode = oldPixelOffset;
        }
    }

    internal static class DialogBrandLogo
    {
        private static readonly Image officialImage = LoadOfficialImage();

        internal static Image Image { get { return officialImage; } }

        private static Image LoadOfficialImage()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("LlamaLift.Logo.png"))
                {
                    if (stream != null)
                    using (Image source = System.Drawing.Image.FromStream(stream))
                        return new Bitmap(source);
                }
            }
            catch { }
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                    return icon == null ? null : icon.ToBitmap();
            }
            catch { return null; }
        }
    }

    internal sealed class DialogCloseButton : Control
    {
        private readonly ThemePalette palette;
        private bool hovered;
        private bool pressed;

        internal DialogCloseButton(ThemePalette colors)
        {
            palette = colors;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle surface = new Rectangle(2, 2, Math.Max(8, Width - 5), Math.Max(8, Height - 5));
            if (hovered || pressed || Focused)
            {
                Color back = ThemeService.Mix(palette.Surface, palette.Danger, pressed ? 0.18F : 0.10F);
                using (GraphicsPath path = DialogVisuals.RoundRect(surface, 9))
                using (SolidBrush fill = new SolidBrush(back)) e.Graphics.FillPath(fill, path);
            }
            Color stroke = hovered || pressed ? palette.Danger : palette.Muted;
            float scale = Math.Max(1F, DeviceDpi / 96F);
            int half = Convert.ToInt32(5 * scale);
            Point center = new Point(Width / 2, Height / 2);
            using (Pen pen = new Pen(stroke, 1.6F * scale))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                e.Graphics.DrawLine(pen, center.X - half, center.Y - half, center.X + half, center.Y + half);
                e.Graphics.DrawLine(pen, center.X + half, center.Y - half, center.X - half, center.Y + half);
            }
            if (Focused)
            {
                using (GraphicsPath focusPath = DialogVisuals.RoundRect(surface, 9))
                using (Pen focus = new Pen(palette.Accent, 1.4F)) e.Graphics.DrawPath(focus, focusPath);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }
    }

    public static class LlamaLiftDialog
    {
        public static DialogResult Show(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, message, title, buttons, icon);
        }

        public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (LlamaLiftMessageDialog dialog = new LlamaLiftMessageDialog(message, title, buttons, icon, ThemeService.CurrentPalette))
                return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }
    }

    internal sealed class LlamaLiftMessageDialog : LlamaLiftDialogForm
    {
        private readonly ThemePalette palette;

        internal LlamaLiftMessageDialog(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon, ThemePalette colors)
        {
            palette = colors ?? ThemePalette.Create(false, ThemeService.GetAccent("Blue"));
            string safeTitle = string.IsNullOrWhiteSpace(title) ? "LlamaLift" : title.Trim();
            string safeMessage = string.IsNullOrWhiteSpace(message) ? "操作已完成。" : message.Trim();
            Font bodyFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            Size measured = TextRenderer.MeasureText(safeMessage, bodyFont, new Size(470, 1000),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
            int bodyHeight = Math.Max(76, Math.Min(270, measured.Height + 32));

            Text = safeTitle;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 24 + 62 + bodyHeight + 62 + 20);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = palette.Background;
            ForeColor = palette.Text;
            AccessibleName = safeTitle;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 20, 24, 16);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            root.BackColor = palette.Background;

            TableLayoutPanel heading = new TableLayoutPanel();
            heading.Dock = DockStyle.Fill;
            heading.Margin = new Padding(0);
            heading.ColumnCount = 2;
            heading.RowCount = 1;
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            heading.BackColor = palette.Background;
            DialogGlyph glyph = new DialogGlyph(icon, palette);
            glyph.Dock = DockStyle.Fill;
            glyph.Margin = new Padding(0, 2, 10, 12);
            heading.Controls.Add(glyph, 0, 0);

            TableLayoutPanel headingText = new TableLayoutPanel();
            headingText.Dock = DockStyle.Fill;
            headingText.Margin = new Padding(0);
            headingText.ColumnCount = 1;
            headingText.RowCount = 2;
            headingText.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headingText.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            headingText.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
            headingText.BackColor = palette.Background;

            Label titleLabel = new Label();
            titleLabel.Text = safeTitle;
            titleLabel.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = palette.Text;
            titleLabel.AutoSize = false;
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Margin = new Padding(0);
            titleLabel.AutoEllipsis = true;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            headingText.Controls.Add(titleLabel, 0, 0);

            Label context = new Label();
            context.Text = ContextText(icon);
            context.Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            context.ForeColor = palette.Muted;
            context.AutoSize = false;
            context.Dock = DockStyle.Fill;
            context.Margin = new Padding(0);
            context.TextAlign = ContentAlignment.TopLeft;
            headingText.Controls.Add(context, 0, 1);
            heading.Controls.Add(headingText, 1, 0);
            root.Controls.Add(heading, 0, 0);

            DialogCardPanel body = new DialogCardPanel(palette);
            body.Name = "messageDialogBody";
            body.Dock = DockStyle.Fill;
            body.Margin = new Padding(0);
            body.Padding = new Padding(16, 13, 16, 12);
            body.ColumnCount = 1;
            body.RowCount = 1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            TextBox content = new TextBox();
            content.Name = "messageDialogContent";
            content.Text = safeMessage;
            content.Dock = DockStyle.Fill;
            content.BorderStyle = BorderStyle.None;
            content.ReadOnly = true;
            content.Multiline = true;
            content.WordWrap = true;
            content.ScrollBars = measured.Height + 32 > bodyHeight ? ScrollBars.Vertical : ScrollBars.None;
            content.BackColor = palette.SurfaceAlt;
            content.ForeColor = palette.Text;
            content.Font = bodyFont;
            content.TabStop = false;
            content.AccessibleName = "提示内容";
            body.Controls.Add(content, 0, 0);
            root.Controls.Add(body, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Padding = new Padding(0, 14, 0, 0);
            actions.BackColor = palette.Background;
            AddButtons(actions, buttons);
            root.Controls.Add(actions, 0, 2);

            InstallDialogChrome(root, safeTitle, palette);
            Shown += delegate
            {
                if (AcceptButton is Control) ((Control)AcceptButton).Focus();
            };
        }

        private void AddButtons(FlowLayoutPanel actions, MessageBoxButtons buttons)
        {
            if (buttons == MessageBoxButtons.OK)
            {
                AddButton(actions, "知道了", DialogResult.OK, true, true);
                return;
            }
            if (buttons == MessageBoxButtons.OKCancel)
            {
                AddButton(actions, "确定", DialogResult.OK, true, false);
                AddButton(actions, "取消", DialogResult.Cancel, false, true);
                return;
            }
            if (buttons == MessageBoxButtons.YesNo)
            {
                AddButton(actions, "确定", DialogResult.Yes, true, false);
                AddButton(actions, "取消", DialogResult.No, false, true);
                return;
            }
            if (buttons == MessageBoxButtons.YesNoCancel)
            {
                AddButton(actions, "保存", DialogResult.Yes, true, false);
                AddButton(actions, "不保存", DialogResult.No, false, false);
                AddButton(actions, "取消", DialogResult.Cancel, false, true);
                return;
            }
            if (buttons == MessageBoxButtons.RetryCancel)
            {
                AddButton(actions, "重试", DialogResult.Retry, true, false);
                AddButton(actions, "取消", DialogResult.Cancel, false, true);
                return;
            }
            if (buttons == MessageBoxButtons.AbortRetryIgnore)
            {
                AddButton(actions, "中止", DialogResult.Abort, true, false);
                AddButton(actions, "重试", DialogResult.Retry, false, false);
                AddButton(actions, "忽略", DialogResult.Ignore, false, true);
            }
        }

        private void AddButton(FlowLayoutPanel actions, string text, DialogResult result, bool primary, bool cancel)
        {
            AButton button = new AButton();
            button.Name = "dialogButton" + result;
            button.Text = text;
            button.Width = text.Length > 3 ? 104 : 92;
            button.Height = 38;
            button.Radius = 10;
            button.BorderWidth = 1F;
            button.Type = primary ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            button.DialogResult = result;
            button.Margin = new Padding(8, 0, 0, 0);
            button.AccessibleName = text;
            actions.Controls.Add(button);
            if (primary) AcceptButton = button;
            if (cancel) CancelButton = button;
        }

        private static string ContextText(MessageBoxIcon icon)
        {
            if (icon == MessageBoxIcon.Error) return "操作遇到问题";
            if (icon == MessageBoxIcon.Warning) return "请确认后继续";
            if (icon == MessageBoxIcon.Question) return "需要你的选择";
            return "来自 LlamaLift 的提示";
        }
    }

    internal sealed class LlamaLiftPromptDialog : LlamaLiftDialogForm
    {
        private readonly AInput input;

        internal string Value { get { return input.Text; } }

        internal LlamaLiftPromptDialog(string labelText, string title, string initial, ThemePalette colors)
        {
            ThemePalette palette = colors ?? ThemeService.CurrentPalette;
            Text = string.IsNullOrWhiteSpace(title) ? "输入内容" : title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(540, 270);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = palette.Background;
            ForeColor = palette.Text;
            AccessibleName = Text;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 20, 24, 16);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
            root.BackColor = palette.Background;

            Label heading = new Label();
            heading.Text = Text;
            heading.Dock = DockStyle.Fill;
            heading.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold, GraphicsUnit.Point);
            heading.ForeColor = palette.Text;
            heading.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(heading, 0, 0);

            Label label = new Label();
            label.Text = labelText;
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = palette.Text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(label, 0, 1);

            TableLayoutPanel inputRegion = new TableLayoutPanel();
            inputRegion.Dock = DockStyle.Fill;
            inputRegion.ColumnCount = 1;
            inputRegion.RowCount = 2;
            inputRegion.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            inputRegion.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inputRegion.BackColor = palette.Background;
            input = new AInput();
            input.Name = "promptInput";
            input.Text = initial ?? string.Empty;
            input.Dock = DockStyle.Fill;
            input.Margin = new Padding(0, 4, 0, 4);
            input.Radius = 10;
            input.Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            input.BackColor = palette.SurfaceAlt;
            input.ForeColor = palette.Text;
            input.BorderColor = palette.Border;
            input.BorderHover = ThemeService.Mix(palette.Border, palette.Accent, 0.45F);
            input.BorderActive = palette.Accent;
            input.CaretColor = palette.Accent;
            input.SelectionColor = ThemeService.Mix(palette.SurfaceAlt, palette.Accent, 0.28F);
            input.AccessibleName = labelText;
            inputRegion.Controls.Add(input, 0, 0);
            Label help = new Label();
            help.Text = "输入后按 Enter 确定，按 Esc 取消。";
            help.Dock = DockStyle.Fill;
            help.Font = new Font("Microsoft YaHei UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
            help.ForeColor = palette.Muted;
            help.TextAlign = ContentAlignment.TopLeft;
            inputRegion.Controls.Add(help, 0, 1);
            root.Controls.Add(inputRegion, 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Padding = new Padding(0, 10, 0, 0);
            actions.BackColor = palette.Background;
            AButton ok = MakeButton("确定", true, DialogResult.OK);
            AButton cancel = MakeButton("取消", false, DialogResult.Cancel);
            actions.Controls.Add(ok);
            actions.Controls.Add(cancel);
            root.Controls.Add(actions, 0, 3);

            InstallDialogChrome(root, Text, palette);
            AcceptButton = ok;
            CancelButton = cancel;
            Shown += delegate
            {
                input.Focus();
                input.SelectAll();
            };
        }

        private static AButton MakeButton(string text, bool primary, DialogResult result)
        {
            AButton button = new AButton();
            button.Text = text;
            button.Width = 92;
            button.Height = 38;
            button.Radius = 10;
            button.BorderWidth = 1F;
            button.Type = primary ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            button.DialogResult = result;
            button.Margin = new Padding(8, 0, 0, 0);
            return button;
        }
    }

    internal sealed class DialogGlyph : Control
    {
        private readonly MessageBoxIcon icon;
        private readonly ThemePalette palette;

        internal DialogGlyph(MessageBoxIcon value, ThemePalette colors)
        {
            icon = value;
            palette = colors;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            TabStop = false;
            AccessibleName = LlamaLiftMessageDialogIconName(value);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = IconColor(icon, palette);
            Rectangle circle = new Rectangle(2, 2, Math.Max(12, Width - 5), Math.Max(12, Height - 5));
            using (SolidBrush fill = new SolidBrush(ThemeService.Mix(palette.SurfaceAlt, color, palette.IsDark ? 0.24F : 0.12F)))
                e.Graphics.FillEllipse(fill, circle);
            string mark = icon == MessageBoxIcon.Error ? "×" : icon == MessageBoxIcon.Warning ? "!" : icon == MessageBoxIcon.Question ? "?" : "i";
            using (Font font = new Font("Segoe UI", mark == "i" ? 16F : 17F, FontStyle.Bold, GraphicsUnit.Point))
            using (SolidBrush brush = new SolidBrush(color))
            {
                SizeF size = e.Graphics.MeasureString(mark, font);
                e.Graphics.DrawString(mark, font, brush, circle.Left + (circle.Width - size.Width) / 2F,
                    circle.Top + (circle.Height - size.Height) / 2F - 1F);
            }
        }

        private static string LlamaLiftMessageDialogIconName(MessageBoxIcon value)
        {
            if (value == MessageBoxIcon.Error) return "错误";
            if (value == MessageBoxIcon.Warning) return "警告";
            if (value == MessageBoxIcon.Question) return "问题";
            return "信息";
        }

        private static Color IconColor(MessageBoxIcon value, ThemePalette colors)
        {
            if (value == MessageBoxIcon.Error) return colors.Danger;
            if (value == MessageBoxIcon.Warning) return colors.Warning;
            if (value == MessageBoxIcon.Question) return colors.Accent;
            return colors.Accent;
        }
    }

    internal class DialogCardPanel : TableLayoutPanel
    {
        private readonly ThemePalette palette;

        internal DialogCardPanel(ThemePalette colors)
        {
            palette = colors;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color outside = Parent == null ? palette.Background : Parent.BackColor;
            e.Graphics.Clear(outside);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = DialogVisuals.RoundRect(bounds, 14))
            using (SolidBrush fill = new SolidBrush(palette.SurfaceAlt))
            using (Pen border = new Pen(palette.Border, 1F))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }

    internal static class DialogVisuals
    {
        internal static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2));
            Rectangle arc = new Rectangle(bounds.Left, bounds.Top, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
