# 0021 — The wall shape generator

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `shake-profile` at commits 85bba66 and 083221e; `VSSiding.Tests/WallShapeGen.cs`; the `shake-profile` proposal

## Summary
Both wall shape files are generated from one element table in the test project, golden-tested against the committed JSON, and stay committed.

## Context
`wall.json` was 2,461 lines and `cornerout.json` 4,290 — 6,751 lines of machine-shaped JSON nobody hand-edits.
`cornerout` carries a `secondfront-*` copy of every front profile, so each new cladding profile costs three element groups written by hand.

Measured, every uv in both files derives from the element's box: north/south use (dx, dy), east/west use (dz, dy), up/down use (dx, dz).
`v` is either flush at 0 (Flat) or position-mapped (Positional: v0 = 16 - to.y, v1 = 16 - from.y), and up/down stay Flat either way.
So the files reduce to 104 rows of name, from, to, texture slot and uv rule.
The whole table plus emitter is 213 lines.

## Design

The per-element escape hatches are the interesting part:

- **`RotatedFaces`**: `back-boards` rotates its outward face 90 degrees to stand plank grain upright (decision 0017 names this). One face on `wall`, two on `cornerout`.
- **`PositionalOverrides`**: `back-logs` position-maps only its outward face while the rest of the log stays Flat — a per-face mix, not a per-element rule. 5 elements per leg.
- **`Faces`**: `infill-pane` declares two faces, not six.
- **Slot is explicit per element** rather than derived from the name, because `glazing-*` draws from `#framing`.
- **Face order is part of the contract**: the golden test compares `JToken.ToString()` strings, not `JToken.DeepEquals`, so it catches key reordering too.
  That was deliberate — `DeepEquals` gives a useless "Expected True, Actual False" on failure and is blind to ordering; string equality names the exact position that diverged.
  Every 6-face element uses north, east, south, west, up, down; `infill-pane`'s two-face lists are west/east and north/south.

**Why the files stay committed.** The game loads plain JSON from the zip; nothing at runtime changes, and no build-order risk.

**Why the generator lives in `VSSiding.Tests` and not `VSSiding`.** It is build-time only and must not ship in the mod DLL.

**Why a golden test rather than a Cake task that regenerates every build.** A build that silently rewrites tracked files hides drift in `git status`.
`just shapes` regenerates on purpose; `just test` fails if the committed files and the table disagree.

**Correcting the record.** The `shake-profile` proposal claimed `front-shakes`, `secondfront-shakes` and `back-boards` were exact duplicates worth -1,868 lines.
They are not; they differ in `faces` (position-mapped UVs, and a 90-degree rotation).
The proposal's audit compared `from`/`to` only.
Decision 0022 covers the shakes question itself.

## Alternatives considered
- **Cake task regenerating every build.** Rejected for the same reason as above — hides drift instead of catching it.
- **A standalone `tools/ShapeGen` console project.** A third csproj, and nothing would enforce that committed files match what it produces.
- **A C#-built mesh at runtime.** Already rejected by decisions 0007 and 0017 — the pipeline is JSON shapes plus selective elements.

## Consequences & open questions
- Adding a cladding profile is now a table edit rather than three hand-written element groups.
- The `buried-shape-faces` proposal becomes a generator rule rather than a hand edit across 6,751 lines.
- One-time cost already paid: the regeneration commit rewrote both files wholesale (semantically identical, verified).
- The table is transcribed from the shapes it replaces, so the golden test is what makes it trustworthy — it is not decoration.
