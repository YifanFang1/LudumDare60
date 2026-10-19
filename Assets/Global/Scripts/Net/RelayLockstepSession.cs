using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// ONLINE PLAY through the Cloudflare relay with deterministic lockstep.
// Everything network-specific lives in this folder (Net/) and nowhere else:
//   NetClient            the websocket
//   RemoteInputSource    the other player's inputs
//   this file            rooms, start message, desync detection
//
// To replace networking: write another MatchSessionBase subclass and swap the component.
// To remove networking: delete this folder and use LocalMatchSession.
public class RelayLockstepSession : MatchSessionBase
{
    // Bump this whenever the sim or the message format changes. The hash of the inspector-made
    // character data is added automatically, so changed characters are refused too.
    // Players on different versions are refused by the relay instead of silently desyncing.
    // 6: player names ("n," message). 7: the shared kit (grab, dash, meter, Special/Ultimate,
    // rounds, pushboxes, new input bits). Both were "6" on separate branches, so the merge is 7:
    // a build with only one of them must not match a build with both. 8: hit stop.
    // 10: two different changes, both called "10" on separate branches: the roster designs
    // (Design/ROSTER_PLAN.md: cooldowns, hold input, status effects, zones, Ultimate copies, held
    // grabs, the seven characters' movesets) and the air dash + Dash button. 11: the merge of both,
    // so a build with only one of them can never match a build with both.
    // 12: rematch ("r" message) and keepalive ("p").
    // 13: synchronized pre-round countdown.
    // 15: synchronized two-character introduction before the countdown.
    // 16: round timer is 80 seconds.
    // 17: ordered matchup dialogue with synchronized reading time.
    // 18: lob rat, back-walk speed while blocking, held grabs, K3N triple-jab Special.
    // 19: Room's Ultimate grows him 1.5x with hyper armor (no more copies).
    // 22: completed matchup intro sheet, including replies and mirror-match timing.
    // 20: Remy's Ultimate waits 1 s, then showers rats. 24: it is uninterruptable (hyper armor).
    // 25: K3N's Light is slower to start and reaches further (spawn 8, 4 active, 1600x500 box). 26: K3N's Charged Punch is slower and long (spawn 20, 7 active, 4000x500 box). 27: K3N's Grab is slower (spawn 17, 10 active, box 400,700,700,1000). 28: K3N's Special has three retimed hitboxes. 30: Ruy's Hadoken spawns higher (box y 1300). 31: K3N's Install Special has five hitboxes, and every Install attack drains 150 of the bar. 32: Dr. Remy takes no damage during his Ultimate. 33: Dr. Remy has 100 HP and a 0.5 s Ultimate delay; K3N's Light, Charged Punch and Install damage are lower; Room gains no meter while grown. 34: Dr. Remy's rats start after 15 ticks. 35: Paintress's back dash slows herself 50% for 2 s, her dash cooldown is 0.5 s instead of costing HP, and she hits harder (Light 6, Heavy 13, Grab 12, Splat 6); Room has 150 HP.
    const string Version = "35";

    public string relayUrl = "ws://localhost:8787";   // used in the Editor and for local testing

    // The deployed relay, e.g. wss://ld-relay.yourname.workers.dev. Builds (itch) use this one
    // when it is not empty, so the localhost address above never reaches the browser.
    public string releaseRelayUrl = "";

    // Download the character data published on the server when the game starts, so everyone
    // plays the same characters and balance changes need no new build. In the Editor this is off
    // by default so your own sliders and assets are what you test.
    public bool fetchServerCharacters = true;
    public bool fetchInEditor = false;
    public bool CharactersLoading { get; private set; }

    // Waiting in a Quick match room for longer than this puts you back in the queue (a fresh room, or
    // someone else's), so a room the server has forgotten about can never strand you.
    public float quickRefreshSeconds = 20f;
    const float KeepAliveSeconds = 15f;

    public override bool SupportsRooms { get { return true; } }
    public override string ServerAddress { get { return relayUrl; } set { relayUrl = value; } }

    NetClient net;
    Dictionary<int, int> myHashes = new Dictionary<int, int>();
    Dictionary<int, int> theirHashes = new Dictionary<int, int>();

    // From the relay's start message, kept until both players have picked a character.
    int pendingSlot, pendingSeed, pendingDelay;
    int myPick, theirPick;
    string theirName = "";
    bool haveTheirPick;

    protected override void Awake()
    {
        base.Awake();
        if (!Application.isEditor && !string.IsNullOrEmpty(releaseRelayUrl)) relayUrl = releaseRelayUrl;
        if (fetchServerCharacters && (!Application.isEditor || fetchInEditor)) StartCoroutine(LoadServerCharacters());
    }

    // ws://host -> http://host, wss://host -> https://host
    static string HttpBase(string wsUrl)
    {
        string url = wsUrl.TrimEnd('/');
        if (url.StartsWith("wss://")) return "https://" + url.Substring(6);
        if (url.StartsWith("ws://")) return "http://" + url.Substring(5);
        return url;
    }

    IEnumerator LoadServerCharacters()
    {
        CharactersLoading = true;
        Status = "Loading characters...";

        using (UnityWebRequest request = UnityWebRequest.Get(HttpBase(relayUrl) + "/characters.json"))
        {
            yield return request.SendWebRequest();

            CharacterDef[] defs;
            string error;
            if (request.result != UnityWebRequest.Result.Success)
            {
                Status = "Could not load characters from the server. Using the ones in this build.";
            }
            else if (!CharacterJson.TryParse(request.downloadHandler.text, out defs, out error))
            {
                Status = "Server character data is invalid: " + error;
                Debug.LogError(Status);
            }
            else
            {
                // An empty file means nothing was published yet: keep the characters in this build.
                if (defs.Length > 0) CharacterRegistry.ApplyServerData(defs);
                Status = "";
            }
        }

        CharactersLoading = false;
    }

    float waitingSince, lastPing;
    bool refreshing;

    protected override void Update()
    {
        if (net != null) net.Tick();
        base.Update();

        if (Phase == MatchPhase.Waiting && net != null)
        {
            if (Time.unscaledTime - lastPing > KeepAliveSeconds)
            {
                lastPing = Time.unscaledTime;
                net.Send("p");
            }
            if (IsQuickMatch && !refreshing && Time.unscaledTime - waitingSince > quickRefreshSeconds) RefreshQuickMatch();
        }
    }

    // ---------- commands ----------

    // Joins a waiting public room, or hosts a new public one if nobody is waiting.
    public override void QuickMatch()
    {
        if (CharactersLoading) { Status = "Still loading characters, try again in a moment"; return; }
        if (quickBusy) return;
        StartCoroutine(QuickMatchRoutine());
    }

    bool quickBusy;

    // Still waiting in a Quick match room: drop it and queue again without going back to the menu.
    void RefreshQuickMatch()
    {
        if (refreshing || quickBusy) return;
        StartCoroutine(RefreshQuickRoutine());
    }

    IEnumerator RefreshQuickRoutine()
    {
        refreshing = true;
        CloseNet();                        // the old room empties and drops out of the queue
        Status = "Finding a match...";
        yield return new WaitForSecondsRealtime(1f);   // also stops a flapping connection from looping instantly

        if (Phase == MatchPhase.Waiting)   // the player may have cancelled meanwhile
        {
            using (UnityWebRequest request = UnityWebRequest.Get(HttpBase(relayUrl) + "/quick"))
            {
                yield return request.SendWebRequest();

                string code = request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text.Trim().ToUpper() : "";
                if (Phase == MatchPhase.Waiting)
                {
                    if (code.Length != 4)
                    {
                        Leave();
                        Status = "Could not reach the server for quick match";
                    }
                    else
                    {
                        StartOnline(code, true);
                        IsQuickMatch = true;
                    }
                }
            }
        }
        refreshing = false;
    }

    IEnumerator QuickMatchRoutine()
    {
        quickBusy = true;
        Status = "Finding a match...";

        using (UnityWebRequest request = UnityWebRequest.Get(HttpBase(relayUrl) + "/quick"))
        {
            yield return request.SendWebRequest();

            string code = request.result == UnityWebRequest.Result.Success ? request.downloadHandler.text.Trim().ToUpper() : "";
            if (code.Length != 4)
            {
                Status = "Could not reach the server for quick match";
            }
            else if (Phase == MatchPhase.Lobby)   // the player may have started something else meanwhile
            {
                StartOnline(code, true);   // quick match rooms are always public
                IsQuickMatch = true;       // after StartOnline, which clears the previous room
            }
        }

        quickBusy = false;
    }

    public override void CreateRoom()
    {
        if (CharactersLoading) { Status = "Still loading characters, try again in a moment"; return; }
        StartOnline(RandomCode(), PublicRoom);
        IsQuickMatch = false;
    }

    public override void JoinRoom(string code)
    {
        if (CharactersLoading) { Status = "Still loading characters, try again in a moment"; return; }
        StartOnline(code, false);
        IsQuickMatch = false;       // joining: the room is already public or private, the host decided
    }

    public override void Leave()
    {
        CloseNet();
        StopMatch();
    }

    // ---------- internals ----------

    void StartOnline(string code, bool isPublic)
    {
        if (code.Length != 4)
        {
            Status = "Room code must be 4 letters";
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Browsers refuse plain ws:// from an https page (itch is https). Say so instead of failing silently.
        if (Application.absoluteURL.StartsWith("https://") && !relayUrl.StartsWith("wss://"))
        {
            Status = "Relay address must start with wss:// when played on itch (it is: " + relayUrl + ")";
            return;
        }
#endif

        Leave();
        waitingSince = Time.unscaledTime;
        lastPing = Time.unscaledTime;
        RoomCode = code;
        Status = "Connecting...";
        Phase = MatchPhase.Waiting;   // set BEFORE connecting: a quick failure must be able to send us back to the lobby

        net = new NetClient();
        net.OnOpen = () => { Status = "Connected. Waiting for an opponent..."; };
        net.OnStart = BeginCharacterSelect;
        net.OnCharacter = OnTheirCharacter;
        net.OnName = (name) =>
        {
            theirName = name;
            OpponentName = PlayerNames.Clean(name).Trim();
            if (Phase == MatchPhase.Playing) SetName(1 - LocalSlot, name);
        };
        net.OnRematch = OnTheirRematch;
        net.OnStatus = HandleStatus;
        net.Connect(relayUrl, code, Version + "-" + CharacterList.DataHash(), isPublic);
    }

    // Both players are in: pick characters before the fight starts.
    void BeginCharacterSelect(int slot, int seed, int delay)
    {
        LocalSlot = slot;
        pendingSlot = slot;
        pendingSeed = seed;
        pendingDelay = delay;
        haveTheirPick = false;
        EnterCharacterSelect(false);
        net.Send("n," + LocalName.Trim());   // the relay passes it on; the opponent shows it above your fighter
    }

    public override void ConfirmCharacter()
    {
        if (net == null) { base.ConfirmCharacter(); return; }   // Play vs AI on this component
        if (Phase != MatchPhase.CharacterSelect || CharacterConfirmed) return;

        CharacterConfirmed = true;
        myPick = LocalCharacter;
        net.Send("c," + myPick);
        Status = "Waiting for opponent to pick...";
        TryBeginOnlineMatch();
    }

    // Online both players have to agree. The first to ask waits; when both have asked, both go back to
    // character select with a new seed worked out the same way on each side.
    public override void Rematch()
    {
        if (net == null) { base.Rematch(); return; }
        if (Phase != MatchPhase.Playing || RematchRequested) return;
        RematchRequested = true;
        net.Send("r");
        TryBeginRematch();
    }

    void OnTheirRematch()
    {
        OpponentWantsRematch = true;
        TryBeginRematch();
    }

    void TryBeginRematch()
    {
        if (!RematchRequested || !OpponentWantsRematch) return;
        pendingSeed = (int)(((long)pendingSeed * 1103515245L + 12345L) % 1000000000L);
        haveTheirPick = false;
        BackToCharacterSelect(false);
    }

    void OnTheirCharacter(int id)
    {
        theirPick = id;
        haveTheirPick = true;
        if (!CharacterConfirmed) Status = "Opponent is ready";
        TryBeginOnlineMatch();
    }

    void TryBeginOnlineMatch()
    {
        if (!CharacterConfirmed || !haveTheirPick) return;

        int slot = pendingSlot;
        OpponentCharacter = theirPick;
        myHashes.Clear();
        theirHashes.Clear();
        Status = "";

        KeyboardInputSource local = new KeyboardInputSource();
        RemoteInputSource remote = new RemoteInputSource();
        local.OnSampled = (tick, bits) => net.Send("i," + tick + "," + bits);
        net.OnInput = remote.Receive;
        net.OnHash = OnTheirHash;

        IInputSource first = slot == 0 ? (IInputSource)local : remote;
        IInputSource second = slot == 0 ? (IInputSource)remote : local;

        MatchSetup setup = new MatchSetup();
        // Both machines must build the same fight, so order the picks by slot, not by "mine".
        setup.seed = pendingSeed;
        setup.p0Character = slot == 0 ? myPick : theirPick;
        setup.p1Character = slot == 0 ? theirPick : myPick;
        StartMatch(setup, first, second, pendingDelay, slot, true);
        SetName(1 - slot, theirName);
    }

    void CloseNet()
    {
        if (net != null)
        {
            net.Close();
            net = null;
        }
    }

    void HandleStatus(string status)
    {
        // The connection dropped while waiting for a Quick match: queue again instead of giving up.
        if (Phase == MatchPhase.Waiting && IsQuickMatch && !refreshing && (status == "closed" || status.StartsWith("error")))
        {
            RefreshQuickMatch();
            return;
        }

        string text;
        if (status == "full") text = "Room is full";
        else if (status == "badversion") text = "Other player has a different version. Refresh the page.";
        else if (status == "left") text = "Opponent left";
        else text = status;

        Leave();
        Status = text;
    }

    protected override void OnTickDone(int tick)
    {
        base.OnTickDone(tick);   // raises the sim events for juice and audio

        // Desync detection: compare a hash of the state every 30 ticks.
        if (net == null || tick % 30 != 0) return;
        int hash = sim.Hash();
        myHashes[tick] = hash;
        net.Send("h," + tick + "," + hash);
        CompareHashes(tick);
    }

    void OnTheirHash(int tick, int hash)
    {
        theirHashes[tick] = hash;
        CompareHashes(tick);
    }

    void CompareHashes(int tick)
    {
        int mine;
        int theirs;
        if (myHashes.TryGetValue(tick, out mine) && theirHashes.TryGetValue(tick, out theirs) && mine != theirs)
        {
            Warning = "DESYNC DETECTED (see console)";
            Debug.LogError("DESYNC at tick " + tick + ": mine " + mine + " theirs " + theirs);
        }
    }

    static string RandomCode()
    {
        const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // no I or O, they look like 1 and 0
        char[] code = new char[4];
        for (int i = 0; i < 4; i++)
        {
            code[i] = letters[Random.Range(0, letters.Length)];
        }
        return new string(code);
    }
}
