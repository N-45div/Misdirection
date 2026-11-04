# Day 5 · Fri Nov 6 · Robust table placement

**Exit criterion:** works on three different tables.

Written on Nov 4, ahead of schedule. The table and crowd geometry has 13 unit tests (61 core tests in total), and everything compiles against Unity 6000.0.66f2 + Meta XR SDK v207.0.1. **Not yet run on a headset.** If Thursday's fun gate says PIVOT, the table pieces carry straight over to Kolam Dawn.

## How the game finds the table now

Three sources, most trusted first. Whichever lands first sets the table; a later, more trusted one can replace it.

| Source | When | Code |
|---|---|---|
| **Palms** | Rest both palms flat on the table, heels at the near edge, hold 1 s (3 s to recalibrate) | `Runtime/Table/TableCalibrator.cs` |
| **Saved anchor** | Every time the table is set, a Meta spatial anchor is saved; next launch it's relocalised before you do anything | `Runtime/Table/TableAnchorStore.cs` |
| **Room scan** | The Quest's Space Setup (MRUK) has a TABLE: the game picks the one you're sitting at and uses the edge nearest you | `Runtime/Table/RoomTableFinder.cs`, `Core/Table/TableDetection.cs` |

- The player's palms always win and are never overridden automatically. A saved anchor beats the room scan's guess.
- A room-scan table must be within 1 m of you, at least 30 × 30 cm, and its top 15–95 cm below your eyes. Otherwise the HUD says why ("too far away", "too small", "the wrong height") and asks for palms.
- The frame sits on the **near edge**, as close to you as the 44 cm mat allows, facing across the table.

## Crowd on real floor

`Core/Crowd/CrowdLayout.cs` places spectators from a floor plan built from the scan:

- **Walls** end a spectator's line: nobody stands behind a wall. Spectators keep 35 cm from every wall.
- **Furniture** (sofa, bed, storage, other tables…) only marks spots nobody can stand on. A coffee table between you and the crowd doesn't hide the floor beyond it.
- Spectators prefer 1.55 m away within ±30° (the tracking arc). Walls pull them in, down to 1.0 m. If they still don't fit, the arc tightens (down to ±8°). Failing that, as many fit as possible.
- **One row holds three.** Five spectators can't stand 55 cm apart inside ±30°; a second row for crowds of 4–5 is future work.
- **No space at all** (table pushed against a wall): spectators fall back to the standard arc and may overlap the wall. That's the Day 13 cut rule's shallow-arc case. The HUD/log line `Crowd placed: … No open floor across the table` flags it.
- When the scan arrives after the table was set (palms or anchor), the crowd re-places itself around the real walls.

Everything is in **Show.unity** and **Main.unity** (the bootstrap now adds the MR Utility Kit block to Show too, and turns on the scene-permission request).

## Test protocol · SYS + PRO · ~1 h, three tables

Before you start: run **Space Setup** on the Quest in each room (Settings → Physical space → Space setup) and make sure the table is marked as a table.

For each of three tables (dining table, desk, coffee table or kitchen counter):

1. Launch. Within a few seconds the mat should appear on the near edge **without** a palms gesture (room scan). Note the log line `Room scan: N table(s)…`.
2. Quit and relaunch. The mat should come back at the same spot immediately (saved anchor: `Restored your table from last time`).
3. Palms-recalibrate (hold 3 s) at a different spot on the edge. Quit, relaunch: the new spot is restored.
4. Look at where the spectators stand: none inside a wall, sofa or cupboard; none closer than about 1 m.
5. No-scan check: in a room without Space Setup (or with the scan deleted) the palms gesture still works.

| Table | Room scan found it? | Anchor restored? | Crowd clear of walls/furniture? | Layout note | Issues |
|---|---|---|---|---|---|
| | | | | | |
| | | | | | |
| | | | | | |

## Tuning knobs

| Feel | Knob |
|---|---|
| Room-scan table ignored ("too far") | *RoomTableFinder → Search → Max Edge Distance* (1.0 m) |
| Wrong table chosen | *Search → Min Side*, *Min/Max Top Below Eyes* |
| Spectators too close to walls | *Crowd → Layout → Wall Clearance* (0.35 m) |
| Spectators bunched | *Layout → Min Spacing* (0.55 m) |
| Too few spectators in small rooms | *Layout → Min Distance* (1.0 m) down, *Min Arc Half Degrees* down |
| Don't restore last session's table | *TableAnchorStore → Restore On Start* off, or call `Forget()` |

## Known limits

- A very deep table (e.g. a 2 m dining table): spectators may stand at its far end rather than beyond it, because the chosen table isn't treated as furniture.
- Room-scan tables are assumed rectangular (MRUK gives a plane rectangle or a box).
- Spatial anchors need the Quest's spatial data permission; the first save may show a system prompt.
