using System.Drawing.Drawing2D;

namespace Bandi;

/// <summary>
/// 트레이에서 설정 및 종료하는 방법을 안내하는 창
/// </summary>
internal sealed class ExitGuideForm : Form
{
    private static readonly Color WindowBg = Color.FromArgb(243, 243, 243);
    private static readonly Color AccentBlue = Color.FromArgb(0, 103, 192);
    private static readonly Color TextPrimary = Color.FromArgb(27, 27, 27);
    private static readonly Color TextSecondary = Color.FromArgb(88, 88, 88);

    public ExitGuideForm()
    {
        Icon? appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);

        Text = "Bandi - 사용 및 종료 방법";
        Icon = appIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WindowBg;
        Font = new Font("맑은 고딕", 10f);
        ClientSize = new Size(560, 476);

        var picIcon = new PictureBox
        {
            Image = appIcon is not null ? new Icon(appIcon, 44, 44).ToBitmap() : null,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(22, 18),
            Size = new Size(44, 44)
        };
        Controls.Add(picIcon);

        Controls.Add(new Label
        {
            Text = "시스템 트레이에서 실행 및 종료합니다",
            Font = new Font("맑은 고딕", 14f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(78, 16),
            AutoSize = true
        });

        Controls.Add(new Label
        {
            Text = "화면 우측 하단(작업 표시줄)의 Bandi 아이콘을 통해 설정하거나 종료할 수 있습니다.",
            Font = new Font("맑은 고딕", 9.2f),
            ForeColor = TextSecondary,
            Location = new Point(78, 45),
            Size = new Size(462, 24)
        });

        var guideCard = new TrayGuidePanel(appIcon)
        {
            Location = new Point(22, 82),
            Size = new Size(516, 326)
        };
        Controls.Add(guideCard);

        var btnStart = new Button
        {
            Text = "확인 (시작하기)",
            Location = new Point(398, 424),
            Size = new Size(140, 36),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 9.8f, FontStyle.Bold),
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnStart.FlatAppearance.BorderColor = AccentBlue;
        btnStart.Click += (_, _) => Close();
        Controls.Add(btnStart);

        AcceptButton = btnStart;
        CancelButton = btnStart;
    }

    /// <summary>
    /// 트레이 종료 방법 예시 패널
    /// </summary>
    private sealed class TrayGuidePanel : Panel
    {
        private readonly Bitmap? _appIconBitmap;

        public TrayGuidePanel(Icon? appIcon)
        {
            BackColor = Color.White;
            DoubleBuffered = true;
            if (appIcon is not null)
            {
                using var icon24 = new Icon(appIcon, 24, 24);
                _appIconBitmap = icon24.ToBitmap();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            using (var border = new Pen(Color.FromArgb(226, 228, 232), 1f))
            {
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }

            using var stepFont = new Font("맑은 고딕", 10f, FontStyle.Bold);
            using var menuFont = new Font("맑은 고딕", 9.5f);
            using var tipFont = new Font("맑은 고딕", 8.8f);
            using var titleBrush = new SolidBrush(TextPrimary);
            using var subBrush = new SolidBrush(TextSecondary);

            g.DrawString("종료 방법 예시:  트레이 아이콘 우클릭 → [종료]", stepFont, titleBrush, 16, 14);

            // 좌측: 작업 표시줄 트레이 영역 모형
            var taskbarRect = new Rectangle(22, 228, 178, 46);
            using (var tbBrush = new SolidBrush(Color.FromArgb(238, 242, 248)))
            using (var tbPen = new Pen(Color.FromArgb(208, 214, 224), 1f))
            {
                g.FillRectangle(tbBrush, taskbarRect);
                g.DrawRectangle(tbPen, taskbarRect);
            }

            // 숨겨진 아이콘 및 앱 아이콘 강조
            g.DrawString("^", menuFont, subBrush, 34, 240);

            var iconHighlight = new Rectangle(62, 234, 34, 34);
            using (var hlBrush = new SolidBrush(Color.FromArgb(255, 236, 225)))
            using (var hlPen = new Pen(Color.FromArgb(255, 110, 40), 1.8f))
            {
                g.FillRectangle(hlBrush, iconHighlight);
                g.DrawRectangle(hlPen, iconHighlight);
            }

            if (_appIconBitmap is not null)
            {
                g.DrawImage(_appIconBitmap, 67, 239, 24, 24);
            }

            using (var clockFont = new Font("맑은 고딕", 8f))
            {
                g.DrawString("오후 7:30", clockFont, subBrush, 116, 235);
                g.DrawString("2026-11-22", clockFont, subBrush, 112, 251);
            }

            // 1. 우클릭 배지
            using (var badge1 = new SolidBrush(Color.FromArgb(255, 110, 40)))
            using (var badgeFont = new Font("맑은 고딕", 8.8f, FontStyle.Bold))
            {
                g.FillRectangle(badge1, 30, 190, 98, 26);
                g.DrawString("1. 우클릭", badgeFont, Brushes.White, 48, 194);
            }

            // 우측: 트레이 컨텍스트 메뉴 모형
            var menuRect = new Rectangle(224, 52, 268, 222);
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
                "✓  강조 효과 켜기 (Ctrl+Shift+H)",
                "     설정...",
                "     ✓  윈도우 시작 시 자동 실행",
                "     종료"
            ];

            int itemY = menuRect.Y + 12;
            for (int i = 0; i < items.Length; i++)
            {
                if (i == 2 || i == 3)
                {
                    using var sepPen = new Pen(Color.FromArgb(234, 236, 240), 1f);
                    g.DrawLine(sepPen, menuRect.X + 10, itemY - 5, menuRect.Right - 10, itemY - 5);
                    itemY += 6;
                }

                bool isExitItem = i == 3;
                var rowRect = new Rectangle(menuRect.X + 8, itemY, menuRect.Width - 16, 38);

                if (isExitItem)
                {
                    using var exitBg = new SolidBrush(Color.FromArgb(255, 236, 238));
                    using var exitPen = new Pen(Color.FromArgb(210, 50, 60), 2f);
                    g.FillRectangle(exitBg, rowRect);
                    g.DrawRectangle(exitPen, rowRect);

                    using var boldFont = new Font("맑은 고딕", 10f, FontStyle.Bold);
                    using var exitBrush = new SolidBrush(Color.FromArgb(195, 35, 45));
                    g.DrawString(items[i], boldFont, exitBrush, rowRect.X + 8, rowRect.Y + 8);
                }
                else
                {
                    g.DrawString(items[i], menuFont, titleBrush, rowRect.X + 8, rowRect.Y + 9);
                }

                itemY += 44;
            }

            // 2. 클릭 배지
            using (var badge2 = new SolidBrush(Color.FromArgb(210, 50, 60)))
            using (var badgeFont = new Font("맑은 고딕", 8.8f, FontStyle.Bold))
            {
                g.FillRectangle(badge2, 412, 236, 70, 24);
                g.DrawString("2. 클릭", badgeFont, Brushes.White, 424, 239);
            }

            // 하단 팁
            g.DrawString("팁: 트레이 아이콘 좌클릭 = 강조 켜기/끄기  |  가운데 클릭 = 설정 열기", tipFont, subBrush, 16, 294);
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
