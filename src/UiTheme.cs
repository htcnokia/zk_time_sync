using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace ZkTimeSync
{
    /// <summary>
    /// 设计令牌（Design Tokens）——界面视觉的唯一来源。
    ///
    /// 视觉语言：Windows 11 Fluent Design 浅色主题（专业运维工具调性）。
    /// 三层结构：
    ///   Primitive（原始色值/字号，如 AccentBlue = #0067C0）
    ///        ↓
    ///   Semantic （语义令牌，如 Primary / TextSecondary / Border）
    ///        ↓
    ///   Component（组件令牌，见 UiButton / CardPanel / NavItem 等控件的取色逻辑）
    ///
    /// 所有界面代码只允许引用语义令牌，不直接写颜色十六进制值。
    /// 图标统一使用 Segoe MDL2 Assets（Fluent 官方图标字体），不使用 emoji。
    /// </summary>
    public static class Theme
    {
        // ==================== Primitive 原始色值 ====================
        // Windows 11 系统强调色（Accent Fill / Fluent Blue）
        private static readonly Color Blue50 = Color.FromArgb(0xE8, 0xF2, 0xFC);
        private static readonly Color Blue100 = Color.FromArgb(0xC9, 0xDF, 0xF5);
        private static readonly Color Blue500 = Color.FromArgb(0x00, 0x67, 0xC0);
        private static readonly Color Blue600 = Color.FromArgb(0x00, 0x5A, 0x9E);
        private static readonly Color Blue700 = Color.FromArgb(0x00, 0x45, 0x78);
        private static readonly Color Blue400 = Color.FromArgb(0x2E, 0x8C, 0xE0);

        // Windows 中性灰阶（Fluent Neutral / Layer 色）
        private static readonly Color Gray25 = Color.FromArgb(0xFB, 0xFB, 0xFB);
        private static readonly Color Gray50 = Color.FromArgb(0xF7, 0xF7, 0xF7);
        private static readonly Color Gray100 = Color.FromArgb(0xF3, 0xF3, 0xF3);
        private static readonly Color Gray150 = Color.FromArgb(0xEC, 0xEC, 0xEC);
        private static readonly Color Gray200 = Color.FromArgb(0xE1, 0xDF, 0xDD);
        private static readonly Color Gray300 = Color.FromArgb(0xC3, 0xC3, 0xC3);
        private static readonly Color Gray400 = Color.FromArgb(0xA6, 0xA6, 0xA6);
        private static readonly Color Gray500 = Color.FromArgb(0x8A, 0x8A, 0x8A);
        private static readonly Color Gray600 = Color.FromArgb(0x61, 0x61, 0x61);
        private static readonly Color Gray800 = Color.FromArgb(0x42, 0x42, 0x42);
        private static readonly Color Gray900 = Color.FromArgb(0x24, 0x24, 0x24);

        // Fluent 语义色（成功/警告/危险）
        private static readonly Color Green500 = Color.FromArgb(0x0F, 0x7B, 0x0F);
        private static readonly Color Green600 = Color.FromArgb(0x0B, 0x61, 0x0B);
        private static readonly Color Green50 = Color.FromArgb(0xE7, 0xF5, 0xE7);

        private static readonly Color Red500 = Color.FromArgb(0xC4, 0x2B, 0x1C);
        private static readonly Color Red550 = Color.FromArgb(0xA9, 0x24, 0x18);
        private static readonly Color Red650 = Color.FromArgb(0x8C, 0x1D, 0x13);
        private static readonly Color Red50 = Color.FromArgb(0xFD, 0xEC, 0xEA);

        private static readonly Color Orange500 = Color.FromArgb(0x9D, 0x62, 0x00);
        private static readonly Color Orange600 = Color.FromArgb(0x7A, 0x4C, 0x00);
        private static readonly Color Orange50 = Color.FromArgb(0xFF, 0xF4, 0xE2);

        // ==================== Semantic 语义令牌 ====================
        /// <summary>页面底色（Fluent 应用背景）</summary>
        public static readonly Color Bg = Gray100;
        /// <summary>卡片/面板底色（Fluent Layer1 表面）</summary>
        public static readonly Color Surface = Color.White;
        /// <summary>次级面板底色（表头、只读区、输入框填充）</summary>
        public static readonly Color SurfaceAlt = Gray50;
        public static readonly Color Border = Gray200;
        public static readonly Color BorderStrong = Gray300;
        /// <summary>交互态描边（hover 时加深）</summary>
        public static readonly Color BorderHover = Gray400;

        public static readonly Color TextPrimary = Gray900;
        public static readonly Color TextSecondary = Gray600;
        public static readonly Color TextMuted = Gray500;
        public static readonly Color TextInverse = Color.White;

        public static readonly Color Primary = Blue500;
        public static readonly Color PrimaryHover = Blue600;
        public static readonly Color PrimaryPressed = Blue700;
        public static readonly Color PrimarySoft = Blue50;
        public static readonly Color PrimarySoftBorder = Blue100;
        /// <summary>应用图标渐变起点</summary>
        public static readonly Color PrimaryGradient = Blue400;
        public static readonly Color Accent = Blue500;

        public static readonly Color Success = Green500;
        public static readonly Color SuccessSoft = Green50;
        public static readonly Color SuccessText = Green600;

        public static readonly Color Danger = Red500;
        public static readonly Color DangerHover = Red550;
        public static readonly Color DangerPressed = Red650;
        public static readonly Color DangerSoft = Red50;

        public static readonly Color Warning = Orange500;
        public static readonly Color WarningSoft = Orange50;
        public static readonly Color WarningText = Orange600;

        public static readonly Color DisabledBg = Gray150;
        public static readonly Color DisabledText = Gray400;
        public static readonly Color DisabledBorder = Gray200;

        /// <summary>进度条底槽 / 分隔填充</summary>
        public static readonly Color TrackBg = Gray200;
        /// <summary>列表选中行底色（Fluent 浅蓝高亮）</summary>
        public static readonly Color RowSelected = Blue50;

        // 侧边导航（Fluent NavigationView 浅色）
        public static readonly Color Sidebar = Gray150;
        public static readonly Color SidebarHover = Color.FromArgb(0xE0, 0xE0, 0xE0);
        public static readonly Color SidebarActive = Blue50;
        public static readonly Color SidebarActiveText = Blue500;
        public static readonly Color SidebarIndicator = Blue500;
        public static readonly Color SidebarText = Gray800;
        public static readonly Color SidebarTextActive = Blue500;
        public static readonly Color SidebarDivider = Gray200;
        public static readonly Color SidebarMuted = Gray500;

        // 日志控制台（Fluent 深色控制台配色，VS Code Dark 中性底）
        public static readonly Color ConsoleBg = Color.FromArgb(0x1E, 0x1E, 0x1E);
        public static readonly Color ConsoleText = Color.FromArgb(0xD4, 0xD4, 0xD4);
        public static readonly Color ConsoleInfo = Color.FromArgb(0x4F, 0xC1, 0xFF);
        public static readonly Color ConsoleWarn = Color.FromArgb(0xE5, 0xC0, 0x7B);
        public static readonly Color ConsoleError = Color.FromArgb(0xF1, 0x4C, 0x4C);
        public static readonly Color ConsoleSuccess = Color.FromArgb(0x4E, 0xC9, 0xB0);
        public static readonly Color ConsoleMuted = Color.FromArgb(0x85, 0x85, 0x85);

        // 数据表格
        public static readonly Color TableHeaderBg = Gray50;
        public static readonly Color TableRowAlt = Gray25;

        // ==================== 字体令牌 ====================
        // Fluent 标准字体为 Segoe UI Variable；GDI+ 下以 Segoe UI 呈现，
        // 中文由系统 FontLink 自动回退到 Microsoft YaHei UI。
        public static readonly Font FontBody = CreateFont(9f, FontStyle.Regular);
        public static readonly Font FontSmall = CreateFont(8.25f, FontStyle.Regular);
        public static readonly Font FontTiny = CreateFont(7.5f, FontStyle.Regular);
        public static readonly Font FontStrong = CreateFont(9f, FontStyle.Bold);
        public static readonly Font FontSection = CreateFont(11.5f, FontStyle.Bold);
        public static readonly Font FontTitle = CreateFont(16f, FontStyle.Bold);
        public static readonly Font FontValue = CreateFont(20f, FontStyle.Bold);
        public static readonly Font FontBrand = CreateFont(12f, FontStyle.Bold);
        public static readonly Font FontNav = CreateFont(9.75f, FontStyle.Regular);
        public static readonly Font FontMono = CreateMonoFont(8.75f, FontStyle.Regular);

        /// <summary>Segoe MDL2 Assets 图标字体（Fluent 官方图标）</summary>
        public static readonly Font FontIcon = CreateIconFont(15f);
        public static readonly Font FontIconSmall = CreateIconFont(12f);

        // ==================== 间距与圆角令牌 ====================
        public const int Sp4 = 4;
        public const int Sp8 = 8;
        public const int Sp12 = 12;
        public const int Sp16 = 16;
        public const int Sp24 = 24;
        public const int Sp32 = 32;

        // Fluent 圆角克制：控件 4px，卡片 6px
        public const int RadiusSm = 3;
        public const int RadiusMd = 4;
        public const int RadiusLg = 6;
        public const int RadiusXl = 8;

        /// <summary>控件统一高度：按钮</summary>
        public const int ButtonHeight = 32;
        /// <summary>控件统一高度：输入框</summary>
        public const int InputHeight = 26;

        // ==================== MDL2 图标字形（Segoe MDL2 Assets） ====================
        // 采用微软官方公开的常用码点；个别字形在不同 Windows 版本略有差异但均为有效图标。
        public const string IconHome = "\uE80F";
        public const string IconList = "\uE8FD";
        public const string IconClock = "\uE823";
        public const string IconSearch = "\uE721";
        public const string IconCalendar = "\uE787";
        public const string IconChat = "\uE8BD";
        public const string IconAdd = "\uE710";
        public const string IconCancel = "\uE711";
        public const string IconSave = "\uE74E";
        public const string IconRefresh = "\uE72C";
        public const string IconPlay = "\uE768";
        public const string IconFolder = "\uE838";
        public const string IconSettings = "\uE713";
        public const string IconDelete = "\uE74D";
        public const string IconTime = "\uE823";

        // ==================== 绘制辅助 ====================

        public static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int r = radius;
            int max = Math.Min(rect.Width, rect.Height) / 2;
            if (r > max) r = max;
            if (r <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int d = r * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle rect, int radius, Color color)
        {
            if (color.A == 0) return;
            using (GraphicsPath path = RoundedPath(rect, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRound(Graphics g, Rectangle rect, int radius, Color color, float width)
        {
            if (color.A == 0) return;
            using (GraphicsPath path = RoundedPath(rect, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>
        /// 用控件自身底色铺满客户区。自绘控件（UserPaint + OptimizedDoubleBuffer）
        /// 必须在每次绘制时整块清底，避免局部重绘残留像素导致文字/边框重影。
        /// </summary>
        public static void FillBackground(Graphics g, Control control)
        {
            using (SolidBrush brush = new SolidBrush(control.BackColor))
            {
                g.FillRectangle(brush, control.ClientRectangle);
            }
        }

        private static Font CreateFont(float size, FontStyle style)
        {
            string[] candidates = new string[] { "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", "Tahoma" };
            foreach (string name in candidates)
            {
                try
                {
                    Font f = new Font(name, size, style);
                    if (string.Equals(f.FontFamily.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                    f.Dispose();
                }
                catch { }
            }
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        private static Font CreateMonoFont(float size, FontStyle style)
        {
            string[] candidates = new string[] { "Consolas", "Courier New" };
            foreach (string name in candidates)
            {
                try
                {
                    Font f = new Font(name, size, style);
                    if (string.Equals(f.FontFamily.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                    f.Dispose();
                }
                catch { }
            }
            return new Font(FontFamily.GenericMonospace, size, style);
        }

        private static Font CreateIconFont(float size)
        {
            string[] candidates = new string[] { "Segoe MDL2 Assets", "Segoe UI Symbol" };
            foreach (string name in candidates)
            {
                try
                {
                    Font f = new Font(name, size);
                    if (string.Equals(f.FontFamily.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return f;
                    }
                    f.Dispose();
                }
                catch { }
            }
            return new Font(FontFamily.GenericSansSerif, size);
        }
    }

    /// <summary>按钮语义变体（Fluent Button 层级）</summary>
    public enum UiButtonKind
    {
        /// <summary>主要操作按钮（实心强调色）</summary>
        Primary,
        /// <summary>次要操作按钮（白底细描边）</summary>
        Secondary,
        /// <summary>危险操作按钮（实心红）</summary>
        Danger,
        /// <summary>弱化按钮（无底无框，仅文字）</summary>
        Ghost
    }

    /// <summary>
    /// 主题化按钮：含 default / hover / pressed / disabled 四态，
    /// 圆角、内边距、字体全部取自设计令牌。
    /// </summary>
    public class UiButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private UiButtonKind _kind = UiButtonKind.Secondary;

        public UiButtonKind Kind
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        public int CornerRadius { get; set; }

        public UiButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            CornerRadius = Theme.RadiusMd;
            BackColor = Theme.Surface;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Font = Theme.FontBody;
            ForeColor = Theme.TextPrimary;
            Height = Theme.ButtonHeight;
            Cursor = Cursors.Hand;
            Padding = new Padding(Theme.Sp12, 0, Theme.Sp12, 0);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            Theme.FillBackground(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color back, border, fore;
            ResolveColors(out back, out border, out fore);

            Theme.FillRound(g, rect, CornerRadius, back);
            if (border.A > 0)
            {
                Theme.DrawRound(g, rect, CornerRadius, border, 1f);
            }

            TextRenderer.DrawText(g, Text, Font, rect, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void ResolveColors(out Color back, out Color border, out Color fore)
        {
            if (!Enabled)
            {
                back = _kind == UiButtonKind.Ghost ? Theme.Surface : Theme.DisabledBg;
                border = _kind == UiButtonKind.Secondary ? Theme.DisabledBorder : Color.Transparent;
                fore = Theme.DisabledText;
                return;
            }

            switch (_kind)
            {
                case UiButtonKind.Primary:
                    back = _pressed ? Theme.PrimaryPressed : (_hover ? Theme.PrimaryHover : Theme.Primary);
                    border = Color.Transparent;
                    fore = Theme.TextInverse;
                    break;

                case UiButtonKind.Danger:
                    back = _pressed ? Theme.DangerPressed : (_hover ? Theme.DangerHover : Theme.Danger);
                    border = Color.Transparent;
                    fore = Theme.TextInverse;
                    break;

                case UiButtonKind.Ghost:
                    back = _hover ? (_pressed ? Theme.PrimarySoftBorder : Theme.PrimarySoft) : Theme.Surface;
                    border = Color.Transparent;
                    fore = Theme.Primary;
                    break;

                default: // Secondary：白底细描边，hover 时浅灰填充
                    back = _pressed ? Theme.Border : (_hover ? Theme.SurfaceAlt : Theme.Surface);
                    border = _pressed ? Theme.BorderHover : (_hover ? Theme.BorderHover : Theme.Border);
                    fore = Theme.TextPrimary;
                    break;
            }
        }

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

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }
    }

    /// <summary>
    /// 卡片容器：Fluent Layer1 白色卡片 + 1px 描边。
    /// 自身底色取页面底色，使圆角外区域自然过渡。
    /// </summary>
    public class CardPanel : Panel
    {
        private string _title = "";
        private string _subtitle = "";

        /// <summary>卡片标题（为空则不绘制标题区）</summary>
        public string Title
        {
            get { return _title; }
            set { _title = value == null ? "" : value; UpdateTitleSpace(); }
        }

        /// <summary>卡片副标题/说明文字</summary>
        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value == null ? "" : value; UpdateTitleSpace(); }
        }

        public int CornerRadius { get; set; }
        public bool ShowBorder { get; set; }

        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Theme.Bg;
            CornerRadius = Theme.RadiusLg;
            ShowBorder = true;
            Padding = new Padding(Theme.Sp16);
        }

        private void UpdateTitleSpace()
        {
            int h = 0;
            if (_title.Length > 0) h += 24;
            if (_subtitle.Length > 0) h += 18;
            if (h > 0) h += Theme.Sp4;

            Padding = new Padding(Theme.Sp16, Theme.Sp16 + h, Theme.Sp16, Theme.Sp16);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.FillBackground(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, rect, CornerRadius, Theme.Surface);
            if (ShowBorder)
            {
                Theme.DrawRound(g, rect, CornerRadius, Theme.Border, 1f);
            }

            int y = Theme.Sp16;
            if (_title.Length > 0)
            {
                TextRenderer.DrawText(g, _title, Theme.FontSection,
                    new Rectangle(Theme.Sp16, y, Width - Theme.Sp32, 22),
                    Theme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                y += 24;
            }
            if (_subtitle.Length > 0)
            {
                TextRenderer.DrawText(g, _subtitle, Theme.FontSmall,
                    new Rectangle(Theme.Sp16, y, Width - Theme.Sp32, 18),
                    Theme.TextMuted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <summary>
    /// 侧边栏顶部品牌区：Fluent 应用图标（蓝色方块 + MDL2 时钟图标）。
    /// </summary>
    public class BrandPanel : Panel
    {
        public BrandPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Sidebar;
            Height = 72;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.FillBackground(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle logo = new Rectangle(Theme.Sp16, (Height - 36) / 2, 36, 36);
            Theme.FillRound(g, logo, Theme.RadiusLg, Theme.Primary);
            TextRenderer.DrawText(g, Theme.IconClock, Theme.FontIcon, logo, Theme.TextInverse,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            int textLeft = 16 + 36 + Theme.Sp12;
            int textWidth = Width - textLeft - Theme.Sp12;
            int nameTop = Height / 2 - 20;

            TextRenderer.DrawText(g, "中控时间同步", Theme.FontBrand,
                new Rectangle(textLeft, nameTop, textWidth, 20), Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, "ZKTeco Time Sync", Theme.FontTiny,
                new Rectangle(textLeft, nameTop + 20, textWidth, 16), Theme.SidebarMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            using (Pen pen = new Pen(Theme.SidebarDivider))
            {
                g.DrawLine(pen, Theme.Sp16, Height - 1, Width - Theme.Sp16, Height - 1);
            }
        }
    }

    /// <summary>
    /// Fluent 导航项：选中态为浅蓝底 + 左侧 3px 强调色指示条。
    /// Glyph 使用 Segoe MDL2 Assets 图标字形。
    /// </summary>
    public class NavItem : Control
    {
        private bool _hover;
        private bool _active;
        private string _glyph = "";

        public string Glyph
        {
            get { return _glyph; }
            set { _glyph = value == null ? "" : value; Invalidate(); }
        }

        public bool Active
        {
            get { return _active; }
            set { _active = value; Invalidate(); }
        }

        public NavItem()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Sidebar;
            Font = Theme.FontNav;
            Height = 36;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.FillBackground(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (_active)
            {
                Theme.FillRound(g, rect, Theme.RadiusMd, Theme.SidebarActive);
                using (SolidBrush brush = new SolidBrush(Theme.SidebarIndicator))
                {
                    g.FillRectangle(brush, 0, 4, 3, rect.Height - 8);
                }
            }
            else if (_hover)
            {
                Theme.FillRound(g, rect, Theme.RadiusMd, Theme.SidebarHover);
            }

            Color fore = _active ? Theme.SidebarTextActive : Theme.SidebarText;
            int textLeft = Theme.Sp12 + 20;
            if (_glyph.Length > 0)
            {
                TextRenderer.DrawText(g, _glyph, Theme.FontIcon,
                    new Rectangle(Theme.Sp12, rect.Y, 20, rect.Height + 1), fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(textLeft, rect.Y, rect.Width - textLeft - Theme.Sp8, rect.Height + 1), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

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
    }

    /// <summary>
    /// 指标卡片：Fluent 信息卡，顶部细色条标识语义，下方数值与说明。
    /// </summary>
    public class StatCard : Control
    {
        private string _title = "";
        private string _value = "--";
        private string _caption = "";
        private Color _accent = Theme.Accent;

        public string Title
        {
            get { return _title; }
            set { _title = value == null ? "" : value; Invalidate(); }
        }

        public string Value
        {
            get { return _value; }
            set { _value = value == null ? "--" : value; Invalidate(); }
        }

        public string Caption
        {
            get { return _caption; }
            set { _caption = value == null ? "" : value; Invalidate(); }
        }

        public Color Accent
        {
            get { return _accent; }
            set { _accent = value; Invalidate(); }
        }

        public StatCard()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Height = 88;
            Font = Theme.FontBody;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.FillBackground(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, rect, Theme.RadiusLg, Theme.Surface);
            Theme.DrawRound(g, rect, Theme.RadiusLg, Theme.Border, 1f);

            // 顶部细色条（Fluent 克制的语义强调）
            using (SolidBrush brush = new SolidBrush(_accent))
            {
                g.FillRectangle(brush, Theme.Sp16, 12, 20, 3);
            }

            TextRenderer.DrawText(g, _title, Theme.FontSmall,
                new Rectangle(Theme.Sp16, 20, Width - Theme.Sp32, 18), Theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, _value, Theme.FontValue,
                new Rectangle(Theme.Sp16, 36, Width - Theme.Sp32, 34), Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (_caption.Length > 0)
            {
                TextRenderer.DrawText(g, _caption, Theme.FontTiny,
                    new Rectangle(Theme.Sp16, 68, Width - Theme.Sp32, 16), Theme.TextMuted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <summary>
    /// 细长圆角进度条
    /// </summary>
    public class UiProgress : Control
    {
        private int _value;
        private Color _barColor = Theme.Accent;
        public bool ShowText { get; set; }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = value;
                if (v < 0) v = 0;
                if (v > 100) v = 100;
                if (_value == v) return;
                _value = v;
                Invalidate();
            }
        }

        public Color BarColor
        {
            get { return _barColor; }
            set { _barColor = value; Invalidate(); }
        }

        public UiProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Height = 8;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.FillBackground(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int barHeight = ShowText ? Math.Max(6, Height - 18) : Height;
            Rectangle track = new Rectangle(0, 0, Width, barHeight);
            Theme.FillRound(g, track, barHeight / 2, Theme.TrackBg);

            int fillWidth = (int)(track.Width * (_value / 100.0));
            if (fillWidth > 0)
            {
                if (fillWidth < barHeight) fillWidth = barHeight;
                Theme.FillRound(g, new Rectangle(0, 0, fillWidth, barHeight), barHeight / 2, _barColor);
            }

            if (ShowText)
            {
                TextRenderer.DrawText(g, _value + "%", Theme.FontTiny,
                    new Rectangle(0, barHeight, Width, Height - barHeight), Theme.TextSecondary,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    /// <summary>
    /// Fluent 风格复选框：14px 方框 + 选中时强调色填充与白色对勾。
    /// 文本颜色由 ForeColor 决定，禁用态自动置灰。
    /// </summary>
    public class UiCheckBox : CheckBox
    {
        private const int BoxSize = 14;

        private bool _hover;

        public UiCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            AutoSize = false;
            BackColor = Theme.Surface;
            ForeColor = Theme.TextPrimary;
            Font = Theme.FontBody;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            Theme.FillBackground(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle box = new Rectangle(0, (Height - BoxSize) / 2, BoxSize, BoxSize);

            Color fill, border;
            if (!Enabled)
            {
                fill = Theme.DisabledBg;
                border = Theme.DisabledBorder;
            }
            else if (Checked)
            {
                fill = Theme.Primary;
                border = Theme.Primary;
            }
            else
            {
                fill = Theme.Surface;
                border = _hover ? Theme.BorderHover : Theme.BorderStrong;
            }

            Theme.FillRound(g, box, Theme.RadiusSm, fill);
            Theme.DrawRound(g, box, Theme.RadiusSm, border, 1f);

            if (Checked && Enabled)
            {
                using (Pen pen = new Pen(Theme.TextInverse, 1.7f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(box.X + 3.4f, box.Y + 7.2f),
                        new PointF(box.X + 6.0f, box.Y + 9.8f),
                        new PointF(box.X + 10.8f, box.Y + 4.2f)
                    });
                }
            }

            int textLeft = BoxSize + Theme.Sp8;
            Rectangle textRect = new Rectangle(textLeft, 0, Width - textLeft, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : Theme.DisabledText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

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
    }

    /// <summary>日志级别（决定控制台配色）</summary>
    public enum LogLevel
    {
        Info,
        Success,
        Warn,
        Error,
        Muted
    }

    /// <summary>
    /// 深色日志控制台（按级别着色，自动滚动，超长自动裁剪）
    /// </summary>
    public class UiLogConsole : RichTextBox
    {
        private const int MaxLines = 3000;

        public UiLogConsole()
        {
            BorderStyle = BorderStyle.None;
            BackColor = Theme.ConsoleBg;
            ForeColor = Theme.ConsoleText;
            Font = Theme.FontMono;
            ReadOnly = true;
            WordWrap = false;
            ScrollBars = RichTextBoxScrollBars.Both;
            DetectUrls = false;
            HideSelection = false;
        }

        public void AppendText(string message, LogLevel level)
        {
            if (message == null) return;

            Color color = ColorFor(level);
            bool atEnd = IsScrolledToBottom();

            SelectionStart = TextLength;
            SelectionLength = 0;
            SelectionColor = color;
            AppendText(message);
            SelectionColor = ForeColor;

            TrimIfTooLong();

            if (atEnd)
            {
                SelectionStart = TextLength;
                ScrollToCaret();
            }
        }

        /// <summary>根据日志正文自动判定级别</summary>
        public static LogLevel DetectLevel(string line)
        {
            if (string.IsNullOrEmpty(line)) return LogLevel.Muted;
            if (line.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("异常", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("失败原因", StringComparison.Ordinal) >= 0) return LogLevel.Error;
            if (line.IndexOf("[WARN]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("失败", StringComparison.Ordinal) >= 0) return LogLevel.Warn;
            if (line.IndexOf("[INFO]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("同步成功", StringComparison.Ordinal) >= 0 ||
                line.IndexOf("成功", StringComparison.Ordinal) >= 0) return LogLevel.Success;
            return LogLevel.Info;
        }

        private static Color ColorFor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error: return Theme.ConsoleError;
                case LogLevel.Warn: return Theme.ConsoleWarn;
                case LogLevel.Success: return Theme.ConsoleSuccess;
                case LogLevel.Muted: return Theme.ConsoleMuted;
                default: return Theme.ConsoleText;
            }
        }

        private bool IsScrolledToBottom()
        {
            int lastChar = Math.Max(0, TextLength - 1);
            int linesBelow = GetLineFromCharIndex(lastChar) - GetLineFromCharIndex(GetCharIndexFromPosition(new Point(1, Math.Max(1, ClientSize.Height - 4))));
            return linesBelow <= 2;
        }

        private void TrimIfTooLong()
        {
            if (Lines.Length <= MaxLines) return;

            int cutLines = Lines.Length - MaxLines;
            int cutChars = GetFirstCharIndexFromLine(cutLines);
            if (cutChars <= 0) return;

            Select(0, cutChars);
            SelectedText = "";
        }
    }

    /// <summary>
    /// 通用控件样式工具
    /// </summary>
    public static class UiStyle
    {
        public static void EnableDoubleBuffer(Control control)
        {
            try
            {
                PropertyInfo pi = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (pi != null) pi.SetValue(control, true, null);
            }
            catch { }
        }

        public static void StyleInput(TextBox box)
        {
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = Theme.FontBody;
            box.ForeColor = Theme.TextPrimary;
            box.BackColor = Theme.Surface;
        }

        public static void StyleCombo(ComboBox combo)
        {
            combo.FlatStyle = FlatStyle.Flat;
            combo.Font = Theme.FontBody;
            combo.ForeColor = Theme.TextPrimary;
            combo.BackColor = Theme.Surface;
            combo.DropDownStyle = ComboBoxStyle.DropDown;
        }

        public static void StyleCheck(CheckBox check)
        {
            check.FlatStyle = FlatStyle.Flat;
            check.Font = Theme.FontBody;
            check.ForeColor = Theme.TextPrimary;
            check.BackColor = Theme.Surface;
            check.Cursor = Cursors.Hand;
        }

        public static void StyleRadio(RadioButton radio)
        {
            radio.FlatStyle = FlatStyle.Flat;
            radio.Font = Theme.FontBody;
            radio.ForeColor = Theme.TextPrimary;
            radio.BackColor = Theme.Surface;
            radio.Cursor = Cursors.Hand;
        }

        public static Label CreateLabel(string text, Font font, Color color)
        {
            return CreateLabel(text, font, color, Theme.Surface);
        }

        /// <summary>
        /// 创建标签。backColor 必须是不透明色：卡片内用 Theme.Surface，
        /// 页面/侧边栏上分别用 Theme.Bg / Theme.Sidebar，避免透明子控件
        /// 叠加在自绘父控件上出现绘制残留。
        /// </summary>
        public static Label CreateLabel(string text, Font font, Color color, Color backColor)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = backColor;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            return label;
        }

        public static void StyleGrid(DataGridView grid)
        {
            EnableDoubleBuffer(grid);
            grid.BackgroundColor = Theme.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Theme.Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToOrderColumns = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.MultiSelect = true;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 36;
            grid.RowTemplate.Height = 32;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.TableHeaderBg;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.TableHeaderBg;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Font = Theme.FontSmall;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(Theme.Sp8, 0, Theme.Sp8, 0);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            grid.DefaultCellStyle.BackColor = Theme.Surface;
            grid.DefaultCellStyle.ForeColor = Theme.TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Theme.Primary;
            grid.DefaultCellStyle.SelectionForeColor = Theme.TextInverse;
            grid.DefaultCellStyle.Font = Theme.FontBody;
            grid.DefaultCellStyle.Padding = new Padding(Theme.Sp8, 0, Theme.Sp8, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.TableRowAlt;
        }
    }
}
