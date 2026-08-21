using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using AButton = AntdUI.Button;
using AInput = AntdUI.Input;

namespace LlamaServerManager
{
    public sealed class ApiKeyManagerDialog : LlamaLiftDialogForm
    {
        private readonly ApiKeyStore store;
        private readonly ThemePalette palette;
        private readonly string initialPath;
        private readonly ListBox lstKeys;
        private readonly AInput txtName;
        private readonly AInput txtKeys;
        private readonly Label lblSummary;
        private readonly AButton btnReveal;
        private List<ManagedApiKeyFile> records;
        private string secretContent;
        private bool secretRevealed;
        private bool loadingEditor;
        private bool editorDirty;
        private bool managedSelectionChanged;

        public string SelectedPath { get; private set; }

        public ApiKeyManagerDialog(string currentPath, ThemePalette theme)
        {
            store = new ApiKeyStore(ConfigStore.ApiKeyDirectory);
            palette = theme ?? ThemePalette.Create(false, Color.FromArgb(0, 122, 255));
            initialPath = currentPath ?? string.Empty;
            SelectedPath = initialPath;
            records = new List<ManagedApiKeyFile>();
            secretContent = string.Empty;

            Text = "API Key 管理";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(860, 580);
            MinimumSize = new Size(800, 550);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = palette.Background;
            ForeColor = palette.Text;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 20, 24, 16);
            root.ColumnCount = 2;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));

            Label title = MakeLabel("API Key 管理", 16F, FontStyle.Bold, palette.Text);
            Label subtitle = MakeLabel("创建、导入并为当前模型配置选择鉴权密钥", 9F, FontStyle.Regular, palette.Muted);
            System.Windows.Forms.Panel heading = new System.Windows.Forms.Panel();
            heading.Dock = DockStyle.Fill;
            title.Location = new Point(0, 0);
            title.AutoSize = true;
            subtitle.Location = new Point(1, 31);
            subtitle.AutoSize = true;
            heading.Controls.Add(title);
            heading.Controls.Add(subtitle);
            root.Controls.Add(heading, 0, 0);
            root.SetColumnSpan(heading, 2);

            DialogCardPanel left = MakeSurfacePanel();
            left.Margin = new Padding(0, 0, 8, 0);
            left.RowCount = 3;
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            Label libraryTitle = MakeLabel("密钥库", 10F, FontStyle.Bold, palette.Text);
            libraryTitle.Dock = DockStyle.Fill;
            libraryTitle.TextAlign = ContentAlignment.MiddleLeft;
            left.Controls.Add(libraryTitle, 0, 0);
            lstKeys = new ListBox();
            lstKeys.Dock = DockStyle.Fill;
            lstKeys.BorderStyle = BorderStyle.None;
            lstKeys.IntegralHeight = false;
            lstKeys.BackColor = palette.SurfaceAlt;
            lstKeys.ForeColor = palette.Text;
            lstKeys.Font = new Font("Microsoft YaHei UI", 9F);
            lstKeys.DrawMode = DrawMode.OwnerDrawFixed;
            lstKeys.ItemHeight = 42;
            lstKeys.DrawItem += DrawKeyItem;
            lstKeys.SelectedIndexChanged += KeySelectionChanged;
            left.Controls.Add(lstKeys, 0, 1);
            FlowLayoutPanel leftActions = new FlowLayoutPanel();
            leftActions.Dock = DockStyle.Fill;
            leftActions.FlowDirection = FlowDirection.LeftToRight;
            leftActions.WrapContents = false;
            leftActions.BackColor = Color.Transparent;
            leftActions.Controls.Add(MakeButton("新建", 60, false, NewClicked));
            leftActions.Controls.Add(MakeButton("导入", 60, false, ImportClicked));
            leftActions.Controls.Add(MakeButton("删除", 60, false, DeleteClicked));
            left.Controls.Add(leftActions, 0, 2);
            root.Controls.Add(left, 0, 1);

            DialogCardPanel editor = MakeSurfacePanel();
            editor.Margin = new Padding(8, 0, 0, 0);
            editor.Padding = new Padding(16);
            editor.RowCount = 8;
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            editor.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            editor.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            Label editorTitle = MakeLabel("密钥详情", 10F, FontStyle.Bold, palette.Text);
            editorTitle.Dock = DockStyle.Fill;
            editorTitle.TextAlign = ContentAlignment.MiddleLeft;
            editor.Controls.Add(editorTitle, 0, 0);
            editor.Controls.Add(MakeLabel("名称", 9F, FontStyle.Bold, palette.Text), 0, 1);
            txtName = MakeTextBox(false);
            txtName.TextChanged += EditorTextChanged;
            editor.Controls.Add(txtName, 0, 2);
            editor.Controls.Add(MakeLabel("密钥内容 · 每行一个 Key", 9F, FontStyle.Bold, palette.Text), 0, 3);
            txtKeys = MakeTextBox(true);
            txtKeys.Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
            txtKeys.TextChanged += EditorTextChanged;
            editor.Controls.Add(txtKeys, 0, 4);
            FlowLayoutPanel keyActions = new FlowLayoutPanel();
            keyActions.Dock = DockStyle.Fill;
            keyActions.WrapContents = false;
            keyActions.BackColor = Color.Transparent;
            keyActions.Controls.Add(MakeButton("生成随机 Key", 118, false, GenerateClicked));
            btnReveal = MakeButton("显示", 76, false, RevealClicked);
            keyActions.Controls.Add(btnReveal);
            keyActions.Controls.Add(MakeButton("保存密钥", 94, true, SaveClicked));
            editor.Controls.Add(keyActions, 0, 5);
            lblSummary = MakeLabel("选择一个托管密钥，或新建密钥文件。", 8.5F, FontStyle.Regular, palette.Muted);
            lblSummary.Dock = DockStyle.Fill;
            lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            editor.Controls.Add(lblSummary, 0, 6);
            Label security = MakeLabel("为兼容 llama.cpp，密钥以本地文本文件保存，并继承所在目录权限。请勿共享 data/api-keys 目录或把密钥打包发布。", 8.25F, FontStyle.Regular, palette.Muted);
            security.Dock = DockStyle.Fill;
            editor.Controls.Add(security, 0, 7);
            root.Controls.Add(editor, 1, 1);

            FlowLayoutPanel bottom = new FlowLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.FlowDirection = FlowDirection.RightToLeft;
            bottom.WrapContents = false;
            bottom.Padding = new Padding(0, 9, 0, 0);
            bottom.BackColor = palette.Background;
            AButton useButton = MakeButton("用于当前配置", 128, true, UseClicked);
            AButton closeButton = MakeButton("关闭", 84, false, CloseClicked);
            bottom.Controls.Add(useButton);
            bottom.Controls.Add(closeButton);
            bottom.Controls.Add(MakeButton("清除当前配置", 118, false, ClearClicked));
            root.Controls.Add(bottom, 0, 2);
            root.SetColumnSpan(bottom, 2);

            InstallDialogChrome(root, "API Key 管理", palette);
            AcceptButton = null;
            CancelButton = closeButton;
            Shown += delegate
            {
                RefreshRecords(initialPath);
                lstKeys.Focus();
                string readError;
                if (!string.IsNullOrWhiteSpace(initialPath) && !ApiKeyFileSupport.TryOpenForRead(initialPath, out readError))
                {
                    lblSummary.Text = "当前配置的密钥无法读取，请重新选择或新建密钥。";
                    lblSummary.ForeColor = palette.Danger;
                }
            };
            FormClosed += delegate { txtKeys.Text = string.Empty; };
        }

        private void RefreshRecords(string selectPath)
        {
            records = store.List();
            lstKeys.BeginUpdate();
            try
            {
                lstKeys.Items.Clear();
                foreach (ManagedApiKeyFile record in records) lstKeys.Items.Add(record);
                for (int i = 0; i < records.Count; i++)
                    if (string.Equals(records[i].FilePath, selectPath, StringComparison.OrdinalIgnoreCase))
                    {
                        lstKeys.SelectedIndex = i;
                        break;
                    }
            }
            finally { lstKeys.EndUpdate(); }
            if (lstKeys.SelectedIndex < 0 && records.Count > 0) lstKeys.SelectedIndex = 0;
            if (records.Count == 0) NewClicked(this, EventArgs.Empty);
        }

        private ManagedApiKeyFile SelectedRecord()
        {
            int index = lstKeys.SelectedIndex;
            return index >= 0 && index < records.Count ? records[index] : null;
        }

        private void KeySelectionChanged(object sender, EventArgs e)
        {
            ManagedApiKeyFile record = SelectedRecord();
            if (record == null) return;
            try
            {
                loadingEditor = true;
                try
                {
                    txtName.Text = record.Name;
                    secretContent = store.Read(record.FilePath);
                    SetSecretRevealed(false);
                }
                finally { loadingEditor = false; }
                lblSummary.Text = record.KeyCount + " 个 Key · " + record.MaskedPreview + " · " + record.FilePath;
                lblSummary.ForeColor = palette.Muted;
                editorDirty = false;
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void DrawKeyItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstKeys.Items.Count) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Rectangle item = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top + 3,
                Math.Max(1, e.Bounds.Width - 5), Math.Max(1, e.Bounds.Height - 6));
            Color back = selected ? ThemeService.Mix(palette.SurfaceAlt, palette.Accent, palette.IsDark ? 0.24F : 0.12F) : palette.SurfaceAlt;
            using (GraphicsPath path = DialogVisuals.RoundRect(item, 9))
            using (SolidBrush fill = new SolidBrush(back)) e.Graphics.FillPath(fill, path);
            Color fore = selected ? palette.Accent : palette.Text;
            TextRenderer.DrawText(e.Graphics, Convert.ToString(lstKeys.Items[e.Index]), lstKeys.Font,
                new Rectangle(item.Left + 12, item.Top, Math.Max(1, item.Width - 24), item.Height), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
                ControlPaint.DrawFocusRectangle(e.Graphics, item, fore, back);
        }

        private void NewClicked(object sender, EventArgs e)
        {
            loadingEditor = true;
            lstKeys.ClearSelected();
            txtName.Text = "api-key-" + DateTime.Now.ToString("yyyyMMdd");
            secretContent = string.Empty;
            SetSecretRevealed(true);
            loadingEditor = false;
            editorDirty = true;
            lblSummary.Text = "输入名称并粘贴密钥，或生成一个随机 Key。";
            txtName.Focus();
            txtName.SelectAll();
        }

        private void GenerateClicked(object sender, EventArgs e)
        {
            string generated = ApiKeyStore.GenerateKey();
            if (!secretRevealed) SetSecretRevealed(true);
            if (!string.IsNullOrWhiteSpace(txtKeys.Text) && !txtKeys.Text.EndsWith(Environment.NewLine)) txtKeys.AppendText(Environment.NewLine);
            txtKeys.AppendText(generated);
            secretContent = txtKeys.Text;
            editorDirty = true;
            lblSummary.Text = "已生成随机 Key。请先保存，再用于当前配置。";
        }

        private void RevealClicked(object sender, EventArgs e)
        {
            SetSecretRevealed(!secretRevealed);
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            SaveEditor();
        }

        private ManagedApiKeyFile SaveEditor()
        {
            try
            {
                string target = Path.Combine(store.DirectoryPath, (txtName.Text.Trim().Length == 0 ? "api-key" : txtName.Text.Trim()) + ".txt");
                ManagedApiKeyFile selected = SelectedRecord();
                if (File.Exists(target) && (selected == null || !string.Equals(selected.FilePath, target, StringComparison.OrdinalIgnoreCase)))
                {
                    DialogResult overwrite = LlamaLiftDialog.Show(this, "同名密钥文件已存在，是否覆盖？", "覆盖 API Key", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (overwrite != DialogResult.Yes) return null;
                }
                ManagedApiKeyFile saved = store.Save(txtName.Text, CurrentSecretContent());
                RefreshRecords(saved.FilePath);
                lblSummary.Text = "已保存 " + saved.KeyCount + " 个 Key · " + saved.MaskedPreview;
                lblSummary.ForeColor = palette.Success;
                return saved;
            }
            catch (Exception ex) { ShowError(ex.Message); return null; }
        }

        private void ImportClicked(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "API Key 文本文件|*.txt|所有文件|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ManagedApiKeyFile imported = store.Import(dialog.FileName, Path.GetFileNameWithoutExtension(dialog.FileName));
                    RefreshRecords(imported.FilePath);
                }
                catch (Exception ex) { ShowError(ex.Message); }
            }
        }

        private void DeleteClicked(object sender, EventArgs e)
        {
            ManagedApiKeyFile record = SelectedRecord();
            if (record == null) return;
            if (LlamaLiftDialog.Show(this, "确定删除托管密钥“" + record.Name + "”吗？\n该文件删除后无法恢复。", "删除 API Key",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                store.Delete(record.FilePath);
                if (string.Equals(SelectedPath, record.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedPath = string.Empty;
                    managedSelectionChanged = true;
                }
                RefreshRecords(string.Empty);
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void UseClicked(object sender, EventArgs e)
        {
            ManagedApiKeyFile record = SelectedRecord();
            if (record == null || editorDirty || !string.Equals(txtName.Text.Trim(), record.Name, StringComparison.Ordinal))
                record = SaveEditor();
            if (record == null) return;
            SelectedPath = record.FilePath;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ClearClicked(object sender, EventArgs e)
        {
            SelectedPath = string.Empty;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CloseClicked(object sender, EventArgs e)
        {
            DialogResult = managedSelectionChanged ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (managedSelectionChanged && DialogResult == DialogResult.None) DialogResult = DialogResult.OK;
            base.OnFormClosing(e);
        }

        private void ShowError(string message)
        {
            lblSummary.Text = message;
            lblSummary.ForeColor = palette.Danger;
            LlamaLiftDialog.Show(this, message, "API Key 管理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void EditorTextChanged(object sender, EventArgs e)
        {
            if (loadingEditor) return;
            if (ReferenceEquals(sender, txtKeys) && secretRevealed) secretContent = txtKeys.Text;
            editorDirty = true;
        }

        private string CurrentSecretContent()
        {
            if (secretRevealed) secretContent = txtKeys.Text;
            return secretContent ?? string.Empty;
        }

        private void SetSecretRevealed(bool reveal)
        {
            bool wasLoading = loadingEditor;
            if (secretRevealed && !wasLoading) secretContent = txtKeys.Text;
            loadingEditor = true;
            secretRevealed = reveal;
            txtKeys.ReadOnly = !reveal;
            txtKeys.Text = reveal ? (secretContent ?? string.Empty) : MaskContent(secretContent);
            txtKeys.BackColor = reveal ? palette.SurfaceAlt : palette.Surface;
            btnReveal.Text = reveal ? "隐藏" : "显示";
            loadingEditor = wasLoading;
        }

        private static string MaskContent(string content)
        {
            string normalized = (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            List<string> masked = new List<string>();
            foreach (string raw in normalized.Split(new char[] { '\n' }, StringSplitOptions.None))
            {
                string value = raw.Trim();
                if (value.Length == 0) continue;
                int visible = Math.Min(4, value.Length);
                masked.Add(new string('•', Math.Max(4, Math.Min(18, value.Length - visible))) + value.Substring(value.Length - visible));
            }
            return string.Join(Environment.NewLine, masked.ToArray());
        }

        private DialogCardPanel MakeSurfacePanel()
        {
            DialogCardPanel panel = new DialogCardPanel(palette);
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(12);
            panel.ColumnCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return panel;
        }

        private AInput MakeTextBox(bool multiline)
        {
            AInput input = new AInput();
            input.Dock = DockStyle.Fill;
            input.Multiline = multiline;
            input.BackColor = palette.SurfaceAlt;
            input.ForeColor = palette.Text;
            input.BorderColor = palette.Border;
            input.BorderHover = ThemeService.Mix(palette.Border, palette.Accent, 0.45F);
            input.BorderActive = palette.Accent;
            input.PlaceholderColor = palette.Muted;
            input.CaretColor = palette.Accent;
            input.SelectionColor = ThemeService.Mix(palette.SurfaceAlt, palette.Accent, 0.28F);
            input.Radius = 10;
            input.AutoScroll = multiline;
            input.Margin = new Padding(0, 4, 0, 6);
            return input;
        }

        private AButton MakeButton(string text, int width, bool primary, EventHandler handler)
        {
            AButton button = new AButton();
            button.Text = text;
            button.Width = width;
            button.Height = 38;
            button.Radius = 10;
            button.BorderWidth = 1F;
            button.Type = primary ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            button.Margin = new Padding(4, 6, 4, 5);
            button.Cursor = Cursors.Hand;
            if (!primary)
            {
                Color buttonFore = text == "删除" || text == "清除当前配置" ? palette.Danger : palette.Text;
                button.DefaultBack = palette.SurfaceAlt;
                button.DefaultBorderColor = palette.Border;
                button.ForeColor = buttonFore;
                button.ForeHover = buttonFore;
                button.ForeActive = buttonFore;
                button.BackHover = ThemeService.Mix(palette.SurfaceAlt, buttonFore, 0.08F);
                button.BackActive = ThemeService.Mix(palette.SurfaceAlt, buttonFore, 0.15F);
            }
            button.Click += handler;
            return button;
        }

        private static Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Font = new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            return label;
        }
    }
}
