using ShotMark.Core.Settings;
namespace ShotMark.Core.Capture;

/// <summary>
/// Entscheidet anhand der Tastenereignisse eines Low-Level-Hooks, wann der Bildschirm schon beim ersten Tastendruck
/// des Aufnahme-Kürzels festgehalten wird. Viele Programme schließen Tooltips, sobald sie eine Taste sehen – bei
/// Strg+Umschalt+P also schon beim Strg. Der Hook läuft, bevor das Programm die Taste bekommt; ein dort aufgenommenes
/// Bild enthält den Tooltip noch.
/// Regeln: aufnehmen beim ersten Modifier des Kürzels (bzw. bei der Haupttaste, wenn das Kürzel keine Modifier hat),
/// aber nicht mitten im Tippen. Jede fremde Taste verwirft das Bild. Zeiten in Millisekunden.
/// </summary>
public sealed class PreCaptureTracker
{
    /// <summary>So lange vorher keine Taste: kein Tippen, sondern ein neues Kürzel.</summary>
    public const long QuietMs = 500;

    /// <summary>Älter wird das Bild nicht verwendet.</summary>
    public const long MaxAgeMs = 3000;

    /// <summary>Danach gilt eine Taste als losgelassen, auch wenn der Hook das Loslassen nie gesehen hat.</summary>
    public const long StaleKeyMs = 10_000;

    private readonly Dictionary<int, long> _held = [];
    private HotkeySetting? _hotkey;
    private long _lastKeyDown = long.MinValue / 2;
    private long? _grabbedAt;

    /// <summary>Aktives Kürzel; null, solange keines registriert ist.</summary>
    public HotkeySetting? Hotkey
    {
        get => _hotkey;
        set
        {
            _hotkey = value;
            Reset();
        }
    }

    /// <summary>Taste gedrückt. true: jetzt den Bildschirm aufnehmen.</summary>
    public bool KeyDown(int vk, long now)
    {
        foreach (var stale in _held.Where(k => now - k.Value > StaleKeyMs).Select(k => k.Key).ToList())
            _held.Remove(stale);
        if (_held.ContainsKey(vk)) return false; // Tastenwiederholung

        var chordStart = _held.Count == 0;
        var quiet = now - _lastKeyDown >= QuietMs;
        _held[vk] = now;
        _lastKeyDown = now;
        if (_hotkey is not { } hotkey) return false;

        var modifier = ModifierOf(vk);
        if (modifier is null && vk != hotkey.VirtualKey) return Drop();
        if (modifier is { } m && !HasModifier(hotkey, m)) return Drop();

        if (modifier is null)
        {
            // Haupttaste: ohne Modifier im Kürzel ist sie selbst der Anfang, sonst müssen alle Modifier gehalten sein
            if (!HasAnyModifier(hotkey)) return chordStart ? Grab(now) : Drop();
            if (!AllModifiersHeld(hotkey)) Reset();
            return false;
        }

        if (!HasAnyModifier(hotkey)) return false;
        if (chordStart) return quiet ? Grab(now) : Drop();
        return false;
    }

    public void KeyUp(int vk, long now)
    {
        // Bild bleibt gültig: WM_HOTKEY kann nach dem Loslassen ankommen
        _held.Remove(vk);
    }

    /// <summary>Gilt das festgehaltene Bild für ein jetzt ausgelöstes Kürzel?</summary>
    public bool IsValid(long now) => _grabbedAt is { } at && now - at <= MaxAgeMs;

    /// <summary>Bild verwerfen (verbraucht, abgelaufen oder ungültig).</summary>
    public void Reset() => _grabbedAt = null;

    private bool Grab(long now)
    {
        _grabbedAt = now;
        return true;
    }

    private bool Drop()
    {
        Reset();
        return false;
    }

    private enum Modifier { Control, Shift, Alt, Windows }

    private static Modifier? ModifierOf(int vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => Modifier.Shift,
        0x11 or 0xA2 or 0xA3 => Modifier.Control,
        0x12 or 0xA4 or 0xA5 => Modifier.Alt,
        0x5B or 0x5C => Modifier.Windows,
        _ => null,
    };

    private static bool HasModifier(HotkeySetting hotkey, Modifier modifier) => modifier switch
    {
        Modifier.Control => hotkey.Control,
        Modifier.Shift => hotkey.Shift,
        Modifier.Alt => hotkey.Alt,
        _ => hotkey.Windows,
    };

    private static bool HasAnyModifier(HotkeySetting hotkey) => hotkey.Control || hotkey.Shift || hotkey.Alt || hotkey.Windows;

    private bool AllModifiersHeld(HotkeySetting hotkey) =>
        Enum.GetValues<Modifier>().All(m => !HasModifier(hotkey, m) || _held.Keys.Any(vk => ModifierOf(vk) == m));
}
