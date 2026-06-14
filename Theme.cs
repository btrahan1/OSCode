using System.Drawing;

namespace OSCode;

public static class Theme
{
    public static readonly Color Background = Color.FromArgb(245, 245, 247);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(220, 220, 225);
    public static readonly Color Sidebar = Color.FromArgb(235, 235, 240);
    public static readonly Color Primary = Color.FromArgb(0, 122, 255); // Blue Accent
    public static readonly Color PrimaryHover = Color.FromArgb(0, 100, 210);
    public static readonly Color Success = Color.FromArgb(52, 199, 89); // Green Accent
    public static readonly Color Error = Color.FromArgb(255, 59, 48); // Red Accent
    public static readonly Color TextMain = Color.FromArgb(30, 30, 30);
    public static readonly Color TextSecondary = Color.FromArgb(100, 100, 105);
    
    public static readonly Font HeaderFont = new Font("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font RegularFont = new Font("Segoe UI", 9f);
    public static readonly Font CodeFont = new Font("Consolas", 9.5f);
}
