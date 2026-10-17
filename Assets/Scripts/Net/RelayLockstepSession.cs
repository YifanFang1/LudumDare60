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
    const string Version = "3";

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

    public override bool SupportsRooms { get { return true; } }
    public override string ServerAddress { get { return relayUrl; } set { relayUrl = value; } }

    NetClient net;
    Dictionary<int, int> myHashes = new Dictionary<int, int>();
    Dictionary<int, int> theirHashes = new Dictionary<int, int>();

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

    protected override void Update()
    {
        if (net != null) net.Tick();
        base.Update();
    }

    // ---------- commands ----------

    public override void CreateRoom()
    {
        if (CharactersLoading) { Status = "Still loading characters, try again in a moment"; return; }
        StartOnline(RandomCode());
    }

    public override void JoinRoom(string code)
    {
        if (CharactersLoading) { Status = "Still loading characters, try again in a moment"; return; }
        StartOnline(code);
    }

    public override void Leave()
    {
        CloseNet();
        StopMatch();
    }

    // ---------- internals ----------

    void StartOnline(string code)
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
        RoomCode = code;
        Status = "Connecting...";
        Phase = MatchPhase.Waiting;   // set BEFORE connecting: a quick failure must be able to send us back to the lobby

        net = new NetClient();
        net.OnOpen = () => { Status = "Connected. Waiting for an opponent..."; };
        net.OnStart = BeginOnlineMatch;
        net.OnStatus = HandleStatus;
        net.Connect(relayUrl, code, Version + "-" + CharacterList.DataHash());
    }

    void BeginOnlineMatch(int slot, int seed, int delay)
    {
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
        setup.seed = seed;   // character ids are not exchanged yet: both players are Brawler (id 0)
        StartMatch(setup, first, second, delay, slot, true);
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
