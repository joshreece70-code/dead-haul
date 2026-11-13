// Dead Haul multiplayer server for Cloudflare Workers.
// Static files in ./public are served by the assets binding. /room/<code> opens a WebSocket to one Durable Object per lobby code.
// The room keeps nothing on disk: it holds each connected player's latest state and relays it to everyone else in the room.

const MAX_PLAYERS = 16;
const MAX_MESSAGE_BYTES = 6000;
const FLUSH_MS = 60;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const m = url.pathname.match(/^\/room\/([a-z0-9][a-z0-9_.-]{0,47})$/);
    if (m) return env.ROOMS.get(env.ROOMS.idFromName(m[1])).fetch(request);
    if (url.pathname.startsWith('/room/')) return new Response('bad room name', { status: 400 });
    return env.ASSETS.fetch(request);
  },
};

export class Room {
  constructor(state, env) {
    this.peers = new Map(); // id -> { ws, presence }
    this.dirty = false;
    this.timer = null;
    this.nextId = 1;
  }

  async fetch(request) {
    if ((request.headers.get('Upgrade') || '').toLowerCase() !== 'websocket') {
      return new Response('expected a websocket', { status: 426 });
    }
    if (this.peers.size >= MAX_PLAYERS) return new Response('room is full', { status: 503 });

    const pair = new WebSocketPair();
    const client = pair[0], server = pair[1];
    server.accept();

    const id = 'p' + (this.nextId++) + '-' + Math.random().toString(36).slice(2, 8);
    const peer = { ws: server, presence: {} };
    this.peers.set(id, peer);
    server.send(JSON.stringify({ t: 'hi', id }));
    this.touch();

    server.addEventListener('message', (event) => {
      if (typeof event.data !== 'string' || event.data.length > MAX_MESSAGE_BYTES) return;
      let d;
      try { d = JSON.parse(event.data); } catch (e) { return; }
      if (d && d.t === 'p' && d.p && typeof d.p === 'object' && !Array.isArray(d.p)) {
        peer.presence = d.p;
        this.touch();
      }
    });
    const gone = () => {
      if (this.peers.get(id) === peer) { this.peers.delete(id); this.touch(); }
    };
    server.addEventListener('close', gone);
    server.addEventListener('error', gone);

    return new Response(null, { status: 101, webSocket: client });
  }

  touch() {
    this.dirty = true;
    if (this.timer) return;
    this.timer = setTimeout(() => { this.timer = null; this.flush(); }, FLUSH_MS);
  }

  flush() {
    if (!this.dirty) return;
    this.dirty = false;
    const all = {};
    for (const [id, p] of this.peers) all[id] = p.presence;
    const msg = JSON.stringify({ t: 's', p: all });
    for (const [id, p] of this.peers) {
      try { p.ws.send(msg); } catch (e) { this.peers.delete(id); this.dirty = true; }
    }
    if (this.dirty) this.touch();
  }
}
