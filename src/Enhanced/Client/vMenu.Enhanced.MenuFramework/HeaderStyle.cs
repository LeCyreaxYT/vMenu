using CitizenFX.FiveM.Client;

using MenuAPI;

using vMenu.Enhanced.Configuration;
using vMenu.Enhanced.Logging;
using vMenu.Enhanced.Serialization;

using AppearanceSettings = vMenu.Enhanced.Data.Configuration.Settings.MenuAppearance;

namespace vMenu.Enhanced.MenuFramework;

public static class HeaderStyle
{
    private static string? _reportedAlignment;

    private static string? _reportedScale;

    /// <summary>Call after ClientConfig.Initialize, before the menus are built.</summary>
    public static void Initialize()
    {
        ClientConfig.AddEventListenerFor(
            [
                AppearanceSettings.TitleAlignment,
                AppearanceSettings.HeaderGlare,
                AppearanceSettings.Scale,
            ],
            Apply);

        Apply();
    }

    internal static void Apply()
    {
        MenuController.DefaultTitleAlignment = Alignment();
        MenuController.DefaultTitleFont = MenuFont.ChaletComprimeCologne;
        MenuController.DefaultShowHeaderGlare = ClientConfig.Value(AppearanceSettings.HeaderGlare);

        var scale = ClientConfig.Value(AppearanceSettings.Scale);
        if (float.IsNaN(scale) || scale < 0.5f || scale > 1.25f)
        {
            Report(ref _reportedScale, scale.ToString(), $"{AppearanceSettings.Scale.Name} must be between 0.5 and 1.25. Using 1.0.");
            scale = 1f;
        }

        Native.SendNuiMessage(ClientJson.Serialize(new AppearanceMessage { Scale = scale }));
    }

    private sealed class AppearanceMessage
    {
        public string Type { get; } = "menu_appearance";

        public float Scale { get; set; }
    }

    private static Menu.TitleAlignmentOption Alignment()
    {
        var raw = ClientConfig.Value(AppearanceSettings.TitleAlignment);
        var value = raw.Trim();

        if (string.Equals(value, "left", StringComparison.OrdinalIgnoreCase))
        {
            return Menu.TitleAlignmentOption.Left;
        }

        if (string.Equals(value, "center", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "centre", StringComparison.OrdinalIgnoreCase))
        {
            return Menu.TitleAlignmentOption.Center;
        }

        if (string.Equals(value, "right", StringComparison.OrdinalIgnoreCase))
        {
            return Menu.TitleAlignmentOption.Right;
        }

        Report(
            ref _reportedAlignment,
            raw,
            $"{AppearanceSettings.TitleAlignment.Name} is set to '{raw}', which is not left, center or right. Using left.");

        return Menu.TitleAlignmentOption.Left;
    }

    private static void Report(ref string? reported, string raw, string message)
    {
        if (string.Equals(reported, raw, StringComparison.Ordinal))
        {
            return;
        }

        reported = raw;

        Log.Warning($"[Menu] {message}");
    }
}
