# Contributing to Match3 Lab

Thanks for looking. You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and nothing
else — Unity is only for the presentation side, and no contribution below requires it.

```
git clone https://github.com/RizgarOzan/match3-lab
cd match3-lab
dotnet test tests/Match3Lab.Core.Tests
```

Pick an issue labelled [`good first issue`](https://github.com/RizgarOzan/match3-lab/labels/good%20first%20issue),
comment that you are taking it, and open a pull request that says `Closes #N`.

## Adding a level

A level is one text file in [`levels/`](levels/). The format is documented in the
[README](README.md#the-level-format); copy the closest existing level and change it.

1. **Name the file** `NN-short-name.txt`, with `NN` one higher than the last level.
2. **Declare its band.** `band 40 60` means "the greedy bot should win this level 40–60 % of the
   time". The issue you picked names the band; otherwise choose one and say why in the PR.
3. **Check it:**

   ```
   dotnet run --project tools/Match3Lab.Cli -- validate levels/NN-short-name.txt
   dotnet run --project tools/Match3Lab.Cli -- show levels/NN-short-name.txt --seed 1
   dotnet run --project tools/Match3Lab.Cli -- check levels/NN-short-name.txt
   ```

   `validate` explains format mistakes (for example a goal asking for more grass than the layout
   has). `show` prints the starting board. `check` plays the level 1000 times with the greedy bot
   and fails if the win rate is outside the band.
4. **Out of band?** Let the tuner find the move budget, then run `check` again:

   ```
   dotnet run --project tools/Match3Lab.Cli -- tune levels/NN-short-name.txt --band 0.40:0.60 --write
   ```

   If no move budget fits, change the layout: more obstacles or fewer colours make a level harder.

CI runs `check levels` on every pull request, with fixed seeds, so the number you see locally is
the number CI sees. Paste the `check` line into the PR description.

What makes a good level: one idea (a mechanic, a board shape) that a player can name after
playing it. Rows must stay readable in a diff — one token per cell, separated by spaces.

## Adding a bot

Bots implement `IBot` in
[`packages/com.rizgarozan.match3lab.core/Runtime/Simulation/IBot.cs`](packages/com.rizgarozan.match3lab.core/Runtime/Simulation/IBot.cs):

```csharp
public interface IBot
{
    string Name { get; }
    Move Choose(Game game, List<Move> legalMoves, Pcg32 rng);
}
```

Rules that keep the simulator honest:

- **Deterministic.** Use only the `rng` you are given; never `System.Random` or the clock.
- **No peeking at the future.** To try a move, copy the game with `game.CloneWithUnknownFuture(seed)`,
  never `game.Clone()` — the second one knows which pieces will fall next
  ([decision 0003](docs/decisions/0003-bots-and-the-foresight-bug.md)).
- **No `UnityEngine`.** The core is plain C#.

Add the bot next to `GreedyBot`, a test in `tests/Match3Lab.Core.Tests/BotTests.cs`, and its name to
`--bot` in `tools/Match3Lab.Cli/Program.cs`. In the PR, paste a `curve levels --bot <name>` table.

## Fixing a rule

The rules are written down in [decision 0002](docs/decisions/0002-resolution-rules.md), and the
README's *Known issues* lists the places where the code disagrees with it. Write a failing test in
`tests/Match3Lab.Core.Tests/RulesTests.cs` first, then fix. If `check levels` changes, say so in the PR.

## Before you open the pull request

```
dotnet test tests/Match3Lab.Core.Tests
dotnet run --project tools/Match3Lab.Cli -- check levels
```

Both must pass. Keep the change to one thing. If you add a `.cs` file under `packages/` and do
not have Unity, leave out the `.meta` file — it is generated on the maintainer's side.
