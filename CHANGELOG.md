# Changelog

What changed in the project, newest first. Contributions from outside the project are named.
v0.1.0 is the first tagged release and covers everything below.

## v0.1.0 (2026-10-31) — first outside contributions

- Level 07 "Rainfall", the first level with empty starting cells, by
  [@jay23606](https://github.com/jay23606) ([#9](https://github.com/RizgarOzan/match3-lab/pull/9)).
- A level grid row of the wrong width now reports its line number in the file, by
  [@Shadow16Ua](https://github.com/Shadow16Ua) ([#8](https://github.com/RizgarOzan/match3-lab/pull/8)).
- Core tests for the rules that ADR 0002 spells out (51 → 59).
- The ice rule fix: an ice-covered match next to a box now costs the box one hit point, not two.

## 2026-09 — public release

- Play in the browser: the WebGL build is on GitHub Pages and linked from the top of the README.
- Difficulty bands in the level format, `m3lab check` (CI fails a level outside its band), and
  `CONTRIBUTING.md`.
- Layout evolver: hill-climbs a level toward a target win-rate band (ADR 0005).
- Pre-publication audit: score accumulation, invariant output, and a blast crossing an iced cell
  now peels the grass under it (ADR 0002).
- Move-budget tuner (`m3lab tune`, Auto-tune in the Level Editor), play-mode smoke tests where the
  bot plays the demo scene headlessly, and the Level Editor and Difficulty Curve windows.
- First version: pure C# rules core, bots, simulator, CLI, six measured levels, and the Unity
  presentation layer.
