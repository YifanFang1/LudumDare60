// Smoke test for the relay. Start it first with `npm run dev`, then `npm test`.
import WebSocket from "ws";

const BASE = process.env.RELAY_URL || "ws://localhost:8787";
let failed = 0;

function check(name, ok) {
  console.log((ok ? "PASS  " : "FAIL  ") + name);
  if (!ok) failed++;
}

function connect(code, version = "1", pub = false) {
  const ws = new WebSocket(`${BASE}/room/${code}?v=${version}${pub ? "&pub=1" : ""}`);
  const msgs = [];
  const waiters = [];
  ws.on("message", (data) => {
    const text = data.toString();
    msgs.push(text);
    for (const w of waiters.slice()) w();
  });
  const waitFor = (prefix, ms = 3000) =>
    new Promise((resolve) => {
      const timer = setTimeout(() => resolve(null), ms);
      const look = () => {
        const hit = msgs.find((m) => m.startsWith(prefix));
        if (hit) {
          clearTimeout(timer);
          resolve(hit);
        }
      };
      waiters.push(look);
      look();
    });
  const open = new Promise((resolve) => ws.on("open", resolve));
  return { ws, msgs, waitFor, open };
}

// character data endpoint
{
  const res = await fetch(BASE.replace(/^ws/, "http") + "/characters.json");
  const body = await res.json();
  check("GET /characters.json returns a bundle", res.status === 200 && body.format === 1 && Array.isArray(body.characters));
  check("characters.json allows cross-origin reads (itch page)", res.headers.get("access-control-allow-origin") === "*");
}

// quick match + public/private rooms
{
  const http = BASE.replace(/^ws/, "http");
  const get = async () => {
    const r = await fetch(http + "/quick");
    return { code: (await r.text()).trim(), cors: r.headers.get("access-control-allow-origin") };
  };
  const pause = (ms) => new Promise((r) => setTimeout(r, ms));

  // nobody waiting -> a fresh code, and the caller hosts it
  const qa = await get();
  check("quick: returns a 4-letter code with CORS", /^[A-Z]{4}$/.test(qa.code) && qa.cors === "*");
  const hostA = connect(qa.code, "1", true);
  await hostA.open;

  // a private room (no pub flag) must never be handed out
  const privCode = "P" + Math.floor(Math.random() * 1000);
  const priv = connect(privCode);
  await priv.open;
  await pause(100);

  const qb = await get();
  check("quick: joins the waiting public room, not the private one", qb.code === qa.code && qb.code !== privCode);
  const joinB = connect(qb.code, "1", true);
  await joinB.open;
  check("quick: both players start", (await hostA.waitFor("start")) !== null && (await joinB.waitFor("start")) !== null);

  // a public room made with Create room (pub=1) is found by quick match
  const pubCode = "U" + Math.floor(Math.random() * 1000);
  const pub = connect(pubCode, "1", true);
  await pub.open;
  await pause(100);
  const qc = await get();
  check("quick: finds a public room made by Create room", qc.code === pubCode);

  // a full room is skipped; a room whose host left is skipped
  const qd = await get();
  check("quick: empty pool gives a new room", qd.code !== pubCode && qd.code !== qa.code);

  for (const x of [hostA, joinB, priv, pub]) x.ws.close();
}

const code = "T" + Math.floor(Math.random() * 1000);

const a = connect(code);
await a.open;
const b = connect(code);
await b.open;

const startA = await a.waitFor("start");
const startB = await b.waitFor("start");
check("both players get start", startA !== null && startB !== null);
if (startA && startB) {
  const pa = startA.split(",");
  const pb = startB.split(",");
  check("slots are 0 and 1", pa[1] === "0" && pb[1] === "1");
  check("same seed", pa[2] === pb[2]);
  check("delay is sent", pa[3] === "3");
}

a.ws.send("i,5,16");
check("input forwarded A -> B", (await b.waitFor("i,5,16")) !== null);
b.ws.send("h,30,12345");
check("hash forwarded B -> A", (await a.waitFor("h,30,12345")) !== null);

const c = connect(code);
await c.open;
check("third player gets full", (await c.waitFor("full")) !== null);

a.ws.close();
check("B is told the other player left", (await b.waitFor("left")) !== null);

// A fresh room must work again after both left.
const code2 = "V" + Math.floor(Math.random() * 1000);
const d = connect(code2, "1");
await d.open;
const e = connect(code2, "2");
await e.open;
check("version mismatch is rejected", (await e.waitFor("badversion")) !== null);

for (const x of [a, b, c, d, e]) x.ws.close();
console.log(failed === 0 ? "\nALL PASSED" : `\n${failed} FAILED`);
process.exit(failed === 0 ? 0 : 1);
