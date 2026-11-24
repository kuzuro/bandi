using System.Diagnostics;
using System.Security.Principal;

namespace Bandi;

internal static class Program
{
    private const string MutexName = @"Global\BandiMouseHighlighterMutex";
    private static Mutex? _singleInstanceMutex;

    internal static void ReleaseMutexForRestart()
    {
        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch
            {
                // 이미 해제되었거나 소유하지 않은 경우 무시
            }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
    }

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 관리자 권한 확인: 없으면 실행 방법 예시와 함께 안내 후 종료
        if (!IsRunningAsAdministrator())
        {
            Application.Run(new AdminRequiredForm());
            return;
        }

        // 중복 실행 방지
        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            MessageBox.Show(
                "Bandi가 이미 시스템 트레이에서 실행 중입니다.",
                "Bandi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            AppSettings settings = AppSettings.Load();

            // 1단계: 고정 설치 경로가 아닌 곳에서 실행 중일 때 설치 여부 확인
            if (!Installer.IsRunningFromInstallDir() && !settings.SkipInstallPrompt)
            {
                using var installForm = new InstallPromptForm();
                DialogResult result = installForm.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(installForm.InstalledExecutablePath))
                {
                    ReleaseMutexForRestart();

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = installForm.InstalledExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    return;
                }

                if (installForm.DontAskAgain)
                {
                    settings.SkipInstallPrompt = true;
                    settings.Save();
                }
            }

            // 2단계: 첫 실행 시 트레이 종료 방법 안내
            if (!settings.ExitGuideShown)
            {
                using var exitGuideForm = new ExitGuideForm();
                exitGuideForm.ShowDialog();

                settings.ExitGuideShown = true;
                settings.Save();
            }

            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
    }

    private static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
