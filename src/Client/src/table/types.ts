export type ChatVoice = 'InCharacter' | 'OutOfCharacter'

export type RollVisibility = 'Public' | 'Private' | 'MasterOnly'

export interface Participant {
  userId: string
  username: string
}

export interface ChatLine {
  id: string
  authorUserId: string
  authorUsername: string
  body: string
  voice: ChatVoice
  createdAt: string
}

export interface RollLine {
  id: string
  rollerUserId: string
  rollerUsername: string
  expression: string
  /** The individual faces. Rendered, never re-added — see TableConnection. */
  kept: number[]
  dropped: number[]
  modifier: number
  total: number
  visibility: RollVisibility
  createdAt: string
}
