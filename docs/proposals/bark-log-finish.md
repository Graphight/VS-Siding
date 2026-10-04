# Bark log finish

- Status: Draft
- Created: 2026-10-04
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-243659 and #cmt-243661 with its first screenshot; `config/materials.json`'s `shakes-{wood}`; `SidingWallEntity.FinishElement`, `SidingWallTexSource`, `SidingModePicker.Rows`, `VSSiding.Tests/WallShapeGen`; vanilla textures under `block/wood/bark/`; decisions 0017, 0027, 0031, 0040, 0045; not yet played

## Summary
A player frames houses with full log blocks and cannot make a siding wall match them: the log finish offers shakes or debarked logs, never bark.
The proposal adds two styles to the log finish, bark running up the wall and bark running along it, drawn as a flat face in vanilla's own bark texture.

## Context
The request: "a version of the logs option for walls, where it is the 'base game log outer texture' both horizontal, or vertical", because "I like to frame my houses with logs, but I can't frame, and use your wall mod in the same block".
A later comment asks for the "log texture carry up on the corners outside, but proper siding inside", with a screenshot of log posts and beams around plank and cobble panels.

`shakes-{wood}` consumes a placed log and has two styles, `shakes` and `logs` (decision 0017).
`logs` draws four round courses per block in `block/wood/debarked/{wood}`, a hewn cabin wall, which sits beside a bark log block as a different material.

Vanilla's log block shows `block/wood/bark/{wood}` on its sides, and ships a `{wood}-h` copy with the grain turned for a log lying down.
A log block's side is flat, so the match for it is a flat face in that texture, not relief.

A style names both its element and its texture.
`FinishElement` builds the element name as `{face}-{style}`, and `SidingWallTexSource` reads `StyleTextures[style]` before the entry's `Texture`.
Finishes are per face (decision 0003), so bark outside and boards inside is already how the wall works once the style exists.

## Design
**Two styles on `shakes-{wood}`.**
`bark` and `hbark`, named as `boards` and `hboards` are (decision 0045), added to the entry's `Styles`.
`StyleTextures` gains `bark: "game:block/wood/bark/{wood}"` and `hbark: "game:block/wood/bark/{wood}-h"`.
The cost stays one placed log, and the drop stays that log.

**Flat groups in the shape.**
`WallShapeGen` emits `front-`, `back-` and `secondfront-` groups for each style, flat slabs the thickness of the finish layer, as the board groups are.

**Walls only, for now.**
A floor picks its element through the entry's `FloorElements`, and `shakes-{wood}` has none, so a log finish on a floor draws the plain slab whatever style is picked.
Bark on a floor is left out until someone wants it.

**Two more options on the Logs row.**
`SidingModePicker.Rows` lists `shakes`, `logs`, `bark`, `hbark` on `vssidingLogs`, with an icon each (decision 0031) and their `toolmode-` names.
With the row off, the finish keeps its defaults: shakes on the front, logs on the back.

## Alternatives considered
- **Round relief in bark, reusing the `logs` geometry.** It reads as a stack of thin poles, and the request is to match a full log block, whose side is flat.
- **One style and one group, turned by a UV rotation.** Vanilla already ships the turned texture, so two texture paths are less code than a rotation rule on the generator.
- **A separate `bark-{wood}` finish entry.** A second entry consuming the same placed log would make the held log match two finishes, and `MatchConsumes` returns the first.
- **Log framing**, so the frame itself shows as a log. That changes the frame's shape, and the request is for the face.

## Consequences & open questions
- `StyleTextures.logs` already names the `game` domain where `Texture` uses `{domain}`; whether another mod's woods (Wildcraft Trees, fixed for shakes in 1.3.0) ship `bark/{wood}` and `bark/{wood}-h` under their own domain needs checking, for `logs` as well as these.
- Whether the bark texture tiles cleanly across stacked walls as a flat face, or needs the positional UVs decision 0028 gave weatherboard.
- Four options on one picker row; the Boards row has three.
- Which texture a log finish shows on a floor once a bark style is the picked one: the slab is the same, but `StyleTextures` may still apply, which would put bark on a floor by accident.
