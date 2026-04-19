internal sealed class ConfigurationManagerAttributes
{
    public int? Order { get; set; }
    public bool? Browsable { get; set; } = true;
    public string Category { get; set; } = string.Empty;
    public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer { get; set; } = null;
}
