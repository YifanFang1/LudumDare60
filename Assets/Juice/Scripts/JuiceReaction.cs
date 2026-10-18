using System;
using UnityEngine;

/// <summary>Where a reaction's effect plays.</summary>
public enum JuicePlace
{
    /// <summary>At the point where it happened (e.g. where the hitbox met the body). Falls back to the
    /// owner's anchor for triggers that have no point (states, round events, code calls without one).</summary>
    AtEvent = 0,
    /// <summary>On the fighter whose profile this row is in.</summary>
    OnSelf = 1,
    /// <summary>On the other fighter.</summary>
    OnOpponent = 2,
}

/// <summary>
/// One row in a CharacterJuiceProfile: "when TRIGGER happens to this character, play EFFECT".
/// <para>
/// Triggers are names (not case sensitive). Two kinds:
/// <code>
///   moment triggers   play once when something happens       Hit, Dash, EnterHitstun, RoundWin, ...
///   While... triggers play for as long as something is true  WhileLowHealth, WhileWalk, WhileMeterFull, ...
/// </code>
/// See <see cref="JuiceTriggers"/> for every built-in name. Code can fire its own moment triggers
/// with JuiceManager.Trigger("Name", fighter).
/// </para>
/// </summary>
[Serializable]
public class JuiceReaction
{
    [Tooltip("When this plays. Moments: Hit, LightHit, HeavyHit, Hit:<Move>, GotHit, GotLightHit, GotHeavyHit, Grab, " +
             "GotGrabbed, Block, AttackBlocked, AttackStart, AttackStart:<Move>, Dash, DashForward, DashBack, " +
             "SuperFlash, KnockedOut, ScoredKO, RoundStart, RoundEnd, RoundWin, RoundLose, RoundDraw, Enter<State>.\n" +
             "While (plays as long as true): WhileMoving, WhileLowHealth, WhileMeterFull, WhileBlocking, While<State>, WhileAttack:<Move>.\n" +
             "States: Idle, Walk, Attack, Hitstun, KO, Blockstun, Dash. Moves: Light, Heavy, Grab, Special, Ultimate.\n" +
             "Or any name your own code fires with JuiceManager.Trigger. Turn on Log All Triggers on JuiceManager to see them live.")]
    public string trigger = "";

    [Tooltip("Effect name (a JuiceEffect prefab in Assets/Juice/Resources). Empty = no particles (e.g. a shake-only row).")]
    public string effect = "";

    [Tooltip("At Event: where it happened (hits, blocks, grabs). On Self / On Opponent: on a fighter, at the anchor below.")]
    public JuicePlace place = JuicePlace.AtEvent;

    [Tooltip("Where on the fighter, for On Self / On Opponent (and At Event when there is no point). Default = the prefab's anchor.")]
    public JuiceAnchor anchor = JuiceAnchor.Default;

    [Tooltip("Extra nudge in Unity units, X = forward. Added to the prefab's own offset.")]
    public Vector2 offset;

    [Tooltip("Moment triggers only, On Self / On Opponent only: 0 = play once where the fighter is, " +
             "N = follow the fighter for N ticks (60 = 1 second). While... triggers ignore this: they follow until the condition ends.")]
    [Min(0)] public int ticks;

    [Tooltip("While... triggers only. 0 = one effect that follows the fighter for as long as the condition holds. " +
             "N = play the effect once every N ticks (60 = 1 second) while it holds, e.g. footstep dust every 15 ticks " +
             "with WhileMoving. Each one plays where the fighter is at that moment and stays there.")]
    [Min(0)] public int everyTicks;

    [Tooltip("Size multiplier. 0 counts as 1.")]
    [Min(0f)] public float scale = 1f;

    [Tooltip("Extra screen shake (trauma 0..1) when this plays, on top of the global hit shake. Moment triggers only.")]
    [Range(0f, 1f)] public float shake;

    /// <summary>True for "While..." triggers, which play for as long as their condition holds.</summary>
    public bool IsWhile
    {
        get { return trigger != null && trigger.Trim().StartsWith(JuiceTriggers.WhilePrefix, StringComparison.OrdinalIgnoreCase); }
    }

    /// <summary>True if this row reacts to <paramref name="name"/> (not case sensitive, spaces around ignored).</summary>
    public bool Matches(string name)
    {
        return trigger != null && string.Equals(trigger.Trim(), name, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Names of the built-in juice triggers, so code can use JuiceTriggers.Hit instead of typing "Hit".
/// Every trigger is relative to the character whose profile the row is in: "Hit" means THIS
/// character landed a hit, "GotHit" means THIS character was hit.
/// <para>
/// Names built from the sim's enums (Enter&lt;State&gt;, While&lt;State&gt;, &lt;Move&gt; suffixes,
/// and future SimEventTypes) appear automatically when the sim adds new ones.
/// </para>
/// </summary>
public static class JuiceTriggers
{
    #region Moments from sim events
    /// <summary>This character landed a hit (or grab, see Grab). Also fires LightHit or HeavyHit, and Hit:&lt;Move&gt; for melee hits.</summary>
    public const string Hit = "Hit";
    /// <summary>A hit below JuiceProfile.heavyThreshold damage.</summary>
    public const string LightHit = "LightHit";
    /// <summary>A hit at or above JuiceProfile.heavyThreshold damage.</summary>
    public const string HeavyHit = "HeavyHit";
    /// <summary>This character was hit. Also fires GotLightHit or GotHeavyHit.</summary>
    public const string GotHit = "GotHit";
    /// <summary>This character was hit lightly.</summary>
    public const string GotLightHit = "GotLightHit";
    /// <summary>This character was hit hard.</summary>
    public const string GotHeavyHit = "GotHeavyHit";
    /// <summary>This character's grab connected.</summary>
    public const string Grab = "Grab";
    /// <summary>This character was grabbed.</summary>
    public const string GotGrabbed = "GotGrabbed";
    /// <summary>This character blocked a hit.</summary>
    public const string Block = "Block";
    /// <summary>This character's attack was blocked.</summary>
    public const string AttackBlocked = "AttackBlocked";
    /// <summary>This character started an attack. Also fires AttackStart:&lt;Move&gt;, e.g. AttackStart:Heavy.</summary>
    public const string AttackStart = "AttackStart";
    /// <summary>This character dashed. Also fires DashForward or DashBack.</summary>
    public const string Dash = "Dash";
    /// <summary>A forward dash.</summary>
    public const string DashForward = "DashForward";
    /// <summary>A back dash.</summary>
    public const string DashBack = "DashBack";
    /// <summary>This character started an Ultimate (the sim freezes briefly).</summary>
    public const string SuperFlash = "SuperFlash";
    /// <summary>This character was knocked out.</summary>
    public const string KnockedOut = "KnockedOut";
    /// <summary>This character knocked the opponent out.</summary>
    public const string ScoredKO = "ScoredKO";
    /// <summary>A new round began (round 2 onwards). Fires for both characters.</summary>
    public const string RoundStart = "RoundStart";
    /// <summary>A round ended. Fires for both characters, plus RoundWin / RoundLose / RoundDraw.</summary>
    public const string RoundEnd = "RoundEnd";
    /// <summary>This character won the round.</summary>
    public const string RoundWin = "RoundWin";
    /// <summary>This character lost the round.</summary>
    public const string RoundLose = "RoundLose";
    /// <summary>The round ended in a double KO.</summary>
    public const string RoundDraw = "RoundDraw";
    #endregion

    #region Moments from fighter states
    /// <summary>Prefix for "this character just entered a state", e.g. EnterHitstun, EnterKO, EnterDash.</summary>
    public const string EnterPrefix = "Enter";
    #endregion

    #region While triggers
    /// <summary>Prefix that marks a trigger as "while": it plays for as long as its condition holds.</summary>
    public const string WhilePrefix = "While";
    /// <summary>HP is at or below JuiceProfile.lowHealthPercent (and above 0).</summary>
    public const string WhileLowHealth = "WhileLowHealth";
    /// <summary>The meter is full (the Ultimate is ready).</summary>
    public const string WhileMeterFull = "WhileMeterFull";
    /// <summary>Holding back, so a hit right now would be blocked.</summary>
    public const string WhileBlocking = "WhileBlocking";
    /// <summary>Sliding sideways for any reason: walking, dashing or being knocked back. Pair it with
    /// Every Ticks on the row for footstep dust.</summary>
    public const string WhileMoving = "WhileMoving";
    #endregion

    #region Builders
    /// <summary>"Hit:Heavy", "AttackStart:Special", ... A move-specific version of a trigger.</summary>
    public static string ForMove(string trigger, string moveName) { return trigger + ":" + moveName; }

    /// <summary>"EnterHitstun", "EnterDash", ...</summary>
    public static string Enter(FighterStateId state) { return EnterPrefix + state; }

    /// <summary>"WhileWalk", "WhileHitstun", ...</summary>
    public static string While(FighterStateId state) { return WhilePrefix + state; }
    #endregion
}
