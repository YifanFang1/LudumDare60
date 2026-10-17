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
    [Tooltip("Prefab with an Animator. Needs states named Idle, Walk, Hitstun, Blockstun, KO and one per move name. Art should face RIGHT. Empty = Sprite, or a coloured box.\nFight > Create Animator for Selected Character builds one for you.")]
    public GameObject viewPrefab;
    [Tooltip("A single pose for blockout or simple characters. Ignored if a View Prefab is set. Art should face RIGHT.")]
    public Sprite sprite;
    [Tooltip("Where the art sits relative to the fighter's feet, in Unity units. X is forward.")]
    [Range(-2f, 2f)] public float spriteOffsetX;
    [Range(-1f, 3f)] public float spriteOffsetY;
    [Range(0.1f, 4f)] public float spriteScale = 1f;
    public Color placeholderColor = new Color(0.3f, 0.5f, 1f);
    [Tooltip("Ticks for one loop of the Idle / Walk animation (60 ticks = 1 second).")]
    public int idleLoopTicks = 60;
    public int walkLoopTicks = 30;

    // The CharacterList id this look belongs to.
    public abstract int CharacterId { get; }
}
