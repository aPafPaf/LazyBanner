using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Nodes;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LazyBanner;

public class LazyBanner : BaseSettingsPlugin<LazyBannerSettings>
{
    private record BannerBuffData
    {
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public float Timer { get; init; }
        public bool HasBuff { get; init; }
        public bool IsStanding { get; init; }
        public bool CanBeUsed { get; init; }
        public bool AllowedToCast { get; init; }
        public int ValourThreshold { get; init; }
        public required HotkeyNodeV2.HotkeyNodeValue Hotkey { get; init; }
        public int CurrentValour { get; init; }
        public int Priority { get; init; }
    }

    private readonly Dictionary<string, HotkeyNodeV2.HotkeyNodeValue> _buffKeys = new();
    private readonly Dictionary<string, int> _buffValourThresholds = new();
    private readonly Dictionary<string, int> _buffPriorities = new();

    private Element LifeOrb => GameController.Game.IngameState.IngameUi.GameUI.LifeOrb;
    private float _valour;
    private DateTime _lastPressTime;
    private DateTime _lastDiagTime;
    private string _logPath = string.Empty;

    /// <summary>
    /// Mapping: Banner skill name -> Aura buff name
    /// </summary>
    private static readonly Dictionary<string, string> BannerToAura = new()
    {
        { "DefianceBanner", "armour_evasion_banner_buff_aura" },
        { "WarBanner",      "bloodstained_banner_buff_aura"   },
        { "DreadBanner",    "puresteel_banner_buff_aura"      }
    };

    /// <summary>
    /// Mapping: Aura buff name -> Banner skill name
    /// </summary>
    private static readonly Dictionary<string, string> AuraToBanner = BannerToAura.ToDictionary(x => x.Value, x => x.Key);

    private void Log(string message)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); }
        catch { }
    }

    /// <summary>
    /// Собирает полные данные о всех настроенных бафах.
    /// </summary>
    private List<BannerBuffData> CollectBuffData()
    {
        var buffDataList = new List<BannerBuffData>();
        var now = DateTime.Now;

        // Получаем компоненты
        var hasBuffs = GameController.Player.TryGetComponent(out Buffs buffs) && buffs.BuffsList is not null;
        var hasActor = GameController.Player.TryGetComponent(out Actor actor) && actor.ActorSkills is not null;

        // Считываем текущий Valour
        UpdateValour();
        var currentValour = (int)_valour;

        // Для каждого настроенного бафа собираем данные
        foreach (var kvp in _buffKeys)
        {
            var auraName = kvp.Key;
            var hotkey = kvp.Value;
            var threshold = _buffValourThresholds.GetValueOrDefault(auraName, 0);
            var priority = _buffPriorities.GetValueOrDefault(auraName, 0);

            // Получаем данные о бафе
            var buff = hasBuffs ? buffs!.BuffsList.Find(b => b.Name == auraName) : null;
            var hasBuff = buff != null;
            var timer = buff?.Timer ?? 0f;
            var isStanding = hasBuff && timer == float.PositiveInfinity;

            // Получаем данные о скилле
            var bannerName = AuraToBanner.GetValueOrDefault(auraName, string.Empty);
            var skill = hasActor ? actor!.ActorSkills.FirstOrDefault(s => s.Name == bannerName) : default;
            var canBeUsed = skill != default && skill.CanBeUsed;
            var allowedToCast = skill != default && skill.AllowedToCast;

            buffDataList.Add(new BannerBuffData
            {
                Name = auraName,
                DisplayName = bannerName,
                Timer = timer,
                HasBuff = hasBuff,
                IsStanding = isStanding,
                CanBeUsed = canBeUsed,
                AllowedToCast = allowedToCast,
                ValourThreshold = threshold,
                Hotkey = hotkey,
                CurrentValour = currentValour,
                Priority = priority
            });
        }

        return buffDataList;
    }

    /// <summary>
    /// Принимает решение, какой баф активировать на основе собранных данных.
    /// Баннеры проверяются строго по приоритету: сначала самый высокий приоритет.
    /// Если не хватает Valour для текущего баннера — ждём, не переходя к следующему.
    /// Возвращает null, если ни один баф не подходит для активации.
    /// </summary>
    private BannerBuffData? SelectBuffToActivate(List<BannerBuffData> buffDataList)
    {
        var minBuffTimerSec = Settings.MinBuffTimerMs.Value;

        // Сортируем ВСЕ бафы по приоритету (от высшего к низшему)
        var sortedBuffs = buffDataList
            .OrderByDescending(b => b.Priority)
            .ToList();

        // Проходим по приоритету и ищем первый баф, который нужно активировать
        foreach (var buff in sortedBuffs)
        {
            // Пропускаем, если баф уже активен (стоячий или с достаточным временем)
            if (buff.IsStanding)
                continue; // стоячий баф — переходим к следующему

            if (buff.HasBuff && buff.Timer >= minBuffTimerSec)
                continue; // баф активен (время выше порога) — переходим к следующему

            // Баф не активен — проверяем Valour
            if (buff.CurrentValour < buff.ValourThreshold)
                return null; // не хватает Valour — ждём, не переходим к следующему

            // Проверяем, можно ли кастовать
            if (!buff.AllowedToCast || !buff.CanBeUsed)
                continue;

            // Все проверки пройдены — активируем
            return buff;
        }

        // Все баннеры активны или нельзя активировать
        return null;
    }

    public override bool Initialise()
    {
        _logPath = Path.Combine(DirectoryFullName, "log.txt");
        File.WriteAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] LazyBanner initialising{Environment.NewLine}");

        Settings.Work.Value = false;

        Settings.EnableDefianceBanner.OnValueChanged += (_, _) => UpdateSettings();
        Settings.EnableWarBanner.OnValueChanged += (_, _) => UpdateSettings();
        Settings.EnableDreadBanner.OnValueChanged += (_, _) => UpdateSettings();

        UpdateSettings();
        Log($"Initialise complete. BuffKeys count: {_buffKeys.Count}");

        GameController.PluginBridge.SaveMethod("LazyBanner.Enable",()=> Settings.Work.Value = true);
        GameController.PluginBridge.SaveMethod("LazyBanner.Disable", () => Settings.Work.Value = false);

        return true;
    }

    public override Job Tick()
    {
        LogDiagnostics();

        // Проверка: не в городе/убежище
        if (IsInTownOrHideout())
        {
            _lastPressTime = default;
            return null;
        }

        // Переключение работы по горячей клавише
        if (Settings.OnOff.PressedOnce())
        {
            Settings.Work.Value = !Settings.Work.Value;
            Log($"Work toggled: {Settings.Work.Value}");
        }

        if (!Settings.Work.Value)
            return null;

        // Проверка условий для активации
        if (!ShouldProcess())
            return null;

        // Сбор данных о бафах
        var buffDataList = CollectBuffData();

        // Принятие решения о том, какой баф активировать
        var buffToActivate = SelectBuffToActivate(buffDataList);

        // Активация бафа
        if (buffToActivate != null)
        {
            if (GameController.Window.IsForeground())
            {
                Log($"Pressing hotkey for {buffToActivate.Name}, valour={buffToActivate.CurrentValour}");
                InputHelper.SendInputPress(buffToActivate.Hotkey);
                _lastPressTime = DateTime.Now;
            }
        }

        return null;
    }

    /// <summary>
    /// Проверяет, находимся ли мы в городе или убежище.
    /// </summary>
    private bool IsInTownOrHideout()
    {
        return GameController.Area.CurrentArea.IsTown || GameController.Area.CurrentArea.IsHideout;
    }

    /// <summary>
    /// Проверяет все условия для обработки (работа, автоэкзерция, кулдаун).
    /// </summary>
    private bool ShouldProcess()
    {
        if (Settings.Autoexertion.Value && !HasBuffAutoexertion())
            return false;

        var now = DateTime.Now;
        if ((now - _lastPressTime).TotalMilliseconds < Settings.Cooldown.Value)
            return false;

        return true;
    }

    private void LogDiagnostics()
    {
        var now = DateTime.Now;
        if ((now - _lastDiagTime).TotalSeconds < 2)
            return;
        _lastDiagTime = now;

        if (!GameController.Player.TryGetComponent(out Buffs buffs) || buffs.BuffsList is null)
        {
            Log("DIAG: no Buffs component");
            return;
        }

        if (!GameController.Player.TryGetComponent(out Actor actor) || actor.ActorSkills is null)
        {
            Log("DIAG: no Actor component");
            return;
        }

        UpdateValour();
        Log($"DIAG: valour={_valour}, work={Settings.Work.Value}");

        foreach (var buffName in _buffKeys.Keys)
        {
            var buff = buffs.BuffsList.Find(x => x.Name == buffName);
            var buffState = buff != null
                ? $"Timer={buff.Timer}, MaxTime={buff.MaxTime}"
                : "not present";

            var skillName = AuraToBanner.GetValueOrDefault(buffName, "?");
            var skill = actor.ActorSkills.FirstOrDefault(s => s.Name == skillName);
            var skillState = skill != default
                ? $"CanBeUsed={skill.CanBeUsed}, AllowedToCast={skill.AllowedToCast}"
                : "skill not found";

            Log($"DIAG: [{buffName}] buff=({buffState}) | skill=({skillState})");
        }
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

        int yOffset = 24;
        foreach (var buff in _buffKeys)
        {
            var pos = new System.Numerics.Vector2(origin.X, origin.Y + yOffset);
            var hasBuff = activeLookup.TryGetValue(buff.Key, out float timer);
            var standing = hasBuff && timer == float.PositiveInfinity;

            var color = standing ? Color.Yellow
                      : hasBuff ? Color.Green
                                 : Color.White;

            var text = standing ? $"{buff.Key}: standing"
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

    private void UpdateSettings()
    {
        _buffKeys.Clear();
        _buffValourThresholds.Clear();
        _buffPriorities.Clear();

        if (Settings.EnableDefianceBanner.Value)
        {
            var aura = BannerToAura["DefianceBanner"];
            _buffKeys[aura] = Settings.DefianceBanner.Value;
            _buffValourThresholds[aura] = Settings.DefianceBannerValor.Value;
            _buffPriorities[aura] = Settings.DefianceBannerPriority.Value;
            Log($"Registered DefianceBanner -> {aura}, threshold={Settings.DefianceBannerValor.Value}, priority={Settings.DefianceBannerPriority.Value}");
        }

        if (Settings.EnableWarBanner.Value)
        {
            var aura = BannerToAura["WarBanner"];
            _buffKeys[aura] = Settings.WarBanner.Value;
            _buffValourThresholds[aura] = Settings.WarBannerValor.Value;
            _buffPriorities[aura] = Settings.WarBannerPriority.Value;
            Log($"Registered WarBanner -> {aura}, threshold={Settings.WarBannerValor.Value}, priority={Settings.WarBannerPriority.Value}");
        }

        if (Settings.EnableDreadBanner.Value)
        {
            var aura = BannerToAura["DreadBanner"];
            _buffKeys[aura] = Settings.DreadBanner.Value;
            _buffValourThresholds[aura] = Settings.DreadBannerValor.Value;
            _buffPriorities[aura] = Settings.DreadBannerPriority.Value;
            Log($"Registered DreadBanner -> {aura}, threshold={Settings.DreadBannerValor.Value}, priority={Settings.DreadBannerPriority.Value}");
        }

        Log($"UpdateSettings done. Total registered: {_buffKeys.Count}");
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