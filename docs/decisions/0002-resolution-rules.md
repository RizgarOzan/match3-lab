# 0002 — The resolution rules, and why each one is the way it is

**Date:** 2026-09-05 · **Status:** accepted (v1 rule set; each item is a tunable, not a law)

The rules below are the ones a level designer reasons about. They are written down so that the
simulator, the editor preview and the playable build cannot drift apart, and so that changing
one is a visible decision rather than an accident in a `switch`.

## Moves

- **Swap** two edge-adjacent pieces. Legal only if it makes a line of three, or is a special
  combination (rainbow with anything; two rockets/bombs). An illegal swap costs nothing and
  produces no events — the presentation shows a wiggle, the core stays silent.
- **Tap** a special piece to fire it in place. Specials are colourless, so without taps a lone
  rocket could only be fired by a blast; Royal Match and Toon Blast both use tap-to-fire.
- Both cost exactly one move. Cascades are free.

## Matches and specials

| Shape | Reward | Where it lands |
|---|---|---|
| Line of 3 | nothing | — |
| Line of 4 | rocket, oriented **across** the line (horizontal 4 → vertical rocket) | the dropped cell if it is in the group, else the middle of the run |
| L / T / + (a horizontal and a vertical run sharing a cell) | bomb (3×3) | the dropped cell if in the group, else the intersection |
| Line of 5 or more | rainbow | the dropped cell if in the group, else the middle of the run |

Priority when a shape qualifies twice: rainbow > bomb > rocket. A rocket fires across its own
line because the line it came from is already cleared; firing along it would be wasted reach.
If the reward's cell is still occupied (a frozen piece survived under ice), the reward moves to
the first cleared cell of the group.

A special created in a phase cannot be set off by a blast in that same phase. Otherwise a
cascade would routinely consume the reward before the player ever sees it.

## Combinations

| Pair | Effect (centred on the dropped cell) |
|---|---|
| rocket + rocket | full row + full column |
| rocket + bomb | three rows + three columns |
| bomb + bomb | 5×5 |
| rainbow + normal | every piece of that colour |
| rainbow + rocket/bomb | every piece of the most common colour turns into that special and fires (rockets alternate orientation by cell parity) |
| rainbow + rainbow | the whole board |

## Obstacles

- **Grass** is under the piece. It loses a layer whenever the piece on it is cleared or a blast
  crosses the cell. It never moves.
- **Ice** is on the piece. The piece can still be matched but not swapped; a match or blast
  removes one ice layer and the piece stays. Frozen pieces are solid floor for gravity.
- **Box** replaces the piece. It loses a hit point when a match happens next to it (once per
  match group) or a blast hits it directly. At zero it becomes an ordinary empty cell.
- **Hole** is not part of the board. Blasts skip it; nothing falls through it.

## Gravity and refill

Pieces fall straight down within a column segment; segments are bounded by holes, boxes and
frozen pieces. **Every segment refills from its own top.** The alternative — only refilling from
the board's top edge and letting pieces slide diagonally around obstacles — is what most shipped
games do and is markedly harder to reason about; for v1 the simple rule wins, and pockets under
a box never become permanent voids. Revisit if a designer needs diagonal flow.

## Goals, winning, losing

Goal progress counts events, not cells: a two-layer ice cell contributes two to an ice goal.
After a move resolves, **win is checked before lose**, so the last move can still win. When the
board has no legal move, normal unfrozen pieces are reshuffled (up to 100 attempts, preferring an
arrangement without ready-made lines); this is an event the presentation can show.

## Scoring

10 per normal piece and 20 per special fired, multiplied by (1 + cascade index). Score is
flavour, not a goal, and no rule depends on it.
