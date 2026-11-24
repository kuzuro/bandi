namespace Bandi;

/// <summary>
/// 전역 단축키 등록 및 메시지 수신 관리
/// </summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    public const int HotkeyToggleHighlightId = 1;

    private bool _registered;
    private bool _disposed;

    public event Action? ToggleHighlightPressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams());
    }

    /// <summary>
    /// 단축키를 등록하거나 해제한다.
    /// </summary>
    /// <returns>요청한 상태가 되었으면 true. 다른 프로그램이 단축키를 선점해 등록에 실패하면 false.</returns>
    public bool SetEnabled(bool enabled)
    {
        if (enabled && !_registered)
        {
            uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
            _registered = NativeMethods.RegisterHotKey(Handle, HotkeyToggleHighlightId, modifiers, (uint)Keys.H);
            return _registered;
        }

        if (!enabled && _registered)
        {
            UnregisterAll();
        }

        return true;
    }

    private void UnregisterAll()
    {
        if (!_registered) return;
        NativeMethods.UnregisterHotKey(Handle, HotkeyToggleHighlightId);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyToggleHighlightId)
        {
            ToggleHighlightPressed?.Invoke();
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        DestroyHandle();
    }
}
