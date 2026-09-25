# Horizontal boards

- Status: Draft
- Created: 2026-09-25
- Reflects: `SidingModePicker.cs`; `WallShapeGen.cs`'s `boards`/`weatherboard` groups; `wall.json`'s `planks`/`oak-planks` entries; decisions 0007, 0021, 0023, 0027, 0028, 0031, 0040

## Summary
A player asked whether flat board finish can run horizontally.
Today's "boards" style is one flat slab per face with the plank texture rotated 90° only on the room-facing side, giving vertical grain (decision 0007's "flat vertical boards on the back").
Add `hboards` ("horizontal boards") as a third option on the boards row: the same slab, texture left unrotated.

## Context
`WallShapeGen.cs:248-249` (and the `cornerout` equivalent at 434-437) define `front-boards`/`back-boards`/`secondfront-boards` as a single 1-voxel-deep, 16x16 slab per face, `UvRule.Flat`, with `RotatedFaces: ["west"]` (front) or `["east"]` (back) — the room-facing side only.
`RotatedFaces` sets `rotation: 90` on that face's UV in the emitted shape (`WallShapeGen.cs:615`).
Decision 0007 built this deliberately as "one slab with the texture UVs rotated 90° on the room-facing face" for vertical interior boards, contrasted with the lapped, stepped `weatherboard` elements on the exterior.

`UvRule.Flat` maps the whole 0..16 face straight onto the plank texture (`game:block/wood/planks/oak1` for the base `planks` entry), no `Positional` slicing.
Unrotated, the texture's own grain direction paints the face; rotated, that grain turns 90°.
Nobody has checked the plank texture's pixels for which way is "up": 0007 calls the *rotated* result vertical boards, so the unrotated face is taken to be horizontal grain by elimination.
That should be checked in play (Stage 2) rather than assumed correct from this reasoning alone.

Because there's no `Positional` UV rule here, stacking two wall blocks already tiles the texture continuously in whichever direction — the same as any `Flat` face — so nothing like decision 0028's positional-UV fix is needed for either orientation; the concern that drove 0028 was specific to `Course`'s four-row crop, which `boards` never used.

`SidingModePicker.Rows` (`SidingModePicker.cs:27-30`) has the boards row as `("vssidingBoards", ["weatherboard", "boards"], AllowNone: true)`.
`wall.json`'s finish entries list `Styles: ["weatherboard", "boards"]` and `Elements: { front: "front-weatherboard", back: "back-boards" }` (e.g. lines 129-151); `SidingWallBlock.HasStyle` gates which finishes offer which styles, and `SidingWallTexSource`'s `StyleTextures`/`StyleSuffix` (decision 0040) resolve a texture by style name where an entry needs one (plain `planks` doesn't — one texture, no `StyleTextures`).
`FinishElementGroupsTests.cs:25` collects every `Styles` string across `wall.json` and cross-checks it against generated element groups, so a new style name has to appear in both places or that test fails.

## Design

**New style name `hboards`, same geometry as `boards`, rotation dropped.**
Add `front-hboards`/`back-hboards`/`secondfront-hboards` to `WallShapeGen.cs` (both the `wall` and `cornerout` builders), identical `From`/`To`/slot to the existing `boards` elements, but with `RotatedFaces` omitted — the visible face keeps the texture's native orientation.
Run `just shapes` to regenerate `wall.json`/`cornerout.json`'s committed shapes.

**Row gains a third option.**
`SidingModePicker.Rows`: `("vssidingBoards", ["weatherboard", "boards", "hboards"], AllowNone: true)`.
Icons load twice per option already (`LoadIcons`, decision 0040), so a third option just needs its own SVG: `VSSiding/assets/vssiding/textures/icons/hboards.svg`, sized and styled to match `boards.svg`/`weatherboard.svg` (same viewBox/stroke weight — copy `boards.svg` and rotate the board lines 90°).

**`wall.json` entries: add `hboards` to `Styles`, add the element.**
Each plank-consuming finish entry (the `planks`/`oak-planks` family, lines ~129-151) gets `Styles: ["weatherboard", "boards", "hboards"]`.
`Elements` doesn't need a third key — it only names the *default* per face (`front: "front-weatherboard"`), and `SelectiveElements`/`StyleSuffix` already resolve a non-default style's element name as `{face}-{style}` (decision 0027/0040), so `front-hboards` is picked up by name once it exists in the shape, no entry change beyond `Styles`.

**Lang entries.**
`toolmode-hboards`: `"Horizontal boards"` (matching `toolmode-boards: "Flat boards"`'s pattern, though "flat" no longer distinguishes the two — consider renaming `toolmode-boards` to `"Vertical boards"` at the same time, since a row showing "Flat boards" next to "Horizontal boards" reads oddly; flag this for sign-off, not do unasked).
A `toolmoderow-boards` row label already exists and doesn't change.
`gamemechanicinfo-siding-text` names "Flat boards" under Saw modes; if `toolmode-boards` is renamed, this prose line should follow.

**Tests.**
`FinishElementGroupsTests.cs` needs no new test — it already asserts every `Styles` string round-trips to an element group, so it fails until `hboards` groups exist and passes once they do.
`SidingWallBlockBuildFlowTests.OnlyAFinishListingAStyleOffersIt` is a template for a companion case: assert `HasStyle(planks, "hboards")` is true and `HasStyle(daub, "hboards")` is false, alongside the existing `weatherboard`/`boards` assertions (`SidingWallBlockBuildFlowTests.cs:60-72`).
No `StyleTextures` test is needed — plain planks don't key texture by style.

## Alternatives considered
- **New board boxes (lips/steps) instead of a texture rotation.** Rejected: horizontal boards read fine as a flat rotated texture the same way vertical ones do today; new geometry would be solving a problem the vertical style didn't have.
- **A `Positional` UV rule for either orientation**, mirroring 0028. Rejected: 0028's fix was for `Course`'s four-row crop specifically; `Flat` already tiles the full texture, so there's nothing to slice.
- **Reuse `boards` and add a per-block "rotate 180/90" render flag instead of a new style.** Rejected: styles are already the mod's mechanism for "same material, different look" (0027/0040); a flag would be a second, parallel mechanism for the same job.

## Consequences & open questions
- **Saved-wall risk: none for existing walls.** `hboards` is an addition to `Styles`/rows, not a rename of `boards` or `weatherboard`; a wall entity already storing `FrontStyle: "boards"` keeps resolving to the same vertical element it always did (`StyleSuffix`/`HasStyle` fall through unchanged). Only the optional `toolmode-boards` label rename above is cosmetic and touches no saved state.
- **Open question, needs playtest:** confirm which raw orientation the plank texture actually paints unrotated, before assuming "unrotated = horizontal" ships the right look; if it's backwards, swap which of `boards`/`hboards` carries `RotatedFaces`.
- Every other plank-consuming finish (if any get added later) inherits the same three-way choice for free, since the mechanism is per-`Styles`-entry, not per-finish-hardcoded.

## Stages
1. **Shape:** add `front-hboards`/`back-hboards`/`secondfront-hboards` to `WallShapeGen.cs` for both `wall` and `cornerout`, run `just shapes`, add the `HasStyle` test case.
2. **Wire-up:** third row option in `SidingModePicker.cs`, `hboards.svg` icon, `Styles` additions in `wall.json`, lang entries.
3. **Playtest:** build a plank wall, cycle the boards row through all three options on front, back and a `cornerout`'s second leg; confirm grain direction reads as intended and stacked walls show no seam.
4. **Graduate** as a decision extending 0007/0040.
