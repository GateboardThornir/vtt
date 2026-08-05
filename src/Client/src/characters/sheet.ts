/**
 * Reading and writing the parts of a 5e sheet this screen edits.
 *
 * The sheet is a document whose shape belongs to the game system, so the client treats it as data
 * rather than as a typed model — a field the schema knows and this screen does not must survive a
 * round trip untouched rather than being silently dropped on save.
 */

export const abilities = [
  'strength',
  'dexterity',
  'constitution',
  'intelligence',
  'wisdom',
  'charisma',
] as const

export type Ability = (typeof abilities)[number]

export interface Derived {
  abilityModifiers?: Record<string, number>
  savingThrows?: Record<string, number>
  skills?: Record<string, number>
  passivePerception?: number
}

export interface Sheet {
  identity: { name: string; class?: string; level?: number }
  abilities: Record<Ability, number>
  proficiencyBonus: number
  hitPoints: { current: number; maximum: number; temporary?: number }
  armourClass: number
  savingThrowProficiencies?: string[]
  skillProficiencies?: string[]
  derived?: Derived
  [key: string]: unknown
}

export function emptySheet(name: string): Sheet {
  return {
    identity: { name, level: 1 },
    abilities: {
      strength: 10,
      dexterity: 10,
      constitution: 10,
      intelligence: 10,
      wisdom: 10,
      charisma: 10,
    },
    proficiencyBonus: 2,
    hitPoints: { current: 8, maximum: 8, temporary: 0 },
    armourClass: 10,
    savingThrowProficiencies: [],
    skillProficiencies: [],
  }
}

export function parseSheet(json: string): Sheet {
  return JSON.parse(json) as Sheet
}

/** Strips derived before sending: the server recomputes it and ignores whatever we claim. */
export function forSaving(sheet: Sheet): string {
  const rest: Record<string, unknown> = { ...sheet }
  delete rest.derived

  return JSON.stringify(rest)
}

export function signed(value: number): string {
  return value >= 0 ? `+${value}` : `${value}`
}
