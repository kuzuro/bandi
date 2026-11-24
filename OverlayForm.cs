using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Bandi;

/// <summary>
/// 커서 주변 원형 강조 및 클릭 파동 효과를 그리는 투명 오버레이 창
/// </summary>
internal sealed class OverlayForm : Form
{
    private const int CanvasSize = 320;
    private const int HalfCanvas = CanvasSize / 2;
    private const int RippleDurationMs = 360;

    private readonly AppSettings _settings;
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly List<ClickRipple> _ripples = [];

    private Bitmap? _cachedBitmap;
    private Point _cursorPos;
    private bool _needsRender = true;
    private int _topMostTickCount;

    /// <summary>이 영역 안에서는 실제 오버레이 표시를 일시 숨긴다.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Rectangle? SuppressedScreenRect { get; set; }

    private sealed class ClickRipple
    {
        public Point ScreenPos { get; init; }
        public long StartTick { get; init; }
        public MouseButtons Button { get; init; }
    }

    public OverlayForm(AppSettings settings)
    {
        _settings = settings;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(CanvasSize, CanvasSize);
        TopMost = true;

        if (NativeMethods.GetCursorPos(out var pt))
        {
            _cursorPos = new Point(pt.X, pt.Y);
        }

        _renderTimer = new System.Windows.Forms.Timer
        {
            Interval = 16 // 약 60fps
        };
        _renderTimer.Tick += OnRenderTick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                        | NativeMethods.WS_EX_TRANSPARENT
                        | NativeMethods.WS_EX_TOPMOST
                        | NativeMethods.WS_EX_TOOLWINDOW
                        | NativeMethods.WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyCaptureAffinity();
    }

    public void ApplyCaptureAffinity()
    {
        if (!IsHandleCreated) return;
        uint affinity = _settings.ExcludeFromCapture
            ? NativeMethods.WDA_EXCLUDEFROMCAPTURE
            : NativeMethods.WDA_NONE;
        NativeMethods.SetWindowDisplayAffinity(Handle, affinity);
    }

    private bool IsOverlayNeeded => _settings.HighlightEnabled;

    public void StartOverlay()
    {
        if (!IsOverlayNeeded)
        {
            StopOverlay();
            return;
        }

        ApplyCaptureAffinity();
        if (!Visible)
        {
            Show();
        }
        _needsRender = true;
        _renderTimer.Start();
        RenderFrame();
    }

    public void StopOverlay()
    {
        _renderTimer.Stop();
        _ripples.Clear();
        if (Visible)
        {
            Hide();
        }
    }

    public void UpdateCursorPosition(Point screenPos)
    {
        if (_cursorPos != screenPos)
        {
            _cursorPos = screenPos;
            _needsRender = true;
        }
    }

    public void TriggerClick(Point screenPos, MouseButtons button)
    {
        if (!IsOverlayNeeded || !_settings.ClickEffectEnabled) return;
        if (SuppressedScreenRect.HasValue && SuppressedScreenRect.Value.Contains(screenPos)) return;

        _cursorPos = screenPos;
        _ripples.Add(new ClickRipple
        {
            ScreenPos = screenPos,
            StartTick = Environment.TickCount64,
            Button = button
        });
        _needsRender = true;
    }

    public void RequestRedraw()
    {
        _needsRender = true;
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (!IsOverlayNeeded)
        {
            StopOverlay();
            return;
        }

        if (NativeMethods.GetCursorPos(out var pt))
        {
            var actual = new Point(pt.X, pt.Y);
            if (actual != _cursorPos)
            {
                _cursorPos = actual;
                _needsRender = true;
            }
        }

        if (_ripples.Count > 0)
        {
            _needsRender = true;
        }

        _topMostTickCount++;
        if (_topMostTickCount >= 60)
        {
            _topMostTickCount = 0;
            NativeMethods.SetWindowPos(
                Handle,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        if (_needsRender)
        {
            RenderFrame();
        }
    }

    private void RenderFrame()
    {
        _needsRender = false;
        long now = Environment.TickCount64;

        _cachedBitmap ??= new Bitmap(CanvasSize, CanvasSize, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(_cachedBitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceOver;

            // 설정 창 미리보기 영역 안에서는 실제 오버레이를 숨겨 겹침 방지
            bool suppressed = SuppressedScreenRect.HasValue && SuppressedScreenRect.Value.Contains(_cursorPos);
            if (!suppressed)
            {
                DrawEffects(g, now);
            }
        }

        ApplyLayeredBitmap(_cachedBitmap);
    }

    private void DrawEffects(Graphics g, long now)
    {
        int radius = Math.Clamp(_settings.HighlightRadius, 12, 120);

        // 1. 커서 주변 반투명 원형 하이라이트
        if (_settings.HighlightEnabled)
        {
            int alpha = Math.Clamp(_settings.HighlightAlpha, 10, 220);
            Color baseColor = _settings.GetHighlightColor();

            using var brush = new SolidBrush(Color.FromArgb(alpha, baseColor));
            g.FillEllipse(brush, HalfCanvas - radius, HalfCanvas - radius, radius * 2f, radius * 2f);
        }

        // 2. 클릭 파동 애니메이션
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            ClickRipple ripple = _ripples[i];
            long elapsed = now - ripple.StartTick;
            if (elapsed >= RippleDurationMs)
            {
                _ripples.RemoveAt(i);
                continue;
            }

            float progress = (float)elapsed / RippleDurationMs;
            float eased = 1f - (1f - progress) * (1f - progress);
            float rippleRadius = radius * 0.6f + eased * (radius * 1.15f + 24f);
            int rippleAlpha = (int)(220 * (1f - progress));

            float cx = HalfCanvas + (ripple.ScreenPos.X - _cursorPos.X);
            float cy = HalfCanvas + (ripple.ScreenPos.Y - _cursorPos.Y);

            Color rippleColor = ripple.Button switch
            {
                MouseButtons.Right => _settings.GetRightClickColor(),
                MouseButtons.Middle => _settings.GetMiddleClickColor(),
                _ => _settings.GetLeftClickColor()
            };

            using var pen = new Pen(Color.FromArgb(rippleAlpha, rippleColor), 3.2f * (1f - progress * 0.5f));
            g.DrawEllipse(pen, cx - rippleRadius, cy - rippleRadius, rippleRadius * 2f, rippleRadius * 2f);
        }
    }

    private void ApplyLayeredBitmap(Bitmap bitmap)
    {
        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;

        try
        {
            hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

            var size = new NativeMethods.SIZE { Width = bitmap.Width, Height = bitmap.Height };
            var sourcePoint = new NativeMethods.POINT { X = 0, Y = 0 };
            var topPos = new NativeMethods.POINT
            {
                X = _cursorPos.X - HalfCanvas,
                Y = _cursorPos.Y - HalfCanvas
            };
            var blend = new NativeMethods.BLENDFUNCTION
            {
                BlendOp = NativeMethods.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = NativeMethods.AC_SRC_ALPHA
            };

            NativeMethods.UpdateLayeredWindow(
                Handle,
                screenDc,
                ref topPos,
                ref size,
                memDc,
                ref sourcePoint,
                0,
                ref blend,
                NativeMethods.ULW_ALPHA);
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memDc, oldBitmap);
            }
            if (hBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(hBitmap);
            }
            NativeMethods.DeleteDC(memDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderTimer.Stop();
            _renderTimer.Dispose();
            _cachedBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }
}
