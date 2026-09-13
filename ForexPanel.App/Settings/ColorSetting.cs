using System;
using System.Windows.Media;

namespace ForexPanel.App.Settings;

/// <summary>
/// A color stored as a base (intended to be a soft/muted anchor color) plus a separate
/// adjustable shade percentage, per PS's requirement for professional color settings that
/// stay easy to retune without re-picking raw hex every time.
/// ShadePercent: 100 = base color unchanged. Below 100 darkens toward black,
/// above 100 lightens toward white. Range is clamped to 0-200.
/// </summary>
public sealed class ColorSetting
{
    public Color BaseColor { get; set; }

    private double shadePercent = 100;
    public double ShadePercent
    {
        get => shadePercent;
        set => shadePercent = Math.Clamp(value, 0, 200);
    }

    public Color Effective => Adjust(BaseColor, ShadePercent);

    public ColorSetting Clone() => new() { BaseColor = BaseColor, ShadePercent = ShadePercent };

    public static ColorSetting From(Color color, double shadePercent = 100) =>
        new() { BaseColor = color, ShadePercent = shadePercent };

    private static Color Adjust(Color c, double percent)
    {
        double factor = percent / 100.0;

        if (factor <= 1.0)
        {
            return Color.FromArgb(
                c.A,
                (byte)(c.R * factor),
                (byte)(c.G * factor),
                (byte)(c.B * factor));
        }

        double t = Math.Min(factor - 1.0, 1.0);
        return Color.FromArgb(
            c.A,
            (byte)(c.R + (255 - c.R) * t),
            (byte)(c.G + (255 - c.G) * t),
            (byte)(c.B + (255 - c.B) * t));
    }
}
