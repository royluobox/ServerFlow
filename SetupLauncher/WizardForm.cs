using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class WizardForm : Form
{
    private const int LogicalWidth = 640;
    private const int HeaderHeight = 96;
    private const int FooterHeight = 72;
    private const int PageMargin = 28;

    private readonly InstalledPackage _installed;
    private readonly ToolTip _tips = new ToolTip();
    private readonly float? _scaleOverride;

    private float _scale = 1f;
    private bool _built;
    private bool _busy;
    private bool _willLaunch;

    private Panel _optionsPage;
    private Panel _progressPage;
    private Panel _donePage;
    private FooterBand _footer;

    private CheckBox _shortcutBox;
    private CheckBox _launchBox;

    private AccentButton _installButton;
    private AccentButton _cancelButton;
    private AccentButton _doneButton;
    private FlatLinkButton _uninstallButton;

    private Label _progressTitle;
    private Label _progressStatus;
    private MarqueeBar _bar;

    private StateBadge _badge;
    private Label _doneTitle;
    private Label _doneMessage;
    private Label _doneWarning;

    public WizardForm(InstalledPackage installed)
        : this(installed, null)
    {
    }

    public WizardForm(InstalledPackage installed, float? scaleOverride)
    {
        _installed = installed;
        _scaleOverride = scaleOverride;
    }

    public int ExitCode { get; private set; }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_built)
        {
            return;
        }

        _built = true;
        _scale = _scaleOverride ?? (DeviceDpi / 96f);
        BuildUi();
    }

    private int S(int value)
    {
        return (int)Math.Round(value * _scale);
    }

    private Font F(float size, FontStyle style = FontStyle.Regular)
    {
        // 真实高 DPI 下字体由系统按 DPI 放大；仅预览缩放时手动同步，保证截图比例可信。
        return Theme.Font(_scaleOverride.HasValue ? size * _scaleOverride.Value : size, style);
    }

    private void BuildUi()
    {
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = true;
        Text = "ServerFlow 安装程序";
        BackColor = Theme.Canvas;
        Font = F(9F);
        DoubleBuffered = true;
        Icon = Program.LoadWindowIcon();

        var header = BuildHeader();
        Controls.Add(header);

        _optionsPage = NewPage();
        _progressPage = NewPage();
        _donePage = NewPage();

        var pageTop = S(HeaderHeight);
        var y = 20;

        var locationTitle = MakeSectionTitle("安装位置");
        locationTitle.Location = new Point(S(PageMargin), S(y));
        _optionsPage.Controls.Add(locationTitle);
        y += 26;

        var locationCard = BuildLocationCard(out var locationHeight);
        locationCard.Location = new Point(S(PageMargin), S(y));
        _optionsPage.Controls.Add(locationCard);
        y += locationHeight + 22;

        var optionTitle = MakeSectionTitle("安装选项");
        optionTitle.Location = new Point(S(PageMargin), S(y));
        _optionsPage.Controls.Add(optionTitle);
        y += 26;

        var optionCard = BuildOptionCard(out var optionHeight);
        optionCard.Location = new Point(S(PageMargin), S(y));
        _optionsPage.Controls.Add(optionCard);
        y += optionHeight;

        var contentHeight = y + 24;

        foreach (var page in new[] { _optionsPage, _progressPage, _donePage })
        {
            page.SetBounds(0, pageTop, S(LogicalWidth), S(contentHeight));
            Controls.Add(page);
        }

        BuildProgressContent(contentHeight);
        BuildDoneContent(contentHeight);

        var footerTop = HeaderHeight + contentHeight;
        _footer = BuildFooter(footerTop);
        Controls.Add(_footer);

        ClientSize = new Size(S(LogicalWidth), S(footerTop + FooterHeight));

        _progressPage.Visible = false;
        _donePage.Visible = false;
        ShowPage(0);

        ResumeLayout(true);

        Shown += (sender, args) =>
        {
            _installButton.Focus();
        };
    }

    private HeaderBand BuildHeader()
    {
        var header = new HeaderBand { Bounds = new Rectangle(0, 0, S(LogicalWidth), S(HeaderHeight)) };
        header.Icon = Program.LoadHeaderIcon(S(44));
        header.IconBounds = new Rectangle(S(28), S(24), S(44), S(44));

        var title = new Label
        {
            Text = "ServerFlow 服务器排班",
            Font = F(15F, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Bounds = new Rectangle(S(86), S(22), S(430), S(30)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Text = _installed.Installed
                ? "覆盖安装会保留现有排班数据"
                : "服务器排班工具 · v" + Program.TargetVersion + " · 数据仅保存在本机",
            Font = F(9F),
            ForeColor = Theme.TextSecondary,
            BackColor = Color.Transparent,
            AutoSize = false,
            Bounds = new Rectangle(S(88), S(54), S(430), S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };

        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        if (_installed.Installed)
        {
            var pill = new PillLabel
            {
                HorizontalPadding = S(16),
                PillHeight = S(22)
            };
            pill.Font = F(8.5F);
            pill.Text = "已安装 v" + _installed.Version;
            pill.Location = new Point(S(LogicalWidth - PageMargin) - pill.Width, S(38));
            header.Controls.Add(pill);
        }

        return header;
    }

    private CardPanel BuildLocationCard(out int height)
    {
        const int pad = 18;
        const int captionWidth = 66;
        var width = LogicalWidth - PageMargin * 2;
        var card = new CardPanel();
        var valueFont = F(9F);

        var programCaption = MakeCaption("程序目录");
        programCaption.Location = new Point(S(pad), S(17));

        var installedPath = _installed.Installed && _installed.Location.Length > 0;
        var programWidth = width - pad * 2 - captionWidth;
        var programValue = MakeValue(installedPath
            ? MiddleEllipsis(_installed.Location, valueFont, S(programWidth))
            : "安装后由 Windows 创建");
        if (!installedPath)
        {
            programValue.ForeColor = Theme.TextTertiary;
        }

        programValue.Location = new Point(S(pad + captionWidth), S(17));
        programValue.Size = new Size(S(programWidth), S(20));
        if (installedPath)
        {
            _tips.SetToolTip(programValue, _installed.Location);
        }

        var dataCaption = MakeCaption("排班数据");
        dataCaption.Location = new Point(S(pad), S(49));

        var fullDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Packages\ServerFlow.Scheduler_pbhj1ej857fh2\LocalCache\Local\ServerFlow\schedules.json");

        var dataWidth = width - pad * 2 - captionWidth - 52;
        var dataValue = MakeValue(MiddleEllipsis(
            @"%LOCALAPPDATA%\Packages\ServerFlow.Scheduler_pbhj1ej857fh2\LocalCache\Local\ServerFlow\schedules.json",
            valueFont,
            S(dataWidth)));
        dataValue.Location = new Point(S(pad + captionWidth), S(49));
        dataValue.Size = new Size(S(dataWidth), S(20));
        _tips.SetToolTip(dataValue, fullDataPath);

        var copyButton = new FlatLinkButton
        {
            Text = "复制",
            Font = F(8.5F),
            ForeColor = Theme.Accent,
            HoverFill = Color.FromArgb(236, 242, 254),
            Bounds = new Rectangle(S(width - pad - 44), S(47), S(44), S(23))
        };
        copyButton.Click += (sender, args) =>
        {
            try
            {
                Clipboard.SetText(fullDataPath);
                copyButton.Text = "已复制";
                var timer = new Timer { Interval = 1600 };
                timer.Tick += (s2, a2) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (!copyButton.IsDisposed)
                    {
                        copyButton.Text = "复制";
                    }
                };
                timer.Start();
            }
            catch
            {
                // 剪贴板被占用时保持原状
            }
        };
        _tips.SetToolTip(copyButton, "复制完整路径：" + fullDataPath);

        var divider = new Panel
        {
            BackColor = Theme.Hairline,
            Bounds = new Rectangle(S(pad), S(81), S(width - pad * 2), Math.Max(1, S(1)))
        };

        const string hintText = "MSIX 应用由 Windows 统一安装，不支持自定义目录；更换安装盘请在系统设置中调整。";
        var hint = new Label
        {
            Text = hintText,
            Font = F(8.5F),
            ForeColor = Theme.TextTertiary,
            BackColor = Theme.Surface,
            AutoSize = true,
            MaximumSize = new Size(S(width - pad * 2), 0),
            Location = new Point(S(pad), S(91))
        };

        card.Controls.Add(programCaption);
        card.Controls.Add(programValue);
        card.Controls.Add(dataCaption);
        card.Controls.Add(dataValue);
        card.Controls.Add(copyButton);
        card.Controls.Add(divider);
        card.Controls.Add(hint);

        height = 91 + hint.Height + 14;
        card.Size = new Size(S(width), S(height));
        return card;
    }

    /// <summary>路径过长时保留头尾、省略中段，比尾部截断更容易辨认。</summary>
    private static string MiddleEllipsis(string text, Font font, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || TextRenderer.MeasureText(text, font).Width <= maxWidth)
        {
            return text;
        }

        var tail = Path.GetFileName(text);
        var head = text.Length > tail.Length ? text.Substring(0, text.Length - tail.Length) : string.Empty;
        var suffix = "…\\" + tail;
        var available = maxWidth - TextRenderer.MeasureText(suffix, font).Width - 2;
        if (available <= 0)
        {
            return suffix;
        }

        var cut = head.Length;
        while (cut > 0 && TextRenderer.MeasureText(head.Substring(0, cut), font).Width > available)
        {
            cut--;
        }

        return head.Substring(0, cut).TrimEnd('\\', '/') + suffix;
    }

    private CardPanel BuildOptionCard(out int height)
    {
        const int pad = 18;
        var width = LogicalWidth - PageMargin * 2;
        var card = new CardPanel();

        _shortcutBox = MakeCheckBox("创建桌面快捷方式", true);
        _shortcutBox.Location = new Point(S(pad), S(16));

        _launchBox = MakeCheckBox("安装完成后立即启动", true);
        _launchBox.Location = new Point(
            S(pad),
            S(16) + Math.Max(_shortcutBox.PreferredSize.Height, S(20)) + S(9));

        card.Controls.Add(_shortcutBox);
        card.Controls.Add(_launchBox);

        height = 16 + Math.Max(_shortcutBox.PreferredSize.Height, S(20)) + S(9) +
                 Math.Max(_launchBox.PreferredSize.Height, S(20)) + 16;
        card.Size = new Size(S(width), S(height));
        return card;
    }

    private void BuildProgressContent(int contentHeight)
    {
        var width = LogicalWidth;
        // 进度页隐藏底部操作区，按整窗高度做视觉居中
        var area = contentHeight + FooterHeight;
        var top = area / 2 - 60;

        _progressTitle = new Label
        {
            Text = "正在安装…",
            Font = F(13F, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            BackColor = Theme.Canvas,
            AutoSize = false,
            Bounds = new Rectangle(0, S(top), S(width), S(28)),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _progressPage.Controls.Add(_progressTitle);

        _progressStatus = new Label
        {
            Text = "正在准备…",
            Font = F(9F),
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Canvas,
            AutoSize = false,
            Bounds = new Rectangle(0, S(top + 40), S(width), S(22)),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _progressPage.Controls.Add(_progressStatus);

        _bar = new MarqueeBar
        {
            BackColor = Theme.Canvas,
            Bounds = new Rectangle((S(width) - S(320)) / 2, S(top + 76), S(320), S(6))
        };
        _progressPage.Controls.Add(_bar);

        _progressPage.Controls.Add(new Label
        {
            Text = "安装过程通常需要 10 秒左右，请勿关闭窗口。",
            Font = F(8.5F),
            ForeColor = Theme.TextTertiary,
            BackColor = Theme.Canvas,
            AutoSize = false,
            Bounds = new Rectangle(0, S(top + 94), S(width), S(20)),
            TextAlign = ContentAlignment.MiddleCenter
        });
    }

    private void BuildDoneContent(int contentHeight)
    {
        var width = LogicalWidth;
        var top = contentHeight / 2 - 78;
        var centerX = (S(width) - S(52)) / 2;

        _badge = new StateBadge
        {
            BackColor = Theme.Canvas,
            Bounds = new Rectangle(centerX, S(top), S(52), S(52))
        };
        _donePage.Controls.Add(_badge);

        _doneTitle = new Label
        {
            Text = "安装完成",
            Font = F(13F, FontStyle.Bold),
            ForeColor = Theme.TextPrimary,
            BackColor = Theme.Canvas,
            AutoSize = false,
            Bounds = new Rectangle(0, S(top + 68), S(width), S(28)),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _donePage.Controls.Add(_doneTitle);

        _doneMessage = new Label
        {
            Text = string.Empty,
            Font = F(9F),
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Canvas,
            AutoSize = false,
            Bounds = new Rectangle(S(70), S(top + 104), S(width - 140), S(52)),
            TextAlign = ContentAlignment.TopCenter
        };
        _donePage.Controls.Add(_doneMessage);

        _doneWarning = new Label
        {
            Text = string.Empty,
            Font = F(8.5F),
            ForeColor = Color.FromArgb(180, 83, 9),
            BackColor = Theme.Canvas,
            AutoSize = false,
            Visible = false,
            Bounds = new Rectangle(S(60), S(top + 158), S(width - 120), S(60)),
            TextAlign = ContentAlignment.TopCenter
        };
        _donePage.Controls.Add(_doneWarning);
    }

    private FooterBand BuildFooter(int footerTop)
    {
        var footer = new FooterBand
        {
            Bounds = new Rectangle(0, S(footerTop), S(LogicalWidth), S(FooterHeight))
        };

        var top = S(19);
        var primaryWidth = S(112);
        var secondaryWidth = S(96);
        var primaryLeft = S(LogicalWidth - PageMargin) - primaryWidth;
        var secondaryLeft = primaryLeft - S(10) - secondaryWidth;

        _uninstallButton = new FlatLinkButton
        {
            Text = "卸载",
            ForeColor = Theme.Danger,
            HoverFill = Color.FromArgb(252, 240, 240),
            AlignLeft = true,
            Font = F(9F),
            Bounds = new Rectangle(S(PageMargin), top, S(44), S(34)),
            Visible = _installed.Installed
        };
        _uninstallButton.Click += UninstallButton_Click;

        _cancelButton = new AccentButton
        {
            Text = "取消",
            Primary = false,
            Font = F(9F),
            Bounds = new Rectangle(secondaryLeft, top, secondaryWidth, S(34))
        };
        _cancelButton.Click += (sender, args) =>
        {
            if (!_busy)
            {
                Close();
            }
        };

        _installButton = new AccentButton
        {
            Text = _installed.Installed ? "覆盖安装" : "安装",
            Primary = true,
            Font = F(9F),
            Bounds = new Rectangle(primaryLeft, top, primaryWidth, S(34))
        };
        _installButton.Click += InstallButton_Click;

        _doneButton = new AccentButton
        {
            Text = "完成",
            Primary = true,
            Font = F(9F),
            Visible = false,
            Bounds = new Rectangle(primaryLeft, top, primaryWidth, S(34))
        };
        _doneButton.Click += DoneButton_Click;

        footer.Controls.Add(_uninstallButton);
        footer.Controls.Add(_cancelButton);
        footer.Controls.Add(_installButton);
        footer.Controls.Add(_doneButton);
        return footer;
    }

    private Panel NewPage()
    {
        return new Panel
        {
            BackColor = Theme.Canvas,
            Visible = true
        };
    }

    private Label MakeSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            Font = F(9F, FontStyle.Bold),
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Canvas,
            AutoSize = false,
            Bounds = new Rectangle(0, 0, S(200), S(18)),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Label MakeCaption(string text)
    {
        return new Label
        {
            Text = text,
            Font = F(8.5F),
            ForeColor = Theme.TextSecondary,
            BackColor = Theme.Surface,
            AutoSize = false,
            Bounds = new Rectangle(0, 0, S(58), S(20)),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private Label MakeValue(string text)
    {
        return new Label
        {
            Text = text,
            Font = F(9F),
            ForeColor = Theme.TextPrimary,
            BackColor = Theme.Surface,
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private CheckBox MakeCheckBox(string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            Checked = isChecked,
            AutoSize = true,
            Font = F(9F),
            ForeColor = Theme.TextPrimary,
            BackColor = Theme.Surface,
            Cursor = Cursors.Hand
        };
    }

    private void ShowPage(int index)
    {
        _optionsPage.Visible = index == 0;
        _progressPage.Visible = index == 1;
        _donePage.Visible = index == 2;

        _uninstallButton.Visible = index == 0 && _installed.Installed;
        _cancelButton.Visible = index == 0;
        _installButton.Visible = index == 0;
        _doneButton.Visible = index == 2;

        _bar.Running = index == 1;
        _footer.Visible = index != 1;

        if (index == 2)
        {
            AcceptButton = _doneButton;
        }
        else if (index == 0)
        {
            AcceptButton = _installButton;
        }
        else
        {
            AcceptButton = null;
        }
    }

    private void BeginWork(string title, string status)
    {
        _busy = true;
        ControlBox = false;
        _progressTitle.Text = title;
        _progressStatus.Text = status;
        ShowPage(1);
        Refresh();
        Application.DoEvents();
    }

    private void EndWork()
    {
        _busy = false;
        ControlBox = true;
    }

    private void Report(string text)
    {
        _progressStatus.Text = text;
        _progressStatus.Refresh();
        Application.DoEvents();
    }

    private void InstallButton_Click(object sender, EventArgs e)
    {
        var overwrite = _installed.Installed;
        var createShortcut = _shortcutBox.Checked;
        _willLaunch = _launchBox.Checked;

        BeginWork(overwrite ? "正在覆盖安装…" : "正在安装…", "正在解压安装文件…");

        var ok = Program.Install(_installed, createShortcut, Report, out var error, out var warning);
        EndWork();

        if (!ok)
        {
            _willLaunch = false;
            ExitCode = 1;
            ShowResult(false, "安装未完成", error);
            return;
        }

        ShowResult(
            true,
            overwrite ? "覆盖安装完成" : "安装完成",
            overwrite
                ? "ServerFlow " + Program.TargetVersion + " 已更新，原有排班数据保持不变。"
                : "ServerFlow " + Program.TargetVersion + " 已安装到本机，可从开始菜单打开。",
            warning);
    }

    private void UninstallButton_Click(object sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            this,
            "将卸载 ServerFlow 服务器排班，并删除本机排班数据。\n\n如需保留排班，请先备份 schedules.json。",
            "卸载 ServerFlow",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _willLaunch = false;
        BeginWork("正在卸载…", "正在移除 ServerFlow…");
        var ok = Program.Uninstall(Report, out var message);
        EndWork();

        if (!ok)
        {
            ExitCode = 1;
            ShowResult(false, "卸载未完成", message);
            return;
        }

        ShowResult(true, "已卸载", "ServerFlow 已从本机移除，排班数据已一并删除。");
    }

    private void ShowResult(bool success, string title, string message, string warning = null)
    {
        _badge.Success = success;
        _badge.Invalidate();
        _doneTitle.Text = title;
        _doneMessage.Text = message;
        _doneWarning.Text = warning ?? string.Empty;
        _doneWarning.Visible = !string.IsNullOrEmpty(warning);
        ShowPage(2);
        _doneButton.Focus();
    }

    private void DoneButton_Click(object sender, EventArgs e)
    {
        if (_willLaunch)
        {
            Program.Launch();
        }

        Close();
    }

    /// <summary>仅供 /render 预览流程页使用。</summary>
    internal void PreviewPage(int page)
    {
        if (page == 1)
        {
            _progressTitle.Text = "正在安装…";
            _progressStatus.Text = "正在安装 ServerFlow 服务器排班…";
            ShowPage(1);
        }
        else if (page == 2)
        {
            ShowResult(
                true,
                _installed.Installed ? "覆盖安装完成" : "安装完成",
                "ServerFlow " + Program.TargetVersion + " 已安装到本机，可从开始菜单打开。");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }
}
