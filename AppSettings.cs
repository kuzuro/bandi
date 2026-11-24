using System.Text.Json;

namespace Bandi;

/// <summary>
/// 사용자 설정 저장 및 로드 관리
/// </summary>
internal sealed class AppSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Bandi");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>강조 효과 사용 여부</summary>
    public bool HighlightEnabled { get; set; } = true;

    /// <summary>클릭 파동 효과 사용 여부</summary>
    public bool ClickEffectEnabled { get; set; } = true;

    /// <summary>전역 단축키 사용 여부</summary>
    public bool HotkeysEnabled { get; set; } = true;

    /// <summary>화면 캡처에서 강조 효과 제외 여부</summary>
    public bool ExcludeFromCapture { get; set; } = true;

    /// <summary>강조 원 반지름(픽셀)</summary>
    public int HighlightRadius { get; set; } = 36;

    /// <summary>강조 투명도(10~220)</summary>
    public int HighlightAlpha { get; set; } = 110;

    /// <summary>강조 색상(기본: 노란색)</summary>
    public int HighlightColorArgb { get; set; } = Color.FromArgb(255, 255, 215, 40).ToArgb();

    /// <summary>좌클릭 파동 색상(기본: 주황색)</summary>
    public int LeftClickColorArgb { get; set; } = Color.FromArgb(255, 255, 110, 40).ToArgb();

    /// <summary>우클릭 파동 색상(기본: 파란색)</summary>
    public int RightClickColorArgb { get; set; } = Color.FromArgb(255, 65, 120, 245).ToArgb();

    /// <summary>가운데 클릭 파동 색상(기본: 초록색)</summary>
    public int MiddleClickColorArgb { get; set; } = Color.FromArgb(255, 55, 185, 100).ToArgb();

    /// <summary>Bandi 실행 중 적용할 시스템 커서 크기(32~256, 기본 32)</summary>
    public int CursorBaseSize { get; set; } = CursorSizeManager.DefaultSize;

    /// <summary>
    /// Bandi가 커서 크기를 바꾸기 전 사용자의 원래 커서 크기.
    /// 종료 시 이 값으로 복원하며, 비정상 종료 후 재실행 시에도 원래 값을 잃지 않도록 파일에 보관한다.
    /// </summary>
    public int? OriginalCursorSize { get; set; }

    /// <summary>설치 안내 건너뛰기 여부</summary>
    public bool SkipInstallPrompt { get; set; } = false;

    /// <summary>트레이 종료 안내 확인 여부</summary>
    public bool ExitGuideShown { get; set; } = false;

    public Color GetHighlightColor() => Color.FromArgb(HighlightColorArgb);
    public Color GetLeftClickColor() => Color.FromArgb(LeftClickColorArgb);
    public Color GetRightClickColor() => Color.FromArgb(RightClickColorArgb);
    public Color GetMiddleClickColor() => Color.FromArgb(MiddleClickColorArgb);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // 설정 파일 손상 시 기본값으로 복구
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // 설정 저장 실패 시 앱 동작은 유지
        }
    }
}
