using UnityEngine;
using UnityEngine.SceneManagement;

// Implements the main menu UI
public class MenuUI : MonoBehaviour
{
    IMatchSession match;

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

        if(GUILayout.Button("Play")){
            SceneManager.LoadScene("Fight");
        }
    }
}
