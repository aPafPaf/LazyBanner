using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace LazyBanner;

public class LazyBannerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Autoexertion", "")]
    public ToggleNode Autoexertion { get; set; } = new ToggleNode(false);

    [Menu("Valor Triger Value")]
    public RangeNode<int> ValorTrigerValue { get; set; } = new RangeNode<int>(50, 0, 105);

    [Menu("Re Place Banner Time Value")]
    public RangeNode<float> RePlaceBannerTimeValue { get; set; } = new RangeNode<float>(0f, 0f, 4f);

    [Menu("Cooldown (ms)")]
    public RangeNode<int> Cooldown { get; set; } = new RangeNode<int>(100, 0, 1000);

    // ==============================

    [Menu("Enable Defiance Banner", "")]
    public ToggleNode EnableDefianceBanner { get; set; } = new ToggleNode(false);

    [Menu("Defiance Banner", "")]
    public HotkeyNodeV2 DefianceBanner { get; set; } = Keys.E;

    // ==============================

    [Menu("Enable WarBanner", "")]
    public ToggleNode EnableWarBanner { get; set; } = new ToggleNode(false);

    [Menu("War Banner", "")]
    public HotkeyNodeV2 WarBanner { get; set; } = Keys.R;

    // ==============================

    [Menu("Enable Dread Banner", "")]
    public ToggleNode EnableDreadBanner { get; set; } = new ToggleNode(false);

    [Menu("Dread Banner", "")]
    public HotkeyNodeV2 DreadBanner { get; set; } = Keys.T;

    // ==============================

    [Menu("Update Settings", "")]
    public ButtonNode UpdateSettingsButton { get; set; } = new ButtonNode();
}