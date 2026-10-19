using UnityEngine;

// The art and text for the character select screen, one entry per character. It lives at
// Assets/UI/Resources/CharacterSelectArt.asset and is (re)filled by Fight > Rebuild Default Canvas from the
// pictures in Assets/UI/CharacterSelect/<CHARACTER>/. Edit the texts here; a rebuild keeps them.
// Characters with no entry (no art yet) show their normal sprite on a plain colour and have no More page.
[CreateAssetMenu(fileName = "CharacterSelectArt", menuName = "Fight/Character Select Art")]
public class CharacterSelectArt : ScriptableObject
{
    public const string ResourceName = "CharacterSelectArt";

    [System.Serializable]
    public class Skill
    {
        public string name;
        [TextArea(1, 4)] public string description;
    }

    [System.Serializable]
    public class Entry
    {
        public string label;
        public int characterId;
        [Tooltip("Coloured backdrop: the grid tile and the big panel behind the character (500x1000).")]
        public Sprite backing;
        [Tooltip("The big character picture.")]
        public Sprite sprite;
        [Tooltip("The part of the sprite that is not transparent, in sprite pixels from its top-left (x, y, width, height). Used to centre it. Empty = the whole sprite.")]
        public Rect spriteBounds;
        [Tooltip("Grid tile only: the portrait's Pos Y in the Inspector (top-anchored, so bigger = higher). 0 = automatic (head 6 px from the top).")]
        public float tilePortraitY;
        public Sprite nameArt;
        public Sprite moreArt;
        public Sprite backArt;
        public Sprite miniSkill;
        public Sprite skillBlock;
        [Tooltip("The tilted word banners (up to 3), top to bottom.")]
        public Sprite[] traits;
        [Tooltip("Optional: the top-left of each banner in the 1920x1080 layout. Leave empty for the automatic stagger.")]
        public Vector2[] traitPositions;
        [Tooltip("Where the bottom-right corner of the name picture goes in the 1920x1080 layout.")]
        public Vector2 nameCorner = new Vector2(1868f, 830f);
        [TextArea(4, 14)] public string background = "Coming soon.";
        public Skill[] skills;
    }

    public Entry[] entries;

    public Entry Find(int characterId)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null && entries[i].characterId == characterId) return entries[i];
        }
        return null;
    }

    static CharacterSelectArt cached;

    public static CharacterSelectArt Load()
    {
        if (cached == null) cached = Resources.Load<CharacterSelectArt>(ResourceName);
        return cached;
    }
}
