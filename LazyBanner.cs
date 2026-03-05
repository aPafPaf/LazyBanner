using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Nodes;
using SharpDX;
using System.Collections.Generic;
using System.Linq;

namespace LazyBanner;

public class LazyBanner : BaseSettingsPlugin<LazyBannerSettings>
{
    private record struct BannerBuff(string Name, float Timer);

    private const float MaxTime = 3f;

    private readonly Dictionary<string, HotkeyNodeV2.HotkeyNodeValue> _buffKeys = new();
    private readonly Dictionary<string, float> _currentBannerBuffs = new();
    private readonly List<BannerBuff> _missingBuffs = new();

    private Element LifeOrb => GameController.Game.IngameState.IngameUi.GameUI.LifeOrb;
    private float _valour;
    private System.DateTime _lastPressTime;

    private static readonly Dictionary<string, string> BannerBuffNames = new()
    {
        { "DefianceBanner", "armour_evasion_banner_buff_aura" },
        { "WarBanner", "bloodstained_banner_buff_aura" },
        { "DreadBanner", "puresteel_banner_buff_aura" }
    };

    private static readonly Dictionary<string, string> BannerBuffNamesActor = new()
    {
        { "armour_evasion_banner_buff_aura","DefianceBanner" },
        { "bloodstained_banner_buff_aura","WarBanner" },
        { "puresteel_banner_buff_aura","DreadBanner" }
    };

    public override bool Initialise()
    {
        Settings.UpdateSettingsButton.OnPressed += UpdateSettings;
        UpdateSettings();
        return true;
    }

    public override Job Tick()
    {
        if (!UpdateData())
        {
            _lastPressTime = default;
            return null;
        }

        if(Settings.OnOff.PressedOnce())
            Settings.Work.Value = !Settings.Work.Value;

        if (!Settings.Work.Value)
            return null;

        if (Settings.Autoexertion.Value && !HasBuffAutoexertion())
            return null;

        var now = System.DateTime.Now;

        if ((now - _lastPressTime).TotalMilliseconds < Settings.Cooldown.Value)
            return null;

        foreach (var buff in _missingBuffs.OrderBy(x => x.Timer))
        {
            if (!_buffKeys.TryGetValue(buff.Name, out HotkeyNodeV2.HotkeyNodeValue value))
                continue;

            if (GameController.Window.IsForeground() && AllowToCast(buff.Name))
            {
                InputHelper.SendInputPress(value);
                _lastPressTime = now;
                break;
            }
        }

        return null;
    }

    public override void Render()
    {
        if (!Settings.Render.Value)
            return;

        Graphics.DrawText($"Valour: {_valour:F0}", LifeOrb.GetClientRectCache.TopLeft);

        if (!GameController.Player.TryGetComponent(out Buffs buffs) || buffs.BuffsList is null)
            return;

        var activeBuffNames = buffs.BuffsList
            .Where(b => _buffKeys.ContainsKey(b.Name))
            .ToDictionary(b => b.Name, b => b.Timer);

        int yOffset = 10;

        foreach (var buff in _buffKeys)
        {
            var position = new System.Numerics.Vector2(LifeOrb.GetClientRectCache.TopLeft.X, LifeOrb.GetClientRectCache.TopLeft.Y + yOffset);
            var hasBuff = activeBuffNames.TryGetValue(buff.Key, out float timer);
            var color = hasBuff ? Color.Green : Color.White;
            var text = hasBuff ? $"{buff.Key}: {timer:F3}" : buff.Key;

            Graphics.DrawText(text, position, color);
            yOffset += 10;
        }
    }

    private bool HasBuffAutoexertion()
    {
        if (!GameController.Player.TryGetComponent(out Buffs buffs) || buffs.BuffsList is null)
            return false;

        return buffs.BuffsList.Any(x => x.DisplayName.Equals("Autoexertion"));
    }

    private bool AllowToCast(string buffName)
    {
        if (string.IsNullOrEmpty(buffName))
            return false;

        if (!BannerBuffNamesActor.TryGetValue(buffName, out var actorSkillName))
            return false;

        return HasAllowedSkill(actorSkillName);
    }

    private bool HasAllowedSkill(string skillName)
    {
        if (!GameController.Player.TryGetComponent(out Actor componentActor))
            return false;

        var actorSkills = componentActor.ActorSkills;

        if (actorSkills is null)
            return false;

        return actorSkills.Any(skill => skill.Name == skillName && skill.AllowedToCast);
    }

    private bool UpdateData()
    {
        if (GameController.Area.CurrentArea.IsTown || GameController.Area.CurrentArea.IsHideout)
        {
            _valour = 0;
            _currentBannerBuffs.Clear();
            _missingBuffs.Clear();
            return false;
        }

        UpdateValour();

        if (_valour < Settings.ValorTrigerValue.Value)
        {
            _currentBannerBuffs.Clear();
            _missingBuffs.Clear();
            return false;
        }

        UpdateBannerBuffs();

        return _missingBuffs.Count > 0;
    }

    private void UpdateBannerBuffs()
    {
        _currentBannerBuffs.Clear();
        _missingBuffs.Clear();

        if (!GameController.Player.TryGetComponent(out Buffs buffs) || buffs.BuffsList is null)
            return;

        foreach (var buff in buffs.BuffsList)
        {
            if (_buffKeys.ContainsKey(buff.Name))
            {
                if (buff.Timer == float.PositiveInfinity)
                    _currentBannerBuffs[buff.Name] = buff.Timer;

                if (buff.Timer > Settings.RePlaceBannerTimeValue.Value &&
                    buff.MaxTime == MaxTime)
                    _currentBannerBuffs[buff.Name] = buff.Timer;
            }
        }

        foreach (var buff in _buffKeys)
        {
            if (!_currentBannerBuffs.TryGetValue(buff.Key, out float value))
                _missingBuffs.Add(new BannerBuff(buff.Key, value));
        }
    }

    private void UpdateSettings()
    {
        _buffKeys.Clear();

        if (Settings.EnableDefianceBanner.Value)
            _buffKeys.Add(BannerBuffNames["DefianceBanner"], Settings.DefianceBanner.Value);

        if (Settings.EnableWarBanner.Value)
            _buffKeys.Add(BannerBuffNames["WarBanner"], Settings.WarBanner.Value);

        if (Settings.EnableDreadBanner.Value)
            _buffKeys.Add(BannerBuffNames["DreadBanner"], Settings.DreadBanner.Value);
    }

    private void UpdateValour()
    {
        _valour = 0f;

        if (!GameController.Player.TryGetComponent(out Buffs buffs) || buffs.BuffsList is null)
            return;

        var valourBuff = buffs.BuffsList.Find(x => x.Name == "valour");
        _valour = valourBuff?.BuffCharges ?? 0f;
    }
}
