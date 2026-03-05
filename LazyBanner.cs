using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Nodes;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LazyBanner;

public class LazyBanner : BaseSettingsPlugin<LazyBannerSettings>
{
    private record struct BannerBuff(string Name, float Timer);

    private readonly Dictionary<string, HotkeyNodeV2.HotkeyNodeValue> _buffKeys = new();
    private readonly Dictionary<string, int> _buffValourThresholds = new();
    private readonly Dictionary<string, float> _currentBannerBuffs = new();
    private readonly List<BannerBuff> _missingBuffs = new();
    private readonly Dictionary<string, DateTime> _lastPressPerBanner = new();

    private Element LifeOrb => GameController.Game.IngameState.IngameUi.GameUI.LifeOrb;
    private float _valour;
    private DateTime _lastPressTime;

    private static readonly Dictionary<string, string> BannerBuffNames = new()
    {
        { "DefianceBanner", "armour_evasion_banner_buff_aura" },
        { "WarBanner",      "bloodstained_banner_buff_aura"   },
        { "DreadBanner",    "puresteel_banner_buff_aura"      }
    };

    private static readonly Dictionary<string, string> BannerBuffNamesActor = new()
    {
        { "armour_evasion_banner_buff_aura", "DefianceBanner" },
        { "bloodstained_banner_buff_aura",   "WarBanner"      },
        { "puresteel_banner_buff_aura",      "DreadBanner"    }
    };

    public override bool Initialise()
    {
        Settings.Work.Value = false;

        Settings.EnableDefianceBanner.OnValueChanged += (_, _) => UpdateSettings();
        Settings.EnableWarBanner.OnValueChanged += (_, _) => UpdateSettings();
        Settings.EnableDreadBanner.OnValueChanged += (_, _) => UpdateSettings();

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

        if (Settings.OnOff.PressedOnce())
            Settings.Work.Value = !Settings.Work.Value;

        if (!Settings.Work.Value)
            return null;

        if (Settings.Autoexertion.Value && !HasBuffAutoexertion())
            return null;

        var now = DateTime.Now;

        if ((now - _lastPressTime).TotalMilliseconds < Settings.Cooldown.Value)
            return null;

        foreach (var buff in _missingBuffs.OrderBy(x => x.Timer))
        {
            if (!_buffKeys.TryGetValue(buff.Name, out var hotkey))
                continue;

            if (_buffValourThresholds.TryGetValue(buff.Name, out int threshold) && _valour < threshold)
                continue;

            if (_lastPressPerBanner.TryGetValue(buff.Name, out var lastPress) &&
                (now - lastPress).TotalMilliseconds < Settings.BannerCooldown.Value)
                continue;

            if (GameController.Window.IsForeground() && AllowToCast(buff.Name))
            {
                InputHelper.SendInputPress(hotkey);
                _lastPressTime = now;
                _lastPressPerBanner[buff.Name] = now;
                break;
            }
        }

        return null;
    }

    public override void Render()
    {
        if (!Settings.Render.Value)
            return;

        var basePos = LifeOrb.GetClientRectCache.TopLeft;
        var origin = new System.Numerics.Vector2(
            basePos.X + Settings.OverlayX.Value,
            basePos.Y + Settings.OverlayY.Value
        );

        var statusText = Settings.Work.Value ? "LazyBanner: ON" : "LazyBanner: OFF";
        var statusColor = Settings.Work.Value ? Color.Green : Color.Red;
        Graphics.DrawText(statusText, origin, statusColor);

        Graphics.DrawText(
            $"Valour: {_valour:F0}",
            new System.Numerics.Vector2(origin.X, origin.Y + 12)
        );

        if (!GameController.Player.TryGetComponent(out Buffs buffs) || buffs.BuffsList is null)
            return;

        var activeLookup = buffs.BuffsList
            .Where(b => _buffKeys.ContainsKey(b.Name))
            .ToDictionary(b => b.Name, b => b.Timer);

        var now = DateTime.Now;
        int yOffset = 24;
        foreach (var buff in _buffKeys)
        {
            var pos = new System.Numerics.Vector2(origin.X, origin.Y + yOffset);
            var hasBuff = activeLookup.TryGetValue(buff.Key, out float timer);
            var standing = hasBuff && timer == float.PositiveInfinity;

            var onCooldown = _lastPressPerBanner.TryGetValue(buff.Key, out var lastPress) &&
                             (now - lastPress).TotalMilliseconds < Settings.BannerCooldown.Value;

            var color = standing ? Color.Yellow
                      : onCooldown ? Color.Orange
                      : hasBuff ? Color.Green
                                    : Color.White;

            var text = standing ? $"{buff.Key}: standing"
                     : onCooldown ? $"{buff.Key}: cd {(Settings.BannerCooldown.Value - (now - lastPress).TotalMilliseconds) / 1000.0:F1}s"
                     : hasBuff ? $"{buff.Key}: {timer:F2}s (buff)"
                                  : $"{buff.Key}: —";

            Graphics.DrawText(text, pos, color);
            yOffset += 12;
        }
    }

    private bool HasBuffAutoexertion()
    {
        if (!GameController.Player.TryGetComponent(out Buffs buffs) || buffs.BuffsList is null)
            return false;

        return buffs.BuffsList.Any(x => x.DisplayName.Equals("Autoexertion"));
    }

    private bool AllowToCast(string auraBuffName)
    {
        if (string.IsNullOrEmpty(auraBuffName))
            return false;

        if (!BannerBuffNamesActor.TryGetValue(auraBuffName, out var skillName))
            return false;

        return HasAllowedSkill(skillName);
    }

    private bool HasAllowedSkill(string skillName)
    {
        if (!GameController.Player.TryGetComponent(out Actor actor))
            return false;

        var skills = actor.ActorSkills;
        return skills is not null && skills.Any(s => s.Name == skillName && s.AllowedToCast);
    }

    private bool UpdateData()
    {
        if (GameController.Area.CurrentArea.IsTown || GameController.Area.CurrentArea.IsHideout)
        {
            _valour = 0;
            _currentBannerBuffs.Clear();
            _missingBuffs.Clear();
            _lastPressPerBanner.Clear();
            return false;
        }

        UpdateValour();
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
            if (!_buffKeys.ContainsKey(buff.Name))
                continue;

            if (buff.Timer == float.PositiveInfinity)
                _currentBannerBuffs[buff.Name] = buff.Timer;
        }

        foreach (var buff in _buffKeys)
        {
            if (!_currentBannerBuffs.ContainsKey(buff.Key))
                _missingBuffs.Add(new BannerBuff(buff.Key, 0f));
        }
    }

    private void UpdateSettings()
    {
        _buffKeys.Clear();
        _buffValourThresholds.Clear();

        if (Settings.EnableDefianceBanner.Value)
        {
            var aura = BannerBuffNames["DefianceBanner"];
            _buffKeys[aura] = Settings.DefianceBanner.Value;
            _buffValourThresholds[aura] = Settings.DefianceBannerValor.Value;
        }

        if (Settings.EnableWarBanner.Value)
        {
            var aura = BannerBuffNames["WarBanner"];
            _buffKeys[aura] = Settings.WarBanner.Value;
            _buffValourThresholds[aura] = Settings.WarBannerValor.Value;
        }

        if (Settings.EnableDreadBanner.Value)
        {
            var aura = BannerBuffNames["DreadBanner"];
            _buffKeys[aura] = Settings.DreadBanner.Value;
            _buffValourThresholds[aura] = Settings.DreadBannerValor.Value;
        }
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
