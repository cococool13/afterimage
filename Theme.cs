namespace Afterimage;

static class Theme
{
    public static readonly Color Canvas = Color.FromArgb(4, 5, 6);
    public static readonly Color Raised = Color.FromArgb(17, 18, 20);
    public static readonly Color Line = Color.FromArgb(54, 55, 57);
    public static readonly Color Mute = Color.FromArgb(156, 156, 157);
    public static readonly Color Text = Color.White;
    public static readonly Color Ash = Color.FromArgb(230, 230, 230);
    public static readonly Color AshText = Color.FromArgb(47, 48, 49);
    public static readonly Color Ember = Color.FromArgb(255, 99, 99);
    public static readonly Color Mint = Color.FromArgb(89, 212, 153);

    public static readonly Font Ui = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font UiSmall = new("Segoe UI", 8.5f, FontStyle.Regular);
    public static readonly Font Title = new("Segoe UI", 12f, FontStyle.Bold);
}
