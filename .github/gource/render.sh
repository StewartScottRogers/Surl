#!/usr/bin/env bash
# Renders Surl's history across every branch, once, at 7680x4320 (8K), into the directory
# given:
#
#   hls/av1/master.m3u8    adaptive stream in AV1 at 8K, 4K and 1080p, for browsers that
#                          decode AV1 - what the viewer page plays wherever it can
#   hls/h264/master.m3u8   the same in H.264 at 4K and 1080p, for browsers that do not
#   hls/<codec>/<height>p/ each quality as 2-second fragmented-MP4 segments, all under
#                          GitHub's 100 MB file limit however large the render gets
#   gource.mp4             the 4K H.264 quality as one file, for download
#   gource.gif             the widest GIF under GitHub's 10 MB inline limit, for the README
#   still-8k.jpg           the final frame at full 8K, for download
#   poster.jpg             the final frame at 1920 px, shown before the video plays
#   stats.json             the numbers the viewer page shows
#
# Each deliverable is written under a temporary name or directory and moved into place
# only once it is complete. Runs on the GitHub runner (under xvfb) and locally in Git Bash.
# The viewer page itself is .github/gource/site/index.html; the workflow copies it in.
set -euo pipefail

out="${1:?usage: render.sh <output directory>}"
mkdir -p "$out"
here="$(cd "$(dirname "$0")" && pwd)"
# The log, captions, playlist and stats generators are C# file-based apps (make-*.cs, base
# class library only). `dotnet run --file` compiles each on first use and caches the build;
# the SDK is the one global.json names. Run from the repository root: they read git there.
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
cs() { dotnet run --file "$here/$1.cs" -- "${@:2}"; }
work="$out/.work"
rm -rf "$work"
mkdir -p "$work/hls/av1/4320p" "$work/hls/av1/2160p" "$work/hls/av1/1080p" \
    "$work/hls/h264/2160p" "$work/hls/h264/1080p"

width=7680
height=4320
fps=30
# Gource sizes text and user icons in pixels. Scaling them 4x makes 8K read like 1080p: file
# and project names stay legible on a wall-sized screen without crowding the tree.
scale=4
# Target length of the animation, however long or short the project's history is.
seconds=75

cs make-log > "$work/gource.log"
cs make-captions > "$work/captions.txt"
first=$(head -n 1 "$work/gource.log" | cut -d'|' -f1)
last=$(tail -n 1 "$work/gource.log" | cut -d'|' -f1)
days=$(awk -v s=$(( last - first + 1 )) 'BEGIN { printf "%.2f", s / 86400 }')
# Spread the history over the target length, at least 0.2 seconds per day so a long
# history is not a blur. Quiet stretches are skipped (--auto-skip-seconds), so the
# finished animation can come in a little shorter.
spd=$(awk -v d="$days" -v t="$seconds" 'BEGIN { s = (t - 5) / d; if (s < 0.2) s = 0.2; printf "%.2f", s }')

run=()
if command -v xvfb-run > /dev/null; then run=(xvfb-run -a -s "-screen 0 ${width}x${height}x24"); fi

# Multi-sampling smooths every edge, which shows on a large screen, but not every OpenGL
# (Xvfb's software renderer among them) can provide it. Probe once, use it if it works.
msaa=()
if "${run[@]}" gource "$work/gource.log" --log-format custom -320x180 --multi-sampling \
        --stop-at-time 0.2 --output-ppm-stream "$work/probe.ppm" > /dev/null 2>&1; then
    msaa=(--multi-sampling)
fi
rm -f "$work/probe.ppm"

# AV1 carries 8K at a fraction of H.264's size and is what 8K displays decode in hardware;
# a browser plays one codec per stream, so AV1 gets a full ladder of its own. SVT-AV1 is
# fast enough for a runner (it refuses 8K below preset 8); libaom is the slow fallback if
# ffmpeg lacks it.
gop=$(( fps * 2 ))
if ffmpeg -hide_banner -encoders 2> /dev/null | grep -q libsvtav1; then
    av1_8k=(-c:v libsvtav1 -preset 8 -crf 32 -svtav1-params tune=0:scd=0)
    av1_small=(-c:v libsvtav1 -preset 8 -crf 36 -svtav1-params tune=0:scd=0)
else
    av1_8k=(-c:v libaom-av1 -cpu-used 8 -row-mt 1 -crf 32 -b:v 0)
    av1_small=("${av1_8k[@]}")
fi
av1_common=(-g "$gop" -pix_fmt yuv420p)
hls=(-f hls -hls_time 2 -hls_playlist_type vod -hls_segment_type fmp4
     -hls_fmp4_init_filename init.mp4)
x264=(-c:v libx264 -preset medium -profile:v high -pix_fmt yuv420p
      -g "$gop" -keyint_min "$gop" -sc_threshold 0)
d="$work/hls"

echo "rendering ${width}x${height} at ${fps} fps: $days day(s) at ${spd}s/day, ${msaa[*]:-no multi-sampling}"
"${run[@]}" gource "$work/gource.log" --log-format custom -"${width}x${height}" "${msaa[@]}" \
    --title "Surl  -  the server-side mate of curl, built in C# and .NET 10 by a dark factory of AI agents" \
    --seconds-per-day "$spd" --auto-skip-seconds 0.5 --max-file-lag 0.1 \
    --file-idle-time 0 --stop-at-end --camera-mode overview --padding 1.15 \
    --key --highlight-users --highlight-dirs --dir-name-depth 1 --filename-time 4 \
    --user-scale "$scale" \
    --caption-file "$work/captions.txt" --caption-size $(( 20 * scale )) \
    --caption-duration 6 --caption-colour FFD866 \
    --bloom-multiplier 1.3 --bloom-intensity 0.9 \
    --hide mouse,progress --date-format "%A %d %B %Y" --background-colour 05070b \
    --font-size 18 --font-scale "$scale" --dir-colour 8AB4F8 --highlight-colour FFFFFF \
    --output-framerate "$fps" --output-ppm-stream - \
  | ffmpeg -y -loglevel error -r "$fps" -f image2pipe -vcodec ppm -i - \
      -filter_complex "[0:v]split=4[a8][s4][s2][s1];[s4]scale=3840:2160:flags=lanczos,split[a4][h4];[s2]scale=1920:1080:flags=lanczos,split[a2][h2];[s1]fps=1[still]" \
      -map "[a8]" "${av1_8k[@]}" "${av1_common[@]}" "${hls[@]}" \
          -hls_segment_filename "$d/av1/4320p/seg_%03d.m4s" "$d/av1/4320p/index.m3u8" \
      -map "[a4]" "${av1_small[@]}" "${av1_common[@]}" "${hls[@]}" \
          -hls_segment_filename "$d/av1/2160p/seg_%03d.m4s" "$d/av1/2160p/index.m3u8" \
      -map "[a2]" "${av1_small[@]}" "${av1_common[@]}" "${hls[@]}" \
          -hls_segment_filename "$d/av1/1080p/seg_%03d.m4s" "$d/av1/1080p/index.m3u8" \
      -map "[h4]" "${x264[@]}" -crf 18 -maxrate 14M -bufsize 28M -level 5.1 "${hls[@]}" \
          -hls_segment_filename "$d/h264/2160p/seg_%03d.m4s" "$d/h264/2160p/index.m3u8" \
      -map "[h2]" "${x264[@]}" -crf 18 -maxrate 6M -bufsize 12M -level 4.1 "${hls[@]}" \
          -hls_segment_filename "$d/h264/1080p/seg_%03d.m4s" "$d/h264/1080p/index.m3u8" \
      -map "[still]" -update 1 -q:v 2 "$work/still-8k.jpg"

for ladder in av1 h264; do
    cs make-master-playlist "$d/$ladder" > "$d/$ladder/master.m3u8"
done

# A fragmented MP4 is its init segment followed by its media segments; remux the 4K H.264
# one into a plain, seekable MP4 for download, and use the 1080p one as the GIF's source.
cat "$d/h264/2160p/init.mp4" "$d/h264/2160p"/seg_*.m4s > "$work/2160p.frag.mp4"
ffmpeg -y -loglevel error -i "$work/2160p.frag.mp4" -c copy -movflags +faststart "$work/gource.mp4"
cat "$d/h264/1080p/init.mp4" "$d/h264/1080p"/seg_*.m4s > "$work/1080p.frag.mp4"
ffmpeg -y -loglevel error -i "$work/still-8k.jpg" -vf scale=1920:-2:flags=lanczos -q:v 3 "$work/poster.jpg"

# GitHub only plays a GIF inline, and shows nothing over 10 MB: the largest that fits.
limit=$(( 9500 * 1024 ))
for spec in "1280 12 0.5" "1024 12 0.5" "800 12 0.5" "640 10 0.5" "640 10 0.33" "480 8 0.33"; do
    read -r gw gfps speed <<< "$spec"
    ffmpeg -y -loglevel error -i "$work/1080p.frag.mp4" -vf \
        "setpts=${speed}*PTS,fps=${gfps},scale=${gw}:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=bayer:bayer_scale=5" \
        -loop 0 "$work/gource.gif"
    if [ "$(wc -c < "$work/gource.gif")" -le "$limit" ]; then break; fi
done

cs make-stats "$work/gource.log" "$width" "$height" "$fps" > "$work/stats.json"

# Move everything into place only now that every piece exists.
rm -rf "$out/hls"
mv "$d" "$out/hls"
for f in gource.mp4 gource.gif still-8k.jpg poster.jpg stats.json; do mv -f "$work/$f" "$out/$f"; done
rm -rf "$work"

du -sh "$out/hls"/*/* | sed 's/^/  /'
echo "rendered: mp4 $(wc -c < "$out/gource.mp4") bytes, gif ${gw}px $(wc -c < "$out/gource.gif") bytes, hls $(du -sh "$out/hls" | cut -f1)"
