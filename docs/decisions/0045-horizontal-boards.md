# 0045 — Horizontal boards

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `horizontal-boards` (PR #61); played; `SidingModePicker.Rows`; `WallShapeGen.cs`'s `boards` and `hboards` groups; `wall.json`'s `planks`, `planks-veryaged` and `planks-{wood}` entries and its `shapebytype` `ignoreElements`; decisions 0007, 0027, 0028, 0031, 0040

## Summary
A player asked whether flat boards can run horizontally.
The `boards` style is one flat slab per face with the plank texture turned 90° on its visible face, which gives vertical boards (decision 0007).
This adds `hboards` as a third option on the picker's boards row: the same slab with the texture left unturned.

## Context
`front-boards`, `back-boards` and `secondfront-boards` are each one 1-voxel slab across the face (`WallShapeGen.cs:252-253`, `cornerout` at `:442-445`), `UvRule.Flat`, with `RotatedFaces` naming the visible face: `west` for the front, `east` for the back.
`RotatedFaces` writes `rotation: 90` on that face's UV (`WallShapeGen.cs:629`).

Nobody had checked which way the plank texture's boards run unturned.
0007 calls the turned result vertical, so the unturned face should be horizontal; the playtest confirmed it.

A `Flat` face maps the whole texture onto each block, so stacked walls tile it with no seam in either orientation.
0028's positional-UV fix was for weatherboard's four-row crop and does not apply.

## Design
**Geometry.**
`WallShapeGen` has `front-hboards` and `back-hboards` for `wall` (`:254-255`), and `front-`, `back-` and `secondfront-hboards` for `cornerout` (`:446-449`): the `boards` elements with `RotatedFaces` dropped.
Styles resolve to `{face}-{style}` groups by name (0027, 0040), so no entry needs a new `Elements` key.

**Default shape.**
Every `shapebytype` entry in `wall.json` (`:238-245`, four wall and four cornerout) lists the new groups in `ignoreElements`, or they would draw on the block's default mesh.
The proposal missed this; `FinishElementGroupsTests` enforces it.

**Picker.**
The boards row in `SidingModePicker.Rows` (`SidingModePicker.cs:31`) is `["weatherboard", "boards", "hboards"]`.
`icons/hboards.svg` is `boards.svg` with its bars turned to rows, loaded white and grey like the others (0031, 0040).

**Finishes.**
All three plank finishes list `hboards` in `Styles`: `planks` and `planks-veryaged` under `Finishes`, and the `planks-{wood}` template under `FinishFamilies` (`wall.json:131`, `:140`, `:152`).
`SidingWallBlockBuildFlowTests.OnlyAFinishListingAStyleOffersIt` covers `hboards` alongside the other styles.

**Names.**
`toolmode-hboards` is "Horizontal boards", and `toolmode-boards` changed from "Flat boards" to "Vertical boards", since both are flat.
The handbook's "Saw modes" paragraph (`gamemechanicinfo-siding-text`) still described the flat list of modes 0040 replaced, with no deck or logs row; it now describes the picker row by row.

## Alternatives considered
- **Board geometry with lips or steps.** The vertical style is a flat textured slab and reads fine; horizontal boards need nothing more.
- **A per-wall rotate flag instead of a style.** Styles are already how one material gets a different look (0027); a flag would be a second mechanism for the same job.

## Consequences & open questions
- Existing walls are untouched: `boards` keeps its name and geometry, and only its label changed.
- A fourth board style is one more group set in `WallShapeGen`, one more name in each `ignoreElements` list, `Styles` and `Rows`, and an icon.
