using System;

// Unvoiced pre-match exchanges from the team sheet. Class names and roster ids are unchanged:
// Ruy=10, 0d3tte/Painter=11, Noir=12, ROOM/Elephant=13, Patience/Penance/Executioner=14,
// Dr. Remy=15, K3N/Nek=16. Every matchup and mirror match has at least one exchange.
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

    // One entry per exchange on the team sheet ("(UNVOICED) Character Dialogue"), grouped by
    // matchup. The first id speaks first. Several entries for the same pair = one is picked per match.
    static readonly Exchange[] exchanges =
    {
        // Ruy + K3N
        new Exchange(10, 16, "Who are you supposed to be?", "Uhhh, I can be anything I want to be!"),
        new Exchange(16, 10, "Prepare to get humbled like the side character you are.",
            "Let's not get it twisted. You're the only side I see. I'm usually the main course, but today I'm feeling like dessert, since I'm gonna be a dish best served cold."),
        new Exchange(10, 16, "I remember your birthday is soon. I hope you can get a cake out of this fight.",
            "You actually remembered? Well, don't fly off the candle this fight, Broski."),
        new Exchange(16, 10, "I always knew this day would come.", "What, Tuesday?"),

        // Ruy + Noir
        new Exchange(10, 12, "We'd make a pretty good Western together. Whaddya think, Partner?", "Wet Urn? What are ya on about, Buckaroo?"),
        new Exchange(12, 10, "I hear you think you're the star of the show. I think it's about time I show you who's really in charge of this rodeo.",
            "I typically wouldn't take that, but I wish you'd remember who you truly are. I loved your movies growing up..."),
        new Exchange(10, 12, "So if you're new R, what happened to old R?", "I don't have a clue as to what you're saying, varmint."),
        new Exchange(12, 10, "Anyone ever tell ya that you seem overconfident?",
            "Honestly, no. Maybe it's because I'm too tall, 'cause it seems people never speak up to their superiors."),

        // Ruy + ROOM
        new Exchange(10, 13, "What did the envelope that got left at the mail office without any stamps say?",
            "Your games do not amuse me, mortal. Now die with more dignity than your humor."),
        new Exchange(13, 10, "Your resistance is futile. Compared to me, you are a worm facing a mountain.",
            "Eh, I think size is irrelephant to the worthiness of a challenger."),
        new Exchange(10, 13, "Oh, sick, I always wanted a pet elephant!", "Silence. You're like a pet to me."),
        new Exchange(13, 10, "Your breath alone irks me.", "Tusk tusk. You're one to talk."),

        // Ruy + Patience
        new Exchange(10, 14, "Tick tock. Tick tock... Let's get this fight started. My patience is running thin.",
            "You will suffer for the transgressions of terrible comedy."),
        new Exchange(14, 10, "Repent, sinner, and I might just spare you.", "Sinner? I hardly know her!"),
        new Exchange(10, 14, "I hear you punish people for their sins, but isn't it a sin to have no sense of humor?", "No."),

        // Ruy + 0d3tte
        new Exchange(10, 11, "May you rust in peace.", "Your lyrics lack a melody, dear."),
        new Exchange(11, 10, "I have never met someone who could sum up the word \"brute\" so perfectly until now.",
            "Well, they did say Brutus was a handsome man..."),

        // Ruy + Dr. Remy
        new Exchange(10, 15, "Uh, what's with the rat?", "Squeak? (Which one?)"),
        new Exchange(15, 10, "Squeak squeak. (Got something in your teeth.)",
            "Squeak? You'll need a sponge after this fight to make the stage squeaky clean!"),

        // K3N + Noir
        new Exchange(12, 16, "Howdy. It's strange seein' a new face around these parts.", "Don't worry, I plan to get very acquainted."),
        new Exchange(16, 12, "You're straight out of the movies! I remember your face from this old Western my dad showed me.",
            "Seems another galoot is out on the loose."),

        // K3N + ROOM
        new Exchange(16, 13, "Do you actually think I'm strong enough to humble Ruy?",
            "I do not care enough about mortal affairs to give you a response."),
        new Exchange(13, 16, "You seem different from the others. Are you the one that I dragged here?",
            "You could say I got a bit of a glow-up since getting here xD"),

        // K3N + Patience
        new Exchange(16, 14, "You know, I always thought that you were the coolest.", "Thank you."),
        new Exchange(14, 16, "Youth of your years needs the most repentance.", "Hey! What do you mean by that?"),

        // K3N + 0d3tte
        new Exchange(16, 11, "Are you supposed to be some kind of Barbie?", "I know not of this Barbie. I write my own song."),
        new Exchange(11, 16, "Fix your stance. Don't slouch. It bothers me.", "Chillax, you're not my mom!"),

        // K3N + Dr. Remy
        new Exchange(16, 15, "OMG, Remy, I can't believe it's you! Tell me, do you work like a hive mind? I've always been curious.",
            "Squeak squeak. (We're all our own rat. Dr. Remy is merely our leader.)"),
        new Exchange(15, 16, "Squeak squeak. (I wish I had the powers of your modern medicine...)", "At least your outfit is poppin'."),

        // Noir + ROOM
        new Exchange(12, 13, "Well, if I had a bottle of gin for each time I faced off against an elephant... well, I'd have one bottle. Eh, the point is that it's weird.",
            "And it'll stay at one after I end you, mortal."),
        new Exchange(13, 12, "...", "Not a creature of many words, eh?"),

        // Noir + Patience
        new Exchange(12, 14, "Well, you're certainly a character.", "I am merely a messenger. Repent."),
        new Exchange(14, 12, "You have ruined so many lives with your \"justice.\"",
            "Eh, I'm sure you'd agree sidewinders and tinhorns oughta be punished."),

        // Noir + 0d3tte
        new Exchange(12, 11, "You ever try line dancin'?", "Mostly ballet, but I'd love to try."),
        new Exchange(11, 12, "I absolutely detest alcoholics. Go clean yourself up.", "It's a lifestyle, partner. Care for a shot?"),

        // Noir + Dr. Remy
        new Exchange(15, 12, "Squeak squeak. (Looks like I'm up against you again. Drats.)", "Seems some varmint got loose."),
        new Exchange(12, 15, "Well, well, well. Did someone call an exterminator?",
            "Squeak squeak squeak. (I've seen plenty of cats scarier than you.)"),

        // Patience + 0d3tte
        new Exchange(11, 14, "I see we have an iron maiden fighting... an iron maiden.", "I will admit, this is most interesting."),
        new Exchange(14, 11, "I sense you and I could be kindred spirits.", "Would you join me for this dance?"),

        // Patience + Dr. Remy
        new Exchange(15, 14, "Squeak squeak? (Would you say I've repented with my remedies?)",
            "I do not favor those who choose a life of deception."),
        new Exchange(14, 15, "You poor souls, I shall absolve you of your pain and suffering.",
            "Squeak squeak. (That's my job. Want some tonics?)"),

        // 0d3tte + Dr. Remy
        new Exchange(11, 15, "I'm a doll brought to life, but I still think that you take the cake for being weirder.",
            "Squeak squeak... (Maybe I do wish I could give you the plague...)"),
        new Exchange(15, 11, "Squeak squeak. (You're made of many parts, just like me...)", "Hmm, what was that?"),

        // ROOM + Patience
        new Exchange(13, 14, "You're the closest to a worthy competitor here.", "My fighting isn't for pleasure."),
        new Exchange(14, 13, "You're just as much a sinner as everyone here.", "My goals are beyond your understanding."),

        // ROOM + 0d3tte
        new Exchange(13, 11, "I see a pathetic attempt by humans at making superior life.", "I may be a robot, but I perform my own ballet."),
        new Exchange(11, 13, "That's a beautiful trunk you have. I'm sure with the right rhythm you'd be an excellent dancer.",
            "Don't dance around threats. Address them."),

        // ROOM + Dr. Remy
        new Exchange(15, 13, "Squeak squeak!", "Toot toot!"),
        new Exchange(13, 15, "Toot.", "Squeak."),

        // Mirror matches
        new Exchange(10, 10, "Are you from Tennessee? 'Cause you're the only 10 I see.",
            "Do you know what my shirt is made of? It's made of warrior material."),
        new Exchange(16, 16, "Hey! It's the best character in the game!", "This is so baller!"),
        new Exchange(12, 12, "Howdy, partner.", "Lookin' sharp as always."),
        new Exchange(13, 13, "*nod*", "*nod*"),
        new Exchange(14, 14, "Repent.", "Serve."),
        new Exchange(11, 11, "So this is what I look like?", "Just as graceful as I expected."),
        new Exchange(15, 15, "Squeak squeak squeak!", "Squeak squeak!!")
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
