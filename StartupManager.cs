using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

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

        if (!enable)
        {
            return Run($"/Delete /TN \"{TaskName}\" /F", out errorMessage);
        }

        string? exePath = customExePath ?? Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            errorMessage = "실행 파일 경로를 확인할 수 없습니다.";
            return false;
        }

        // /SC ONLOGON으로 만들면 작업 스케줄러 기본값(배터리 사용 시 시작 안 함, 3일 후 강제 종료,
        // 낮은 우선순위)이 적용되므로, 설정을 명시한 XML로 등록한다.
        string xmlPath = Path.Combine(Path.GetTempPath(), $"bandi_task_{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, BuildTaskXml(exePath), Encoding.Unicode);
            return Run($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F", out errorMessage);
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch { /* 임시 파일 삭제 실패는 무시 */ }
        }
    }

    private static string BuildTaskXml(string exePath)
    {
        string userId = SecurityElement.Escape(WindowsIdentity.GetCurrent().Name);
        string command = SecurityElement.Escape(exePath);

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>bandi 마우스 강조 도구 자동 실행</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{userId}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{userId}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
                <AllowHardTerminate>true</AllowHardTerminate>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{command}</Command>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static bool Run(string arguments, out string? errorMessage)
    {
        errorMessage = null;
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
