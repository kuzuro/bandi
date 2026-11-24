using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Bandi;

/// <summary>
/// 좌측 설정 및 우측 실시간 미리보기를 제공하는 설정 창
/// </summary>
internal sealed class SettingsForm : Form
{
    private static readonly Color WindowBg = Color.FromArgb(243, 243, 243);
    private static readonly Color CardBg = Color.White;
    private static readonly Color CardBorder = Color.FromArgb(229, 229, 229);
    private static readonly Color DividerColor = Color.FromArgb(239, 239, 239);
    private static readonly Color AccentBlue = Color.FromArgb(0, 103, 192);
    private static readonly Color TextPrimary = Color.FromArgb(27, 27, 27);
    private static readonly Color TextSecondary = Color.FromArgb(96, 96, 96);

    private static readonly Color[] PresetColors =
    [
        Color.FromArgb(255, 255, 215, 40),  // 노랑
        Color.FromArgb(255, 255, 110, 40),  // 주황
        Color.FromArgb(255, 232, 65, 65),   // 빨강
        Color.FromArgb(255, 55, 185, 100),  // 초록
        Color.FromArgb(255, 35, 175, 220),  // 하늘
        Color.FromArgb(255, 65, 120, 245),  // 파랑
        Color.FromArgb(255, 165, 85, 235),  // 보라
        Color.FromArgb(255, 235, 90, 165)   // 분홍
    ];

    private readonly AppSettings _settings;
    private readonly Action _onSettingsChanged;
    private readonly Action<Rectangle?>? _onPreviewRectChanged;

    // 편집 중인 임시 값(적용/확인 시에만 실제 반영)
    private bool _draftHighlightEnabled;
    private bool _draftClickEffectEnabled;
    private bool _draftHotkeysEnabled;
    private bool _draftExcludeFromCapture;
    private int _draftCursorSize;
    private int _draftHighlightRadius;
    private int _draftHighlightAlpha;
    private Color _draftHighlightColor;
    private Color _draftLeftClickColor;
    private Color _draftRightClickColor;
    private Color _draftMiddleClickColor;

    private readonly Win11ToggleSwitch _tglHighlight;
    private readonly Win11ToggleSwitch _tglClickEffect;
    private readonly Win11ToggleSwitch _tglHotkeys;
    private readonly Win11ToggleSwitch _tglExcludeCapture;

    private readonly TrackBar _trkCursorSize;
    private readonly Label _lblCursorSizeValue;
    private readonly TrackBar _trkRadius;
    private readonly Label _lblRadiusValue;
    private readonly TrackBar _trkAlpha;
    private readonly Label _lblAlphaValue;

    private readonly ColorRowControl _highlightColorRow;
    private readonly ColorRowControl _leftClickColorRow;
    private readonly ColorRowControl _rightClickColorRow;
    private readonly ColorRowControl _middleClickColorRow;

    private readonly PreviewCanvasPanel _previewCanvas;
    private bool _isSyncing;

    public SettingsForm(
        AppSettings settings,
        Action onSettingsChanged,
        Action<Rectangle?>? onPreviewRectChanged = null,
        Icon? appIcon = null)
    {
        UiScaling.BeginLayout(this);

        _settings = settings;
        _onSettingsChanged = onSettingsChanged;
        _onPreviewRectChanged = onPreviewRectChanged;
        LoadDraftFromSettings();

        Text = "Bandi 설정";
        Icon = appIcon ?? Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WindowBg;
        Font = new Font("맑은 고딕", 9.5f);
        ClientSize = new Size(960, 726);

        const int marginX = 16;
        const int leftWidth = 428;
        int y = 14;

        // 좌측 상단 헤더
        var picIcon = new PictureBox
        {
            Image = Icon?.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(marginX, y + 2),
            Size = new Size(32, 32)
        };
        Controls.Add(picIcon);

        Controls.Add(new Label
        {
            Text = "마우스 강조 설정",
            Font = new Font("맑은 고딕", 13.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(marginX + 42, y - 1),
            AutoSize = true
        });

        Controls.Add(new Label
        {
            Text = "우측에서 테스트한 뒤 [적용] 또는 [확인]을 누르세요.",
            Font = new Font("맑은 고딕", 8.5f),
            ForeColor = TextSecondary,
            Location = new Point(marginX + 42, y + 23),
            AutoSize = true
        });

        y += 52;

        // 카드 1: 동작 및 단축키 (높이 198)
        var cardFeatures = new FluentCardPanel
        {
            Location = new Point(marginX, y),
            Size = new Size(leftWidth, 198)
        };
        Controls.Add(cardFeatures);

        _tglHighlight = AddToggleRow(cardFeatures, 6, "원형 하이라이트 사용", "단축키 Ctrl + Shift + H로 켜고 끕니다", _draftHighlightEnabled);
        AddDivider(cardFeatures, 51, leftWidth);
        _tglClickEffect = AddToggleRow(cardFeatures, 54, "클릭 파동 효과 사용", "좌/우/가운데 클릭을 서로 다른 색상으로 표시", _draftClickEffectEnabled);
        AddDivider(cardFeatures, 99, leftWidth);
        _tglHotkeys = AddToggleRow(cardFeatures, 102, "전역 단축키 활성화", "다른 앱을 사용 중일 때도 단축키가 동작합니다", _draftHotkeysEnabled);
        AddDivider(cardFeatures, 147, leftWidth);
        _tglExcludeCapture = AddToggleRow(cardFeatures, 150, "화면 캡처에서 제외", "스크린샷이나 화면 녹화에 강조 효과가 찍히지 않습니다", _draftExcludeFromCapture);

        y += cardFeatures.Height + 10;

        // 카드 2: 크기 및 투명도 (높이 212)
        var cardSizes = new FluentCardPanel
        {
            Location = new Point(marginX, y),
            Size = new Size(leftWidth, 212)
        };
        Controls.Add(cardSizes);

        (_trkCursorSize, _lblCursorSizeValue) = AddSliderSection(
            cardSizes, 10, leftWidth, "시스템 커서 크기",
            CursorSizeManager.MinSize, CursorSizeManager.MaxSize, _draftCursorSize, 16,
            v => $"{v} px",
            onScroll: val =>
            {
                _draftCursorSize = val;
                UpdatePreview();
            },
            resetButtonAction: (slider, label) =>
            {
                slider.Value = CursorSizeManager.DefaultSize;
                label.Text = $"{CursorSizeManager.DefaultSize} px";
                _draftCursorSize = CursorSizeManager.DefaultSize;
                UpdatePreview();
            });

        AddDivider(cardSizes, 76, leftWidth);

        (_trkRadius, _lblRadiusValue) = AddSliderSection(
            cardSizes, 82, leftWidth, "강조 원 반지름",
            12, 120, _draftHighlightRadius, 12,
            v => $"{v} px",
            onScroll: val =>
            {
                _draftHighlightRadius = val;
                UpdatePreview();
            });

        AddDivider(cardSizes, 144, leftWidth);

        (_trkAlpha, _lblAlphaValue) = AddSliderSection(
            cardSizes, 150, leftWidth, "강조 투명도",
            10, 220, _draftHighlightAlpha, 20,
            v => $"{(int)Math.Round(v / 255.0 * 100)}%",
            onScroll: val =>
            {
                _draftHighlightAlpha = val;
                UpdatePreview();
            });

        y += cardSizes.Height + 10;

        // 카드 3: 원클릭 색상 설정 (높이 168)
        var cardColors = new FluentCardPanel
        {
            Location = new Point(marginX, y),
            Size = new Size(leftWidth, 168)
        };
        Controls.Add(cardColors);

        _highlightColorRow = new ColorRowControl(cardColors, 10, "강조 원", _draftHighlightColor, PresetColors, c =>
        {
            _draftHighlightColor = c;
            UpdatePreview();
        });

        AddDivider(cardColors, 45, leftWidth);

        _leftClickColorRow = new ColorRowControl(cardColors, 50, "좌클릭", _draftLeftClickColor, PresetColors, c =>
        {
            _draftLeftClickColor = c;
            UpdatePreview();
        });

        AddDivider(cardColors, 85, leftWidth);

        _rightClickColorRow = new ColorRowControl(cardColors, 90, "우클릭", _draftRightClickColor, PresetColors, c =>
        {
            _draftRightClickColor = c;
            UpdatePreview();
        });

        AddDivider(cardColors, 125, leftWidth);

        _middleClickColorRow = new ColorRowControl(cardColors, 130, "가운데", _draftMiddleClickColor, PresetColors, c =>
        {
            _draftMiddleClickColor = c;
            UpdatePreview();
        });

        y += cardColors.Height + 14;

        // 좌측 하단 버튼: [취소] (미설치 시 [PC에 설치]), 우측 끝 [적용] [확인]
        var btnCancel = CreateActionButton("취소", isPrimary: false);
        btnCancel.Location = new Point(marginX, y);
        btnCancel.Click += (_, _) => Close();
        Controls.Add(btnCancel);

        if (!Installer.IsRunningFromInstallDir())
        {
            var btnInstall = CreateActionButton("PC에 설치", isPrimary: false);
            btnInstall.Size = new Size(92, 32);
            btnInstall.Location = new Point(marginX + 88, y);
            btnInstall.ForeColor = AccentBlue;
            btnInstall.FlatAppearance.BorderColor = AccentBlue;
            btnInstall.Click += (_, _) => InstallToProgramFiles();
            Controls.Add(btnInstall);
        }

        var btnApply = CreateActionButton("적용", isPrimary: false);
        btnApply.Location = new Point(marginX + leftWidth - 170, y);
        btnApply.Click += (_, _) => CommitDraftToSettings();
        Controls.Add(btnApply);

        var btnConfirm = CreateActionButton("확인", isPrimary: true);
        btnConfirm.Location = new Point(marginX + leftWidth - 80, y);
        btnConfirm.Click += (_, _) =>
        {
            CommitDraftToSettings();
            Close();
        };
        Controls.Add(btnConfirm);

        // 우측 전체 영역: 인터랙티브 테스트 미리보기 카드 (가로 넓게 확장)
        const int rightX = marginX + leftWidth + 14;
        const int rightWidth = 486;
        const int rightHeight = 694;

        var cardPreview = new FluentCardPanel
        {
            Location = new Point(rightX, 16),
            Size = new Size(rightWidth, rightHeight)
        };
        Controls.Add(cardPreview);

        cardPreview.Controls.Add(new Label
        {
            Text = "실시간 테스트 미리보기",
            Font = new Font("맑은 고딕", 10.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 14),
            AutoSize = true
        });

        cardPreview.Controls.Add(new Label
        {
            Text = "아래 영역에서 마우스를 움직이고 좌/우/가운데 클릭해 보세요.",
            Font = new Font("맑은 고딕", 8.4f),
            ForeColor = TextSecondary,
            Location = new Point(16, 36),
            AutoSize = true
        });

        _previewCanvas = new PreviewCanvasPanel
        {
            Location = new Point(14, 62),
            Size = new Size(rightWidth - 28, rightHeight - 76)
        };
        cardPreview.Controls.Add(_previewCanvas);

        AcceptButton = btnConfirm;
        CancelButton = btnCancel;

        Shown += (_, _) => NotifyPreviewBounds();
        LocationChanged += (_, _) => NotifyPreviewBounds();
        SizeChanged += (_, _) => NotifyPreviewBounds();

        UpdatePreview();

        UiScaling.EndLayout(this);
    }

    private void NotifyPreviewBounds()
    {
        if (_onPreviewRectChanged is null || IsDisposed || !_previewCanvas.IsHandleCreated) return;
        _onPreviewRectChanged(_previewCanvas.RectangleToScreen(_previewCanvas.ClientRectangle));
    }

    private void LoadDraftFromSettings()
    {
        _draftHighlightEnabled = _settings.HighlightEnabled;
        _draftClickEffectEnabled = _settings.ClickEffectEnabled;
        _draftHotkeysEnabled = _settings.HotkeysEnabled;
        _draftExcludeFromCapture = _settings.ExcludeFromCapture;
        _draftCursorSize = Math.Clamp(_settings.CursorBaseSize, CursorSizeManager.MinSize, CursorSizeManager.MaxSize);
        _draftHighlightRadius = Math.Clamp(_settings.HighlightRadius, 12, 120);
        _draftHighlightAlpha = Math.Clamp(_settings.HighlightAlpha, 10, 220);
        _draftHighlightColor = _settings.GetHighlightColor();
        _draftLeftClickColor = _settings.GetLeftClickColor();
        _draftRightClickColor = _settings.GetRightClickColor();
        _draftMiddleClickColor = _settings.GetMiddleClickColor();
    }

    private void CommitDraftToSettings()
    {
        bool cursorSizeChanged = _settings.CursorBaseSize != _draftCursorSize;

        _settings.HighlightEnabled = _draftHighlightEnabled;
        _settings.ClickEffectEnabled = _draftClickEffectEnabled;
        _settings.HotkeysEnabled = _draftHotkeysEnabled;
        _settings.ExcludeFromCapture = _draftExcludeFromCapture;
        _settings.CursorBaseSize = _draftCursorSize;
        _settings.HighlightRadius = _draftHighlightRadius;
        _settings.HighlightAlpha = _draftHighlightAlpha;
        _settings.HighlightColorArgb = _draftHighlightColor.ToArgb();
        _settings.LeftClickColorArgb = _draftLeftClickColor.ToArgb();
        _settings.RightClickColorArgb = _draftRightClickColor.ToArgb();
        _settings.MiddleClickColorArgb = _draftMiddleClickColor.ToArgb();

        _settings.Save();
        _onSettingsChanged();

        if (cursorSizeChanged)
        {
            int targetSize = _draftCursorSize;
            Cursor = Cursors.WaitCursor;
            Task.Run(() => CursorSizeManager.SetCursorSize(targetSize))
                .ContinueWith(_ =>
                {
                    if (!IsDisposed)
                    {
                        BeginInvoke(() => Cursor = Cursors.Default);
                    }
                });
        }
    }

    private void InstallToProgramFiles()
    {
        DialogResult confirm = MessageBox.Show(
            $"Bandi를 아래 경로에 설치하고 다시 실행할까요?\n\n{Installer.FixedInstallDir}",
            "Bandi - PC에 설치",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        if (!Installer.TryInstall(out string? targetExe, out string? installError) || targetExe is null)
        {
            MessageBox.Show(
                $"설치 중 오류가 발생했습니다.\n{installError}",
                "Bandi - 설치 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try
        {
            CommitDraftToSettings();

            // 자동 실행이 켜져 있다면 새 설치 경로로 작업 스케줄러 갱신
            if (StartupManager.IsEnabled())
            {
                StartupManager.SetEnabled(true, out _, targetExe);
            }

            Program.ReleaseMutexForRestart();
            Process.Start(new ProcessStartInfo
            {
                FileName = targetExe,
                UseShellExecute = true,
                Verb = "runas"
            });
            Application.Exit();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"설치 중 오류가 발생했습니다.\n{ex.Message}",
                "Bandi - 설치 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdatePreview()
    {
        _previewCanvas.UpdateState(
            _draftHighlightEnabled,
            _draftClickEffectEnabled,
            _draftCursorSize,
            _draftHighlightRadius,
            _draftHighlightAlpha,
            _draftHighlightColor,
            _draftLeftClickColor,
            _draftRightClickColor,
            _draftMiddleClickColor);
    }

    private static Button CreateActionButton(string text, bool isPrimary)
    {
        var btn = new Button
        {
            Text = text,
            Size = new Size(80, 32),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 9.2f, isPrimary ? FontStyle.Bold : FontStyle.Regular),
            BackColor = isPrimary ? AccentBlue : Color.White,
            ForeColor = isPrimary ? Color.White : TextPrimary,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = isPrimary ? AccentBlue : Color.FromArgb(205, 205, 205);
        return btn;
    }

    private Win11ToggleSwitch AddToggleRow(Control parent, int top, string title, string subtitle, bool initial)
    {
        parent.Controls.Add(new Label
        {
            Text = title,
            Font = new Font("맑은 고딕", 9.3f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, top + 2),
            AutoSize = true
        });

        parent.Controls.Add(new Label
        {
            Text = subtitle,
            Font = new Font("맑은 고딕", 8.0f),
            ForeColor = TextSecondary,
            Location = new Point(16, top + 20),
            AutoSize = true
        });

        var toggle = new Win11ToggleSwitch
        {
            Checked = initial,
            Location = new Point(parent.Width - 62, top + 10)
        };
        toggle.CheckedChanged += (_, _) =>
        {
            if (_isSyncing) return;
            _draftHighlightEnabled = _tglHighlight.Checked;
            _draftClickEffectEnabled = _tglClickEffect.Checked;
            _draftHotkeysEnabled = _tglHotkeys.Checked;
            _draftExcludeFromCapture = _tglExcludeCapture.Checked;
            UpdatePreview();
        };
        parent.Controls.Add(toggle);

        return toggle;
    }

    private static (TrackBar Slider, Label ValueLabel) AddSliderSection(
        Control parent,
        int top,
        int cardWidth,
        string title,
        int min,
        int max,
        int initial,
        int tickFrequency,
        Func<int, string> formatter,
        Action<int> onScroll,
        Action<TrackBar, Label>? resetButtonAction = null)
    {
        int clamped = Math.Clamp(initial, min, max);

        parent.Controls.Add(new Label
        {
            Text = title,
            Font = new Font("맑은 고딕", 9.2f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, top),
            AutoSize = true
        });

        var lblValue = new Label
        {
            Text = formatter(clamped),
            Font = new Font("맑은 고딕", 9.2f, FontStyle.Bold),
            ForeColor = AccentBlue,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(cardWidth - 150, top),
            Size = new Size(resetButtonAction is null ? 134 : 64, 20)
        };
        parent.Controls.Add(lblValue);

        var slider = new TrackBar
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = tickFrequency,
            SmallChange = Math.Max(1, tickFrequency / 2),
            LargeChange = tickFrequency,
            Value = clamped,
            BackColor = CardBg,
            TickStyle = TickStyle.None,
            Location = new Point(12, top + 22),
            Size = new Size(cardWidth - 24, 34)
        };
        slider.Scroll += (_, _) =>
        {
            lblValue.Text = formatter(slider.Value);
            onScroll(slider.Value);
        };
        parent.Controls.Add(slider);

        if (resetButtonAction is not null)
        {
            var btnReset = new Button
            {
                Text = "기본(32)",
                Font = new Font("맑은 고딕", 8.2f),
                Location = new Point(cardWidth - 80, top - 2),
                Size = new Size(64, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 246, 248),
                ForeColor = TextPrimary,
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderColor = Color.FromArgb(210, 210, 210);
            btnReset.Click += (_, _) => resetButtonAction(slider, lblValue);
            parent.Controls.Add(btnReset);
        }

        return (slider, lblValue);
    }

    private static void AddDivider(Control parent, int top, int cardWidth)
    {
        parent.Controls.Add(new Panel
        {
            BackColor = DividerColor,
            Location = new Point(16, top),
            Size = new Size(cardWidth - 32, 1)
        });
    }

    public void SyncFromSettings()
    {
        _isSyncing = true;
        try
        {
            LoadDraftFromSettings();
            _tglHighlight.Checked = _draftHighlightEnabled;
            _tglClickEffect.Checked = _draftClickEffectEnabled;
            _tglHotkeys.Checked = _draftHotkeysEnabled;
            _tglExcludeCapture.Checked = _draftExcludeFromCapture;
            _trkCursorSize.Value = _draftCursorSize;
            _trkRadius.Value = _draftHighlightRadius;
            _trkAlpha.Value = _draftHighlightAlpha;
            _lblCursorSizeValue.Text = $"{_draftCursorSize} px";
            _lblRadiusValue.Text = $"{_draftHighlightRadius} px";
            _lblAlphaValue.Text = $"{(int)Math.Round(_draftHighlightAlpha / 255.0 * 100)}%";
            _highlightColorRow.SetSelectedColor(_draftHighlightColor);
            _leftClickColorRow.SetSelectedColor(_draftLeftClickColor);
            _rightClickColorRow.SetSelectedColor(_draftRightClickColor);
            _middleClickColorRow.SetSelectedColor(_draftMiddleClickColor);
            UpdatePreview();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>
    /// 우측 전체 영역에서 실제 마우스 이동/클릭을 테스트할 수 있는 인터랙티브 미리보기 캔버스
    /// </summary>
    private sealed class PreviewCanvasPanel : Panel
    {
        private const int RippleDurationMs = 420;

        private sealed class PreviewRipple
        {
            public Point Pos { get; init; }
            public long StartTick { get; init; }
            public MouseButtons Button { get; init; }
        }

        private readonly System.Windows.Forms.Timer _animTimer;
        private readonly List<PreviewRipple> _ripples = [];

        private bool _highlightEnabled;
        private bool _clickEnabled;
        private int _cursorSize = CursorSizeManager.DefaultSize;
        private int _radius = 36;
        private int _alpha = 110;
        private Color _highlightColor = Color.Gold;
        private Color _leftClickColor = Color.OrangeRed;
        private Color _rightClickColor = Color.DodgerBlue;
        private Color _middleClickColor = Color.MediumSeaGreen;

        private bool _mouseInside;
        private Point _mousePos;

        public PreviewCanvasPanel()
        {
            BackColor = Color.FromArgb(247, 248, 251);
            DoubleBuffered = true;

            _animTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _animTimer.Tick += (_, _) =>
            {
                if (_ripples.Count > 0)
                {
                    Invalidate();
                }
                else
                {
                    _animTimer.Stop();
                }
            };
        }

        public void UpdateState(
            bool highlightEnabled,
            bool clickEnabled,
            int cursorSize,
            int radius,
            int alpha,
            Color highlightColor,
            Color leftClickColor,
            Color rightClickColor,
            Color middleClickColor)
        {
            _highlightEnabled = highlightEnabled;
            _clickEnabled = clickEnabled;
            _cursorSize = cursorSize;
            _radius = radius;
            _alpha = alpha;
            _highlightColor = highlightColor;
            _leftClickColor = leftClickColor;
            _rightClickColor = rightClickColor;
            _middleClickColor = middleClickColor;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _mouseInside = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouseInside = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouseInside = true;
            _mousePos = e.Location;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_highlightEnabled || !_clickEnabled) return;

            _ripples.Add(new PreviewRipple
            {
                Pos = e.Location,
                StartTick = Environment.TickCount64,
                Button = e.Button
            });
            _animTimer.Start();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 은은한 도트 그리드 배경
            using (var dotBrush = new SolidBrush(Color.FromArgb(220, 224, 232)))
            {
                for (int gx = 16; gx < Width; gx += 24)
                {
                    for (int gy = 16; gy < Height - 44; gy += 24)
                    {
                        g.FillEllipse(dotBrush, gx, gy, 2, 2);
                    }
                }
            }

            using (var border = new Pen(Color.FromArgb(228, 230, 236), 1f))
            {
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }

            float cx = _mouseInside ? _mousePos.X : Width / 2f;
            float cy = _mouseInside ? _mousePos.Y : (Height - 40) / 2f;

            // 강조 원
            if (_highlightEnabled)
            {
                using var hlBrush = new SolidBrush(Color.FromArgb(_alpha, _highlightColor));
                g.FillEllipse(hlBrush, cx - _radius, cy - _radius, _radius * 2f, _radius * 2f);
            }

            // 클릭 파동 애니메이션
            long now = Environment.TickCount64;
            for (int i = _ripples.Count - 1; i >= 0; i--)
            {
                PreviewRipple ripple = _ripples[i];
                long elapsed = now - ripple.StartTick;
                if (elapsed >= RippleDurationMs)
                {
                    _ripples.RemoveAt(i);
                    continue;
                }

                float progress = (float)elapsed / RippleDurationMs;
                float eased = 1f - (1f - progress) * (1f - progress);
                float rippleRadius = _radius * 0.6f + eased * (_radius * 1.15f + 24f);
                int rippleAlpha = (int)(220 * (1f - progress));

                Color rippleColor = ripple.Button switch
                {
                    MouseButtons.Right => _rightClickColor,
                    MouseButtons.Middle => _middleClickColor,
                    _ => _leftClickColor
                };
                using var pen = new Pen(Color.FromArgb(rippleAlpha, rippleColor), 3.2f * (1f - progress * 0.5f));
                g.DrawEllipse(pen, ripple.Pos.X - rippleRadius, ripple.Pos.Y - rippleRadius, rippleRadius * 2f, rippleRadius * 2f);
            }

            // 커서 크기 비율 미리보기 (32~256px -> 20~96px 스케일)
            float cursorScale = 20f + (_cursorSize - 32f) / (256f - 32f) * 76f;
            DrawCursorArrow(g, cx, cy, cursorScale);

            // 하단 상태 바
            var footerRect = new Rectangle(1, Height - 38, Width - 2, 37);
            using (var footerBrush = new SolidBrush(Color.FromArgb(250, 250, 252)))
            {
                g.FillRectangle(footerBrush, footerRect);
            }
            using (var divPen = new Pen(Color.FromArgb(232, 234, 238), 1f))
            {
                g.DrawLine(divPen, 1, Height - 38, Width - 2, Height - 38);
            }

            using var infoFont = new Font("맑은 고딕", 8.4f);
            using var infoBrush = new SolidBrush(TextSecondary);
            string status = $"커서 {_cursorSize}px  |  반지름 {_radius}px  |  투명도 {(int)Math.Round(_alpha / 255.0 * 100)}%";
            g.DrawString(status, infoFont, infoBrush, 12, Height - 27);
        }

        private static void DrawCursorArrow(Graphics g, float x, float y, float size)
        {
            PointF[] pts =
            [
                new PointF(x, y),
                new PointF(x, y + size),
                new PointF(x + size * 0.28f, y + size * 0.74f),
                new PointF(x + size * 0.48f, y + size * 1.12f),
                new PointF(x + size * 0.63f, y + size * 1.04f),
                new PointF(x + size * 0.43f, y + size * 0.67f),
                new PointF(x + size * 0.76f, y + size * 0.67f)
            ];
            g.FillPolygon(Brushes.White, pts);
            using var pen = new Pen(Color.FromArgb(40, 40, 40), 1.5f);
            g.DrawPolygon(pen, pts);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animTimer.Stop();
                _animTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class FluentCardPanel : Panel
    {
        public FluentCardPanel()
        {
            BackColor = CardBg;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = CreateRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8);
            using var pen = new Pen(CardBorder, 1f);
            e.Graphics.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class Win11ToggleSwitch : Control
    {
        private bool _checked;

        public event EventHandler? CheckedChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public Win11ToggleSwitch()
        {
            Size = new Size(44, 22);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            SizeF size = UiScaling.ApplyPaintScale(this, g);
            int w = (int)Math.Round(size.Width);
            int h = (int)Math.Round(size.Height);

            var trackRect = new Rectangle(1, 1, w - 3, h - 3);
            using var path = new GraphicsPath();
            int d = trackRect.Height;
            path.AddArc(trackRect.X, trackRect.Y, d, d, 90, 180);
            path.AddArc(trackRect.Right - d, trackRect.Y, d, d, 270, 180);
            path.CloseFigure();

            if (_checked)
            {
                using var fill = new SolidBrush(AccentBlue);
                g.FillPath(fill, path);
                g.FillEllipse(Brushes.White, w - 18, 4, 14, 14);
            }
            else
            {
                g.FillPath(Brushes.White, path);
                using var border = new Pen(Color.FromArgb(135, 135, 135), 1.4f);
                g.DrawPath(border, path);
                using var knob = new SolidBrush(Color.FromArgb(95, 95, 95));
                g.FillEllipse(knob, 5, 5, 12, 12);
            }
        }
    }

    private sealed class ColorCircleButton : Control
    {
        private bool _selected;
        private bool _hovered;

        public Color CircleColor { get; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        public ColorCircleButton(Color color)
        {
            CircleColor = color;
            Size = new Size(24, 24);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            SizeF size = UiScaling.ApplyPaintScale(this, g);

            if (_selected)
            {
                using var ringPen = new Pen(AccentBlue, 2f);
                g.DrawEllipse(ringPen, 2, 2, size.Width - 5, size.Height - 5);
            }
            else if (_hovered)
            {
                using var hoverPen = new Pen(Color.FromArgb(180, 180, 180), 1.5f);
                g.DrawEllipse(hoverPen, 2, 2, size.Width - 5, size.Height - 5);
            }

            using var brush = new SolidBrush(CircleColor);
            g.FillEllipse(brush, 5, 5, size.Width - 11, size.Height - 11);
        }
    }

    private sealed class ColorRowControl
    {
        private readonly ColorCircleButton[] _swatches;
        private readonly Panel _customPreview;
        private Color _selectedColor;

        public ColorRowControl(
            Control parent,
            int top,
            string title,
            Color initialColor,
            Color[] presets,
            Action<Color> onSelected)
        {
            _selectedColor = initialColor;

            parent.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("맑은 고딕", 9.2f, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(16, top + 3),
                Size = new Size(56, 22)
            });

            _customPreview = new Panel
            {
                BackColor = initialColor,
                Location = new Point(76, top + 5),
                Size = new Size(18, 18),
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(_customPreview);

            _swatches = new ColorCircleButton[presets.Length];
            int x = 108;
            for (int i = 0; i < presets.Length; i++)
            {
                Color c = presets[i];
                var btn = new ColorCircleButton(c)
                {
                    Location = new Point(x, top + 2),
                    Selected = c.ToArgb() == initialColor.ToArgb()
                };
                btn.Click += (_, _) =>
                {
                    SetSelectedColor(c);
                    onSelected(c);
                };
                parent.Controls.Add(btn);
                _swatches[i] = btn;
                x += 29;
            }

            var btnCustom = new PaletteIconButton
            {
                Location = new Point(parent.Width - 46, top + 1)
            };
            var tooltip = new ToolTip();
            tooltip.SetToolTip(btnCustom, "다른 색상 선택");
            btnCustom.Click += (_, _) =>
            {
                using var dialog = new ColorDialog { FullOpen = true, Color = _selectedColor };
                if (dialog.ShowDialog(parent.FindForm()) == DialogResult.OK)
                {
                    SetSelectedColor(dialog.Color);
                    onSelected(dialog.Color);
                }
            };
            parent.Controls.Add(btnCustom);
        }

        public void SetSelectedColor(Color color)
        {
            _selectedColor = color;
            _customPreview.BackColor = color;
            foreach (ColorCircleButton swatch in _swatches)
            {
                swatch.Selected = swatch.CircleColor.ToArgb() == color.ToArgb();
            }
        }
    }

    /// <summary>
    /// 컬러 팔레트 아이콘을 그리는 둥근 버튼
    /// </summary>
    private sealed class PaletteIconButton : Control
    {
        private bool _hovered;

        public PaletteIconButton()
        {
            Size = new Size(30, 26);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            SizeF size = UiScaling.ApplyPaintScale(this, g);
            var rect = new Rectangle(0, 0, (int)Math.Round(size.Width) - 1, (int)Math.Round(size.Height) - 1);
            using var bgPath = new GraphicsPath();
            int d = 8;
            bgPath.AddArc(rect.X, rect.Y, d, d, 180, 90);
            bgPath.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            bgPath.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            bgPath.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            bgPath.CloseFigure();

            using var bgBrush = new SolidBrush(_hovered ? Color.FromArgb(236, 240, 246) : Color.FromArgb(247, 248, 250));
            using var borderPen = new Pen(_hovered ? AccentBlue : Color.FromArgb(210, 214, 220), 1f);
            g.FillPath(bgBrush, bgPath);
            g.DrawPath(borderPen, bgPath);

            // 팔레트 본체 (화판 모양 원형)
            using var paletteBrush = new SolidBrush(Color.FromArgb(255, 245, 220));
            using var palettePen = new Pen(Color.FromArgb(95, 100, 110), 1.2f);
            g.FillEllipse(paletteBrush, 6, 4, 17, 17);
            g.DrawEllipse(palettePen, 6, 4, 17, 17);

            // 팔레트 위 4색 물감 점
            g.FillEllipse(Brushes.Crimson, 9, 8, 4, 4);
            g.FillEllipse(Brushes.Gold, 14, 7, 4, 4);
            g.FillEllipse(Brushes.DodgerBlue, 17, 11, 4, 4);
            g.FillEllipse(Brushes.MediumSeaGreen, 11, 14, 4, 4);
        }
    }
}
