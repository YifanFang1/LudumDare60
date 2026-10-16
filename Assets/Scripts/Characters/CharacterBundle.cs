using System;

// The portable file format for characters: what the Unity editor exports, what the server hosts
// (relay/characters.json) and what the game downloads. Plain data, no UnityEngine.
[Serializable]
public class CharacterBundle
{
    public const int CurrentFormat = 1;   // bump if the shape of CharacterDef changes incompatibly

    public int format = CurrentFormat;
    public CharacterDef[] characters;
}
