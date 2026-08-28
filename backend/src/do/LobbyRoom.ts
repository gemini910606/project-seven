import { DurableObject } from 'cloudflare:workers'

/**
 * A lobby: presence, chat and readiness for a handful of players before a match.
 *
 * READ THIS BEFORE BUILDING MULTIPLAYER ON IT. A Durable Object is a correct
 * place to put lobby state - it is a single-threaded consistency point with a
 * stable address, which is exactly what matchmaking wants. It is NOT a place to
 * put a shooter's authoritative simulation:
 *
 *   - Workers cannot open UDP sockets. Everything here is WebSocket over TCP,
 *     so one lost packet stalls every later packet (head-of-line blocking).
 *     A twitch shooter needs to drop stale state, not queue it.
 *   - Every player's traffic is relayed through whichever colo the DO lives in,
 *     so two players in the same city can still pay a transcontinental round
 *     trip if the object was created elsewhere.
 *
 * That makes this fine for lobby, chat, presence, readiness and a low-rate
 * co-op PvE relay, and wrong for competitive 60Hz play. When you need real
 * netcode, keep this object for matchmaking and hand the match itself to a
 * dedicated host (Edgegap / Hathora / a plain VM) that speaks UDP.
 *
 * Hibernation: the WebSocket hibernation API is used so an idle lobby costs no
 * duration charges while players sit in it.
 */

interface Member {
  playerId: string
  displayName: string
  ready: boolean
}

type ClientMessage =
  | { type: 'join'; playerId: string; displayName: string }
  | { type: 'ready'; ready: boolean }
  | { type: 'chat'; text: string }
  | { type: 'ping' }

const MAX_MEMBERS = 8
const MAX_CHAT_LENGTH = 200

export class LobbyRoom extends DurableObject {
  async fetch(request: Request): Promise<Response> {
    if (request.headers.get('Upgrade') !== 'websocket') {
      // Plain GET returns a snapshot, which is handy for a "who is in this
      // lobby" web view without opening a socket.
      return Response.json({ members: this.members() })
    }

    const pair = new WebSocketPair()
    const [client, server] = [pair[0], pair[1]]

    // acceptWebSocket (not server.accept) is what enables hibernation.
    this.ctx.acceptWebSocket(server)

    return new Response(null, { status: 101, webSocket: client })
  }

  async webSocketMessage(ws: WebSocket, raw: string | ArrayBuffer): Promise<void> {
    if (typeof raw !== 'string') return

    let msg: ClientMessage
    try {
      msg = JSON.parse(raw) as ClientMessage
    } catch {
      ws.send(JSON.stringify({ type: 'error', error: 'invalid_json' }))
      return
    }

    switch (msg.type) {
      case 'join': {
        if (this.ctx.getWebSockets().length > MAX_MEMBERS) {
          ws.send(JSON.stringify({ type: 'error', error: 'lobby_full' }))
          ws.close(1013, 'lobby full')
          return
        }
        const member: Member = {
          playerId: String(msg.playerId).slice(0, 64),
          displayName: String(msg.displayName).slice(0, 24),
          ready: false,
        }
        // Attachments survive hibernation, so membership does not need storage.
        ws.serializeAttachment(member)
        this.broadcast({ type: 'members', members: this.members() })
        return
      }

      case 'ready': {
        const member = ws.deserializeAttachment() as Member | null
        if (!member) return
        member.ready = Boolean(msg.ready)
        ws.serializeAttachment(member)
        this.broadcast({ type: 'members', members: this.members() })
        return
      }

      case 'chat': {
        const member = ws.deserializeAttachment() as Member | null
        if (!member) return
        const text = String(msg.text).slice(0, MAX_CHAT_LENGTH).trim()
        if (!text) return
        this.broadcast({ type: 'chat', from: member.displayName, text })
        return
      }

      case 'ping':
        ws.send(JSON.stringify({ type: 'pong' }))
        return
    }
  }

  async webSocketClose(_ws: WebSocket, _code: number, _reason: string, _clean: boolean): Promise<void> {
    // getWebSockets() already excludes the closing socket by the time this runs.
    this.broadcast({ type: 'members', members: this.members() })
  }

  async webSocketError(): Promise<void> {
    this.broadcast({ type: 'members', members: this.members() })
  }

  private members(): Member[] {
    return this.ctx
      .getWebSockets()
      .map((ws) => ws.deserializeAttachment() as Member | null)
      .filter((m): m is Member => m !== null)
  }

  private broadcast(payload: unknown): void {
    const data = JSON.stringify(payload)
    for (const ws of this.ctx.getWebSockets()) {
      try {
        ws.send(data)
      } catch {
        // A socket that died between getWebSockets() and send() is not an error
        // worth failing the whole broadcast over.
      }
    }
  }
}
