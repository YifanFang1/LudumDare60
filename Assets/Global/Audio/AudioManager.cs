using UnityEngine;
using UnityEngine.Serialization;

// Sound effects and music. Put it on the same GameObject as the match session (GameManager).
//
// It only LISTENS: fight sounds come from the sim's events through IMatchSession, and music
// follows the match phase. The sim never knows this exists, so sounds can never cause a desync,
// and removing this component just makes the game silent.
//
// Every slot is optional: an empty slot plays nothing. The sound lists pick a random clip each
// time so repeated hits do not all sound the same.
public class AudioManager : MonoBehaviour
{
    [Header("Fight sounds (add several clips per list for variety)")]
    public AudioClip[] attackSounds;     // a move starts
    public AudioClip[] hitSounds;        // a normal hit lands
    public AudioClip[] heavyHitSounds;   // a hit doing at least heavyHitDamage (empty = use hitSounds)
    public AudioClip[] blockSounds;      // a hit is blocked
    public AudioClip[] grabSounds;       // a grab lands (empty = use hitSounds)
    public AudioClip[] dashSounds;       // a dash starts
    public AudioClip[] jumpSounds;       // a fighter jumps off the ground
    public AudioClip superFlashSound;    // an Ultimate starts (the freeze)
    public AudioClip koSound;
    // Roster extras (all optional, empty = silent).
    public AudioClip[] reflectSounds;    // a projectile was reflected (Paintress's canvas)
    public AudioClip[] destroySounds;    // a projectile was destroyed (Room's Vine Boom: the meme sound goes here)
    public AudioClip telegraphSound;     // a warning outline appeared (the Guillotine)
    public AudioClip[] summonSounds;     // Ultimate copies appeared
    public AudioClip[] holdSounds;       // a held grab caught someone (empty = use grabSounds)
    public AudioClip[] healSounds;       // someone healed
    public AudioClip[] powerUpSounds;    // a power-up switched on (K3N's Install)
    public AudioClip[] outOfAmmoSounds;  // a Special press with no ammo (Noir's empty click)
    public int heavyHitDamage = 10;      // Brawler: Light does 5, Heavy does 12

    [Header("Match")]
    // The round call ("Round 1", "Round 2", "Final Round") plays just before each round's countdown,
    // timed so it finishes as the countdown starts (during the intros in round 1, during the pause
    // after the KO in later rounds). The countdown sound plays when the 3-2-1 starts.
    [FormerlySerializedAs("roundStart")] public AudioClip round1Sound;
    public AudioClip round2Sound;        // any round that isn't the first or the final one
    public AudioClip finalRoundSound;    // a round that decides the match (empty = round2Sound)
    [FormerlySerializedAs("fightStart")] public AudioClip countdownSound;   // "3, 2, 1, Fight!"
    public AudioClip winJingle;          // from your side: you won
    public AudioClip loseJingle;
    public AudioClip drawJingle;

    [Header("Menu")]
    public AudioClip buttonClick;
    public AudioClip confirm;            // Ready / Fight! on character select
    public AudioClip pageSwitch;         // the menu page changes (the middle of the glitch transition)
    public AudioClip glitchSound;        // the glitch transition starts
    public AudioClip glassBreak;         // the screen shatters (title press, match start)
    public AudioClip tvOff;              // the TV switches off on Quit

    [Header("Music")]
    public AudioClip menuMusic;          // lobby and waiting for an opponent
    public AudioClip characterSelectMusic;   // empty = keep the menu music playing
    public AudioClip fightMusic;         // round 1, and later rounds if the winner has no theme

    [Header("Volume")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 0.8f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float voiceVolume = 1f;   // character voice lines (CharacterLook.voice)
    [Tooltip("Editor only: log each on-screen line that has no voice clip yet, to see which recordings are missing.")]
    public bool logMissingVoiceLines;

    // So menus can play click sounds without needing a reference (or this component at all).
    static AudioManager instance;

    IMatchSession match;
    AudioSource sfx;      // fight sounds, slightly random pitch
    AudioSource ui;       // clicks and jingles, normal pitch
    AudioSource music;
    bool resultPlayed;    // the win/lose jingle for this match has played
    // Character themes: round 1 plays fightMusic, then each new round plays the theme of whoever
    // won the round before (a drawn round keeps the current music).
    int themeSlot = -1;          // fighter slot whose theme is playing, -1 = fightMusic
    int lastRoundWinner = -1;    // remembered during the pause after a KO, used when the next round starts
    int calledRound;             // the last round whose round call has played
    int countedRound;            // the last round whose countdown sound has played
    // Character voices: one source per fighter, so a new line cuts off that fighter's last one.
    readonly AudioSource[] voices = new AudioSource[2];
    MatchCinematics cinematic;   // the intro and win/lose dialogue (added by WorldView at run time)
    string spokenLine = "";      // the dialogue line already voiced, so each is spoken once

    void Awake()
    {
        match = GetComponent<IMatchSession>();
        if (match == null)
        {
            Debug.LogError("AudioManager needs a component that implements IMatchSession (LocalMatchSession or RelayLockstepSession) on the same object.");
            enabled = false;
            return;
        }

        sfx = AddSource(false);
        ui = AddSource(false);
        music = AddSource(true);
        voices[0] = AddSource(false);
        voices[1] = AddSource(false);

        // Clips with "Preload Audio Data" off load on first use, which can make that first play
        // late or silent. The announcer is timed, so load these up front.
        Preload(round1Sound);
        Preload(round2Sound);
        Preload(finalRoundSound);
        Preload(countdownSound);
    }

    static void Preload(AudioClip clip)
    {
        if (clip != null) clip.LoadAudioData();
    }

    AudioSource AddSource(bool loop)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        return s;
    }

    void OnEnable()
    {
        instance = this;
        if (match == null) return;
        match.SimEventRaised += OnSimEvent;
        match.MatchStarted += OnMatchStarted;
    }

    void OnDisable()
    {
        if (instance == this) instance = null;
        if (match == null) return;
        match.SimEventRaised -= OnSimEvent;
        match.MatchStarted -= OnMatchStarted;
    }

    void Update()
    {
        // The win/lose jingle waits until the win/lose dialogue is over (and its voice has finished).
        if (match.Phase == MatchPhase.Playing && match.Sim.IsOver && !resultPlayed && !DialogueRunning())
        {
            resultPlayed = true;
            PlayResult(match.Sim.Winner);
        }
        // The sim clears roundWinner when the next round starts, so note it while the KO pause lasts.
        if (match.Phase == MatchPhase.Playing && match.Sim.roundWinner >= 0)
            lastRoundWinner = match.Sim.roundWinner;
        if (match.Phase == MatchPhase.Playing) Announce(match.Sim);
        if (match.Phase == MatchPhase.Playing) SpeakDialogue();
        else if (spokenLine != "") { spokenLine = ""; StopVoices(); }   // left the match mid-line

        music.volume = musicVolume;
        SetMusic(MusicFor(match.Phase));
    }

    // ---------- for menus ----------

    public static void Click()
    {
        if (instance != null) instance.PlayUI(instance.buttonClick);
    }

    public static void Confirm()
    {
        if (instance != null) instance.PlayUI(instance.confirm);
    }

    public static void PageSwitch() { if (instance != null) instance.PlayUI(instance.pageSwitch); }
    public static void Glitch() { if (instance != null) instance.PlayUI(instance.glitchSound); }
    public static void GlassBreak() { if (instance != null) instance.PlayUI(instance.glassBreak); }
    public static void TvOff() { if (instance != null) instance.PlayUI(instance.tvOff); }

    // ---------- fight ----------

    void OnMatchStarted(GameSim sim)
    {
        resultPlayed = false;
        themeSlot = -1;
        lastRoundWinner = -1;
        calledRound = 0;
        countedRound = 0;
        spokenLine = "";
        StopVoices();
    }

    // ---------- character voices ----------

    // The voice set of the character a fighter slot is playing, or null.
    CharacterVoice VoiceOf(int slot)
    {
        GameSim sim = match.Sim;
        if (sim == null || slot < 0 || slot >= sim.fighters.Length || sim.fighters[slot] == null) return null;
        CharacterLook look = CharacterRegistry.GetLook(sim.fighters[slot].characterId);
        return look != null ? look.voice : null;
    }

    // Speaks the intro / win / lose line on screen, once, when it first appears.
    void SpeakDialogue()
    {
        if (cinematic == null) cinematic = GetComponent<MatchCinematics>();
        if (cinematic == null || !cinematic.Active) return;

        int slot = cinematic.FocusSlot;
        string text = cinematic.Text;
        string key = slot + ":" + text;
        if (slot < 0 || string.IsNullOrEmpty(text) || key == spokenLine) return;
        spokenLine = key;

        CharacterVoice v = VoiceOf(slot);
        AudioClip clip = v != null ? v.FindLine(text) : null;
        if (clip == null)
        {
            if (logMissingVoiceLines && Application.isEditor)
                Debug.Log("[Voice] No clip for " + cinematic.Speaker + ": \"" + text + "\"");
            return;
        }
        StopVoices();   // the other fighter's line ends when this one starts
        PlayVoice(slot, clip);
    }

    // True while the end-of-match dialogue is on screen or a character is still speaking.
    bool DialogueRunning()
    {
        if (cinematic == null) cinematic = GetComponent<MatchCinematics>();
        if (cinematic != null && cinematic.OutroActive) return true;
        return voices[0].isPlaying || voices[1].isPlaying;
    }

    // Combat shouts from the sim's events. Runs alongside the sound effects in OnSimEvent.
    void Shout(SimEvent e)
    {
        GameSim sim = match.Sim;
        switch (e.type)
        {
            case SimEventType.AttackStart:
            {
                CharacterVoice v = VoiceOf(e.a);
                if (v != null && sim.fighters[e.a].move != null) PlayVoice(e.a, v.FindMove(sim.fighters[e.a].move.name));
                break;
            }
            case SimEventType.Hit:
            {
                CharacterVoice v = VoiceOf(e.b);
                if (v != null && Random.value < v.hurtChance) PlayVoice(e.b, v.hurt);
                break;
            }
            case SimEventType.KO:
            {
                CharacterVoice v = VoiceOf(e.a);
                if (v != null) PlayVoice(e.a, v.ko);
                break;
            }
            case SimEventType.RoundEnd:
            {
                // Not after the last round: the winner's win line plays then.
                CharacterVoice v = VoiceOf(e.a);
                if (v != null && !sim.IsOver) PlayVoice(e.a, v.roundWin);
                break;
            }
        }
    }

    void PlayVoice(int slot, AudioClip[] clips)
    {
        if (!Has(clips)) return;
        PlayVoice(slot, clips[Random.Range(0, clips.Length)]);
    }

    void PlayVoice(int slot, AudioClip clip)
    {
        if (clip == null || slot < 0 || slot > 1) return;
        AudioSource s = voices[slot];
        s.Stop();
        s.clip = clip;
        s.volume = voiceVolume;
        s.Play();
    }

    void StopVoices()
    {
        for (int i = 0; i < voices.Length; i++) if (voices[i] != null) voices[i].Stop();
    }

    // The round call and the countdown, read from the sim's timers each frame (they are not sim
    // events because the round call has to start BEFORE the round's countdown does).
    void Announce(GameSim sim)
    {
        if (sim.IsOver) return;

        // Ticks until the next countdown starts, and which round it is for.
        int next = sim.round;
        int ticksLeft = 0;
        if (sim.introTicks > 0) ticksLeft = sim.introTicks;
        else if (sim.roundOverTicks > 0) { next = sim.round + 1; ticksLeft = sim.roundOverTicks; }

        if (calledRound < next)
        {
            AudioClip call = RoundCall(sim, next);
            int lead = call != null ? Mathf.CeilToInt(call.length * 60f) : 0;
            if (ticksLeft <= lead)
            {
                calledRound = next;
                PlayUI(call);
            }
        }

        if (countedRound < sim.round && sim.introTicks == 0 && sim.roundOverTicks == 0 && sim.countdownTicks > 0)
        {
            countedRound = sim.round;
            PlayUI(countdownSound);
        }
    }

    // Round 1, the final round (both players one round from winning), or any other round.
    AudioClip RoundCall(GameSim sim, int round)
    {
        if (round == 1) return round1Sound;
        int last = GameSim.RoundsToWin - 1;
        bool final = sim.roundsWon[0] == last && sim.roundsWon[1] == last;
        if (final && finalRoundSound != null) return finalRoundSound;
        return round2Sound;
    }

    // The sound set of the character a fighter slot is playing, or null.
    CharacterSfx SfxOf(int slot)
    {
        GameSim sim = match.Sim;
        if (sim == null || slot < 0 || slot >= sim.fighters.Length || sim.fighters[slot] == null) return null;
        CharacterLook look = CharacterRegistry.GetLook(sim.fighters[slot].characterId);
        return look != null ? look.sfx : null;
    }

    static bool Has(AudioClip[] c) { return c != null && c.Length > 0; }

    // The character's clips if it has any, else the shared ones.
    static AudioClip[] Pick(AudioClip[] own, AudioClip[] shared) { return Has(own) ? own : shared; }

    void OnSimEvent(SimEvent e)
    {
        Shout(e);
        CharacterSfx c = SfxOf(e.a);
        switch (e.type)
        {
            case SimEventType.AttackStart:
            {
                AudioClip[] clips = null;
                GameSim sim = match.Sim;
                if (c != null && sim != null && e.a >= 0 && e.a < sim.fighters.Length && sim.fighters[e.a].move != null)
                    clips = c.FindMove(sim.fighters[e.a].move.name);
                if (!Has(clips) && c != null) clips = c.attackStart;
                PlaySfx(Pick(clips, attackSounds));
                break;
            }
            case SimEventType.Hit:
            {
                bool big = e.amount >= heavyHitDamage;
                AudioClip[] own = null;
                if (c != null) own = big && Has(c.heavyHit) ? c.heavyHit : c.hit;
                bool sharedHeavy = big && Has(heavyHitSounds);
                PlaySfx(Pick(own, sharedHeavy ? heavyHitSounds : hitSounds));
                break;
            }
            case SimEventType.Block:
                PlaySfx(Pick(SfxOf(e.b) != null ? SfxOf(e.b).block : null, blockSounds));
                break;
            case SimEventType.Grab:
            {
                AudioClip[] own = c != null ? (Has(c.grab) ? c.grab : c.hit) : null;
                PlaySfx(Pick(own, Has(grabSounds) ? grabSounds : hitSounds));
                break;
            }
            case SimEventType.Dash:
                PlaySfx(Pick(c != null ? c.dash : null, dashSounds));
                break;
            case SimEventType.SuperFlash:
                PlayUI(c != null && c.superFlash != null ? c.superFlash : superFlashSound);
                break;
            case SimEventType.KO:
                PlayUI(c != null && c.ko != null ? c.ko : koSound);
                break;
            case SimEventType.Jump:
                PlaySfx(Pick(c != null ? c.jump : null, jumpSounds));
                break;
            case SimEventType.RoundStart:
                // The round call and countdown are played by Announce.
                if (lastRoundWinner == 0 || lastRoundWinner == 1) themeSlot = lastRoundWinner;
                lastRoundWinner = -1;
                break;
            case SimEventType.Reflect:
                PlaySfx(Pick(c != null ? c.reflect : null, reflectSounds));
                break;
            case SimEventType.ProjectileDestroyed:
                PlaySfx(Pick(c != null ? c.destroy : null, destroySounds));
                break;
            case SimEventType.Telegraph:
                PlayUI(telegraphSound);
                break;
            case SimEventType.SummonSpawn:
                PlaySfx(Pick(c != null ? c.summon : null, summonSounds));
                break;
            case SimEventType.Hold:
            {
                AudioClip[] own = c != null ? (Has(c.hold) ? c.hold : c.grab) : null;
                PlaySfx(Pick(own, Has(holdSounds) ? holdSounds : grabSounds));
                break;
            }
            case SimEventType.PowerUp:
                if (e.amount == 1) PlaySfx(Pick(c != null ? c.powerUp : null, powerUpSounds));
                break;
            case SimEventType.OutOfAmmo:
                PlaySfx(Pick(c != null ? c.outOfAmmo : null, outOfAmmoSounds));
                break;
            case SimEventType.Heal:
                PlaySfx(Pick(c != null ? c.heal : null, healSounds));
                break;
        }
    }

    // Winner: 0 or 1 = that slot won, 2 = draw. Vs AI you are always slot 0.
    void PlayResult(int winner)
    {
        if (winner == 2) PlayUI(drawJingle);
        else if (winner == match.LocalSlot) PlayUI(winJingle);
        else PlayUI(loseJingle);
    }

    // ---------- playing clips ----------

    void PlaySfx(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        sfx.pitch = Random.Range(0.95f, 1.05f);
        sfx.PlayOneShot(clip, sfxVolume);
    }

    void PlayUI(AudioClip clip)
    {
        if (clip != null) ui.PlayOneShot(clip, uiVolume);
    }

    AudioClip MusicFor(MatchPhase phase)
    {
        switch (phase)
        {
            case MatchPhase.CharacterSelect:
                return characterSelectMusic != null ? characterSelectMusic : menuMusic;
            case MatchPhase.Playing:
                // Silent once the match is won, so the dialogue and then the jingle stand out.
                return match.Sim.IsOver ? null : FightMusic();
            default:
                return menuMusic;
        }
    }

    // The theme of the fighter in themeSlot, or fightMusic if none is set (or that character has none).
    AudioClip FightMusic()
    {
        CharacterSfx c = themeSlot >= 0 ? SfxOf(themeSlot) : null;
        return c != null && c.theme != null ? c.theme : fightMusic;
    }

    void SetMusic(AudioClip clip)
    {
        if (music.clip == clip) return;
        music.clip = clip;
        if (clip != null) music.Play();
        else music.Stop();
    }
}
