using System.Drawing.Drawing2D;

namespace Bandi;

/// <summary>
/// 시스템 트레이 상주, 메뉴/단축키 제어 및 오버레이/훅 생명주기 관리
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly NotifyIcon _trayIcon;
    private readonly Icon _activeIcon;
    private readonly Icon _inactiveIcon;
    private readonly OverlayForm _overlayForm;
    private readonly MouseHook _mouseHook;
    private readonly HotkeyManager _hotkeyManager;
    private readonly ToolStripMenuItem _toggleHighlightItem;
    private readonly ToolStripMenuItem _startupItem;
    private SettingsForm? _settingsForm;
    private bool _hotkeyFailureNotified;
    private bool _cleanedUp;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();

        ApplyCursorSizeOnStartup();

        _overlayForm = new OverlayForm(_settings);

        _mouseHook = new MouseHook();
        _mouseHook.MouseMoved += pt => _overlayForm.UpdateCursorPosition(pt);
        _mouseHook.MouseClicked += (pt, btn) => _overlayForm.TriggerClick(pt, btn);
        _mouseHook.Start();

        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.ToggleHighlightPressed += () => OnToggleHighlight(null, EventArgs.Empty);

        _activeIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
            ?? CreateTrayIcon(active: true);
        _inactiveIcon = CreateTrayIcon(active: false);

        _toggleHighlightItem = new ToolStripMenuItem("강조 효과 켜기 (Ctrl+Shift+H)", null, OnToggleHighlight)
        {
            Checked = _settings.HighlightEnabled
        };

        _startupItem = new ToolStripMenuItem("윈도우 시작 시 자동 실행", null, OnToggleStartup)
        {
            Checked = StartupManager.IsEnabled()
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_toggleHighlightItem);
        menu.Items.Add(new ToolStripMenuItem("설정...", null, OnOpenSettings));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("종료", null, OnExit));

        _trayIcon = new NotifyIcon
        {
            Icon = _activeIcon,
            Text = "Bandi - 마우스 강조기",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.MouseClick += OnTrayMouseClick;

        SyncState();
    }

    private void OnTrayMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            OnToggleHighlight(sender, EventArgs.Empty);
        }
        else if (e.Button == MouseButtons.Middle)
        {
            OnOpenSettings(sender, EventArgs.Empty);
        }
    }

    private void OnToggleHighlight(object? sender, EventArgs e)
    {
        _settings.HighlightEnabled = !_settings.HighlightEnabled;
        _settings.Save();
        SyncState();
    }

    private void SyncState()
    {
        _toggleHighlightItem.Checked = _settings.HighlightEnabled;
        if (!_hotkeyManager.SetEnabled(_settings.HotkeysEnabled) && !_hotkeyFailureNotified)
        {
            // 다른 프로그램이 같은 단축키를 이미 사용 중인 경우 한 번만 안내
            _hotkeyFailureNotified = true;
            _trayIcon.ShowBalloonTip(
                5000,
                "Bandi",
                "Ctrl+Shift+H 단축키를 다른 프로그램이 사용 중이라 등록하지 못했습니다. 트레이 아이콘 클릭으로 켜고 끌 수 있습니다.",
                ToolTipIcon.Warning);
        }

        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.SyncFromSettings();
        }

        if (_settings.HighlightEnabled)
        {
            _overlayForm.StartOverlay();
            _overlayForm.RequestRedraw();
            _trayIcon.Icon = _activeIcon;
            _trayIcon.Text = "Bandi - 마우스 강조기 (켜짐)";
        }
        else
        {
            _overlayForm.StopOverlay();
            _trayIcon.Icon = _inactiveIcon;
            _trayIcon.Text = "Bandi - 마우스 강조기 (꺼짐)";
        }
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(
            _settings,
            SyncState,
            rect => _overlayForm.SuppressedScreenRect = rect,
            _activeIcon);
        _settingsForm.FormClosed += (_, _) =>
        {
            _overlayForm.SuppressedScreenRect = null;
            _settingsForm = null;
        };
        _settingsForm.Show();
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        bool targetState = !_startupItem.Checked;
        if (StartupManager.SetEnabled(targetState, out string? error))
        {
            _startupItem.Checked = targetState;
        }
        else
        {
            MessageBox.Show(
                $"자동 실행 설정 변경에 실패했습니다.\n{error}",
                "Bandi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Cleanup();
        ExitThread();
        Application.Exit();
    }

    /// <summary>
    /// 원래 커서 크기를 기억해 두고, 설정된 크기와 다를 때만 시스템 커서 크기를 변경한다.
    /// </summary>
    private void ApplyCursorSizeOnStartup()
    {
        int current = CursorSizeManager.GetCursorSize();

        // 이전 실행이 비정상 종료되어 값이 남아 있으면 그 값을 원래 크기로 유지
        if (_settings.OriginalCursorSize is null)
        {
            _settings.OriginalCursorSize = current;
            _settings.Save();
        }

        if (current != _settings.CursorBaseSize)
        {
            CursorSizeManager.SetCursorSize(_settings.CursorBaseSize);
        }
    }

    /// <summary>
    /// 종료 시 사용자의 원래 커서 크기로 복원한다.
    /// </summary>
    private void RestoreCursorSize()
    {
        if (_settings.OriginalCursorSize is not int original) return;

        if (CursorSizeManager.GetCursorSize() != original)
        {
            CursorSizeManager.SetCursorSize(original);
        }

        _settings.OriginalCursorSize = null;
        _settings.Save();
    }

    /// <summary>
    /// 리소스 해제 및 커서 복원. 종료 메뉴와 Dispose 양쪽에서 호출되므로 한 번만 실행된다.
    /// </summary>
    private void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;

        _trayIcon.Visible = false;
        _hotkeyManager.Dispose();
        _mouseHook.Dispose();
        _overlayForm.StopOverlay();
        _overlayForm.Dispose();
        _settingsForm?.Dispose();
        _trayIcon.Dispose();
        _activeIcon.Dispose();
        _inactiveIcon.Dispose();

        RestoreCursorSize();
    }

    private static Icon CreateTrayIcon(bool active)
    {
        using var bmp = new Bitmap(32, 32);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            Color fillColor = active
                ? Color.FromArgb(210, 255, 210, 40)
                : Color.FromArgb(140, 150, 150, 150);
            Color ringColor = active
                ? Color.FromArgb(255, 255, 130, 30)
                : Color.FromArgb(200, 100, 100, 100);

            using var glowBrush = new SolidBrush(fillColor);
            g.FillEllipse(glowBrush, 2, 2, 28, 28);

            using var ringPen = new Pen(ringColor, 2f);
            g.DrawEllipse(ringPen, 3, 3, 26, 26);

            Point[] pointer =
            [
                new Point(11, 8),
                new Point(11, 23),
                new Point(15, 19),
                new Point(19, 25),
                new Point(21, 24),
                new Point(17, 18),
                new Point(22, 18)
            ];
            g.FillPolygon(Brushes.White, pointer);
            g.DrawPolygon(Pens.Black, pointer);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DeleteObject(hIcon);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cleanup();
        }
        base.Dispose(disposing);
    }
}
