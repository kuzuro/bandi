namespace Bandi;

/// <summary>
/// 고정 설치 경로(Program Files\bandi) 관련 공통 로직
/// </summary>
internal static class Installer
{
    public static string FixedInstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "bandi");

    /// <summary>
    /// 현재 프로세스가 고정 설치 경로에서 실행 중인지 확인한다.
    /// </summary>
    public static bool IsRunningFromInstallDir()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath)) return false;

        string? currentDir = Path.GetDirectoryName(Path.GetFullPath(exePath));
        return string.Equals(currentDir, Path.GetFullPath(FixedInstallDir), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 현재 실행 파일을 고정 설치 경로로 복사한다. 이미 설치 경로에서 실행 중이면 복사를 건너뛴다.
    /// </summary>
    /// <param name="targetExe">설치된 실행 파일 경로</param>
    /// <param name="errorMessage">실패 시 사용자에게 보여줄 메시지</param>
    public static bool TryInstall(out string? targetExe, out string? errorMessage)
    {
        targetExe = null;
        errorMessage = null;

        string? sourceExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(sourceExe) || !File.Exists(sourceExe))
        {
            errorMessage = "현재 실행 파일 경로를 확인할 수 없습니다.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(FixedInstallDir);
            string target = Path.Combine(FixedInstallDir, Path.GetFileName(sourceExe));

            // 실행 중인 자기 자신에게 덮어쓰기 방지
            if (!string.Equals(Path.GetFullPath(sourceExe), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourceExe, target, overwrite: true);
            }

            targetExe = target;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
