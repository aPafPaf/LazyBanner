using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace LazyBanner;

public class LazyBannerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Autoexertion", "Only place banners when Autoexertion buff is active")]
    public ToggleNode Autoexertion { get; set; } = new ToggleNode(false);

    [Menu("On/Off Hotkey", "Toggle automation on/off in-game")]
    public HotkeyNodeV2 OnOff { get; set; } = Keys.Delete;

    public ToggleNode Work { get; set; } = new ToggleNode(false);

    [Menu("Cooldown (ms)", "Minimum delay between any key presses")]
    public RangeNode<int> Cooldown { get; set; } = new RangeNode<int>(100, 0, 1000);

    [Menu("Min Buff Timer (ms)", "Consider buff active if remaining time is above this value")]
    public RangeNode<int> MinBuffTimerMs { get; set; } = new RangeNode<int>(500, 0, 5000);

    [Menu("Enable Defiance Banner")]
    public ToggleNode EnableDefianceBanner { get; set; } = new ToggleNode(false);

    [Menu("Defiance Banner Hotkey")]
    public HotkeyNodeV2 DefianceBanner { get; set; } = Keys.E;

    [Menu("Defiance Banner Valour Threshold", "Minimum Valour required to place Defiance Banner")]
    public RangeNode<int> DefianceBannerValor { get; set; } = new RangeNode<int>(50, 0, 105);

    [Menu("Defiance Banner Priority", "Priority for Defiance Banner (higher = more important)")]
    public RangeNode<int> DefianceBannerPriority { get; set; } = new RangeNode<int>(3, 1, 10);

    [Menu("Enable War Banner")]
    public ToggleNode EnableWarBanner { get; set; } = new ToggleNode(false);

    [Menu("War Banner Hotkey")]
    public HotkeyNodeV2 WarBanner { get; set; } = Keys.R;

    [Menu("War Banner Valour Threshold", "Minimum Valour required to place War Banner")]
    public RangeNode<int> WarBannerValor { get; set; } = new RangeNode<int>(50, 0, 105);

    [Menu("War Banner Priority", "Priority for War Banner (higher = more important)")]
    public RangeNode<int> WarBannerPriority { get; set; } = new RangeNode<int>(2, 1, 10);

    [Menu("Enable Dread Banner")]
    public ToggleNode EnableDreadBanner { get; set; } = new ToggleNode(false);

    [Menu("Dread Banner Hotkey")]
    public HotkeyNodeV2 DreadBanner { get; set; } = Keys.T;

    [Menu("Dread Banner Valour Threshold", "Minimum Valour required to place Dread Banner")]
    public RangeNode<int> DreadBannerValor { get; set; } = new RangeNode<int>(50, 0, 105);

    [Menu("Dread Banner Priority", "Priority for Dread Banner (higher = more important)")]
    public RangeNode<int> DreadBannerPriority { get; set; } = new RangeNode<int>(1, 1, 10);

    [Menu("Show Overlay", "Display banner status and Valour on screen")]
    public ToggleNode Render { get; set; } = new ToggleNode(false);

    [Menu("Overlay X Offset", "Horizontal offset from the Life Orb position")]
    public RangeNode<int> OverlayX { get; set; } = new RangeNode<int>(0, -500, 500);

    [Menu("Overlay Y Offset", "Vertical offset from the Life Orb position")]
    public RangeNode<int> OverlayY { get; set; } = new RangeNode<int>(0, -500, 500);
}
