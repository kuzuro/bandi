using System.Diagnostics;

namespace Bandi;

/// <summary>
/// 윈도우 작업 스케줄러를 이용한 부팅 시 관리자 권한 자동 실행 관리
/// </summary>
internal static class StartupManager
{
    private const string TaskName = "BandiMouseHighlighter";

    /// <summary>
    /// 현재 자동 실행 작업이 등록되어 있는지 확인한다.
    /// </summary>
    public static bool IsEnabled()
    {
        int? exitCode = RunSchtasks($"/Query /TN \"{TaskName}\"", 3000, out _);
        return exitCode == 0;
    }

    /// <summary>
    /// 작업 스케줄러에 최고 수준 권한으로 로그온 시 자동 실행을 등록하거나 해제한다.
    /// </summary>
    public static bool SetEnabled(bool enable, out string? errorMessage, string? customExePath = null)
    {
        errorMessage = null;
        string? exePath = customExePath ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            errorMessage = "실행 파일 경로를 확인할 수 없습니다.";
            return false;
        }

        string arguments = enable
            ? $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\"\" /SC ONLOGON /RL HIGHEST /F"
            : $"/Delete /TN \"{TaskName}\" /F";

        int? exitCode = RunSchtasks(arguments, 5000, out string stderr);
        if (exitCode == 0) return true;

        errorMessage = exitCode is null
            ? (string.IsNullOrWhiteSpace(stderr) ? "작업 스케줄러가 응답하지 않습니다." : stderr)
            : (string.IsNullOrWhiteSpace(stderr) ? $"작업 스케줄러 오류 (코드: {exitCode})" : stderr);
        return false;
    }

    /// <summary>
    /// schtasks.exe를 실행하고 종료 코드를 반환한다. 실행 실패 또는 시간 초과 시 null.
    /// </summary>
    private static int? RunSchtasks(string arguments, int timeoutMs, out string stderr)
    {
        stderr = string.Empty;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null)
            {
                stderr = "schtasks.exe를 실행하지 못했습니다.";
                return null;
            }

            // 출력 버퍼가 가득 차 교착되지 않도록 두 스트림을 모두 비동기로 비운다
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { /* 이미 종료된 경우 무시 */ }
                return null;
            }

            Task.WaitAll([stdoutTask, stderrTask], 1000);
            stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result.Trim() : string.Empty;
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            stderr = ex.Message;
            return null;
        }
    }
}
