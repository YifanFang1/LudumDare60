using UnityEngine;

// One character, edited in the Unity inspector.
//
//   Create:  Project window > right click > Create > Fight > Character
//   Save in: Assets/Resources/Characters/   (every asset in that folder joins the roster)
//
// `def` is the gameplay data (stats, hurtbox, moves, hitboxes). It is plain data that the
// deterministic sim plays. Everything under "Look" is only for drawing and never affects the fight.
[CreateAssetMenu(menuName = "Fight/Character", fileName = "NewCharacter")]
public class CharacterData : ScriptableObject
{
    public CharacterDef def = CharacterDef.CreateDefault();

    [Header("Look (optional, never affects the fight)")]
    [Tooltip("Prefab with an Animator. Needs states named Idle, Walk, Hitstun, KO and one per move name. Art should face RIGHT. Empty = coloured box.")]
    public GameObject viewPrefab;
    [Tooltip("A single pose for blockout or simple characters. Ignored if a View Prefab is set. Art should face RIGHT.")]
    public Sprite sprite;
    [Tooltip("Where the sprite sits relative to the fighter's feet, in Unity units. X is forward.")]
    [Range(-2f, 2f)] public float spriteOffsetX;
    [Range(-1f, 3f)] public float spriteOffsetY;
    [Range(0.1f, 4f)] public float spriteScale = 1f;
    public Color placeholderColor = new Color(0.3f, 0.5f, 1f);
    [Tooltip("Ticks for one loop of the Idle / Walk animation (60 ticks = 1 second).")]
    public int idleLoopTicks = 60;
    public int walkLoopTicks = 30;

    // Ids are random and permanent, so 40 people can add characters without coordinating.
    void OnValidate()
    {
        if (def.id == 0) GenerateId();
        if (string.IsNullOrEmpty(def.displayName)) def.displayName = name;
    }

    [ContextMenu("Generate new id")]
    void GenerateId()
    {
        int id = System.Guid.NewGuid().GetHashCode() & 0x7fffffff;
        def.id = id == 0 ? 1 : id;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
