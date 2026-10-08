# 0063 — Bark log finish

- Status: Accepted
- Created: 2026-10-07
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-243659 and #cmt-243661 with its first screenshot; branch `feat/bark-and-plaster-finishes`; `config/materials.json`'s `shakes-{wood}`, `SidingWallEntity.FinishElement`, `SidingWallTexSource`, `SidingModePicker.Rows`, `SidingFloorEntity.HasFloorStyle`, `VSSiding.Tests/WallShapeGen`, `MaterialTextureOpacityTests`; vanilla textures under `block/wood/bark/`; decisions 0007, 0017, 0027, 0031, 0040, 0045; unit tests pass; played 2026-10-08, floors and decks not replayed since their fix

## Summary
A player frames houses with full log blocks and cannot make a siding wall match them: the log finish offered shakes or debarked logs, never bark.
The log finish now has two more styles, bark running up the wall and bark running along it, drawn as a flat face in the wood's own bark texture.

## Context
The request: "a version of the logs option for walls, where it is the 'base game log outer texture' both horizontal, or vertical", because "I like to frame my houses with logs, but I can't frame, and use your wall mod in the same block".
A later comment asks for the "log texture carry up on the corners outside, but proper siding inside", with a screenshot of log posts and beams around plank and cobble panels.

`shakes-{wood}` consumes a placed log and had two styles, `shakes` and `logs` (decision 0017).
`logs` draws four round courses per block in `block/wood/debarked/{wood}`, a hewn cabin wall, which sits beside a bark log block as a different material.

Vanilla's log block shows `block/wood/bark/{wood}` on its sides, and ships a `{wood}-h` copy with the grain turned for a log lying down.
A log block's side is flat, so the match for it is a flat face in that texture, not relief.

A style names both its element and its texture.
`FinishElement` builds the element name as `{face}-{style}`, and `SidingWallTexSource` reads `StyleTextures[style]` before the entry's `Texture`.
Finishes are per face (decision 0003), so bark outside and boards inside is how the wall already works once the style exists.

## Design
**Two styles on `shakes-{wood}`.**
`bark` and `hbark`, named as `boards` and `hboards` are (decision 0045), were added to the entry's `Styles`.
`StyleTextures` gained `bark: "{domain}:block/wood/bark/{wood}"` and `hbark: "{domain}:block/wood/bark/{wood}-h"`.
The cost stays one placed log, and the drop stays that log.
The existing `logs: "game:block/wood/debarked/{wood}"` is unchanged.

**Flat groups in the shape.**
`WallShapeGen` emits `front-`, `back-` and `secondfront-` groups for each style, flat slabs the thickness of the finish layer, as the board groups are.
They are `front-bark`, `back-bark`, `front-hbark` and `back-hbark` in the straight wall, and the same per leg and for `secondfront` in the corner.
They use the boxes and `UvRule.Flat` of `front-hboards` and `back-hboards`, with no `RotatedFaces`, since vanilla's `{wood}-h` already has the grain turned.
Both styles are therefore the flat slab `hboards` is, under two names.
The new names are in the `ignoreElements` lists of `blocktypes/wall.json`, so a wall that does not ask for them does not draw them.

**Floors and decks take the texture, not a shape.**
A floor or deck picks a style through `SidingFloorEntity.HasFloorStyle`, which at first read only the entry's `FloorElements`.
`shakes-{wood}` has none, so a log finish on a floor stored no style and drew its default, shakes, whatever was picked.
The first draft of this decision called that "walls only"; in play on 2026-10-08 it read as the picker being ignored.
`HasFloorStyle` now also accepts a style the finish lists in `Styles`, on a face `FloorElements` leaves out.
That face is the plain slab whatever the style, so the style picks the texture alone: `logs` is the debarked texture laid flat, and `bark` and `hbark` are bark in its two directions.
The floor's texture source and the two deck slots of `SidingWallTexSource.ResolveTexture` now pass the stored style; neither did, since no floor finish had a `StyleTextures` to read.

**Two more options on the Logs row.**
`SidingModePicker.Rows` lists `shakes`, `logs`, `bark`, `hbark` on `vssidingLogs`, with an icon each (decision 0031) and the names "Vertical bark" and "Horizontal bark" under `toolmode-bark` and `toolmode-hbark`.
With the row off, the finish keeps its defaults: shakes on the front, logs on the back.

## Alternatives considered
- **Round relief in bark, reusing the `logs` geometry.** It reads as a stack of thin poles, and the request is to match a full log block, whose side is flat.
- **One style and one group, turned by a UV rotation.** Vanilla already ships the turned texture, so two texture paths are less code than a rotation rule on the generator.
- **A separate `bark-{wood}` finish entry.** A second entry consuming the same placed log would make the held log match two finishes, and `MatchConsumes` returns the first.
- **Log framing**, so the frame itself shows as a log. That changes the frame's shape, and the request is for the face.
- **The `game` domain for the bark styles, as `logs` has.** Wildcraft Trees ships bark under its own domain, so a `game:` path would miss it; see the first finding below.

## Consequences & open questions
Findings from reading, settled:
- Wildcraft Trees 1.3.4 ships `bark/{wood}.png` and `bark/{wood}-h.png` for all 44 of its woods under `assets/wildcrafttree/`, and its debarked textures under `assets/game/`.
  The bark styles use `{domain}:`, so a modded wood shows its own bark, and the existing `logs` path with `game:` is correct and stays.
- A floor has no relief groups for a log finish, so `shakes` and `logs` are flat there and differ from each other only in texture.
- Vanilla's `bark/{wood}` is drawn upright and `{wood}-h` is its copy with the grain turned, so neither style needs a rotation rule.

`MaterialTextureOpacityTests.NoMaterialTextureHasPartialAlpha` resolves both bark textures for every vanilla wood and checks they are opaque.
It exempts `game:block/wood/bark/baldcypress` by name.
Of vanilla's 26 bark textures it alone has partial alpha, one column of 32 pixels at alpha 248 to 254, where the brick texture that prompted the test had 840 pixels at alpha 163 (decision 0007).

Played 2026-10-08, on walls and corners with a vanilla wood and a Wildcraft wood: the four styles draw as expected.
The session covered bark across two stacked walls and the right edge of bald cypress vertical bark, and reported nothing wrong with either, so the flat UVs stay and the exemption stands.
It also found the floor ignoring the picked style, fixed as Design describes.

The Logs row now has four options where the Boards row has three.

Still open, and only play can settle it:
- Floors and decks since the fix: the texture source's use of the stored style has no unit test, as it needs a client, and which way `bark` and `hbark` run on a floor and on each side's deck is unseen.
