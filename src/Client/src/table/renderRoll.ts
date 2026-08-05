import type { RollLine } from './types'

/**
 * Renders a roll from what the server sent.
 *
 * Deliberately does no arithmetic beyond formatting. The total comes from the server; adding the
 * faces up here would be a second implementation of the same rule, and the one that disagrees is
 * always the one nobody tested.
 */
export function renderRoll(roll: RollLine): string {
  const kept = `[${roll.kept.join(', ')}]`
  const dropped = roll.dropped.length > 0 ? ` (dropped ${roll.dropped.join(', ')})` : ''
  const modifier = roll.modifier === 0 ? '' : roll.modifier > 0 ? ` + ${roll.modifier}` : ` − ${-roll.modifier}`

  return `${roll.expression}: ${kept}${dropped}${modifier} = ${roll.total}`
}
