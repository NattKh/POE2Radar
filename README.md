# POE2Radar

An external map/radar overlay for Path of Exile 2 with built-in cheats, a web dashboard, and full customization.

Attaches to the PoE2 client, reads game state out of process memory (no injection, no hooks), draws a terrain + entity overlay, and optionally patches game constants for quality-of-life tweaks.

> **Use at your own risk.** This reads another process's memory, patches game instructions, and can send keystrokes. This may violate Path of Exile's Terms of Service and could put your account at risk.

---

## Features

### Radar Overlay
- **Terrain map** — walkable grid rendered as a bitmap on the in-game map
- **Entity dots** — monsters, NPCs, chests, transitions, other players, POIs
- **Rarity shapes** — Normal (circle), Magic (diamond), Rare (triangle), Unique (star)
- **HP nameplates** — world-space health bars over Magic/Rare/Unique monsters
- **Tile landmarks** — static features from terrain data (boss arenas, treasure, waypoints) shown immediately on area entry
- **Custom landmarks** — community-contributed tile labels with boss names, rewards, and directions (embedded JSON, 450+ entries across all acts)
- **Watched entity labels** — add any entity to a watchlist with a custom nickname that renders on the overlay

### Cheats (Byte Patching)
All cheats use AOB pattern scanning + `WriteProcessMemory`. Original bytes are saved and restored on exit.

| Hotkey | Cheat | Description |
|--------|-------|-------------|
| F1 | No Atlas Fog | Removes fog of war from the atlas |
| F2 | Reveal Map | Reveals the full minimap |
| F3 | Infinite Zoom | Removes the zoom clamp (zoom out further) |
| F4 | Enemy HP Bars | Forces all enemy health bars visible |
| F5 | Player Light | Adjustable light radius (slider, default 2000, up to 50000) |

### Web Dashboard (`http://localhost:7777` or F11)
A full browser-based control panel served on localhost.

| Tab | Description |
|-----|-------------|
| **Live Entities** | Real-time list of every entity in your zone. Filter by category, search by metadata path, toggle alive-only. Click "Watch" to add to watchlist with a custom nickname. |
| **Watched List** | All watched entity patterns with nicknames. Add custom patterns, remove entries. Persists to `config/watched_entities.json`. |
| **Entity Database** | 6,692 entity paths extracted from the GGPK. Search, filter by category (Monsters, Chests, NPC, etc.), and add to watchlist directly. |
| **Radar Settings** | Full visual customization with no artificial limits. Includes: visibility toggles, dot sizes, outline width/color, font sizes (up to 72 for 4K), all entity colors, terrain opacity, map calibration, flask thresholds. Saves to `config/radar_settings.json`. |
| **Landmarks** | Browse all terrain tile landmarks in the current zone with paths and distances. |

### Auto-Flask
- Sends flask keystrokes when HP or Mana drops below configurable thresholds
- Foreground-gated (only when PoE2 is focused), per-flask cooldowns
- F8 master kill-switch
- Thresholds adjustable via web dashboard

### Anti-Detection
- Executable launches as a random-named hardlink (e.g. `Tocufe.exe`)
- Overlay window class and title are randomized
- No "POE2Radar" strings in the binary
- Character name hidden from overlay and API
- No branding in any visible UI element

### Settings Persistence
All configuration saves to the `config/` directory next to the executable:
- `radar_settings.json` — all visual/radar settings
- `watched_entities.json` — entity watchlist with nicknames

Settings survive restarts and can be hand-edited.

---

## Hotkeys

| Key | Action |
|-----|--------|
| F1 | Toggle No Atlas Fog |
| F2 | Toggle Reveal Map |
| F3 | Toggle Infinite Zoom |
| F4 | Toggle Enemy HP Bars |
| F5 | Toggle Player Light (slider in F9 window) |
| F8 | Toggle Auto-Flask on/off |
| F9 | Open/close WinForms settings window (cheats + light slider) |
| F10 | Toggle overlay visibility |
| F11 | Open web dashboard in browser |
| PageUp/Down | Adjust map scale |
| Arrow Keys | Adjust map offset |
| Home | Reset calibration |
| Ctrl+C | Exit (restores all patched bytes) |

---

## Building

Requires .NET 10 SDK, Windows, x64.

```
dotnet publish src/POE2Radar.Overlay/POE2Radar.Overlay.csproj -c Release -r win-x64 --self-contained
```

Output: `Overlay.exe` (self-contained, no runtime needed).

## Running

1. Start Path of Exile 2
2. Run `Overlay.exe` as administrator
3. Open the in-game map to see the radar overlay
4. Press F11 to open the web dashboard for full customization

---

## Architecture

| Project | Purpose |
|---------|---------|
| `POE2Radar.Core` | Memory reading, offset tables, AOB scanning, cheat engine |
| `POE2Radar.Overlay` | Tick loop, Direct2D overlay, WinForms settings, HTTP API + web dashboard |
| `POE2Radar.Research` | Dev-time offset discovery and validation tools |

## Credits

- Based on [Sikaka/POE2Radar](https://github.com/Sikaka/POE2Radar) (MIT License)
- Cheat system ported from [GameHelper2](https://github.com/mm3141/GameHelper)
- Community landmark data contributed by PoE2 players
