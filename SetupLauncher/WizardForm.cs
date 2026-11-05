using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class WizardForm : Form
{
    private readonly InstalledPackage _installed;
    private readonly CheckBox _shortcutBox;
    private readonly CheckBox _launchBox;
    private readonly Button _installButton;
    private readonly Button _uninstallButton;

    public WizardForm(InstalledPackage installed)
    {
        _installed = installed;

        Text = "ServerFlow 安装程序";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
        Padding = new Padding(0);

        var title = new Label
        {
            Text = "ServerFlow 服务器排班",
            Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 24, 27),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        var stateText = installed.Installed
            ? "已安装版本 v" + installed.Version + "，可覆盖安装或卸载。"
            : "未安装。将安装到系统应用目录，并创建开始菜单入口。";
        var state = new Label
        {
            Text = stateText,
            ForeColor = installed.Installed ? Color.FromArgb(176, 96, 0) : Color.FromArgb(96, 96, 104),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

        var locationGroup = new GroupBox
        {
            Text = "安装位置",
            ForeColor = Color.FromArgb(64, 64, 72),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(12, 8, 12, 12)
        };

        var locationLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill
        };

        var programPath = new TextBox
        {
            Text = installed.Installed && installed.Location.Length > 0
                ? installed.Location
                : @"C:\Program Files\WindowsApps\ServerFlow.Scheduler_<版本>_x64__pbhj1ej857fh2",
            ReadOnly = true,
            Width = 520,
            BackColor = Color.FromArgb(246, 246, 248),
            BorderStyle = BorderStyle.FixedSingle,
            TabStop = false,
            Margin = new Padding(0, 2, 0, 10)
        };

        var dataPath = new TextBox
        {
            Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Packages\ServerFlow.Scheduler_pbhj1ej857fh2\LocalCache\Local\ServerFlow\schedules.json"),
            ReadOnly = true,
            Width = 520,
            BackColor = Color.FromArgb(246, 246, 248),
            BorderStyle = BorderStyle.FixedSingle,
            TabStop = false,
            Margin = new Padding(0, 2, 0, 10)
        };

        var hint = new Label
        {
            Text = "MSIX 应用由 Windows 统一安装，不支持自定义目录；如需换盘，可在“设置 → 系统 → 存储 → 应用安装位置”中更改。",
            ForeColor = Color.FromArgb(120, 120, 128),
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Margin = new Padding(0)
        };

        locationLayout.Controls.Add(MakeCaption("程序目录（由 Windows 管理，不可更改）"));
        locationLayout.Controls.Add(programPath);
        locationLayout.Controls.Add(MakeCaption("排班数据（卸载会一并删除）"));
        locationLayout.Controls.Add(dataPath);
        locationLayout.Controls.Add(hint);
        locationGroup.Controls.Add(locationLayout);

        _shortcutBox = new CheckBox
        {
            Text = "创建桌面快捷方式",
            Checked = true,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };

        _launchBox = new CheckBox
        {
            Text = "安装完成后立即启动",
            Checked = true,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 14)
        };

        var optionPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0)
        };
        optionPanel.Controls.Add(_shortcutBox);
        optionPanel.Controls.Add(_launchBox);

        _uninstallButton = new Button
        {
            Text = "卸载",
            AutoSize = true,
            MinimumSize = new Size(96, 30),
            Visible = installed.Installed,
            Margin = new Padding(0, 0, 8, 0)
        };
        _uninstallButton.Click += UninstallButton_Click;

        _installButton = new Button
        {
            Text = installed.Installed ? "覆盖安装" : "安装",
            AutoSize = true,
            MinimumSize = new Size(104, 30),
            DialogResult = DialogResult.OK,
            Margin = new Padding(0, 0, 8, 0)
        };

        var cancelButton = new Button
        {
            Text = "取消",
            AutoSize = true,
            MinimumSize = new Size(90, 30),
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Margin = new Padding(0)
        };
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(_installButton);
        buttonPanel.Controls.Add(_uninstallButton);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 5,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(24, 18, 24, 18),
            BackColor = Color.White
        };
        root.RowStyles.Clear();
        for (var index = 0; index < 5; index++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(state, 0, 1);
        root.Controls.Add(locationGroup, 0, 2);
        root.Controls.Add(optionPanel, 0, 3);
        root.Controls.Add(buttonPanel, 0, 4);

        Controls.Add(root);

        AcceptButton = _installButton;
        CancelButton = cancelButton;

        Shown += (sender, args) =>
        {
            // 按当前 DPI 同比放大客户区：控件已被系统放大，客户区不跟着放大会裁掉最后一行。
            AutoSize = false;
            var scale = Math.Max(1f, DeviceDpi / 96f);
            var width = (int)Math.Ceiling(560 * scale);
            var height = (int)Math.Ceiling(400 * scale);
            ClientSize = new Size(
                Math.Max(width, MeasureRight(root, 0) + 28),
                Math.Max(height, MeasureBottom(root, 0) + 26));

            programPath.Select(0, 0);
            _installButton.Focus();
        };
    }

    private static int MeasureBottom(Control parent, int offsetY)
    {
        var max = 0;
        foreach (Control child in parent.Controls)
        {
            var top = offsetY + child.Top;
            max = Math.Max(max, top + child.Height);
            max = Math.Max(max, MeasureBottom(child, top));
        }

        return max;
    }

    private static int MeasureRight(Control parent, int offsetX)
    {
        var max = 0;
        foreach (Control child in parent.Controls)
        {
            var left = offsetX + child.Left;
            max = Math.Max(max, left + child.Width);
            max = Math.Max(max, MeasureRight(child, left));
        }

        return max;
    }

    public bool CreateShortcut => _shortcutBox.Checked;

    public bool LaunchAfterInstall => _launchBox.Checked;

    private static Label MakeCaption(string text)
    {
        return new Label
        {
            Text = text,
            ForeColor = Color.FromArgb(80, 80, 88),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };
    }

    private void UninstallButton_Click(object sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            "将卸载 ServerFlow 服务器排班，并删除本机排班数据。\n\n如需保留排班，请先备份上面的 schedules.json。",
            "确认卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _uninstallButton.Enabled = false;
        _installButton.Enabled = false;
        UseWaitCursor = true;

        var ok = Program.Uninstall(out var message);
        UseWaitCursor = false;

        MessageBox.Show(message, ok ? "卸载完成" : "卸载失败",
            MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);

        if (ok)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        _uninstallButton.Enabled = true;
        _installButton.Enabled = true;
    }
}

internal sealed class ProgressForm : Form
{
    private readonly Label _status;

    public ProgressForm()
    {
        Text = "ServerFlow 安装程序";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(440, 112);
        BackColor = Color.White;

        _status = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(24, 26, 392, 24),
            Text = "正在准备安装…",
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(32, 32, 32)
        };

        var bar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 28,
            Bounds = new Rectangle(24, 60, 392, 16)
        };

        Controls.Add(_status);
        Controls.Add(bar);
    }

    public void SetStatus(string text)
    {
        _status.Text = text;
        _status.Refresh();
        Application.DoEvents();
    }
}
