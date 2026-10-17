using UnityEngine;

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
    public AudioClip superFlashSound;    // an Ultimate starts (the freeze)
    public AudioClip koSound;
    public int heavyHitDamage = 10;      // Brawler: Light does 5, Heavy does 12

    [Header("Match")]
    public AudioClip fightStart;         // "Ready... Fight!" when a match begins
    public AudioClip roundStart;         // when round 2 or 3 begins (empty = use fightStart)
    public AudioClip winJingle;          // from your side: you won
    public AudioClip loseJingle;
    public AudioClip drawJingle;

    [Header("Menu")]
    public AudioClip buttonClick;
    public AudioClip confirm;            // Ready / Fight! on character select

    [Header("Music")]
    public AudioClip menuMusic;          // lobby and waiting for an opponent
    public AudioClip characterSelectMusic;   // empty = keep the menu music playing
    public AudioClip fightMusic;

    [Header("Volume")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 0.8f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    // So menus can play click sounds without needing a reference (or this component at all).
    static AudioManager instance;

    IMatchSession match;
    AudioSource sfx;      // fight sounds, slightly random pitch
    AudioSource ui;       // clicks and jingles, normal pitch
    AudioSource music;
    bool resultPlayed;    // the win/lose jingle for this match has played; the fight music stops

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
        if (match.Phase == MatchPhase.Playing && match.Sim.IsOver && !resultPlayed)
        {
            resultPlayed = true;
            PlayResult(match.Sim.Winner);
        }

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

    // ---------- fight ----------

    void OnMatchStarted(GameSim sim)
    {
        resultPlayed = false;
        PlayUI(fightStart);
    }

    void OnSimEvent(SimEvent e)
    {
        switch (e.type)
        {
            case SimEventType.AttackStart:
                PlaySfx(attackSounds);
                break;
            case SimEventType.Hit:
                bool heavy = e.amount >= heavyHitDamage && heavyHitSounds != null && heavyHitSounds.Length > 0;
                PlaySfx(heavy ? heavyHitSounds : hitSounds);
                break;
            case SimEventType.Block:
                PlaySfx(blockSounds);
                break;
            case SimEventType.Grab:
                PlaySfx(grabSounds != null && grabSounds.Length > 0 ? grabSounds : hitSounds);
                break;
            case SimEventType.Dash:
                PlaySfx(dashSounds);
                break;
            case SimEventType.SuperFlash:
                PlayUI(superFlashSound);
                break;
            case SimEventType.KO:
                PlayUI(koSound);
                break;
            case SimEventType.RoundStart:
                PlayUI(roundStart != null ? roundStart : fightStart);
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
                return resultPlayed ? null : fightMusic;   // silence so the jingle stands out
            default:
                return menuMusic;
        }
    }

    void SetMusic(AudioClip clip)
    {
        if (music.clip == clip) return;
        music.clip = clip;
        if (clip != null) music.Play();
        else music.Stop();
    }
}
