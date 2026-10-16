// Everything two machines must agree on before a match starts. Plain data, no logic.
// If you add a field here that changes the sim, the networking layer has to send it too.
public struct MatchSetup
{
    public int seed;
    public int p0Character;    // index into CharacterList
    public int p1Character;
}
