# desktop-control

Two tiny command-line tools that let an AI agent (or a script) see and drive a macOS desktop:
take a screenshot, decide what to do, click or type, repeat.

Built to let Claude Code operate GUI apps like Insta360 Studio that have no command-line interface.

- **`shot`** — screenshots the main display and downscales it to point size, so a pixel at
  (x, y) in the image is exactly where `dc click x y` lands (handles Retina 2x scaling).
- **`dc`** — a dependency-free Swift CLI that posts mouse and keyboard events via `CGEvent`.

## Install

```sh
git clone https://github.com/fjpa654/desktop-control.git
cd desktop-control
./build.sh   # needs Xcode Command Line Tools
```

Then grant your terminal app two permissions in **System Settings → Privacy & Security**,
and restart the terminal afterwards:

- **Screen Recording** — for `shot` (without it, screenshots show only the wallpaper)
- **Accessibility** — for `dc` (without it, input events are silently ignored)

## Usage

```sh
./shot                  # writes /tmp/dc-shot.png (or ./shot path.png)

./dc click 640 400      # left click
./dc dclick 640 400     # double click
./dc rclick 640 400     # right click
./dc move 640 400
./dc drag 100 100 300 300
./dc scroll -5          # scroll down 5 lines (positive = up)
./dc type "hello world"
./dc key s cmd          # Cmd+S
./dc key tab shift      # Shift+Tab
./dc key return
./dc pos                # print current mouse position
```

Coordinates are in screen points (e.g. 1440×900 on a 2880×1800 Retina display).
Modifiers: `cmd`, `shift`, `alt`/`opt`, `ctrl`, `fn`.

## The agent loop

1. `shot` → look at the image
2. pick a target, e.g. the Export button at (1308, 52)
3. `dc click 1308 52`
4. `shot` again to verify, repeat

## Caveats

- It drives your real mouse and keyboard — don't use the computer while it runs.
- Main display only.
- Tested on macOS 12 (Intel).
