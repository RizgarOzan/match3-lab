# Releasing

## WebGL build

```
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -batchmode -quit -nographics ^
  -projectPath unity -buildTarget WebGL -executeMethod Match3Lab.UnityEditor.Builds.WebGL -logFile webgl.log
```

Output: `Builds/WebGL/` (about 9 MB gzip-compressed: `index.html`, `Build/`, `TemplateData/`).
The build uses **gzip with decompression fallback**, so it runs from any static host without
server configuration — including `python -m http.server` locally and itch.io.

The page template is `unity/Assets/WebGLTemplates/Match3Lab/index.html`: the canvas fills the
viewport, the device pixel ratio is capped at 2, and there is a small loading bar. The game
refits its camera whenever the canvas size changes, so any embed size works.

Query parameters: `?auto=1` starts with the bot playing; `&level=n` picks the level (0-based).

## itch.io

1. Zip the *contents* of `Builds/WebGL/` (so `index.html` is at the zip root).
2. New project → Kind of project: **HTML** → upload the zip → tick *This file will be played in the browser*.
3. Embed options: **Embed in page**, viewport **720 × 1080** (portrait works best; the layout
   adapts to any size). Enable *Mobile friendly* and *Automatically start on page load*.
   *SharedArrayBuffer support* is **not** needed (the build has no threads).
4. Page copy: one sentence of what it is, the "Bot: on" tip, and links to the repository and
   the difficulty-curve table.

## Windows build (for screenshots and GIFs)

```
Unity.exe -batchmode -quit -nographics -projectPath unity -buildTarget Win64 -executeMethod Match3Lab.UnityEditor.Builds.Windows -logFile win.log
pwsh tools/capture/capture-window.ps1 -Exe Builds/Windows/Match3Lab.exe -OutDir docs/media/frames -Frames 48 -IntervalMs 125 -PlayerArgs "-autoplay 1"
bash tools/capture/make-gif.sh docs/media/frames docs/media/bot-plays-level-2.gif 8 360
```

`-autoplay [n]` makes the player start with the bot on, on level `n`. The capture script
launches the player borderless at 720×1280, grabs the client area frame by frame **from the
screen**, and closes it — so it only works on an idle desktop; if another window is in front
(or someone is alt-tabbing), that is what gets captured. Check the first frame before using any.

## Screenshots straight from the WebGL page

The template creates the WebGL context with `preserveDrawingBuffer: true`, so the page can
read its own frame. Run the sink, then post frames from the browser console:

```
python tools/capture/save-server.py docs/media 8766
```

```js
fetch('http://localhost:8766/save?name=level-05.png', { method: 'POST', body: document.querySelector('#unity-canvas').toDataURL('image/png') })
```

`?auto=1&level=4` in the page URL opens level 5 with the bot on; the "Bot: on" button turns it off again.

## Checklist before tagging

- `dotnet test tests/Match3Lab.Core.Tests` green
- `dotnet run --project tools/Match3Lab.Cli -- validate levels` all ok
- `Unity -runTests -testPlatform PlayMode` green
- `docs/curve-1000.csv` regenerated if any level or rule changed
- README curve table matches the CSV
