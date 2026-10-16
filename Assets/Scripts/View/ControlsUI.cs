using UnityEngine;

// Settings screen for key bindings, shown in the lobby. Click a button, press the new key.
// Escape cancels. Bindings are saved by FightControls.
// Remove this component and the game still works with the default keys.
public class ControlsUI : MonoBehaviour
{
    IMatchSession match;
    bool open;
    int waitingFor = -1;

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("ControlsUI needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
        }
    }

    void OnGUI()
    {
        if (match.Phase == MatchPhase.Playing) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);

        if (!open)
        {
            GUILayout.BeginArea(new Rect(20, 420, 200, 40));
            if (GUILayout.Button("Controls")) open = true;
            GUILayout.EndArea();
            return;
        }

        GUILayout.BeginArea(new Rect(360, 20, 320, 360));
        GUILayout.Label("Controls: click one, then press a key (Esc cancels)");

        for (int i = 0; i < FightControls.Count; i++)
        {
            string shown = waitingFor == i ? "press a key..." : FightControls.BindingOf(i);
            if (GUILayout.Button(FightControls.LabelOf(i) + "   [" + shown + "]") && !FightControls.IsRebinding)
            {
                waitingFor = i;
                FightControls.StartRebind(i, () => waitingFor = -1);
            }
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Reset to defaults") && !FightControls.IsRebinding) FightControls.ResetToDefaults();
        if (GUILayout.Button("Close") && !FightControls.IsRebinding) open = false;
        GUILayout.EndArea();
    }
}
