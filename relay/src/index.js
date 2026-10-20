// Tiny relay for 1v1 lockstep games.
// One Durable Object per room code. It holds up to 2 sockets and forwards
// every message from one to the other. No game logic lives here.
//
// Messages the relay SENDS:
//   start,<slot>,<seed>,<delay>   both players are in; slot is 0 or 1
//   full                          room already has 2 players
//   badversion                    other player has a different build version
//   left                          the other player disconnected
// Everything else a client sends is passed to the other client untouched.
//
// Also serves GET /characters.json: the shared character data every client downloads at startup.
// And GET /quick: quick match. Returns the code of a waiting PUBLIC room, or a fresh room code if
// none is waiting (the caller then hosts it). Connect with &pub=1 to be in the pool; rooms without
// it are private and only reachable by code.

// The character data exported from Unity (menu: Fight > Export Characters to Server File).
// It is bundled into the worker when you run `npm run deploy` and served at /characters.json.
import characters from "../characters.json";

const INPUT_DELAY_TICKS = 3;

export default {
  async fetch(request, env) {
    if (new URL(request.url).pathname === "/characters.json") {
      return new Response(JSON.stringify(characters), {
        headers: {
          "content-type": "application/json",
          "access-control-allow-origin": "*", // the itch page is on another domain
          "cache-control": "no-cache",
        },
      });
    }
    if (new URL(request.url).pathname === "/quick") {
      const code = await env.MATCHMAKER.get(env.MATCHMAKER.idFromName("global")).fetch("https://matchmaker/pick").then((r) => r.text());
      return new Response(code, {
        headers: { "content-type": "text/plain", "access-control-allow-origin": "*", "cache-control": "no-cache" },
      });
    }
    if (request.headers.get("Upgrade") !== "websocket") {
      return new Response("ld relay ok");
    }
    // URL looks like  /room/ABCD?v=1
    const code = new URL(request.url).pathname.split("/").pop().toUpperCase();
    if (code.length === 0) {
      return new Response("missing room code", { status: 400 });
    }
    const room = env.ROOMS.get(env.ROOMS.idFromName(code));
    return room.fetch(request);
  },
};

export class Room {
  constructor(state, env) {
    this.env = env;
    this.sockets = [];
    this.version = null;
  }

  async fetch(request) {
    // Plain HTTP (not a websocket): the matchmaker asks how many players are inside.
    if (request.headers.get("Upgrade") !== "websocket") {
      return new Response(String(this.sockets.length));
    }
    const version = new URL(request.url).searchParams.get("v") || "0";

    const pair = new WebSocketPair();
    const client = pair[0];
    const server = pair[1];
    server.accept();

    if (this.sockets.length >= 2) {
      server.send("full");
      server.close(1000, "full");
    } else if (this.version !== null && this.version !== version) {
      server.send("badversion");
      server.close(1000, "badversion");
    } else {
      this.version = version;
      this.sockets.push(server);
      // The first player of a public room puts it in the quick match pool.
      if (this.sockets.length === 1 && new URL(request.url).searchParams.get("pub") === "1") {
        const code = new URL(request.url).pathname.split("/").pop().toUpperCase();
        this.env.MATCHMAKER.get(this.env.MATCHMAKER.idFromName("global")).fetch("https://matchmaker/add?code=" + code).catch(() => {});
      }
      server.addEventListener("message", (e) => this.forward(server, e.data));
      server.addEventListener("close", () => this.leave(server));
      server.addEventListener("error", () => this.leave(server));
      if (this.sockets.length === 2) this.start();
    }

    return new Response(null, { status: 101, webSocket: client });
  }

  start() {
    const seed = Math.floor(Math.random() * 1000000000);
    this.sockets[0].send("start,0," + seed + "," + INPUT_DELAY_TICKS);
    this.sockets[1].send("start,1," + seed + "," + INPUT_DELAY_TICKS);
  }

  forward(from, data) {
    const other = this.sockets.find((s) => s !== from);
    if (other) other.send(data);
  }

  leave(socket) {
    const i = this.sockets.indexOf(socket);
    if (i === -1) return; // already handled
    const other = this.sockets.find((s) => s !== socket);
    this.sockets = [];
    this.version = null;
    if (other) {
      other.send("left");
      other.close(1000, "left");
    }
  }
}

// Quick match. One global instance keeps the pool of PUBLIC rooms that have one player waiting.
// A room joins the pool when its first player connects with &pub=1 (Quick match and public Create room).
// Private rooms never touch it. Before handing a room out the matchmaker asks the room how many
// players are inside, so players who already left are never matched.
const LETTERS = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // same alphabet as the client: no I or O

export class Matchmaker {
  constructor(state, env) {
    this.env = env;
    this.state = state;
    this.pool = new Map(); // code -> time it was added
    this.queue = Promise.resolve(); // one request at a time, so two callers never grab the same room
    // The pool is kept in storage too: if Cloudflare restarts this object while a host is waiting,
    // the waiting room is still found instead of being forgotten.
    state.blockConcurrencyWhile(async () => {
      const saved = await state.storage.get("pool");
      if (saved) this.pool = new Map(saved);
    });
  }

  save() {
    return this.state.storage.put("pool", [...this.pool.entries()]);
  }

  fetch(request) {
    const url = new URL(request.url);
    const job = url.pathname === "/add" ? () => this.add(url.searchParams.get("code")) : () => this.pick();
    const result = this.queue.then(job);
    this.queue = result.catch(() => {});
    return result.then((text) => new Response(text));
  }

  async add(code) {
    if (code) this.pool.set(code, Date.now());
    await this.save();
    return "ok";
  }

  async playersIn(code) {
    const room = this.env.ROOMS.get(this.env.ROOMS.idFromName(code));
    const count = await room.fetch("https://room/count").then((r) => r.text());
    return parseInt(count, 10);
  }

  // Oldest waiting public room, otherwise a brand new one (the caller becomes the host).
  async pick() {
    for (const [code, since] of [...this.pool.entries()]) {
      this.pool.delete(code);
      const inside = await this.playersIn(code);
      // 1 = a host is waiting. 0 = the host is still connecting (give it 10 seconds).
      if (inside === 1 || (inside === 0 && Date.now() - since < 10000)) {
        await this.save();
        return code;
      }
    }
    let code = "";
    for (let i = 0; i < 4; i++) code += LETTERS[Math.floor(Math.random() * LETTERS.length)];
    this.pool.set(code, Date.now()); // so the next quick player finds this room
    await this.save();
    return code;
  }
}
