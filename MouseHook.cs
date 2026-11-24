using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bandi;

/// <summary>
/// 전역 마우스 훅 래퍼
/// </summary>
internal sealed class MouseHook : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _hookCallback;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _disposed;

    public event Action<Point>? MouseMoved;
    public event Action<Point, MouseButtons>? MouseClicked;

    public MouseHook()
    {
        // 가비지 컬렉터 수집 방지를 위해 필드에 보관
        _hookCallback = HookCallback;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;

        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessModule? mainModule = currentProcess.MainModule;
        IntPtr moduleHandle = NativeMethods.GetModuleHandle(mainModule?.ModuleName);

        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookCallback,
            moduleHandle,
            0);
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var pt = new Point(hookStruct.pt.X, hookStruct.pt.Y);
                int msg = wParam.ToInt32();

                if (msg == NativeMethods.WM_MOUSEMOVE)
                {
                    MouseMoved?.Invoke(pt);
                }
                else if (msg == NativeMethods.WM_LBUTTONDOWN)
                {
                    MouseClicked?.Invoke(pt, MouseButtons.Left);
                }
                else if (msg == NativeMethods.WM_RBUTTONDOWN)
                {
                    MouseClicked?.Invoke(pt, MouseButtons.Right);
                }
                else if (msg == NativeMethods.WM_MBUTTONDOWN)
                {
                    MouseClicked?.Invoke(pt, MouseButtons.Middle);
                }
            }
            catch
            {
                // 훅 콜백 내부 예외로 인해 다른 앱의 마우스 입력이 지연/차단되지 않도록 보호
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
