# 0003 — Bots, what they measure, and the foresight bug

**Date:** 2026-09-05 · **Status:** accepted

## What the simulator answers

"If a competent player tries this level a thousand times, how often do they win, and how much
slack is left?" It does **not** answer "is this level fun" — that still needs people. The bots
are a floor and a ceiling for reasoning, not a model of a human:

| Bot | Plays like | Use it for |
|---|---|---|
| `random` | someone who never looks at goals | the floor: a level random wins 40% of the time has no decisions in it |
| `greedy` | a focused player with no planning: tries every legal move once, keeps the one that advances goals most, prefers making specials | the working estimate of "competent" |

Two other numbers come out of every run and are as useful as the win rate: **moves left when
won** (slack — a level won with 0.5 moves to spare is a coin flip dressed as a win) and
**shuffles per 1000 games** (how often the board dead-ends; Thin Ice's frozen centre dead-ends
in a quarter of all games, which is a design smell no win rate would show).

## The foresight bug

The first greedy bot evaluated a candidate move by cloning the game and playing the move on the
clone. `Game.Clone()` copied the RNG too — so the clone's refills were the *real* future refills,
and the bot was choosing moves with knowledge of which pieces would fall. No player has that.

Fix: bots evaluate on `CloneWithUnknownFuture(seed)`, which keeps the visible board and replaces
the spawn stream with an imagined one. One imagined future per decision is shared by all
candidates so they are compared under the same guess.

Measured effect (1000 games per level, same seeds, before → after):

| Level | greedy win % | cascades / move |
|---|---|---|
| 01 First Steps | 100.0 → 100.0 | 3.58 → 2.38 |
| 04 Thin Ice | 99.4 → 93.2 | 0.96 → 0.62 |
| 05 Hourglass | 93.9 → 72.9 | 1.12 → 0.75 |
| 06 Cold Storage | 84.9 → 64.8 | 1.23 → 0.78 |

A third of the "skill" on the hard levels was clairvoyance. Any difficulty number produced by a
bot that can see the RNG is inflated in exactly the levels where accuracy matters most — the
hard ones. This is now a test (`CloneWithUnknownFuture_keeps_the_board_but_not_the_spawn_stream`).

## Bot randomness is a separate stream

The bot gets its own `Pcg32(seed, 0x5EEDBEEF)` for tie-breaks and imagined futures. Changing
the bot therefore never changes the board's spawns, so two bots on the same seed face the same
starting board and the same real refills — the comparison is between policies, not luck.

## Aggregation is integer-only

Every field in `SimulationResult` is a count. Parallel runs merge by addition, so the result is
bit-identical regardless of thread scheduling (there is a test for this). Averages and rates are
computed from the counts at read time.

## Known limits

- One ply. The greedy bot does not set up combos deliberately; a human expert would score higher
  on levels that reward planning (Cold Storage).
- One imagined future per decision. Averaging several would reduce variance in the bot's
  choices at a linear cost in time; not needed at current speeds (1.3–1.9k games/s on a laptop).
- The greedy weights (goal 100, special 15, piece 1) were set by hand, not fitted to human data.
