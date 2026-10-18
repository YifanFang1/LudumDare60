using System;
using UnityEngine;
using UnityEngine.InputSystem;

// The player's controls, built on the new Input System so they can be rebound at runtime.
//
//   - Defaults are defined here, in code (one line per button).
//   - Rebinds are saved to PlayerPrefs, so they survive restarts (and work in WebGL builds).
//   - The sim never sees any of this: Read() turns whatever is pressed into the Buttons bits.
//
// To add a button: add a bit in Sim/Buttons.cs, then add one Make(...) line below.
// The public API uses only strings and ints, so UI code does not need the Input System.
public static class FightControls
{
    class Entry
    {
        public string label;
        public int bit;
        public InputAction action;
    }

    const string PrefsKey = "fight.bindings";

    static InputActionMap map;
    static Entry[] entries;
    static InputActionRebindingExtensions.RebindingOperation rebind;

    // Editor with "Enter Play Mode without domain reload" keeps statics alive, so reset them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        map = null;
        entries = null;
        rebind = null;
    }

    static void EnsureCreated()
    {
        if (map != null) return;

        map = new InputActionMap("Fight");
        entries = new Entry[]
        {
            Make("Left",  Buttons.Left,  "<Keyboard>/a"),
            Make("Right", Buttons.Right, "<Keyboard>/d"),
            Make("Down",  Buttons.Down,  "<Keyboard>/s"),
            Make("Light", Buttons.Light, "<Keyboard>/j"),
            Make("Heavy", Buttons.Heavy, "<Keyboard>/k"),
            Make("Grab", Buttons.Grab, "<Keyboard>/l"),
            Make("Special", Buttons.Special, "<Keyboard>/i"),
            Make("Ultimate", Buttons.Ultimate, "<Keyboard>/o"),
            Make("Jump", Buttons.Up, "<Keyboard>/w"),   // last, because Hint() finds keys by position
            // No "Block" line: blocking is holding away from the opponent, so the Block bit is unused.
        };

        string saved = PlayerPrefs.GetString(PrefsKey, "");
        if (saved != "") map.LoadBindingOverridesFromJson(saved);

        map.Enable();
    }

    static Entry Make(string label, int bit, string defaultBinding)
    {
        Entry e = new Entry();
        e.label = label;
        e.bit = bit;
        e.action = map.AddAction(label, InputActionType.Button, defaultBinding);
        return e;
    }

    // ---------- used by the game ----------

    // What is held right now, as Buttons bits. Returns 0 while the player is rebinding a key.
    public static int Read()
    {
        EnsureCreated();
        if (rebind != null) return 0;

        int bits = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].action.IsPressed()) bits |= entries[i].bit;
        }
        return bits;
    }

    // Short text for the on-screen hint, e.g. "A/D move   J light   K heavy   L grab ...".
    // The numbers are positions in the entries list above.
    public static string Hint()
    {
        EnsureCreated();
        return BindingOf(0) + "/" + BindingOf(1) + " move (tap twice: dash)   " + BindingOf(8) + " jump   " + BindingOf(3) + " light   " + BindingOf(4) + " heavy   "
             + BindingOf(5) + " grab   " + BindingOf(6) + " special   " + BindingOf(7) + " ultimate";
    }

    // ---------- used by the settings screen ----------

    public static int Count { get { EnsureCreated(); return entries.Length; } }
    public static string LabelOf(int i) { EnsureCreated(); return entries[i].label; }
    public static string BindingOf(int i) { EnsureCreated(); return entries[i].action.GetBindingDisplayString(0); }
    public static bool IsRebinding { get { return rebind != null; } }

    // Wait for the next key press and bind it. Escape cancels. `done` is called either way.
    public static void StartRebind(int i, Action done)
    {
        EnsureCreated();
        if (rebind != null) return;

        InputAction action = entries[i].action;
        action.Disable();   // an action must be disabled while it is being rebound

        rebind = action.PerformInteractiveRebinding(0)
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op => FinishRebind(action, done))
            .OnCancel(op => FinishRebind(action, done));
        rebind.Start();
    }

    static void FinishRebind(InputAction action, Action done)
    {
        if (rebind != null)
        {
            rebind.Dispose();
            rebind = null;
        }
        action.Enable();
        Save();
        if (done != null) done();
    }

    public static void ResetToDefaults()
    {
        EnsureCreated();
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i].action.RemoveAllBindingOverrides();
        }
        Save();
    }

    static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, map.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }
}
