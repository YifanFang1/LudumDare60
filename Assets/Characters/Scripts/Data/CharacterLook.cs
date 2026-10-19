using UnityEngine;

// How a character LOOKS: sprite, animated prefab, placeholder colour. Never affects the fight.
//
// Two kinds of asset share these fields:
//   CharacterData       an inspector character: its look AND its gameplay data in one asset
//   CodeCharacterArt    the look for a character written in code (Brawler, Shooter, ...)
// WorldView draws any fighter from whichever of the two matches its id (CharacterRegistry.GetLook).
//
// Art always faces RIGHT; the game flips it for the player on the right.
public abstract class CharacterLook : ScriptableObject
{
    [Header("Look (optional, never affects the fight)")]
    [Tooltip("Prefab with an Animator. Needs states named Idle, Walk, DashForward, DashBack, Jump, Hitstun, Blockstun, KO, Held and one per move name (Light, Heavy, Grab, Special, Ultimate, JumpLight, JumpHeavy). Art should face RIGHT. Empty = Sprite, or a placeholder box.\nFight > Create Animator for Selected Character builds one for you.")]
    public GameObject viewPrefab;
    [Tooltip("A single pose for blockout or simple characters. Ignored if a View Prefab is set. Art should face RIGHT.")]
    public Sprite sprite;
    [Tooltip("Where the art sits relative to the fighter's feet, in Unity units. X is forward.")]
    [Range(-2f, 2f)] public float spriteOffsetX;
    [Range(-1f, 3f)] public float spriteOffsetY;
    [Range(0.1f, 4f)] public float spriteScale = 1f;
    [Tooltip("Tick if the art was drawn facing LEFT. The game expects art facing right, so this mirrors it (Sprite or View Prefab). The offsets above are not mirrored.")]
    public bool flipSprite;
    public Color placeholderColor = new Color(0.3f, 0.5f, 1f);
    [Tooltip("For a single Sprite (no View Prefab): tint it by what the fighter is doing (attack = brighter, hit = darker, block = cyan, powered up = gold), since one pose can't show it. Untick once the character has real animation.")]
    public bool tintByState = true;
    [Tooltip("Ticks for one loop of the Idle / Walk animation (60 ticks = 1 second).")]
    public int idleLoopTicks = 60;
    public int walkLoopTicks = 30;

    [Tooltip("Art for this character's projectiles. 'Visual' must match the move's visual name: \"special\" for the standard Special. Use a Center pivot; art faces RIGHT. A projectile with no art here only shows as a yellow box with the Hitboxes toggle on.")]
    public ProjectileSprite[] projectileSprites;

    [Tooltip("An image flashed over the fighter (stretched to the move's attack area, the box that hurts enemies) when a move starts, then quickly faded out. 'Move' is the move name: Light, Heavy, Grab, Special, Ultimate... View only.")]
    public MoveFlash[] moveFlashes;

    [Tooltip("This character's own sounds. Anything left empty falls back to the AudioManager's shared sounds.")]
    public CharacterSfx sfx = new CharacterSfx();

    [Tooltip("This character's voice: spoken intro and win/lose lines, and combat shouts. See CharacterVoice.cs.")]
    public CharacterVoice voice = new CharacterVoice();

    // -1 when flipSprite is ticked, else 1. Multiply the art's x scale by it.
    public float SpriteFlip { get { return flipSprite ? -1f : 1f; } }

    // The CharacterList id this look belongs to.
    public abstract int CharacterId { get; }

    // The sprite for a projectile's visual name, or null if this look has none.
    public ProjectileSprite FindProjectileEntry(string visual)
    {
        if (projectileSprites == null || string.IsNullOrEmpty(visual)) return null;
        for (int i = 0; i < projectileSprites.Length; i++)
            if (projectileSprites[i] != null && projectileSprites[i].visual == visual) return projectileSprites[i];
        return null;
    }

    public MoveFlash FindMoveFlash(string moveName)
    {
        if (moveFlashes == null || string.IsNullOrEmpty(moveName)) return null;
        for (int i = 0; i < moveFlashes.Length; i++)
            if (moveFlashes[i] != null && moveFlashes[i].sprite != null && moveFlashes[i].move == moveName) return moveFlashes[i];
        return null;
    }

    public Sprite FindProjectileSprite(string visual)
    {
        if (projectileSprites == null || string.IsNullOrEmpty(visual)) return null;
        for (int i = 0; i < projectileSprites.Length; i++)
        {
            if (projectileSprites[i] != null && projectileSprites[i].visual == visual) return projectileSprites[i].sprite;
        }
        return null;
    }
}

// One entry in CharacterLook.projectileSprites: which sprite to draw for a projectile.
[System.Serializable]
public class ProjectileSprite
{
    public string visual = "special";
    public Sprite sprite;
    public float scale = 1f;    // view size multiplier (1 = the import size)
    public bool flip;           // tick if the art faces LEFT: it is mirrored so it flies the right way

    [Header("Animation (optional, view only)")]
    [Tooltip("Frames of an animation, in order. Empty = just the single Sprite above. Use a Center pivot, same as the Sprite.")]
    public Sprite[] frames;
    [Tooltip("How fast the frames play.")]
    public float framesPerSecond = 12f;
    [Tooltip("Repeat the frames while it is on screen. Off = play once and hold the last frame.")]
    public bool loop;
    [Tooltip("Placed hitboxes only (ones that do not fly or follow the fighter, like Patience's blade): keep showing this for this many seconds after it appears, even if the hitbox itself is gone sooner. 0 = only while the hitbox exists.")]
    public float lingerSeconds;
}

// One entry in CharacterLook.moveFlashes: an image shown over the fighter when a move starts.
[System.Serializable]
public class MoveFlash
{
    public string move = "Special";
    public Sprite sprite;
    [Tooltip("Seconds to fade from fully visible to gone.")]
    public float fadeSeconds = 0.35f;
}

// One character's sound set. Every slot is optional: empty = the AudioManager's shared sound plays.
// Clip lists pick a random clip each time.
[System.Serializable]
public class CharacterSfx
{
    [Tooltip("Sound when a specific move starts. 'Move' is the move name: Light, Heavy, Grab, Special, Ultimate, JumpLight, JumpHeavy.")]
    public MoveSfx[] moves;
    [Tooltip("Played when ANY move of this character starts and 'moves' has no entry for it.")]
    public AudioClip[] attackStart;
    [Tooltip("This character's hits landing on someone.")]
    public AudioClip[] hit;
    [Tooltip("Heavy hits (damage at or above the AudioManager's Heavy Hit Damage). Empty = hit.")]
    public AudioClip[] heavyHit;
    [Tooltip("This character's grab landing. Empty = hit.")]
    public AudioClip[] grab;
    [Tooltip("This character blocking a hit.")]
    public AudioClip[] block;
    [Tooltip("This character dashing.")]
    public AudioClip[] dash;
    [Tooltip("This character jumping off the ground.")]
    public AudioClip[] jump;
    [Tooltip("This character's Ultimate freeze.")]
    public AudioClip superFlash;
    [Tooltip("This character being knocked out.")]
    public AudioClip ko;
    public AudioClip[] reflect;
    public AudioClip[] destroy;
    public AudioClip[] summon;
    public AudioClip[] hold;
    public AudioClip[] heal;
    [Tooltip("A power-up switching on (K3N's Install).")]
    public AudioClip[] powerUp;
    [Tooltip("A fresh Special press with no ammo left (Noir's empty click).")]
    public AudioClip[] outOfAmmo;
    [Tooltip("This character's music. Plays in round 2 or 3 after they win a round. Empty = the AudioManager's Fight Music.")]
    public AudioClip theme;

    public AudioClip[] FindMove(string moveName)
    {
        if (moves == null || string.IsNullOrEmpty(moveName)) return null;
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] != null && moves[i].move == moveName && moves[i].clips != null && moves[i].clips.Length > 0)
                return moves[i].clips;
        }
        return null;
    }
}

[System.Serializable]
public class MoveSfx
{
    public string move = "Light";
    public AudioClip[] clips;
}
