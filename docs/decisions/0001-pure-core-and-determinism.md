# 0001 — A pure C# core, deterministic by construction

**Date:** 2026-09-05 · **Status:** accepted

## Context

The point of the project is a *workbench*: the same rules have to run inside the Unity
editor (level editor preview), in a WebGL build (the playable demo), and thousands of times
per second in a headless simulator that estimates difficulty. If the rules touch
`UnityEngine`, the simulator is chained to the editor's main thread and the tests need a
Unity instance to run.

## Decision

1. **The rules live in a Unity package with `noEngineReferences: true`**
   (`packages/com.rizgarozan.match3lab.core`). A plain .NET project compiles the same files
   for xUnit tests and benchmarks. There is one copy of the source.
2. **Randomness comes from an in-repo PCG32**, not `System.Random`. A seed must reproduce the
   exact same game on Mono, IL2CPP and the .NET test runner; `System.Random`'s seeded algorithm
   is an implementation detail that has already changed once between runtimes. PCG32 is ~40
   lines and verifiable against published reference outputs.
3. **Levels are text, not JSON.** A level is readable in a diff and in a code review, needs no
   JSON library on either side, and round-trips (`Parse(Write(x)) == x`) — which is itself a
   test. The Unity editor is a view over this format, not the format's owner.
4. **Coordinates: Y grows downward.** Row 0 is the top row in the text, in the arrays, and on
   screen. Gravity pulls toward increasing Y. No flipping anywhere.
5. **C# 9, nullable off, warnings as errors.** Unity 6's language level; nothing the editor
   would refuse to compile.

## Consequences

- Presentation code may never compute a rule; it replays what the core reports.
- Any new rule needs a test in `tests/` before it gets a sprite in `unity/`.
- The level text grammar is part of the public API; changing it is a versioned decision.
