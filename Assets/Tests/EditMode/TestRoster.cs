using NUnit.Framework;

// Brawler (id 0) and Shooter (id 1) are not part of the game any more. They stay here as plain
// reference fighters for the sim tests (KitTests, CharacterTests...), and are added to
// CharacterList once before any test runs. Sandbag is the real fallback for unknown ids.
[SetUpFixture]
public class TestRoster
{
    [OneTimeSetUp]
    public void RegisterTestFighters()
    {
        CharacterList.Register(Brawler.Id, "Brawler", delegate { return new Brawler(); });
        CharacterList.Register(Shooter.Id, "Shooter", delegate { return new Shooter(); });
    }
}
