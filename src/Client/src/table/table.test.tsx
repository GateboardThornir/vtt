import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderRoll } from './renderRoll'
import type { RollLine } from './types'

function roll(overrides: Partial<RollLine> = {}): RollLine {
  return {
    id: 'r1',
    rollerUserId: 'u1',
    rollerUsername: 'Mattia',
    expression: '2d6+3',
    kept: [4, 6],
    dropped: [],
    modifier: 3,
    total: 13,
    visibility: 'Public',
    createdAt: '2026-08-05',
    ...overrides,
  }
}

describe('rendering a roll', () => {
  it('shows the faces the server sent, and its total', () => {
    expect(renderRoll(roll())).toBe('2d6+3: [4, 6] + 3 = 13')
  })

  it('takes the total from the server rather than adding the faces up', () => {
    // The point of the rule. If this recomputed, it would print 13 and disagree with the table,
    // and the implementation nobody tested is always the one that is wrong.
    const disagreeing = roll({ total: 99 })

    expect(renderRoll(disagreeing)).toContain('= 99')
  })

  it('shows the die that advantage discarded', () => {
    const advantage = roll({ expression: '2d20kh1', kept: [18], dropped: [3], modifier: 0, total: 18 })

    expect(renderRoll(advantage)).toBe('2d20kh1: [18] (dropped 3) = 18')
  })

  it('omits a zero modifier', () => {
    expect(renderRoll(roll({ expression: 'd20', kept: [11], modifier: 0, total: 11 }))).toBe('d20: [11] = 11')
  })

  it('renders a negative modifier as a subtraction', () => {
    const penalised = roll({ expression: '4d6-1', kept: [1, 2, 3, 4], modifier: -1, total: 9 })

    expect(renderRoll(penalised)).toContain('− 1')
  })
})

describe('the roll line in a log', () => {
  it('is readable without recomputing anything', () => {
    render(
      <ul>
        <li>
          <strong>{roll().rollerUsername}</strong> {renderRoll(roll())}
        </li>
      </ul>,
    )

    expect(screen.getByText(/\[4, 6\] \+ 3 = 13/)).toBeInTheDocument()
  })
})
