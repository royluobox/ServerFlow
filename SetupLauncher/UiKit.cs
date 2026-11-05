using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal static class Theme
{
    public static readonly Color Canvas = Color.FromArgb(246, 247, 249);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(229, 232, 238);
    public static readonly Color BorderStrong = Color.FromArgb(211, 217, 226);
    public static readonly Color Hairline = Color.FromArgb(233, 236, 242);
    public static readonly Color TextPrimary = Color.FromArgb(23, 25, 31);
    public static readonly Color TextSecondary = Color.FromArgb(91, 98, 112);
    public static readonly Color TextTertiary = Color.FromArgb(147, 153, 165);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentHover = Color.FromArgb(30, 84, 214);
    public static readonly Color AccentPressed = Color.FromArgb(26, 68, 178);
    public static readonly Color Danger = Color.FromArgb(190, 46, 46);
    public static readonly Color Success = Color.FromArgb(21, 128, 76);

    public static Font Font(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
    }
}

internal static class Round
{
    public static GraphicsPath Create(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>圆角白卡片，替代老式 GroupBox 的蚀刻边框。</summary>
internal class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Surface;
        Radius = 8;
    }

    public int Radius { get; set; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // 整块自绘，避免默认背景覆盖圆角
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Round.Create(bounds, Radius))
        using (var fill = new SolidBrush(Theme.Surface))
        using (var pen = new Pen(Theme.Border))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
    }
}

/// <summary>顶部品牌区：淡蓝渐变 + 底部分隔线。</summary>
internal sealed class HeaderBand : Panel
{
    public HeaderBand()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.White;
    }

    public Image Icon { get; set; }

    public Rectangle IconBounds { get; set; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using (var brush = new LinearGradientBrush(
            new Rectangle(rect.X, rect.Y, rect.Width, rect.Height + 1),
            Color.FromArgb(242, 246, 255),
            Color.White,
            LinearGradientMode.Vertical))
        {
            e.Graphics.FillRectangle(brush, rect);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Icon != null)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(Icon, IconBounds);
        }

        using (var pen = new Pen(Theme.Hairline))
        {
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}

/// <summary>底部操作区：白底 + 顶部分隔线。</summary>
internal sealed class FooterBand : Panel
{
    public FooterBand()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.White;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var pen = new Pen(Theme.Hairline))
        {
            e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
    }
}

internal sealed class AccentButton : Button
{
    private bool _hover;
    private bool _pressed;

    public AccentButton()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.White;
        ForeColor = Theme.TextPrimary;
        Font = Theme.Font(9F);
        Cursor = Cursors.Hand;
        Radius = 6;
        Primary = true;
    }

    public bool Primary { get; set; }

    public int Radius { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        Color fill;
        Color text;
        var border = Color.Empty;

        if (!Enabled)
        {
            fill = Primary ? Color.FromArgb(231, 234, 240) : Color.FromArgb(249, 250, 252);
            text = Color.FromArgb(158, 164, 175);
            border = Primary ? Color.Empty : Theme.Border;
        }
        else if (Primary)
        {
            fill = _pressed ? Theme.AccentPressed : (_hover ? Theme.AccentHover : Theme.Accent);
            text = Color.White;
        }
        else
        {
            fill = _pressed
                ? Color.FromArgb(236, 239, 245)
                : (_hover ? Color.FromArgb(247, 248, 251) : Color.White);
            text = Theme.TextPrimary;
            border = Theme.BorderStrong;
        }

        using (var path = Round.Create(bounds, Radius))
        {
            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }

            if (border != Color.Empty)
            {
                using (var pen = new Pen(border))
                {
                    g.DrawPath(pen, path);
                }
            }

            if (Focused && ShowFocusCues && Enabled)
            {
                var inner = Rectangle.Inflate(bounds, -3, -3);
                using (var ring = Round.Create(inner, Math.Max(1, Radius - 3)))
                using (var pen = new Pen(
                    Primary ? Color.FromArgb(160, 255, 255, 255) : Color.FromArgb(120, Theme.Accent), 1.6f))
                {
                    g.DrawPath(pen, ring);
                }
            }
        }

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            bounds,
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

/// <summary>低权重的文字按钮，用于“复制”“卸载”。</summary>
internal sealed class FlatLinkButton : Button
{
    private bool _hover;

    public FlatLinkButton()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.White;
        Font = Theme.Font(9F);
        Cursor = Cursors.Hand;
        HoverFill = Color.FromArgb(240, 243, 249);
        Radius = 5;
    }

    public Color HoverFill { get; set; }

    public int Radius { get; set; }

    public bool AlignLeft { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

        if (_hover && Enabled)
        {
            using (var path = Round.Create(bounds, Radius))
            using (var brush = new SolidBrush(HoverFill))
            {
                g.FillPath(brush, path);
            }
        }

        var color = Enabled ? ForeColor : Theme.TextTertiary;
        if (Focused && ShowFocusCues && Enabled)
        {
            using (var path = Round.Create(Rectangle.Inflate(bounds, -2, -2), Math.Max(1, Radius - 2)))
            using (var pen = new Pen(Color.FromArgb(110, Theme.Accent)))
            {
                g.DrawPath(pen, path);
            }
        }

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            bounds,
            color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            (AlignLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
    }
}

/// <summary>圆角状态标签，例如“已安装 v1.0.7”。</summary>
internal sealed class PillLabel : Control
{
    private string _text = string.Empty;

    public PillLabel()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Font = Theme.Font(8.5F);
        PillFill = Color.FromArgb(234, 241, 254);
        PillText = Color.FromArgb(29, 78, 216);
        HorizontalPadding = 16;
        PillHeight = 22;
    }

    public Color PillFill { get; set; }

    public Color PillText { get; set; }

    public int HorizontalPadding { get; set; }

    public int PillHeight { get; set; }

    public override string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            AdjustSize();
            Invalidate();
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        AdjustSize();
    }

    private void AdjustSize()
    {
        var size = TextRenderer.MeasureText(
            _text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        Width = size.Width + HorizontalPadding * 2;
        Height = PillHeight;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Round.Create(bounds, Height / 2))
        using (var brush = new SolidBrush(PillFill))
        {
            g.FillPath(brush, path);
        }

        TextRenderer.DrawText(
            g,
            _text,
            Font,
            bounds,
            PillText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPrefix);
    }
}

/// <summary>细长的等待进度条，替代系统 chunky ProgressBar。</summary>
internal sealed class MarqueeBar : Control
{
    private readonly Timer _timer;
    private float _offset;

    public MarqueeBar()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Canvas;

        _timer = new Timer { Interval = 16 };
        _timer.Tick += (sender, args) =>
        {
            _offset += 0.014f;
            if (_offset > 1f)
            {
                _offset -= 1f;
            }

            Invalidate();
        };
    }

    public bool Running
    {
        get => _timer.Enabled;
        set
        {
            _timer.Enabled = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width, Height);
        var radius = Math.Max(2, Height / 2);

        using (var track = Round.Create(bounds, radius))
        {
            using (var brush = new SolidBrush(Color.FromArgb(232, 236, 243)))
            {
                g.FillPath(brush, track);
            }

            if (Running)
            {
                var clip = g.Clip;
                using (var region = new Region(track))
                {
                    g.SetClip(region, CombineMode.Intersect);
                    var segmentWidth = Math.Max(48, (int)(Width * 0.34f));
                    var travel = Width + segmentWidth;
                    var left = (int)(_offset * travel) - segmentWidth;
                    var segment = new Rectangle(left, 0, segmentWidth, Height);
                    using (var path = Round.Create(segment, radius))
                    using (var brush = new LinearGradientBrush(
                        new Rectangle(left - 1, 0, segmentWidth + 2, Math.Max(2, Height)),
                        Color.FromArgb(147, 185, 246),
                        Theme.Accent,
                        LinearGradientMode.Horizontal))
                    {
                        g.FillPath(brush, path);
                    }

                    g.Clip = clip;
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>完成/失败页的状态徽标。</summary>
internal sealed class StateBadge : Control
{
    public StateBadge()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);
        BackColor = Theme.Canvas;
        Success = true;
    }

    public bool Success { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var diameter = Math.Min(Width, Height);
        var bounds = new Rectangle(0, 0, diameter - 1, diameter - 1);
        var foreground = Success ? Theme.Success : Theme.Danger;
        var background = Success ? Color.FromArgb(230, 245, 237) : Color.FromArgb(252, 235, 235);

        using (var path = Round.Create(bounds, diameter / 2))
        using (var brush = new SolidBrush(background))
        {
            g.FillPath(brush, path);
        }

        using (var pen = new Pen(foreground, Math.Max(2f, diameter / 12f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            if (Success)
            {
                g.DrawLines(pen, new[]
                {
                    new PointF(diameter * 0.30f, diameter * 0.52f),
                    new PointF(diameter * 0.44f, diameter * 0.66f),
                    new PointF(diameter * 0.71f, diameter * 0.36f)
                });
            }
            else
            {
                g.DrawLine(pen, diameter * 0.35f, diameter * 0.35f, diameter * 0.65f, diameter * 0.65f);
                g.DrawLine(pen, diameter * 0.65f, diameter * 0.35f, diameter * 0.35f, diameter * 0.65f);
            }
        }
    }
}
