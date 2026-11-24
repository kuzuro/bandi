namespace Bandi;

/// <summary>
/// 코드로 배치한 폼을 화면 배율(125%, 150% 등)에 맞게 자동 확대한다.
/// 모든 좌표와 크기는 100%(96 DPI) 기준으로 작성되어 있다.
/// </summary>
internal static class UiScaling
{
    private const float DesignDpi = 96f;

    /// <summary>폼 생성자 맨 앞에서 호출. 컨트롤 배치 전에 자동 배율 기준을 설정한다.</summary>
    public static void BeginLayout(Form form)
    {
        form.SuspendLayout();
        form.AutoScaleDimensions = new SizeF(DesignDpi, DesignDpi);
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }

    /// <summary>폼 생성자 맨 끝에서 호출. 이 시점에 현재 DPI 배율로 모든 컨트롤이 확대된다.</summary>
    public static void EndLayout(Form form)
    {
        form.ResumeLayout(false);
        form.PerformLayout();
    }

    /// <summary>
    /// 직접 그리는 컨트롤에서 100% 기준 좌표를 그대로 쓸 수 있도록 Graphics에 배율을 적용하고,
    /// 100% 기준의 논리 크기를 돌려준다.
    /// </summary>
    public static SizeF ApplyPaintScale(Control control, Graphics g)
    {
        float k = control.DeviceDpi / DesignDpi;
        if (k != 1f)
        {
            g.ScaleTransform(k, k);
        }
        return new SizeF(control.Width / k, control.Height / k);
    }

    /// <summary>
    /// ApplyPaintScale과 함께 쓰는 글꼴. 픽셀 단위로 만들어 DPI에 의해 한 번 더 커지지 않게 한다.
    /// 100%에서는 같은 포인트 크기의 일반 글꼴과 동일하게 보인다.
    /// </summary>
    public static Font PaintFont(float points, FontStyle style = FontStyle.Regular)
        => new("맑은 고딕", points * DesignDpi / 72f, style, GraphicsUnit.Pixel);
}
