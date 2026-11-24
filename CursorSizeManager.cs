using Microsoft.Win32;

namespace Bandi;

/// <summary>
/// 윈도우 시스템 커서 크기 조회 및 실시간 변경
/// </summary>
internal static class CursorSizeManager
{
    private const string CursorsRegistryPath = @"Control Panel\Cursors";
    private const string AccessibilityRegistryPath = @"Software\Microsoft\Accessibility";
    private const string CursorBaseSizeValueName = "CursorBaseSize";
    private const string AccessibilityCursorSizeValueName = "CursorSize";

    public const int DefaultSize = 32;
    public const int MinSize = 32;
    public const int MaxSize = 256;

    /// <summary>
    /// 현재 레지스트리에 설정된 시스템 커서 크기를 반환한다.
    /// </summary>
    public static int GetCursorSize()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(CursorsRegistryPath, writable: false);
            if (key?.GetValue(CursorBaseSizeValueName) is int size)
            {
                return Math.Clamp(size, MinSize, MaxSize);
            }
        }
        catch
        {
            // 조회 실패 시 기본값 반환
        }

        return DefaultSize;
    }

    /// <summary>
    /// 시스템 커서 크기를 변경하고 즉시 적용한다.
    /// </summary>
    public static bool SetCursorSize(int size)
    {
        int clamped = Math.Clamp(size, MinSize, MaxSize);
        // 접근성 커서 크기는 1(32px)~15(256px) 단계(16px 간격)
        int accessibilityLevel = Math.Clamp((clamped - 32) / 16 + 1, 1, 15);

        try
        {
            using (RegistryKey? cursorsKey = Registry.CurrentUser.CreateSubKey(CursorsRegistryPath))
            {
                cursorsKey?.SetValue(CursorBaseSizeValueName, clamped, RegistryValueKind.DWord);
            }

            using (RegistryKey? accKey = Registry.CurrentUser.CreateSubKey(AccessibilityRegistryPath))
            {
                accKey?.SetValue(AccessibilityCursorSizeValueName, accessibilityLevel, RegistryValueKind.DWord);
            }

            // 커서 크기 즉시 반영
            NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_SETCURSORSIZE,
                0,
                new IntPtr(clamped),
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);

            // 커서 다시 불러오기
            NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_SETCURSORS,
                0,
                IntPtr.Zero,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);

            // 셸 및 실행 중인 앱에 설정 변경 전파
            NativeMethods.SendMessageTimeout(
                NativeMethods.HWND_BROADCAST,
                NativeMethods.WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "Accessibility",
                NativeMethods.SMTO_ABORTIFHUNG,
                150,
                out _);

            NativeMethods.SendMessageTimeout(
                NativeMethods.HWND_BROADCAST,
                NativeMethods.WM_SETTINGCHANGE,
                UIntPtr.Zero,
                @"Control Panel\Cursors",
                NativeMethods.SMTO_ABORTIFHUNG,
                150,
                out _);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
