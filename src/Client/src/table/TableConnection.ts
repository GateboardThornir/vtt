import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'
import type { ChatLine, ChatVoice, Participant, RollLine, RollVisibility } from './types'

export interface TableEvents {
  onParticipants: (participants: Participant[]) => void
  onParticipantJoined: (participant: Participant) => void
  onParticipantLeft: (participant: Participant) => void
  onChatHistory: (lines: ChatLine[]) => void
  onChatSaid: (line: ChatLine) => void
  onRollHistory: (rolls: RollLine[]) => void
  onRolled: (roll: RollLine) => void
}

/**
 * The one owner of the table's connection.
 *
 * `.claude/rules/frontend.md` requires a single connection manager that components subscribe to,
 * rather than components holding their own. That is what makes task 065's reconnection a change in
 * one place instead of in every component that happened to open a socket.
 */
export class TableConnection {
  private connection: HubConnection | null = null

  async start(sessionId: string, events: TableEvents): Promise<boolean> {
    // Relative, so the Vite proxy handles it in development and Caddy will in production.
    const connection = new HubConnectionBuilder().withUrl('/hubs/table').withAutomaticReconnect().build()

    connection.on('Participants', events.onParticipants)
    connection.on('ParticipantJoined', events.onParticipantJoined)
    connection.on('ParticipantLeft', events.onParticipantLeft)
    connection.on('ChatHistory', events.onChatHistory)
    connection.on('ChatSaid', events.onChatSaid)
    connection.on('RollHistory', events.onRollHistory)
    connection.on('Rolled', events.onRolled)

    await connection.start()

    this.connection = connection

    return await connection.invoke<boolean>('JoinSession', sessionId)
  }

  async say(sessionId: string, body: string, voice: ChatVoice): Promise<boolean> {
    return (await this.connection?.invoke<boolean>('Say', sessionId, body, voice)) ?? false
  }

  async roll(sessionId: string, expression: string, visibility: RollVisibility): Promise<boolean> {
    return (await this.connection?.invoke<boolean>('Roll', sessionId, expression, visibility)) ?? false
  }

  async stop(): Promise<void> {
    if (this.connection !== null && this.connection.state !== HubConnectionState.Disconnected) {
      await this.connection.stop()
    }

    this.connection = null
  }
}
