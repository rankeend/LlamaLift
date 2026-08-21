using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AButton = AntdUI.Button;

namespace LlamaServerManager
{
    public sealed class ConnectionInfoDialog : LlamaLiftDialogForm
    {
        private readonly ThemePalette palette;
        private readonly ToolTip toolTip;
        private readonly Label statusLabel;

        public ConnectionInfoDialog(ConnectionInfoSnapshot info, ThemePalette colors)
        {
            if (info == null) throw new ArgumentNullException("info");
            palette = colors ?? ThemePalette.Create(false, ThemeService.GetAccent("Blue"));
            toolTip = new ToolTip();
            toolTip.InitialDelay = 250;
            toolTip.ReshowDelay = 80;
            toolTip.AutoPopDelay = 6500;
            toolTip.ShowAlways = true;
            toolTip.BackColor = palette.SurfaceAlt;
            toolTip.ForeColor = palette.Text;

            Text = "服务连接信息";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(790, 590);
            MinimumSize = new Size(720, 560);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = palette.Background;
            ForeColor = palette.Text;
            AccessibleName = "服务连接信息";

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 20, 24, 16);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            root.BackColor = palette.Background;

            System.Windows.Forms.Panel header = new System.Windows.Forms.Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = palette.Background;
            ConnectionReadyGlyph ready = new ConnectionReadyGlyph(palette);
            ready.Location = new Point(0, 4);
            ready.Size = new Size(46, 46);
            header.Controls.Add(ready);
            Label title = new Label();
            title.Text = "服务已就绪";
            title.Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = palette.Text;
            title.Location = new Point(60, 8);
            title.AutoSize = true;
            header.Controls.Add(title);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel rows = new TableLayoutPanel();
            rows.Name = "connectionInfoRows";
            rows.Dock = DockStyle.Fill;
            rows.ColumnCount = 1;
            rows.RowCount = 6;
            rows.BackColor = palette.Background;
            for (int i = 0; i < 6; i++) rows.RowStyles.Add(new RowStyle(SizeType.Percent, 16.6667F));
            AddValueCard(rows, 0, "Provider ID", info.ProviderId, false, false);
            AddValueCard(rows, 1, "API 协议", info.ApiProtocol, false, false);
            AddValueCard(rows, 2, "API 地址", info.ApiAddress, false, true);
            AddValueCard(rows, 3, "APIKEY", info.ApiKey, info.HasApiKey, true);
            AddValueCard(rows, 4, "模型完整名称", info.ModelFullName, false, false);
            AddValueCard(rows, 5, "支持最大上下文", info.MaximumContext, false, false);
            root.Controls.Add(rows, 0, 1);

            statusLabel = new Label();
            statusLabel.Name = "copyStatus";
            statusLabel.Text = info.HasApiKey
                ? "API Key 默认以星号隐藏；眼睛按钮只负责显示或隐藏，单击卡片复制完整 Key。"
                : "当前配置没有 API Key；其他连接参数仍可单击复制。";
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.ForeColor = palette.Muted;
            statusLabel.AutoEllipsis = true;
            root.Controls.Add(statusLabel, 0, 2);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Padding = new Padding(0, 8, 0, 0);
            actions.BackColor = palette.Background;
            AButton close = new AButton();
            close.Name = "closeConnectionInfo";
            close.Text = "完成";
            close.Type = AntdUI.TTypeMini.Primary;
            close.Radius = 10;
            close.BorderWidth = 1F;
            close.Width = 96;
            close.Height = 38;
            close.DialogResult = DialogResult.OK;
            close.AccessibleName = "关闭服务连接信息";
            actions.Controls.Add(close);
            root.Controls.Add(actions, 0, 3);

            InstallDialogChrome(root, "服务连接信息", palette);
            AcceptButton = close;
            CancelButton = close;
            Shown += delegate { close.Focus(); };
        }

        private void AddValueCard(TableLayoutPanel parent, int row, string label, string value, bool secret, bool monospace)
        {
            CopyValueCard card = new CopyValueCard(label, value, secret, monospace, palette);
            card.Name = "connectionValue" + row;
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 4, 0, 4);
            card.CopyCompleted += delegate(object sender, CopyCompletedEventArgs e)
            {
                statusLabel.Text = e.Succeeded ? "已复制：" + e.Label : "复制失败：" + e.ErrorMessage;
                statusLabel.ForeColor = e.Succeeded ? palette.Success : palette.Danger;
            };
            card.SecretVisibilityChanged += delegate(object sender, EventArgs e)
            {
                statusLabel.Text = card.SecretRevealed
                    ? "API Key 已显示；请避免截屏或向他人泄露。"
                    : "API Key 已重新隐藏；单击卡片仍会复制完整 Key。";
                statusLabel.ForeColor = card.SecretRevealed ? palette.Warning : palette.Muted;
            };
            toolTip.SetToolTip(card, string.IsNullOrWhiteSpace(value) ? label + " 未提供" : "左键单击复制“" + label + "”");
            if (card.VisibilityButton != null)
                toolTip.SetToolTip(card.VisibilityButton, "显示完整 API Key");
            parent.Controls.Add(card, 0, row);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && toolTip != null) toolTip.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class CopyCompletedEventArgs : EventArgs
    {
        internal string Label { get; private set; }
        internal bool Succeeded { get; private set; }
        internal string ErrorMessage { get; private set; }

        internal CopyCompletedEventArgs(string label, bool succeeded, string error)
        {
            Label = label;
            Succeeded = succeeded;
            ErrorMessage = error ?? string.Empty;
        }
    }

    internal sealed class CopyValueCard : Control
    {
        private readonly string label;
        private readonly string value;
        private readonly bool secret;
        private readonly bool monospace;
        private readonly ThemePalette palette;
        private readonly Timer feedbackTimer;
        private bool hovered;
        private bool pressed;
        private bool copied;
        private bool secretRevealed;

        internal event EventHandler<CopyCompletedEventArgs> CopyCompleted;
        internal event EventHandler SecretVisibilityChanged;
        internal EyeToggleButton VisibilityButton { get; private set; }
        internal bool SecretRevealed { get { return secretRevealed; } }
        internal string CompleteValue { get { return value; } }
        internal string CopyActionLabel { get { return copied ? "已复制" : "点此复制"; } }

        internal CopyValueCard(string caption, string content, bool isSecret, bool useMonospace, ThemePalette colors)
        {
            label = caption ?? string.Empty;
            value = content ?? string.Empty;
            secret = isSecret;
            monospace = useMonospace || isSecret;
            palette = colors;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = string.IsNullOrWhiteSpace(value) ? Cursors.Default : Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = string.IsNullOrWhiteSpace(value) ? label + "，未提供" : label + "，单击复制";
            AccessibleDescription = "按 Enter 或空格可复制完整内容到剪贴板。";
            BackColor = palette.Background;
            feedbackTimer = new Timer();
            feedbackTimer.Interval = 1800;
            feedbackTimer.Tick += delegate
            {
                feedbackTimer.Stop();
                copied = false;
                Invalidate();
            };

            if (secret)
            {
                VisibilityButton = new EyeToggleButton(palette);
                VisibilityButton.Name = "toggleApiKeyVisibility";
                VisibilityButton.Size = new Size(40, 40);
                VisibilityButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                VisibilityButton.AccessibleName = "显示完整 API Key";
                VisibilityButton.Click += delegate
                {
                    secretRevealed = !secretRevealed;
                    VisibilityButton.Revealed = secretRevealed;
                    VisibilityButton.AccessibleName = secretRevealed ? "隐藏完整 API Key" : "显示完整 API Key";
                    Invalidate();
                    EventHandler handler = SecretVisibilityChanged;
                    if (handler != null) handler(this, EventArgs.Empty);
                };
                Controls.Add(VisibilityButton);
            }
        }

        internal bool PerformCopy()
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            try
            {
                Clipboard.SetText(value);
                copied = true;
                feedbackTimer.Stop();
                feedbackTimer.Start();
                Invalidate();
                EventHandler<CopyCompletedEventArgs> handler = CopyCompleted;
                if (handler != null) handler(this, new CopyCompletedEventArgs(label, true, string.Empty));
                return true;
            }
            catch (Exception ex)
            {
                EventHandler<CopyCompletedEventArgs> handler = CopyCompleted;
                if (handler != null) handler(this, new CopyCompletedEventArgs(label, false, ex.Message));
                return false;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (VisibilityButton != null)
            {
                int inset = Math.Max(10, VisibilityButton.Width / 4);
                VisibilityButton.Location = new Point(Math.Max(0, Width - VisibilityButton.Width - inset),
                    Math.Max(0, (Height - VisibilityButton.Height) / 2));
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            PerformCopy();
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !string.IsNullOrWhiteSpace(value))
            {
                pressed = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                PerformCopy();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color background = palette.Surface;
            Color border = palette.Border;
            if (hovered && !string.IsNullOrWhiteSpace(value))
            {
                background = ThemeService.Mix(palette.Surface, palette.Accent, 0.07F);
                border = ThemeService.Mix(palette.Border, palette.Accent, 0.48F);
            }
            if (pressed)
            {
                background = ThemeService.Mix(palette.Surface, palette.Accent, 0.13F);
                border = palette.Accent;
            }
            if (copied)
            {
                background = ThemeService.Mix(palette.Surface, palette.Success, palette.IsDark ? 0.13F : 0.07F);
                border = ThemeService.Mix(palette.Border, palette.Success, 0.55F);
            }
            using (GraphicsPath path = DialogVisuals.RoundRect(bounds, 12))
            using (SolidBrush fill = new SolidBrush(background))
            using (Pen line = new Pen(border, Focused ? 1.6F : 1F))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(line, path);
            }

            float uiScale = Math.Max(1F, Math.Min(2F, Height / 56F));
            int sidePadding = Convert.ToInt32(18F * uiScale);
            int labelWidth = Math.Min(Convert.ToInt32(142F * uiScale), Math.Max(Convert.ToInt32(116F * uiScale), Width / 5));
            int actionButtonWidth = Convert.ToInt32(108F * uiScale);
            int actionButtonHeight = Math.Min(Height - Convert.ToInt32(12F * uiScale), Convert.ToInt32(34F * uiScale));
            int actionGap = Convert.ToInt32(10F * uiScale);
            int copyRight = secret && VisibilityButton != null ? VisibilityButton.Left - actionGap : Width - sidePadding;
            Rectangle actionButton = new Rectangle(Math.Max(0, copyRight - actionButtonWidth),
                Math.Max(0, (Height - actionButtonHeight) / 2), actionButtonWidth, Math.Max(20, actionButtonHeight));
            Rectangle labelBounds = new Rectangle(sidePadding, 0, labelWidth, Height);
            using (Font labelFont = new Font("Microsoft YaHei UI", 8.75F * uiScale, FontStyle.Regular, GraphicsUnit.Point))
                TextRenderer.DrawText(e.Graphics, label, labelFont, labelBounds, palette.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            Rectangle valueBounds = new Rectangle(labelBounds.Right + Convert.ToInt32(8F * uiScale), 0,
                Math.Max(40, actionButton.Left - labelBounds.Right - Convert.ToInt32(20F * uiScale)), Height);
            using (Font valueFont = new Font(monospace ? "Cascadia Mono" : "Microsoft YaHei UI", 9F * uiScale, FontStyle.Regular, GraphicsUnit.Point))
                TextRenderer.DrawText(e.Graphics, DisplayValue(), valueFont, valueBounds,
                    string.IsNullOrWhiteSpace(value) ? palette.Muted : palette.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (!string.IsNullOrWhiteSpace(value))
            {
                Color actionColor = copied ? palette.Success : hovered || Focused ? palette.Accent : palette.Muted;
                Color actionBack = copied
                    ? ThemeService.Mix(palette.SurfaceAlt, palette.Success, palette.IsDark ? 0.16F : 0.08F)
                    : hovered || Focused ? ThemeService.Mix(palette.SurfaceAlt, palette.Accent, 0.07F) : palette.SurfaceAlt;
                Color actionBorder = copied ? ThemeService.Mix(palette.Border, palette.Success, 0.60F)
                    : hovered || Focused ? ThemeService.Mix(palette.Border, palette.Accent, 0.60F) : palette.Border;
                using (GraphicsPath actionPath = DialogVisuals.RoundRect(actionButton, Convert.ToInt32(9F * uiScale)))
                using (SolidBrush actionFill = new SolidBrush(actionBack))
                using (Pen actionLine = new Pen(actionBorder, Math.Max(1F, uiScale)))
                {
                    e.Graphics.FillPath(actionFill, actionPath);
                    e.Graphics.DrawPath(actionLine, actionPath);
                }
                int iconSize = Convert.ToInt32(16F * uiScale);
                int iconLeft = actionButton.Left + Convert.ToInt32(11F * uiScale);
                DrawCopyIcon(e.Graphics, new Rectangle(iconLeft, actionButton.Top + (actionButton.Height - iconSize) / 2, iconSize, iconSize), actionColor, copied);
                Rectangle actionText = new Rectangle(iconLeft + iconSize + Convert.ToInt32(6F * uiScale), actionButton.Top,
                    Math.Max(20, actionButton.Right - iconLeft - iconSize - Convert.ToInt32(10F * uiScale)), actionButton.Height);
                using (Font actionFont = new Font("Microsoft YaHei UI", 8F * uiScale, FontStyle.Regular, GraphicsUnit.Point))
                    TextRenderer.DrawText(e.Graphics, copied ? "已复制" : "点此复制", actionFont, actionText,
                        actionColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private string DisplayValue()
        {
            if (string.IsNullOrWhiteSpace(value)) return "未提供";
            if (!secret || secretRevealed) return value;
            return new string('*', Math.Max(12, Math.Min(28, value.Length)));
        }

        private static void DrawCopyIcon(Graphics graphics, Rectangle bounds, Color color, bool done)
        {
            float scale = Math.Max(1F, bounds.Width / 16F);
            using (Pen pen = new Pen(color, 1.5F * scale))
            {
                if (done)
                {
                    graphics.DrawLines(pen, new Point[]
                    {
                        new Point(bounds.Left + 2, bounds.Top + 8),
                        new Point(bounds.Left + 6, bounds.Bottom - 2),
                        new Point(bounds.Right - 1, bounds.Top + 2)
                    });
                    return;
                }
                graphics.DrawRectangle(pen, bounds.Left + 1, bounds.Top + 4, bounds.Width - 6, bounds.Height - 6);
                graphics.DrawRectangle(pen, bounds.Left + 5, bounds.Top + 1, bounds.Width - 6, bounds.Height - 6);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && feedbackTimer != null) feedbackTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class ConnectionReadyGlyph : Control
    {
        private readonly ThemePalette palette;

        internal ConnectionReadyGlyph(ThemePalette colors)
        {
            palette = colors;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            AccessibleName = "服务已就绪";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int side = Math.Max(20, Math.Min(Width, Height) - 2);
            Rectangle bounds = new Rectangle((Width - side) / 2, (Height - side) / 2, side, side);
            using (SolidBrush fill = new SolidBrush(ThemeService.Mix(palette.SurfaceAlt, palette.Success, palette.IsDark ? 0.20F : 0.10F)))
                e.Graphics.FillEllipse(fill, bounds);
            float scale = Math.Max(1F, side / 44F);
            using (Pen check = new Pen(palette.Success, 2.2F * scale))
            {
                check.StartCap = LineCap.Round;
                check.EndCap = LineCap.Round;
                e.Graphics.DrawLines(check, new Point[]
                {
                    new Point(bounds.Left + Convert.ToInt32(11F * scale), bounds.Top + Convert.ToInt32(22F * scale)),
                    new Point(bounds.Left + Convert.ToInt32(18F * scale), bounds.Top + Convert.ToInt32(29F * scale)),
                    new Point(bounds.Left + Convert.ToInt32(32F * scale), bounds.Top + Convert.ToInt32(14F * scale))
                });
            }
        }
    }

    internal sealed class EyeToggleButton : Control
    {
        private readonly ThemePalette palette;
        private bool revealed;
        private bool hovered;
        private bool pressed;

        public bool Revealed
        {
            get { return revealed; }
            set { revealed = value; Invalidate(); }
        }

        public EyeToggleButton(ThemePalette colors)
        {
            palette = colors;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PushButton;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle surface = new Rectangle(1, 1, Math.Max(10, Width - 3), Math.Max(10, Height - 3));
            Color surfaceColor = pressed
                ? ThemeService.Mix(palette.SurfaceAlt, palette.Accent, 0.18F)
                : hovered || Focused ? ThemeService.Mix(palette.SurfaceAlt, palette.Accent, 0.10F) : palette.SurfaceAlt;
            using (GraphicsPath path = DialogVisuals.RoundRect(surface, 9))
            using (SolidBrush fill = new SolidBrush(surfaceColor))
                e.Graphics.FillPath(fill, path);

            Rectangle bounds = new Rectangle(9, 11, Math.Max(12, Width - 19), Math.Max(10, Height - 23));
            Color stroke = Focused || hovered ? palette.Accent : palette.Muted;
            using (Pen pen = new Pen(stroke, 1.7F))
            using (GraphicsPath eye = new GraphicsPath())
            {
                int midY = bounds.Top + bounds.Height / 2;
                eye.AddBezier(bounds.Left, midY, bounds.Left + bounds.Width / 4, bounds.Top,
                    bounds.Right - bounds.Width / 4, bounds.Top, bounds.Right, midY);
                eye.AddBezier(bounds.Right, midY, bounds.Right - bounds.Width / 4, bounds.Bottom,
                    bounds.Left + bounds.Width / 4, bounds.Bottom, bounds.Left, midY);
                e.Graphics.DrawPath(pen, eye);
                int pupil = Math.Max(4, Math.Min(bounds.Width, bounds.Height) / 3);
                e.Graphics.DrawEllipse(pen, bounds.Left + (bounds.Width - pupil) / 2,
                    bounds.Top + (bounds.Height - pupil) / 2, pupil, pupil);
                if (!revealed) e.Graphics.DrawLine(pen, bounds.Left + 1, bounds.Bottom, bounds.Right - 1, bounds.Top);
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
}
