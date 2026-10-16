namespace vMenu.Enhanced.Data.Configuration.Settings;

public static class MenuAppearance
{
    public static readonly FloatSetting Scale = new("vMenu.Enhanced.MenuAppearance.Scale")
    {
        Description = "Menu size multiplier from 0.5 to 1.25. 1.0 keeps the original size; 0.85 makes it 15 percent smaller.",
        Default = 1f,
    };

    public static readonly BoolSetting HudInfoEnabled = new("vMenu.Enhanced.MenuAppearance.HudInfoEnabled")
    {
        Description = "Enables the location, clock and weather panels. Disable when another HUD already provides this information.",
        Default = true,
    };

    public static readonly StringSetting Skin = new("vMenu.Enhanced.MenuAppearance.Skin")
    {
        Description =
            "Choose a default theme: 'default', 'gold', 'green', 'red', 'gta', 'dark' or 'cartoon'. You can also choose any plugin-added theme here.",
        Default = "default",
    };

    public static readonly StringSetting TitleAlignment = new("vMenu.Enhanced.MenuAppearance.TitleAlignment")
    {
        Description =
            "Where the title sits on the banner: 'left', 'center' or 'right'.",
        Default = "left",
    };

    public static readonly BoolSetting HeaderGlare = new("vMenu.Enhanced.MenuAppearance.HeaderGlare")
    {
        Description = "Enables or disables the Globe in the menu header and the glare effect, like GTA Online's interaction menu.",
        Default = true,
    };
}
