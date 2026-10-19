using UnityEngine;

// The art and text for the character select screen, one entry per character. It lives at
// Assets/UI/Resources/CharacterSelectArt.asset and is (re)filled by Fight > Rebuild Default Canvas from the
// pictures in Assets/UI/CharacterSelect/<CHARACTER>/. Edit the texts here; a rebuild keeps them.
// Characters with no entry (no art yet) show their normal sprite on a plain colour and have no More page.
// An entry can hold only some pictures (Sandbag has just its grid tiles so far); the rest fall back the same way.
[CreateAssetMenu(fileName = "CharacterSelectArt", menuName = "Fight/Character Select Art")]
public class CharacterSelectArt : ScriptableObject
{
    public const string ResourceName = "CharacterSelectArt";

    [System.Serializable]
    public class Skill
    {
        public string name;
        [TextArea(1, 4)] public string description;
        [Tooltip("This skill's icon on the More > Skills page. Empty = the character's Skill Block square.")]
        public Sprite icon;
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
        [Tooltip("The grid tile, finished art with backing and portrait (150x200). Empty = the tile is made from backing + sprite.")]
        public Sprite select;
        [Tooltip("The grid tile while this character is NOT the current pick: a black silhouette on the backing (150x200). The pick shows `select` in colour.")]
        public Sprite picked;
        [Tooltip("The part of the sprite that is not transparent, in sprite pixels from its top-left (x, y, width, height). Used to centre it. Empty = the whole sprite.")]
        public Rect spriteBounds;
        [Tooltip("Pixel-art pictures: how many screen pixels each picture pixel becomes. 0 = automatic (native size, shrunk to fit).")]
        public float spriteScale;
        [Tooltip("Moves the big character picture from its automatic spot, in 1920x1080 pixels (+x right, +y down). Set in BuildFightCanvas.BuildSelectArt.")]
        public Vector2 spriteOffset;
        [Tooltip("Grid tile only: the portrait's Rect Transform as the Inspector shows it (Pos X, Pos Y, Width, Height; top-left anchored). Width 0 = automatic placement.")]
        public Rect tilePortraitRect;
        public Sprite nameArt;
        [Tooltip("The part of the name picture that is not transparent (x, y, width, height from its top-left). nameCorner is where the bottom-right of THIS part goes. Empty = the whole picture.")]
        public Rect nameBounds;
        public Sprite moreArt;
        public Sprite backArt;
        public Sprite miniSkill;
        [Tooltip("The four small skill icons under the character on the main panel, left to right. An empty slot shows the Mini Skill square. Set by hand; a rebuild keeps them.")]
        public Sprite[] skillIcons = new Sprite[4];
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
    [Tooltip("The Random (\"?\") tile. Empty = a grey tile with a \"?\".")]
    public Sprite randomSelect;
    [Tooltip("The dark Random (\"?\") tile, shown while Random is NOT the current pick.")]
    public Sprite randomPicked;
    [Tooltip("The \"Choose an opponent\" title, shown instead of \"Choose a renegade\" while picking the AI's fighter. Used when the screen has no separate Opponent Title object.")]
    public Sprite opponentTitle;

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
