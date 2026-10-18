using System.Collections.Generic;
using UnityEngine;

// Picks a random stage background at the start of every match.
//
// Put it on any GameObject in the scene and drag each stage (a GameObject with a
// ParallaxBackground, e.g. the PlagueDoctorBackground prefab) into Stages. When a match starts,
// one of them is switched on and the rest are switched off.
//
// Online, both players must see the same stage, so the pick comes from the match seed (the relay
// sends the same random seed to both machines, and a new one for each match). Vs AI the seed
// never changes, so it uses Unity's random instead.
//
// It only reads the sim and turns GameObjects on and off, so it cannot affect gameplay.
// Outside Play mode it does nothing, so you can still see and edit every stage in the scene.
public class StagePicker : MonoBehaviour
{
    public List<GameObject> stages = new List<GameObject>();

    IMatchSession match;

    void Start()
    {
        match = SessionFinder.Find();
        if (match == null)
        {
            Debug.LogWarning("[StagePicker] No IMatchSession in the scene, so the stage never changes.", this);
            return;
        }
        match.MatchStarted += OnMatchStarted;
    }

    void OnDestroy()
    {
        if (match != null) match.MatchStarted -= OnMatchStarted;
    }

    void OnMatchStarted(GameSim sim)
    {
        if (stages.Count == 0) return;

        int pick;
        if (match.IsOnline) pick = (int)((uint)sim.seed % (uint)stages.Count);
        else pick = Random.Range(0, stages.Count);

        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i] != null) stages[i].SetActive(i == pick);
        }
    }
}
