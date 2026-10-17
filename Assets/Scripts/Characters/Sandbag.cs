// SANDBAG: a training dummy. It can't walk, attack or block; it just stands there and gets hit.
// Use it to test reach, damage and combos. Pick it as the opponent in the lobby.
//
// Its health refills to full when a hit would KO it, so the match never ends. The health bar
// still drops with every hit, so you can read how much damage things do.
//
// Shows the two "do nothing" hooks from Fighter: FilterInput and CanBeKnockedOut.
//
// Id 2.
public class Sandbag : BaseFighter
{
    public const int Id = 2;

    // No LightAttack / HeavyAttack overrides: the template's default is "no move".

    // Ignore every button, from a player or the AI. No walking, no attacks, no blocking.
    protected override int FilterInput(int input) { return 0; }

    public override bool CanBeKnockedOut { get { return false; } }
}
