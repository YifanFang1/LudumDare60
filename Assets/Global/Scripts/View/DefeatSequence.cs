using System.Collections.Generic;
using UnityEngine;

// View only: a K.O. burns away the loser and desaturates the screen. Never changes the sim.
[DefaultExecutionOrder(65)]
public class DefeatSequence : MonoBehaviour
{
    class Burn
    {
        public SpriteRenderer sprite;
        public Material original, effect;
        public SpriteOutline outline;
        public bool outlined;
    }
    readonly List<Burn> burning = new List<Burn>();
    IMatchSession match;
    WorldView world;
    GameSim previousSim;
    int round;
    bool started;
    float elapsed;

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        world = GetComponent<WorldView>();
    }

    void LateUpdate()
    {
        if (match == null || match.Phase != MatchPhase.Playing)
        {
            ResetEffect();
            previousSim = null;
            return;
        }
        GameSim sim = match.Sim;
        if (sim != previousSim || sim.round != round)
        {
            ResetEffect();
            previousSim = sim;
            round = sim.round;
        }
        if (!started && (sim.roundOverTicks > 0 || sim.IsOver))
        {
            started = true;
            Shader shader = Shader.Find("LD/DefeatBurn");
            for (int i = 0; i < 2; i++)
            {
                if (sim.fighters[i].hp > 0) continue;
                GameObject root = world != null ? world.FighterRoot(i) : null;
                if (root == null || shader == null) continue;
                foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var burn = new Burn { sprite = sprite, original = sprite.sharedMaterial,
                        effect = new Material(shader), outline = sprite.GetComponent<SpriteOutline>() };
                    if (burn.outline != null)
                    {
                        burn.outlined = burn.outline.enabled;
                        burn.outline.enabled = false;
                    }
                    burn.original = sprite.sharedMaterial;
                    sprite.SetPropertyBlock(null);
                    sprite.sharedMaterial = burn.effect;
                    burning.Add(burn);
                }
            }
        }
        if (!started) return;
        elapsed += Time.unscaledDeltaTime;
        // The screen only drains of colour when YOU lost (or drew); a win keeps full colour.
        int local = Mathf.Clamp(match.LocalSlot, 0, 1);
        float grey = sim.fighters[local].hp <= 0 ? Mathf.SmoothStep(0f, 1f, elapsed / 0.8f) : 0f;
        Shader.SetGlobalFloat("_DefeatGrey", grey);
        float progress = Mathf.Clamp01((elapsed - 0.15f) / 1.35f);
        foreach (Burn burn in burning)
        {
            if (burn.sprite == null || burn.sprite.sprite == null) continue;
            Bounds bounds = burn.sprite.sprite.bounds;
            burn.effect.SetFloat("_Burn", progress);
            burn.effect.SetFloat("_MinY", bounds.min.y);
            burn.effect.SetFloat("_Height", Mathf.Max(0.001f, bounds.size.y));
        }
    }

    void OnDisable() { ResetEffect(); }

    void ResetEffect()
    {
        foreach (Burn burn in burning)
        {
            if (burn.sprite != null)
            {
                burn.sprite.sharedMaterial = burn.original;
                if (burn.outline != null) burn.outline.enabled = burn.outlined;
            }
            if (burn.effect != null) Destroy(burn.effect);
        }
        burning.Clear();
        Shader.SetGlobalFloat("_DefeatGrey", 0f);
        started = false;
        elapsed = 0f;
    }
}
