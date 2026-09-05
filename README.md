# Match3 Lab

A match-3 **workbench**, not a match-3 game: an engine-independent rules core,
a Unity level editor, and a bot simulator that tells a level designer how hard a
level is before a human ever plays it.

> Status: day 0 — core in progress. Nothing to play yet.

## Layout

```
packages/com.rizgarozan.match3lab.core/   the rules core (pure C#, no UnityEngine) — source of truth
src/Match3Lab.Core/                        .NET project that compiles the same files for tests/benchmarks
tests/Match3Lab.Core.Tests/                xUnit tests
unity/                                     Unity 6 project: presentation, level editor, simulator UI (later)
docs/                                      design notes and decision records
```

## License

MIT — see [LICENSE](LICENSE).
