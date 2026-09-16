# 0005 — The layout evolver, and why it accepts steps that change nothing

**Date:** 2026-09-16 · **Status:** accepted

## Two dials, two tools

`MoveBudgetTuner` owns the move budget and nothing else. Win rate is (very nearly) monotone in
the budget, so a binary search finds the right number in a handful of simulations
([README known issues](../../README.md) records where the monotonicity frays). That covers the
common case — the designer drew the level, the budget is a guess — but not the other one: the
budget is already right and the layout is simply too generous.

`LevelEvolver` is that second pass. It turns the dials a designer turns by hand — obstacle
layers (always together with the matching goal, so the change actually costs the player
something), colour-goal sizes, the colour count, and the shape of the board — one mutation per
step, keeping the ones that move the greedy bot's win rate toward the target band. It never
touches `Moves`; that is the tuner's dial, and a tool that turned both would tell you nothing
about which one mattered.

Hill climbing, not search. The point is a level the designer still recognises, and a readable
record of how it got there: every step is kept in `Result.Steps` with the mutation, the measured
win rate and whether it was accepted. A genetic search over whole layouts would produce better
numbers and a level nobody drew.

## The plateau

The first version accepted a candidate only when it *strictly* reduced the distance to the band.
On the obvious test case — a 7×7 board, 20 moves, one small colour goal, four grass cells — it
never accepted anything: sixty steps, sixty times "100.0%", zero kept.

Nothing was wrong with the mutations. The metric was flat. The level is won with **16.6 of its
20 moves to spare**, so one more grass layer, one carved corner or +1 on a colour goal simply
does not register. Measured (greedy bot, 150 runs, the colour goal alone moved):

| colour goal | 12 | 40 | 80 | 120 | 160 |
|---|---|---|---|---|---|
| greedy win % | 100.0 | 100.0 | 87.3 | 25.3 | 3.3 |

The goal has to grow roughly **sevenfold** before the win rate admits anything happened. A
climber that demands a strict improvement per step cannot make that journey, because it cannot
make the first step. And this is not an unlucky test level: win rate is a saturating metric, so
*every* too-easy level starts on a flat 100% and every too-hard one on a flat 0% — the plateau
is the normal starting condition, not an edge case.

## Decision

**A candidate is accepted when it does not move the win rate further from the band** —
equal-distance steps included. The mutation direction is re-read from the current win rate at
every step, so an equal step is still a step the right way; the measurement just has not noticed
yet. Accepted plateau steps accumulate until the metric responds, and from there the climb is an
ordinary one.

Measured on the same level (band 40–70%, 150 runs, 60 steps, 20 seeds):

| acceptance rule | reached the band |
|---|---|
| strictly better | 2 of 20 seeds |
| equal or better | **20 of 20 seeds**, in 5–32 steps |

The trace now reads the way the level actually behaves — three flat steps, then the descent:

```
  start 100.0%
  keep  colors 4→5 → 100.0%
  keep  grass +1 at (2,1), goal 4→5 → 100.0%
  keep  color 0 goal 12→13 → 100.0%
  keep  grass +1 at (6,5), goal 5→6 → 99.3%
  keep  hole carved at (0,3) → 99.3%
  keep  hole carved at (6,0) → 96.7%
  keep  colors 5→6 → 88.0%
  keep  grass +1 at (0,0), goal 6→7 → 74.7%
  keep  hole carved at (3,0) → 62.7%
in band at 62.7% after 9 steps, 9 kept
```

## Consequences and limits

- On a plateau the walk is effectively random within the chosen direction, so which dial moves
  first is a matter of the seed. Same seed, same result — the evolver is as deterministic as
  everything else in the core — but two seeds give two different levels of the same difficulty.
  That is honest: there is no measurement saying one of them is better.
- A level already inside the band is returned untouched, and the input `LevelDefinition` is
  never mutated; every step works on a copy.
- Each step costs a full simulation. 150 runs is enough to steer; quote a number from a level
  the evolver produced only after re-measuring it at the usual 1000.
- The evolver is library-only for now. The CLI and the Level Editor expose the tuner
  (`m3lab tune`, **Auto-tune moves**); a layout pass belongs behind a confirmation step, because
  unlike a budget change it rewrites the designer's grid.
