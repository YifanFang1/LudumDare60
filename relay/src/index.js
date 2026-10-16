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
    this.sockets = [];
    this.version = null;
  }

  async fetch(request) {
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
