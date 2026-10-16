// Offline only: Play vs AI, no networking code anywhere in this assembly's dependencies.
// This is also the fallback: if the game ever ships without online play, put this on the
// GameObject instead of the network session and everything else keeps working.
public class LocalMatchSession : MatchSessionBase
{
}
