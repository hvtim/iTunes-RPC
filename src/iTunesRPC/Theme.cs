using System.Runtime.InteropServices;

namespace iTunesRPC;

// Manual theming for the Settings window - WinForms doesn't auto-theme controls,
// so this reads the OS light/dark app preference and recolors everything to
// match, including the DWM title bar and native control glyphs (checkboxes,
// radio buttons) via the undocumented but widely-used "DarkMode_Explorer" theme.
public static class Theme
{
    public static bool IsDark { get; } = DetectDark();

    public static Color Background => IsDark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
    public static Color Surface => IsDark ? Color.FromArgb(45, 45, 48) : Color.White;
    public static Color Text => IsDark ? Color.FromArgb(230, 230, 230) : Color.FromArgb(26, 26, 26);
    public static Color MutedText => IsDark ? Color.FromArgb(160, 160, 160) : Color.FromArgb(90, 90, 90);
    public static Color Accent => Color.FromArgb(124, 111, 240);
    public static Color Border => IsDark ? Color.FromArgb(70, 70, 74) : Color.FromArgb(200, 200, 200);

    private static bool DetectDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        ApplyToControls(form.Controls);
        ApplyDarkTitleBar(form);
    }

    private static void ApplyToControls(Control.ControlCollection controls)
    {
        foreach (Control control in controls)
        {
            control.ForeColor = Text;

            switch (control)
            {
                case Button button:
                    button.BackColor = Surface;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = IsDark
                        ? Color.FromArgb(58, 58, 62)
                        : Color.FromArgb(230, 230, 230);
                    break;
                case TextBox textBox:
                    textBox.BackColor = Surface;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    ApplyNativeDarkTheme(textBox);
                    break;
                case NumericUpDown numeric:
                    numeric.BackColor = Surface;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    ApplyNativeDarkTheme(numeric);
                    break;
                case Panel panel:
                    panel.BackColor = Background;
                    if (panel.BorderStyle != BorderStyle.None) panel.BackColor = Surface;
                    break;
                case CheckBox or RadioButton or Label or GroupBox:
                    control.BackColor = Background;
                    ApplyNativeDarkTheme(control);
                    break;
                default:
                    control.BackColor = Background;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyToControls(control.Controls);
            }
        }
    }

    private static void ApplyNativeDarkTheme(Control control)
    {
        if (!IsDark) return;
        try
        {
            _ = control.Handle;
            SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }
        catch
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

    private const int DwmwaUseImmersiveDarkMode = 20;

    private static void ApplyDarkTitleBar(Form form)
    {
        if (!IsDark) return;
        try
        {
            int useDark = 1;
            _ = form.Handle;
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        }
        catch
        {
        }
    }
}
