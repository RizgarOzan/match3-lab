#!/usr/bin/env bash
# Turns a folder of frame_###.png captures into a small, palette-optimised GIF.
# usage: tools/capture/make-gif.sh <frames-dir> <out.gif> [fps=8] [width=360]
set -euo pipefail
dir="$1"; out="$2"; fps="${3:-8}"; width="${4:-360}"
ffmpeg -y -loglevel error -framerate "$fps" -i "$dir/frame_%03d.png" \
  -vf "fps=$fps,scale=$width:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=96:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=4" \
  "$out"
ls -la "$out"
