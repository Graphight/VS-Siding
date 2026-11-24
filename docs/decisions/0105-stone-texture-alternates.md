# 0105 — Stone texture alternates

- Status: Accepted
- Created: 2026-11-23
- Supersedes only the drystone and cobblestone texture choice in 0011 and the stone infill's in 0012.
- Reflects: branch `stone-textures-face-shape`; `config/materials.json`'s `Finishes["drystone-{rock}"]`, `["cobblestone-{rock}"]` and `Infills["stone-{rock}"]`, `SidingWallTexSource.Alternate`/`PickAlternate`/`AnyVaries`; vanilla 1.22.7 `textures/block/stone/drystone/` and `cobblestone/`; decision 0025; issue #192

## Summary
The drystone finish, the stone infill and the cobblestone finish take `{rock}*`, vanilla's per-cell alternates, in place of texture `{rock}1`.
Ashlar stays on `brick/{rock}1`.

## Context
Every cell of a drystone or cobblestone wall drew the same texture, so a long wall showed the same stones repeating cell after cell (#192).
Vanilla's own blocks pick one of 8 drystone or 5 cobblestone textures per rock by position.

## Design
`drystone/{rock}1` becomes `drystone/{rock}*` (finish and infill) and `cobblestone/{rock}1` becomes `cobblestone/{rock}*`.
The planks already go through this path: `AnyVaries` marks a `*` texture as varying, `Alternate` keys the mesh cache by position mod 120, and `PickAlternate` takes the cell's variant.
120 divides 8 and 5, so the pick is the one vanilla makes for that cell.

Ashlar keeps texture 1.
0025 measured its relief against that texture's courses, and the other ashlar textures are not measured.
0025 found no grid in drystone or cobblestone, so their rubble relief does not depend on which texture is drawn.

## Alternatives considered
- **Ashlar on `{rock}*` too.** Its relief could stop matching the painted courses on some cells.

## Consequences & open questions
- The mesh cache holds up to one mesh per alternate for these walls, as it does for planks.
