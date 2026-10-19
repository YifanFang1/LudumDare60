using System;

// Unvoiced pre-match exchanges from the team sheet. Class names and roster ids are unchanged:
// Ruy=10, 0d3tte/Painter=11, Noir=12, ROOM/Elephant=13, Patience/Penance/Executioner=14,
// Dr. Remy=15, K3N/Nek=16. All 43 opening scenes come from the completed team sheet.
public static class MatchupDialogue
{
    public sealed class Exchange
    {
        public readonly int firstId, secondId;
        public readonly string firstText, secondText;
        public Exchange(int firstId, int secondId, string firstText, string secondText = null)
        {
            this.firstId = firstId; this.secondId = secondId;
            this.firstText = firstText; this.secondText = secondText;
        }
        public void Configure(GameSim sim, int slot0Id, int variant)
        {
            sim.introFirstSlot = firstId == slot0Id ? 0 : 1;
            sim.introFirstTicks = ReadingTicks(firstText);
            sim.introSecondTicks = string.IsNullOrEmpty(secondText) ? 0 : ReadingTicks(secondText);
            sim.introVariant = variant;
        }
    }

    // 30 characters/second plus a reading pause; at least four seconds per line.
    public static int ReadingTicks(string text) { return Math.Max(GameSim.IntroLineTicks, 90 + text.Length * 2); }

    static readonly Exchange[] exchanges =
    {
        new Exchange(10, 16, "Who are you supposed to be?", "Uhhh, I can be anything I want to be!"),
        new Exchange(16, 10, "Prepare to get humbled like the side character you are.",
            "Let's not get it twisted. You're the only side I see. I'm usually the main course, but today I'm feeling like dessert, since I'm gonna be a dish best served cold."),
        new Exchange(10, 12, "We'd make a pretty good Western together, whaddya think, Partner?", "Wet Urn? What are you on about, Buckaroo?"),
        new Exchange(12, 10, "Anyone ever tell you that you seem overconfident?",
            "Honestly, no. Maybe it's because I'm too tall, 'cause it seems people never speak up to their superiors."),
        new Exchange(10, 13, "What did the envelope that got left at the mail office without any stamps say?",
            "Your games do not amuse me, mortal. Now die with more dignity than your humor."),
        new Exchange(13, 10, "Your resistance is futile. Compared to me, you are a worm facing a mountain.",
            "Eh, I think size is irrelephant to the worthiness of a challenger."),
        new Exchange(10, 14, "Tick tock. Tick tock... Let's get this fight started. My patience is running thin.",
            "You will suffer for the transgressions of terrible comedy."),
        new Exchange(14, 10, "Repent for your sins and I might just spare you."),
        new Exchange(11, 10, "I have never met someone who could sum up the word \"brute\" so perfectly until now."),
        new Exchange(10, 15, "Uh, what's with the rat?"),
        new Exchange(12, 16, "Howdy, it's strange seeing a new face around these parts.", "Don't worry, I plan to get very acquainted."),
        new Exchange(16, 13, "Do you actually think I'm strong enough to humble Ruy?",
            "I do not care enough about mortal affairs to give you a response, but suppose I did. Then I think that you're on your way."),
        new Exchange(13, 16, "You seem different from the others. Are you the one that I dragged here?"),
        new Exchange(16, 14, "You know, I always thought that you were the coolest."),
        new Exchange(11, 16, "Fix your stance, don't slouch. It bothers me."),
        new Exchange(16, 15, "OMG, Remy, I can't believe it's you! Tell me, do you work like a hive mind? I've always been curious."),
        new Exchange(12, 13, "Well, if I had a bottle of gin for each time I faced off against an elephant... well, I'd have one bottle. Eh, the point is that it's weird."),
        new Exchange(12, 14, "Well, you're a character."),
        new Exchange(11, 12, "I absolutely detest alcoholics. Go clean yourself up."),
        new Exchange(15, 12, "Looks like I'm up against you again. Drats.",
            "Well, I'd almost be offended if it weren't for the fact that I'm gonna win this showdown of ours."),
        new Exchange(12, 15, "Well, well, well. Did someone call an exterminator?"),
        new Exchange(11, 14, "I see we have an iron maiden fighting... an iron maiden.", "I will admit, this is most interesting."),
        new Exchange(14, 11, "I sense you and I could be kindred spirits."),
        new Exchange(14, 15, "You poor souls, I shall absolve you of your pain and suffering."),
        new Exchange(11, 15, "I'm a doll brought to life, but I still think that you take the cake for being weirder."),
        new Exchange(14, 10, "Repent sinner, and I might just spare you.",
            "Sinner? I hardly know her!"),
        new Exchange(10, 11, "May you rust in peace.",
            "Your lyrics lack a melody, Dear."),
        new Exchange(11, 10, "I have never met someone that could sum up the word brute so perfectly until now.",
            "Well, they did say Brutus was a handsome man..."),
        new Exchange(10, 15, "Uh, what's with the rat?",
            "Squeak? (\"Which one?\")"),
        new Exchange(15, 10, "\"Squeak squeak.\" (Got something in your teeth)",
            "Squeak? You'll need a sponge after this fight to make the stage squeaky clean!"),
        new Exchange(12, 16, "Howdy, it's strange seein' a new face around these parts.",
            "Don't worry, I plan to get very aquainted"),
        new Exchange(16, 12, "You're straight out of the movies! I remember your face from this old Western my dad showed me.",
            "Seems another Galoot is out on the loose."),
        new Exchange(16, 13, "Do you actually think I'm strong enough to humble Ruy?",
            "I do not care enough about mortal affairs to give you a response."),
        new Exchange(13, 16, "You seem different from the others, Are you the one that I dragged here?",
            "You can say I got a bit of a glow up since getting here xD"),
        new Exchange(16, 14, "You know, I always thought that you were the coolest.",
            "Thank you."),
        new Exchange(14, 16, "Youth of your years need the most repentance.",
            "Hey! What do you mean by that?"),
        new Exchange(16, 11, "Are you supposed to be some kind of Barbie?",
            "I know not of this Barbie. I write my own song."),
        new Exchange(11, 16, "Fix your stance, don't slouch. It bothers me.",
            "Chillax, you're not my mom!"),
        new Exchange(16, 15, "OMG Remy, I can't believe it's you! Tell me, do you work like a hive mind, I've always been curious.",
            "\"Squeak Squeak.\" (We're all our own rat. Dr. Remy is merely our leader.)"),
        new Exchange(15, 16, "\"Squeak squeak\" (I wish I had the powers of your modern medicine...)",
            "At least your outfit is poppin'."),
        new Exchange(12, 13, "Well, if I had a bottle of gin for each time I faced off against an Elephant...well I'd have one bottle. Eh, the point is that it's weird.",
            "And it'll stay at one after I end you, mortal."),
        new Exchange(13, 12, "...",
            "Not a creature of much words, eh?"),
        new Exchange(12, 14, "Well, you're certainly a character.",
            "I am merely a messenger. Repent."),
        new Exchange(14, 12, "You have ruined so many lives with your \"justice.\"",
            "Eh, I'm sure you'd agree, sidewiners and tinhorns oughta be punished."),
        new Exchange(12, 11, "You ever try line dancin'?",
            "Mostly ballet, but I'd love to try."),
        new Exchange(11, 12, "I absolutely detest Alcoholics, go clean yourself up.",
            "It's a lifestyle partner. Care for a shot?"),
        new Exchange(15, 12, "Squeak squeak. (Looks like I'm up against you again. Drats.)",
            "Seems some varmint got loose."),
        new Exchange(12, 15, "Well, Well, Well, did someone call an exterminator?",
            "\"squeak squeak squeak.\" (I've seent plenty cats scarier than you.)"),
        new Exchange(11, 14, "I see we have an iron maiden fighting... an iron maiden",
            "I will admit, this is most interesting"),
        new Exchange(14, 11, "I sense you and I could be kindred spirits",
            "Would you join me for this dance?"),
        new Exchange(15, 14, "Squeak squeak? (Would you say I've repented with my remedies?)",
            "I do not favor those who choose a life of deception."),
        new Exchange(14, 15, "You poor souls, I shall absolve you of your pain and suffering",
            "\"Squeak squeak.\" (That's my job. Want some tonics?)"),
        new Exchange(11, 15, "I'm a doll brought to life, but still I think that you take the cake for being weirder",
            "\"Squeak squeak...\" (Maybe I do wish I could give you the plague...)"),
        new Exchange(15, 11, "Squeak squeak (You're made of many parts, just like me...)",
            "Hmm, what was that?"),
        new Exchange(10, 10, "Are you from Tennesse? Cause you're the only 10 I see.",
            "Do you know what my shirt is made of? It's made of warrior material."),
        new Exchange(16, 16, "Hey! It's the best character in the game!",
            "This is so baller!"),
        new Exchange(12, 12, "Howdy partner.",
            "Lookin' sharp as always."),
        new Exchange(13, 13, "*nod*",
            "*nod*"),
        new Exchange(14, 14, "Repent.",
            "Serve."),
        new Exchange(11, 11, "So this is what I look like?",
            "Just as graceful as I expected."),
        new Exchange(15, 15, "Squeak squeak squeak!",
            "Squeak squeak!!")
    };

    static bool Matches(Exchange entry, int a, int b)
    {
        return (entry.firstId == a && entry.secondId == b) || (entry.firstId == b && entry.secondId == a);
    }
    public static Exchange Select(int a, int b, int variant)
    {
        int count = 0;
        foreach (Exchange entry in exchanges) if (Matches(entry, a, b)) count++;
        if (count == 0) return new Exchange(a, b, "To be implemented.", "To be implemented.");
        int chosen = ((variant % count) + count) % count;
        foreach (Exchange entry in exchanges)
            if (Matches(entry, a, b) && chosen-- == 0) return entry;
        throw new InvalidOperationException("Unreachable dialogue variant");
    }
}
