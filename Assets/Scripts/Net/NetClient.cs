using System;
using System.Text;
using NativeWebSocket;   // Package Manager > Add package from git URL:
                         //   https://github.com/endel/NativeWebSocket.git#upm

// Talks to the relay. Text messages only:
//   we send:     i,<tick>,<bits>    input for a tick
//                h,<tick>,<hash>    state hash, to detect desyncs
//   we receive:  start,<slot>,<seed>,<delay>   both players are in
//                i,... and h,...    forwarded from the other player
//                full / badversion / left      status messages
public class NetClient
{
    WebSocket ws;

    public Action OnOpen;                   // the websocket connected
    public Action<int, int, int> OnStart;   // slot, seed, delay
    public Action<int, int> OnInput;        // tick, bits   (other player's)
    public Action<int, int> OnHash;         // tick, hash   (other player's)
    public Action<string> OnStatus;         // "full", "badversion", "left", "closed", "error: ..."

    public async void Connect(string baseUrl, string code, string version, bool isPublic = false)
    {
        try
        {
            ws = new WebSocket(baseUrl + "/room/" + code + "?v=" + version + (isPublic ? "&pub=1" : ""));
            ws.OnOpen += () => { if (OnOpen != null) OnOpen(); };
            ws.OnMessage += HandleMessage;
            ws.OnError += (e) => { if (OnStatus != null) OnStatus("error: " + e); };
            ws.OnClose += (c) => { if (OnStatus != null) OnStatus("closed"); };
            await ws.Connect();   // finishes when the connection closes
        }
        catch (Exception e)
        {
            if (OnStatus != null) OnStatus("error: " + e.Message);
        }
    }

    // Call every frame. Needed on desktop/editor so messages are delivered on the main thread.
    public void Tick()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws != null) ws.DispatchMessageQueue();
#endif
    }

    public void Send(string message)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            ws.SendText(message);
        }
    }

    public void Close()
    {
        OnOpen = null;
        OnStart = null;
        OnInput = null;
        OnHash = null;
        OnStatus = null;
        if (ws != null)
        {
            WebSocket old = ws;
            ws = null;
            // In the browser build Close() throws if the socket is already closed (for example
            // right after a failed connection). That must not stop the caller from cleaning up.
            try { old.Close(); }
            catch (Exception) { }
        }
    }

    void HandleMessage(byte[] bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);
        string[] p = text.Split(',');

        if (p[0] == "start" && p.Length >= 4)
        {
            if (OnStart != null) OnStart(int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
        }
        else if (p[0] == "i" && p.Length >= 3)
        {
            if (OnInput != null) OnInput(int.Parse(p[1]), int.Parse(p[2]));
        }
        else if (p[0] == "h" && p.Length >= 3)
        {
            if (OnHash != null) OnHash(int.Parse(p[1]), int.Parse(p[2]));
        }
        else
        {
            if (OnStatus != null) OnStatus(text);
        }
    }
}
