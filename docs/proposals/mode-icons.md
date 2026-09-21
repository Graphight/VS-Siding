# Mode icons

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning, after decision 0027

## Summary
Four flat SVG icons for the saw's tool modes — wall, corner, weatherboard, flat boards — loaded with `capi.Gui.LoadSvg` and hung on the `SkillItem`s in `PlaceWallFrame`.

## Context
`PlaceWallFrame.cs:28` builds four `SkillItem`s with a `Code` and a `Name` and no icon, so the mode picker is four blank tiles you have to hover to read.
Vanilla's own mode pickers are all icons: 55 SVGs under `assets/*/textures/icons/`, single black paths on a transparent ground, tinted by the GUI at draw time (see `assets/survival/textures/icons/rocks.svg`).
`ICoreClientAPI.Gui.LoadSvg(AssetLocation, int textureWidth, int textureHeight, int drawWidth, int drawHeight, int? color)` and `SkillItem.WithIcon` both exist in 1.22.2's API, so this is art plus two lines of wiring.

## Design
Four SVGs in `assets/vssiding/textures/icons/`, authored as the vanilla ones are: one colour, filled paths, no strokes that vanish at 32px, a square viewBox so the tiles line up.

Each icon says what the *mode* does, drawn as a plan or elevation of the thing it builds:

- `wall` — one panel seen in plan: a single thin bar across the cell.
- `corner` — the same bar plus its return, an L in plan. Reads as "two faces of one cell", which is what a `cornerout` is.
- `weatherboard` — elevation: three or four overlapping horizontal boards, the tapered edge visible (decision 0023's profile).
- `boards` — elevation: flush vertical boards, even gaps, no taper. The contrast with `weatherboard` is horizontal-lapped versus vertical-flush, so the two icons must differ in *both* directions and overlap, not just direction.

Wiring: in `PlaceWallFrame.OnLoaded`, inside the existing `ObjectCacheUtil.GetOrCreate`, call `.WithIcon(capi, capi.Gui.LoadSvg(new AssetLocation("vssiding:textures/icons/wall.svg"), 48, 48, 48, 48, null))` per item, and dispose them where the cached array is disposed.

## Alternatives considered
- **Cairo draw delegates** (`DrawSkillIconDelegate`), which vanilla also uses. Code that has to be run to be seen; an SVG is a file you can open and look at.
- **PNG icons.** They blur at other GUI scales, and the game has an SVG loader sitting right there.
- **Rendering the block itself into the tile.** Prettier and wrong: every mode would show the same bare frame, because the mode picks what you *build next*, not what the block looks like now.
- **Naming the modes and stopping.** That is today, and today reads as a broken UI.

## Consequences & open questions
- Whether the cached `SkillItem[]` is disposed anywhere today needs checking; a `LoadedTexture` per mode is four textures for the client's life, which is nothing, but leaking them on reload is sloppy.
- Icons are drawn at one colour, so anything relying on shading to read (a shadow under a lapped board) will flatten. Design for silhouette.
- `modicon.png` for the mod list is art too, and deliberately not here; it belongs with `release-readiness`.
