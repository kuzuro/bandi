namespace Bandi;

/// <summary>
/// 고정 설치 경로가 아닌 위치에서 실행 시 설치 여부를 묻는 창
/// </summary>
internal sealed class InstallPromptForm : Form
{
    private static readonly Color WindowBg = Color.FromArgb(243, 243, 243);
    private static readonly Color AccentBlue = Color.FromArgb(0, 103, 192);
    private static readonly Color TextPrimary = Color.FromArgb(27, 27, 27);
    private static readonly Color TextSecondary = Color.FromArgb(96, 96, 96);

    private readonly RadioButton _radRunPortable;
    private readonly RadioButton _radInstall;
    private readonly CheckBox _chkDontAskAgain;

    /// <summary>설치 후 새 경로에서 실행하기로 선택된 실행 파일 경로 (설치 안 함이면 null)</summary>
    public string? InstalledExecutablePath { get; private set; }

    /// <summary>'다시 묻지 않기' 체크 여부</summary>
    public bool DontAskAgain => _chkDontAskAgain.Checked;

    public InstallPromptForm()
    {
        UiScaling.BeginLayout(this);

        Icon? appIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);

        Text = "bandi - 시작 옵션";
        Icon = appIcon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WindowBg;
        Font = new Font("맑은 고딕", 9.5f);
        ClientSize = new Size(480, 278);

        var picIcon = new PictureBox
        {
            Image = appIcon is not null ? new Icon(appIcon, 40, 40).ToBitmap() : null,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(20, 18),
            Size = new Size(40, 40)
        };
        Controls.Add(picIcon);

        Controls.Add(new Label
        {
            Text = "bandi 실행 방식을 선택해 주세요",
            Font = new Font("맑은 고딕", 13f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(72, 16),
            AutoSize = true
        });

        Controls.Add(new Label
        {
            Text = "현재 위치에서 바로 실행하거나, PC에 설치해 사용할 수 있습니다.",
            Font = new Font("맑은 고딕", 8.8f),
            ForeColor = TextSecondary,
            Location = new Point(72, 42),
            AutoSize = true
        });

        var card = new Panel
        {
            BackColor = Color.White,
            Location = new Point(20, 74),
            Size = new Size(440, 134),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(card);

        _radRunPortable = new RadioButton
        {
            Text = "설치하지 않고 현재 위치에서 바로 실행 (기본값)",
            Checked = true,
            Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 16),
            AutoSize = true
        };
        card.Controls.Add(_radRunPortable);

        _radInstall = new RadioButton
        {
            Text = "PC에 설치 후 실행",
            Checked = false,
            Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(16, 54),
            AutoSize = true
        };
        card.Controls.Add(_radInstall);

        card.Controls.Add(new Label
        {
            Text = $"설치 경로: {Installer.FixedInstallDir}",
            Font = new Font("맑은 고딕", 8.6f),
            ForeColor = TextSecondary,
            Location = new Point(36, 84),
            AutoSize = true
        });

        _chkDontAskAgain = new CheckBox
        {
            Text = "이 위치에서 실행할 때 다시 묻지 않기",
            Font = new Font("맑은 고딕", 8.8f),
            ForeColor = TextSecondary,
            Location = new Point(22, 232),
            AutoSize = true
        };
        Controls.Add(_chkDontAskAgain);

        _radRunPortable.CheckedChanged += (_, _) => _chkDontAskAgain.Enabled = _radRunPortable.Checked;
        _radInstall.CheckedChanged += (_, _) => _chkDontAskAgain.Enabled = _radRunPortable.Checked;

        var btnConfirm = new Button
        {
            Text = "확인",
            Location = new Point(360, 226),
            Size = new Size(100, 34),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("맑은 고딕", 9.5f, FontStyle.Bold),
            BackColor = AccentBlue,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnConfirm.FlatAppearance.BorderColor = AccentBlue;
        btnConfirm.Click += (_, _) => OnConfirm();
        Controls.Add(btnConfirm);

        AcceptButton = btnConfirm;

        UiScaling.EndLayout(this);
    }

    private void OnConfirm()
    {
        if (_radRunPortable.Checked)
        {
            InstalledExecutablePath = null;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (!Installer.TryInstall(out string? targetExe, out string? error))
        {
            MessageBox.Show(
                $"설치 중 오류가 발생했습니다.\n{error}",
                "bandi - 설치 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        InstalledExecutablePath = targetExe;
        DialogResult = DialogResult.OK;
        Close();
    }
}
