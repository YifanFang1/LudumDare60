using UnityEngine;

// One character, edited in the Unity inspector.
//
//   Create:  Project window > right click > Create > Fight > Character
//   Save in: Assets/Characters/<Name>/Resources/   (every CharacterData in a Resources folder joins the roster)
//
// `def` is the gameplay data (stats, hurtbox, moves, hitboxes). It is plain data that the
// deterministic sim plays. The "Look" fields come from CharacterLook and never affect the fight.
[CreateAssetMenu(menuName = "Fight/Character", fileName = "NewCharacter")]
public class CharacterData : CharacterLook
{
    [Header("Gameplay")]
    public CharacterDef def = CharacterDef.CreateDefault();

    public override int CharacterId { get { return def.id; } }

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
        // 0-999 are for characters written in code (see CharacterList.cs)
        def.id = id < CharacterList.FirstDataId ? id + CharacterList.FirstDataId : id;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
