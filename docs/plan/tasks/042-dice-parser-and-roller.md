# 042 — Dice expression parser + roller

**Status:** done
**Depends on:** 041
**Branch:** `task/042-dice-parser-and-roller`

## Goal

`2d6+3` becomes a number, decided by the server. Dice are the first thing in the platform where a
dishonest client would be both easy and tempting, so the rule is absolute: **the server rolls**.

## Scope

In scope:
- A parser for the expressions a table actually uses: `d20`, `2d6+3`, `4d6-1`, `2d20kh1` (keep
  highest, for advantage), `kl1` for disadvantage
- A roller using a cryptographic random source, producing every individual die face
- A result carrying the expression, each die, the modifier and the total — so a client can *show*
  the roll rather than assert it
- Rolls sent through the hub and persisted alongside chat, so history includes them
- Tests: parsing, rejection of nonsense, bounded results, distribution sanity, keep-highest

Explicitly out of scope (and which task covers it instead):
- **Roll visibility** — 043. Everything here is visible to the whole table
- System-specific dice semantics beyond keep-highest/lowest — the module hook is Phase 2
- Rolling from a character sheet — Phase 2
- The UI — 044

## Approach

**Never roll on the client.** The roadmap states it and it is the whole point: a roll a client
computed is a roll a client chose. The client sends an expression; the server returns faces.

**Every die is reported.** A total alone cannot be displayed convincingly and cannot be checked.
Sending the faces is what lets 044 show `[4, 6] + 3 = 13`.

**Cryptographic randomness, not `Random`.** Not because a player will predict a seed, but because
there is no reason to accept a weaker source for the one number the whole game hangs on. It costs
nothing here.

**A parser, not a regex on the request path.** Bounded: a limited number of dice and sides, so
`9999d9999` is a rejection rather than a denial of service.

## Acceptance criteria

- [ ] `d20`, `2d6+3`, `4d6-1`, `2d20kh1`, `2d20kl1` all parse
- [ ] Nonsense, zero dice, zero sides and absurd counts are rejected
- [ ] Every result lies within the possible range of its expression
- [ ] Individual faces are reported and sum consistently with the total
- [ ] Keep-highest and keep-lowest select correctly, and report the dropped dice
- [ ] Over many rolls every face of a d6 appears — a stuck roller is caught
- [ ] Rolls appear in chat history
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **A client-side roll is a cheat**, and the temptation is "just for responsiveness".
- **Unbounded expressions are a denial of service.** Bound the dice count and sides.
- Modulo bias: use a helper that does not have it rather than `% sides`.
