using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tuning values for JuiceManager, editable in the inspector so the feel can be adjusted during
/// playtests without touching code.
/// <para>
/// Create: Project window > right click > Create > Fight > Juice Profile.
/// Use: drag it into the Profile slot on the JuiceManager component.
/// With no profile assigned, JuiceManager uses these default values.
/// </para>
/// Particle effects are NOT listed here: they are prefabs found automatically in
/// Assets/Juice/Resources (see JuiceParticles).
/// </summary>
[CreateAssetMenu(menuName = "Fight/Juice Profile", fileName = "JuiceProfile")]
public class JuiceProfile : ScriptableObject
{
    #region Settings
    [Header("Hits")]
    [Tooltip("Hits at or above this damage count as heavy hits.")]
    [Min(0f)] public float heavyThreshold = 10.0f;

    [Header("Screen shake: how much trauma (0..1) each event adds")]
    [Range(0f, 1f)] public float lightHitTrauma = 0.2f;
    [Range(0f, 1f)] public float heavyHitTrauma = 0.5f;
    [Range(0f, 1f)] public float blockTrauma = 0.1f;
    [Range(0f, 1f)] public float koTrauma = 1f;

    [Header("Screen shake: feel")]
    [Tooltip("Furthest the camera moves at full trauma, in Unity units (a fighter is about 1 unit wide).")]
    [Range(0f, 2f)] public float maxShakeOffset = 0.35f;
    [Tooltip("Most the camera rolls at full trauma, in degrees. 0 = no roll.")]
    [Range(0f, 10f)] public float maxShakeRoll = 1.5f;
    [Tooltip("How fast the shake wobbles. Higher = more jittery, lower = more of a sway.")]
    [Range(1f, 60f)] public float shakeFrequency = 25f;
    [Tooltip("Trauma lost per second. 1.5 means a full-trauma shake lasts about 0.7 seconds.")]
    [Range(0.1f, 5f)] public float traumaDecay = 1.5f;
    [Tooltip("Strength = trauma ^ this. 2 makes small hits subtle and big hits punchy; 1 is linear.")]
    [Range(1f, 3f)] public float traumaExponent = 2f;
    [Tooltip("Scales every shake. Set to 0 to turn screen shake off (e.g. an accessibility option).")]
    [Range(0f, 2f)] public float shakeMultiplier = 1f;

    [Header("Hill tilt: the attacker's side of the screen goes uphill on each hit")]
    [Tooltip("Degrees the hill gets steeper per hit. 0 = no hill tilt.")]
    [Range(0f, 30f)] public float hillTiltPerHit = 4f;
    [Tooltip("The hill never gets steeper than this many degrees. Set it equal to Hill Tilt Per Hit " +
             "to snap to one fixed angle instead of building up.")]
    [Range(0f, 90f)] public float maxHillTilt = 20f;
    [Tooltip("How fast the camera swings to the new hill angle. Higher = snappier.")]
    [Range(0.5f, 30f)] public float hillTiltSpeed = 6f;
    [Tooltip("Seconds without a hit before the hill levels out again. 0 = it stays tilted until the match ends.")]
    [Range(0f, 10f)] public float hillTiltHoldTime = 2f;

    [Header("Impact frame: when an Ultimate hits, the screen goes black and the fighters white")]
    [Tooltip("Also play it (with the zoom punch and CRT glitch) when an Ultimate is blocked.")]
    public bool impactOnBlockedUltimate = true;
    public Color impactBackgroundColor = Color.black;
    public Color impactSilhouetteColor = Color.white;
    [Tooltip("Sparks flip what is behind them: white on the background, black on the fighters. " +
             "Turn off to draw them in Impact Spark Color instead.")]
    public bool impactInvertSparks = true;
    public Color impactSparkColor = Color.white;
    [Tooltip("Ticks at the start shown inverted (background and fighter colours swapped) before the " +
             "normal impact frame. 0 = no flicker.")]
    [Min(0)] public int impactInvertedTicks = 2;

    [Header("Zoom punch: the camera snaps in towards an Ultimate hit")]
    [Tooltip("How much it zooms in, as a fraction of the view. 0 = no zoom punch.")]
    [Range(0f, 0.6f)] public float zoomPunchAmount = 0.25f;
    [Tooltip("How far the camera slides towards the hit. 0 = stays centred, 1 = centred on the hit.")]
    [Range(0f, 1f)] public float zoomPunchPull = 0.5f;
    [Tooltip("Seconds to ease back out after the impact frame ends.")]
    [Range(0f, 2f)] public float zoomPunchReturnTime = 0.35f;

    [Header("CRT glitch: the CRT filter spikes when an Ultimate hits")]
    [Tooltip("Seconds the glitch takes to fade out. 0 = no glitch.")]
    [Range(0f, 2f)] public float crtGlitchTime = 0.5f;

    [Header("Durations (ticks, 60 = 1 second)")]
    public int flashTicks = 4;
    [Tooltip("How long the impact frame shows when an Ultimate hits (including the inverted beat). 0 = off.")]
    public int impactTicks = 8;

    [Header("Triggers")]
    [Tooltip("WhileLowHealth is true while HP is at or below this percent of max HP (and above 0).")]
    [Range(0, 100)] public int lowHealthPercent = 25;

    [Header("Characters")]
    [Tooltip("Reactions every character gets (unless its own profile turns Use Default Reactions off).")]
    public CharacterJuiceProfile defaultCharacter;
    [Tooltip("Per-character profiles. Each one adds its reactions on top of the default ones.")]
    public CharacterJuiceProfile[] characters = new CharacterJuiceProfile[0];
    #endregion

    #region Lookups
    /// <summary>
    /// The character's own juice profile for a character id (see CharacterList), or null if it has
    /// none. Works for inspector characters and code characters alike.
    /// </summary>
    public CharacterJuiceProfile ForCharacter(int characterId)
    {
        for (int i = 0; i < characters.Length; i++)
        {
            CharacterJuiceProfile c = characters[i];
            if (c != null && c.character != null && c.character.CharacterId == characterId) return c;
        }
        return null;
    }

    /// <summary>
    /// Every reaction a character uses: its own profile's rows, then the default profile's rows
    /// (unless its profile turns Use Default Reactions off). Clears <paramref name="into"/> first.
    /// </summary>
    public void CollectReactions(int characterId, List<JuiceReaction> into)
    {
        into.Clear();
        CharacterJuiceProfile own = ForCharacter(characterId);
        AddRows(own, into);
        if (defaultCharacter != null && defaultCharacter != own && (own == null || own.useDefaultReactions))
        {
            AddRows(defaultCharacter, into);
        }
    }

    static void AddRows(CharacterJuiceProfile p, List<JuiceReaction> into)
    {
        if (p == null || p.reactions == null) return;
        for (int i = 0; i < p.reactions.Length; i++)
        {
            JuiceReaction r = p.reactions[i];
            if (r != null && !string.IsNullOrEmpty(r.trigger) && r.trigger.Trim().Length > 0) into.Add(r);
        }
    }
    #endregion
}
