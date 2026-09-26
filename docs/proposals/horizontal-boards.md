# Horizontal boards

- Status: Draft
- Created: 2026-09-25
- Reflects: `SidingModePicker.Rows`; `WallShapeGen.cs`'s `boards` groups; `wall.json`'s `planks`, `planks-veryaged` and `planks-{wood}` entries; decisions 0007, 0027, 0028, 0031, 0040

## Summary
A player asked whether flat boards can run horizontally.
Today's `boards` style is one flat slab per face with the plank texture turned 90° on its visible face, which gives vertical boards (decision 0007).
The proposal adds `hboards` as a third option on the picker's boards row: the same slab with the texture left unturned.

## Context
`front-boards`, `back-boards` and `secondfront-boards` are each one 1-voxel slab across the face (`WallShapeGen.cs:248-249`, `cornerout` at `:434-437`), `UvRule.Flat`, with `RotatedFaces` naming the visible face: `west` for the front, `east` for the back.
`RotatedFaces` writes `rotation: 90` on that face's UV (`WallShapeGen.cs:615`).

Nobody has checked which way the plank texture's boards run unturned.
0007 calls the turned result vertical, so the unturned face should be horizontal, but that is reasoning, not a look at the texture; the playtest settles it.

A `Flat` face maps the whole texture onto each block, so stacked walls tile it with no seam in either orientation.
0028's positional-UV fix was for weatherboard's four-row crop and does not apply.

## Design
**Geometry.**
`WallShapeGen` gains `front-hboards`, `back-hboards` and `secondfront-hboards` for both `wall` and `cornerout`: the `boards` elements with `RotatedFaces` dropped.
`just shapes` regenerates the committed shapes.
Styles resolve to `{face}-{style}` groups by name (0027, 0040), so no entry needs a new `Elements` key.

**Picker.**
The boards row in `SidingModePicker.Rows` (`SidingModePicker.cs:27-30`) becomes `["weatherboard", "boards", "hboards"]`.
A new `icons/hboards.svg` is `boards.svg` with its lines turned 90°, loaded white and grey like the others (0031, 0040).

**Finishes.**
All three plank finishes add `hboards` to `Styles`: `planks` and `planks-veryaged` under `Finishes`, and the `planks-{wood}` template under `FinishFamilies` (`wall.json:126-155`).
`FinishElementGroupsTests` checks each style has element groups, not that every plank entry lists it, so a missed entry fails silently; a `HasStyle` case per entry in `SidingWallBlockBuildFlowTests.OnlyAFinishListingAStyleOffersIt` catches it.

**Names.**
`toolmode-hboards` is "Horizontal boards", and `toolmode-boards` changes from "Flat boards" to "Vertical boards", since both are flat.
The handbook's "Saw modes" paragraph (`gamemechanicinfo-siding-text`, `lang/en.json:203`) still describes the flat list of modes 0040 replaced, and has no logs row; it needs rewriting whether or not this lands, and this is the natural PR for it.

## Alternatives considered
- **Board geometry with lips or steps.** The vertical style is a flat textured slab and reads fine; horizontal boards need nothing more.
- **A per-wall rotate flag instead of a style.** Styles are already how one material gets a different look (0027); a flag would be a second mechanism for the same job.

## Consequences & open questions
- Existing walls are untouched: `boards` keeps its name and geometry, and only its label changes.
- If the texture's boards turn out to run vertically unturned, `boards` and `hboards` swap which one carries `RotatedFaces`.

## Stages
1. **Shape and finishes:** the `hboards` groups from `WallShapeGen`, `just shapes`, `Styles` on the three plank entries, `HasStyle` tests.
2. **Picker and text:** the third option, the icon, the two labels and the handbook paragraph.
3. **Playtest:** each boards option on a front, a back and a `cornerout`'s second leg, on a two-high wall.
4. **Graduate.**
