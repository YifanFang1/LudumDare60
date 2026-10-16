using UnityEngine;

// The art for a character written in code (a BaseFighter subclass such as Brawler or Shooter).
// Code characters have no CharacterData asset, so this is where their sprite / animated prefab goes.
//
//   Create:  Project window > right click Assets/Resources/Characters > Create > Fight > Code Character Art
//   Then:    pick the character in "Character", and drop in a Sprite or a View Prefab.
//
// It only holds the look; the stats and moves stay in the character's .cs file.
// Step-by-step guide: Scripts/Characters/CHARACTERS.md, "Adding art and animation".
[CreateAssetMenu(menuName = "Fight/Code Character Art", fileName = "NewCharacterArt")]
public class CodeCharacterArt : CharacterLook
{
    [Tooltip("Which code character this art is for.")]
    [CodeCharacter] public int characterId = -1;

    public override int CharacterId { get { return characterId; } }
}

// Makes an int field show as a dropdown of the characters written in code
// (drawn by Editor/CodeCharacterDrawer.cs).
public class CodeCharacterAttribute : PropertyAttribute { }
