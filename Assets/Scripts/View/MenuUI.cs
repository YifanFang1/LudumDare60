using UnityEngine;
using UnityEngine.SceneManagement;

// Implements the main menu UI
public class MenuUI : MonoBehaviour
{
    IMatchSession match;
    bool open;
    int waitingFor = -1;

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("MenuUI needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
        }
    }

    void OnGUI(){
        if(match.Phase != MatchPhase.Lobby) return;

        float width, height;
        GuiDraw.BeginVirtualScreen(out width, out height);
        GUILayout.BeginArea(new Rect(20, 20, 320, 380));

        if(GUILayout.Button("Play")){
            SceneManager.LoadScene("Fight");
        }        

        GUILayout.Space(10);

        if(GUILayout.Button("Controls")){
            Debug.Log("open controls");
            showControls();
        }

        GUILayout.Space(10);

        if(GUILayout.Button("Quit")){
            Application.Quit();
            Debug.Log("You Quit!");
        }

        GUILayout.EndArea();
    }

    void showControls(){
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
