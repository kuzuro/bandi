using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Bandi;

/// <summary>
/// 관리자 권한이 없을 때 실행 방법을 안내하는 창
/// </summary>
internal sealed class AdminRequiredForm : Form
{
    private static readonly Color WindowBg = Color.FromArgb(243, 243, 243);
    private static readonly Color AccentBlue = Color.FromArgb(0, 103, 192);
    private static readonly Color TextPrimary = Color.FromArgb(27, 27, 27);
    private static readonly Color TextSecondary = Color.FromArgb(88, 88, 88);

    public AdminRequiredForm()
    {
        UiScaling.BeginLayout(this);

        Icon? appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);

        Text = "Bandi - 관리자 권한 필요";
        Icon = appIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WindowBg;
        Font = new Font("맑은 고딕", 10f);
        ClientSize = new Size(560, 476);

        // 상단 안내 헤더
        var shieldPanel = new ShieldIconPanel
        {
            Location = new Point(22, 20),
            Size = new Size(44, 44)
        };
        Controls.Add(shieldPanel);

        Controls.Add(new Label
        {
            Text = "관리자 권한으로 실행해 주세요",
            Font = new Font("맑은 고딕", 14.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(78, 16),
            AutoSize = true
        });

        Controls.Add(new Label
        {
            Text = "커서 크기 변경과 자동 실행 등록에 관리자 권한이 필요합니다.",
            Font = new Font("맑은 고딕", 9.6f),
            ForeColor = TextSecondary,
            Location = new Point(78, 46),
            Size = new Size(460, 36)
        });

        // 실행 방법 예시 카드
        var guideCard = new GuideIllustrationPanel(appIcon)
        {
            Location = new Point(22, 84),
            Size = new Size(516, 324)
        };
        Controls.Add(guideCard);

        // 하단 버튼: 좌측 바로 재실행 / 우측 확인(종료)
        var btnRunAsAdmin = new Button
        {
            Text = "관리자 권한으로 다시 실행",
            Location = new Point(22, 424),
            Size = new Size(216, 36),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 9.8f, FontStyle.Bold),
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnRunAsAdmin.FlatAppearance.BorderColor = AccentBlue;
        btnRunAsAdmin.Click += (_, _) => TryRestartAsAdmin();
        Controls.Add(btnRunAsAdmin);

        var btnClose = new Button
        {
            Text = "확인 (종료)",
            Location = new Point(418, 424),
            Size = new Size(120, 36),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 9.8f),
            BackColor = Color.White,
            ForeColor = TextPrimary,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(205, 205, 205);
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        AcceptButton = btnClose;
        CancelButton = btnClose;

        UiScaling.EndLayout(this);
    }

    private void TryRestartAsAdmin()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });
            Close();
        }
        catch
        {
            // 권한 상승 창 취소 시 현재 창 유지
        }
    }

    /// <summary>
    /// 관리자 방패 벡터 아이콘
    /// </summary>
    private sealed class ShieldIconPanel : Control
    {
        public ShieldIconPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = WindowBg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            SizeF size = UiScaling.ApplyPaintScale(this, g);
            float width = size.Width;
            float height = size.Height;

            using var path = new GraphicsPath();
            float w = width - 4f;
            float h = height - 4f;
            float cx = width / 2f;

            path.AddLine(cx, 3f, width - 5f, 8f);
            path.AddBezier(width - 5f, 8f, width - 4f, h * 0.62f, cx + w * 0.25f, h * 0.88f, cx, height - 3f);
            path.AddBezier(cx, height - 3f, cx - w * 0.25f, h * 0.88f, 4f, h * 0.62f, 5f, 8f);
            path.CloseFigure();

            using var grad = new LinearGradientBrush(
                new RectangleF(0, 0, width, height),
                Color.FromArgb(40, 140, 230),
                AccentBlue,
                LinearGradientMode.Vertical);
            g.FillPath(grad, path);

            using var borderPen = new Pen(Color.FromArgb(0, 82, 160), 1.4f);
            g.DrawPath(borderPen, path);

            // 방패 내부 흰색 자물쇠/느낌표 심볼
            using var whiteBrush = new SolidBrush(Color.White);
            g.FillRectangle(whiteBrush, cx - 2.2f, 13f, 4.4f, 12f);
            g.FillEllipse(whiteBrush, cx - 2.4f, 28f, 4.8f, 4.8f);
        }
    }

    /// <summary>
    /// 관리자 권한 실행 방법 예시 패널
    /// </summary>
    private sealed class GuideIllustrationPanel : Panel
    {
        private readonly Bitmap? _appIconBitmap;

        public GuideIllustrationPanel(Icon? appIcon)
        {
            BackColor = Color.White;
            DoubleBuffered = true;
            if (appIcon is not null)
            {
                using var largeIcon = new Icon(appIcon, 64, 64);
                _appIconBitmap = largeIcon.ToBitmap();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            SizeF size = UiScaling.ApplyPaintScale(this, g);

            using (var border = new Pen(Color.FromArgb(226, 228, 232), 1f))
            {
                g.DrawRectangle(border, 0, 0, size.Width - 1, size.Height - 1);
            }

            using var stepFont = UiScaling.PaintFont(10f, FontStyle.Bold);
            using var subFont = UiScaling.PaintFont(9.5f);
            using var titleBrush = new SolidBrush(TextPrimary);

            g.DrawString("실행 방법 예시:  Bandi.exe 마우스 우클릭 → [관리자 권한으로 실행]", stepFont, titleBrush, 16, 14);

            // 좌측: 실행 파일 아이콘 모형
            var fileBox = new Rectangle(26, 62, 120, 114);
            using (var selBrush = new SolidBrush(Color.FromArgb(232, 243, 255)))
            using (var selPen = new Pen(Color.FromArgb(155, 200, 245), 1.2f))
            {
                g.FillRectangle(selBrush, fileBox);
                g.DrawRectangle(selPen, fileBox);
            }

            if (_appIconBitmap is not null)
            {
                g.DrawImage(_appIconBitmap, 54, 74, 64, 64);
            }

            using var fileFont = UiScaling.PaintFont(9.5f, FontStyle.Bold);
            g.DrawString("Bandi.exe", fileFont, titleBrush, 52, 146);

            // 1. 우클릭 배지
            using (var badgeBrush = new SolidBrush(Color.FromArgb(255, 110, 40)))
            using (var badgeFont = UiScaling.PaintFont(9f, FontStyle.Bold))
            {
                g.FillRectangle(badgeBrush, 36, 188, 100, 26);
                g.DrawString("1. 우클릭", badgeFont, Brushes.White, 55, 192);
            }

            // 우측: 우클릭 메뉴 모형
            var menuRect = new Rectangle(178, 56, 314, 240);
            using (var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0)))
            {
                g.FillRectangle(shadowBrush, menuRect.X + 4, menuRect.Y + 5, menuRect.Width, menuRect.Height);
            }
            g.FillRectangle(Brushes.White, menuRect);
            using (var menuBorder = new Pen(Color.FromArgb(212, 214, 218), 1f))
            {
                g.DrawRectangle(menuBorder, menuRect);
            }

            string[] items =
            [
                "열기(O)",
                "관리자 권한으로 실행(A)",
                "작업 표시줄에 고정",
                "경로로 복사",
                "속성(R)"
            ];

            int itemY = menuRect.Y + 12;
            for (int i = 0; i < items.Length; i++)
            {
                bool isAdminItem = i == 1;
                var rowRect = new Rectangle(menuRect.X + 8, itemY, menuRect.Width - 16, 40);

                if (isAdminItem)
                {
                    using var hlBrush = new SolidBrush(Color.FromArgb(230, 242, 255));
                    using var hlPen = new Pen(AccentBlue, 2f);
                    g.FillRectangle(hlBrush, rowRect);
                    g.DrawRectangle(hlPen, rowRect);

                    PointF[] shield =
                    [
                        new PointF(rowRect.X + 12, rowRect.Y + 10),
                        new PointF(rowRect.X + 26, rowRect.Y + 10),
                        new PointF(rowRect.X + 26, rowRect.Y + 21),
                        new PointF(rowRect.X + 19, rowRect.Y + 29),
                        new PointF(rowRect.X + 12, rowRect.Y + 21)
                    ];
                    using var shieldBrush = new SolidBrush(AccentBlue);
                    g.FillPolygon(shieldBrush, shield);

                    using var boldFont = UiScaling.PaintFont(10f, FontStyle.Bold);
                    using var accentBrush = new SolidBrush(AccentBlue);
                    g.DrawString(items[i], boldFont, accentBrush, rowRect.X + 36, rowRect.Y + 9);
                }
                else
                {
                    g.DrawString(items[i], subFont, titleBrush, rowRect.X + 36, rowRect.Y + 9);
                }

                itemY += 44;
            }

            // 2. 클릭 배지
            using (var step2Brush = new SolidBrush(AccentBlue))
            using (var badgeFont = UiScaling.PaintFont(8.8f, FontStyle.Bold))
            {
                g.FillRectangle(step2Brush, 412, 66, 72, 24);
                g.DrawString("2. 클릭", badgeFont, Brushes.White, 424, 69);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _appIconBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
