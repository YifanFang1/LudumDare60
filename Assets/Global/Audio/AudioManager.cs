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
    // The round call ("Round 1", "Round 2", "Final Round") plays during the sim's round call phase,
    // after the intro dialogue (round 1) or the pause after the KO (later rounds). The countdown
    // sound plays when the 3-2-1 starts after it.
    [FormerlySerializedAs("roundStart")] public AudioClip round1Sound;
    public AudioClip round2Sound;        // any round that isn't the first or the final one
    public AudioClip finalRoundSound;    // a round that decides the match (empty = round2Sound)
    [FormerlySerializedAs("fightStart")] public AudioClip countdownSound;   // "3, 2, 1, Fight!"
    public AudioClip winJingle;          // from your side: you won
    public AudioClip loseJingle;
    public AudioClip drawJingle;

    public AudioClip matchEndSound;

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
    [Tooltip("Seconds for a song to fade out when the music changes or stops.")]
    public float musicFadeOutSeconds = 0.6f;
    [Tooltip("Seconds for a song to fade in when it starts.")]
    public float musicFadeInSeconds = 1f;
    [Tooltip("The round music starts this many seconds before FIGHT! (during the 3-2-1). 0 = on FIGHT!")]
    public float musicEarlySeconds = 2.5f;
    [Tooltip("Editor only: log each on-screen line that has no voice clip yet, to see which recordings are missing.")]
    public bool logMissingVoiceLines;

    // ---------- player volume settings (the Options page) ----------
    // Separate from the volumes above: those are the team's mix, these are the player's sliders
    // (0-1, multiplied on top). Saved in PlayerPrefs so they're remembered, also in the browser.
    public enum VolumeChannel { Master, Music, Effects, Voices }
    static readonly string[] VolumeKeys = { "volume.master", "volume.music", "volume.effects", "volume.voices" };
    static float[] playerVolumes;

    // A player volume slider's value (1 = full).
    public static float Player(VolumeChannel channel)
    {
        if (playerVolumes == null)
        {
            playerVolumes = new float[VolumeKeys.Length];
            for (int i = 0; i < VolumeKeys.Length; i++) playerVolumes[i] = PlayerPrefs.GetFloat(VolumeKeys[i], 1f);
        }
        return playerVolumes[(int)channel];
    }

    // Changes a slider. Call PlayerPrefs.Save() afterwards (the Options page does when it closes).
    public static void SetPlayer(VolumeChannel channel, float value)
    {
        Player(channel);   // loads the saved values first
        playerVolumes[(int)channel] = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKeys[(int)channel], playerVolumes[(int)channel]);
        if (channel == VolumeChannel.Master) AudioListener.volume = playerVolumes[(int)channel];
    }

    // So menus can play click sounds without needing a reference (or this component at all).
    static AudioManager instance;

    IMatchSession match;
    AudioSource sfx;      // fight sounds, slightly random pitch
    AudioSource ui;       // clicks and jingles, normal pitch
    AudioSource music;
    float musicFade;      // 0-1, multiplies musicVolume while a song fades in or out
    bool resultPlayed;    // the win/lose jingle for this match has played
    // Character themes: round 1 plays fightMusic, then each new round plays the theme of whoever
    // won the round before (a drawn round keeps the current music).
    int themeSlot = -1;          // fighter slot whose theme is playing, -1 = fightMusic
    int lastRoundWinner = -1;    // remembered during the pause after a KO, used when the next round starts
    int calledRound;             // the last round whose round call has played
    int countedRound;            // the last round whose countdown sound has played
    // Character voices: one source per fighter, so a new line cuts off that fighter's last one.
    readonly AudioSource[] voices = new AudioSource[2];
    AudioSource selectVoice;     // character select lines; kept separate so starting the match doesn't cut them
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

        AudioListener.volume = Player(VolumeChannel.Master);   // the player's Master slider
        sfx = AddSource(false);
        ui = AddSource(false);
        music = AddSource(true);
        voices[0] = AddSource(false);
        voices[1] = AddSource(false);
        selectVoice = AddSource(false);

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

        SetMusic(MusicFor(match.Phase));   // also sets the music volume (with the fade)
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

    // A KO'd fighter starts burning away (DefeatSequence): their KO sound, or the shared one.
    public static void Defeated(int slot)
    {
        if (instance == null) return;
        CharacterSfx c = instance.SfxOf(slot);
        instance.PlayUI(c != null && c.ko != null ? c.ko : instance.koSound);
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
        if (cinematic == null || !cinematic.IntroActive) return;   // the end-of-match box is never voiced

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

    // Character select: the character you click says one of their Select lines (always, however
    // few there are, since it's the feedback for your click). Called by CharacterSelectScreen.
    public static void CharacterPicked(int id)
    {
        if (instance != null) {
            instance.SpeakSelect(id);
            CharacterLook look = CharacterRegistry.GetLook(id);
            CharacterSfx sfx = look != null ? look.sfx : null;
            // 2. Assign the character's theme clip if available
            if (sfx != null && sfx.theme != null)
            {
                instance.characterSelectMusic = sfx.theme;
            }
            else
            {
                // Fallback to default menu music if no theme exists
                instance.characterSelectMusic = null; 
            }
        }
    }

    void SpeakSelect(int id)
    {
        CharacterLook look = CharacterRegistry.GetLook(id);
        CharacterVoice v = look != null ? look.voice : null;
        selectVoice.Stop();
        if (v == null || !Has(v.select)) return;
        AudioClip clip = v.select[Random.Range(0, v.select.Length)];
        if (clip == null) return;
        selectVoice.clip = clip;
        selectVoice.volume = voiceVolume * Player(VolumeChannel.Voices) * v.volume;
        selectVoice.Play();
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
                // Only your own character speaks, and only between rounds: nothing on the match
                // win or loss, and nothing on a drawn round (e.a == 2).
                int me = match.LocalSlot;
                CharacterVoice v = VoiceOf(me);
                if (v == null || sim.IsOver || e.a == 2) break;
                PlayVoice(me, e.a == me ? v.roundWin : v.roundLose, true);   // always, however few clips
                break;
            }
        }
    }

    // A random shout from a list. Short lists don't play every time, so the same one or two clips
    // don't get repetitive: 1 clip = 1/3 of the time, 2 clips = 2/3, 3 or more = always.
    // always = skip that rule (round win/lose lines).
    void PlayVoice(int slot, AudioClip[] clips, bool always = false)
    {
        if (!Has(clips)) return;
        if (!always && clips.Length < 3 && Random.value >= clips.Length / 3f) return;
        PlayVoice(slot, clips[Random.Range(0, clips.Length)]);
    }

    void PlayVoice(int slot, AudioClip clip)
    {
        if (clip == null || slot < 0 || slot > 1) return;
        AudioSource s = voices[slot];
        s.Stop();
        s.clip = clip;
        CharacterVoice v = VoiceOf(slot);
        s.volume = voiceVolume * Player(VolumeChannel.Voices) * (v != null ? v.volume : 1f);
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

        // Order: intros (dialogue) -> round call ("ROUND 1", the sim's round call phase) -> 3-2-1.
        bool waiting = sim.introTicks > 0 || sim.roundOverTicks > 0;
        if (waiting) return;

        if (calledRound < sim.round && (sim.roundCallTicks > 0 || sim.countdownTicks > 0))
        {
            calledRound = sim.round;
            PlayUI(RoundCall(sim, sim.round));
        }

        if (countedRound < sim.round && sim.roundCallTicks == 0 && sim.countdownTicks > 0)
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
                // The KO sound plays when the fighter actually burns away, not on the hit:
                // DefeatSequence calls Defeated() so the sound and the effect start together.
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

        if (matchEndSound != null)
        {
            PlayUI(matchEndSound);

        }

        if (winner == 2) PlayUI(drawJingle);
        else if (winner == match.LocalSlot) PlayUI(winJingle);
        else PlayUI(loseJingle);

        //PlaySfx()
    }

    // ---------- playing clips ----------

    void PlaySfx(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        sfx.pitch = Random.Range(0.95f, 1.05f);
        sfx.PlayOneShot(clip, sfxVolume * Player(VolumeChannel.Effects));
    }

    void PlayUI(AudioClip clip)
    {
        if (clip != null) ui.PlayOneShot(clip, uiVolume * Player(VolumeChannel.Effects));
    }

    AudioClip MusicFor(MatchPhase phase)
    {
        switch (phase)
        {
            case MatchPhase.CharacterSelect:
                return characterSelectMusic != null ? characterSelectMusic : menuMusic;
            case MatchPhase.Playing:
                // Silent during the intros and once the match is won, so the dialogue and then the
                // jingle stand out. A character theme (rounds 2-3) starts with the round call; the
                // default battle theme waits for most of the 3-2-1 (Music Early Seconds before FIGHT!).
                GameSim sim = match.Sim;
                if (sim.IsOver || sim.introTicks > 0) return null;
                AudioClip song = FightMusic();
                if (song != fightMusic) return song;
                int earlyTicks = Mathf.RoundToInt(musicEarlySeconds * 60f);
                if (sim.roundCallTicks > 0 || sim.countdownTicks > earlyTicks) return null;
                return song;
            default:
                instance.characterSelectMusic = null;
                return menuMusic;
        }
    }

    // The theme of the fighter in themeSlot, or fightMusic if none is set (or that character has none).
    AudioClip FightMusic()
    {
        CharacterSfx c = themeSlot >= 0 ? SfxOf(themeSlot) : null;
        return c != null && c.theme != null ? c.theme : fightMusic;
    }

    // Switches songs with a fade: the old song fades out, then the new one starts and fades in.
    // If the wanted song changes back mid-fade, the current one just fades back up.
    void SetMusic(AudioClip clip)
    {
        float dt = Time.unscaledDeltaTime;
        if (music.clip != clip)
        {
            if (music.clip != null && music.isPlaying && musicFade > 0f)
            {
                musicFade = Mathf.MoveTowards(musicFade, 0f, musicFadeOutSeconds > 0f ? dt / musicFadeOutSeconds : 1f);
            }
            else
            {
                music.clip = clip;
                musicFade = 0f;
                if (clip != null) music.Play();
                else music.Stop();
            };
        }
        else if (clip != null)
        {
            musicFade = Mathf.MoveTowards(musicFade, 1f, musicFadeInSeconds > 0f ? dt / musicFadeInSeconds : 1f);
        }
        music.volume = musicVolume * Player(VolumeChannel.Music) * musicFade;
    }
}
