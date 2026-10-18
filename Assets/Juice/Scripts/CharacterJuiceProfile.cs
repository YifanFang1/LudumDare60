using UnityEngine;

/// <summary>
/// One character's juice: a list of reactions, each one "when TRIGGER happens to this character,
/// play EFFECT". Effects are referred to by name (the JuiceEffect prefab's Effect Name, not case
/// sensitive); every built-in trigger is listed on <see cref="JuiceTriggers"/> and in the Trigger tooltip.
/// <para>
/// Set one as the JuiceProfile's Default Character: its reactions apply to every character.
/// Add others to the JuiceProfile's Characters list for character-specific juice; those add to
/// the default reactions unless Use Default Reactions is turned off.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CharacterJuiceProfile", menuName = "Scriptable Objects/CharacterJuiceProfile")]
public class CharacterJuiceProfile : ScriptableObject
{
    #region Settings
    [Tooltip("The character this juice belongs to: its CharacterData, or the Code Character Art of a character written in code. Leave empty on the default profile.")]
    public CharacterLook character;

    [Tooltip("Also play the Default Character profile's reactions for this character. Turn off to replace them completely. Ignored on the default profile itself.")]
    public bool useDefaultReactions = true;

    [Tooltip("When Trigger happens to this character, play Effect. Several rows can share a trigger, and one trigger can drive particles and shake.")]
    public JuiceReaction[] reactions = new JuiceReaction[0];
    #endregion
}
