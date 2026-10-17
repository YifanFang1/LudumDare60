using UnityEngine;
using UnityEngine.SceneManagement;

// Finds the match session (LocalMatchSession, RelayLockstepSession, ...) without needing a reference,
// so the canvas UI works with whatever networking component is in the scene.
public static class SessionFinder
{
    public static IMatchSession Find()
    {
        return FindInScene<IMatchSession>();
    }

    // First component of type T on any object (active or not) in the active scene.
    public static T FindInScene<T>() where T : class
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T found = roots[i].GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
