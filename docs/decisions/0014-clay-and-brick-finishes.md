# Clay and brick finishes

- Status: Accepted
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; decision 0007's brick texture fix; `SidingWallTexSource`; vanilla 1.21 assets (`blocktypes/clay/brickcourse.json`, `itemtypes/resource/burnedbrick.json`, `itemtypes/resource/clay.json`, `textures/block/clay/`); shipped in `525c2ce`, `b734768`, `50ab6c4`, `df684d6`; graduated on branch `clay-and-brick-finishes`

## Summary
Fired bricks in every vanilla colour, and daub in every clay colour, become face finishes via `material-families`.
Coloured bricks needed composite textures (a base plus an overlay), which the texture source couldn't do yet.

## Context
Only `brick` (red, `burnedbrick-red`) and `daub` (browngolden, from `clay-blue`) exist.
Vanilla fires `burnedbrick-{type}` in nine colours: fire, black, brown, cream, gray, orange, red, tan, clinker.

Decision 0007 found that the modern brick textures (`block/clay/brick/four/running/*`) are mostly alpha 163, which renders as x-ray on our opaque geometry.
Its fix pointed red at `game:legacy/clay/brick/red1`, which is fully opaque.
But `legacy/clay/brick/` only covers a few colours, not all nine.

How vanilla does it, from `blocktypes/clay/brickcourse.json`: every colour except cream and clinker is a composite texture, an opaque `cream{n}` base with the partially transparent `{color}1` drawn over it as an overlay.
The alpha 163 is the overlay's, and it never reaches the screen because the base underneath is opaque.
Decision 0007 was looking at the overlay on its own.

So every colour can work.
It needs code, though: `SidingWallTexSource` resolves one `Texture` string and hands a single `AssetLocation` to `GetOrInsertTexture`, so a JSON-only change composites nothing.
That's why this stays separate from `masonry-finishes`, which is JSON only.

Depends on `material-families`.

## Design

**`Texture` on any material entry may be a composite, in vanilla's own JSON shape:**
```json
Texture: { base: "game:block/clay/brick/four/running/cream1", overlays: [ "game:block/clay/brick/four/running/{type}1" ] }
```
A plain string still means a single texture, so every existing entry is unchanged.

**`SidingWallTexSource` builds a `CompositeTexture` when `Texture` is an object.**
`ResolveTexturePath` becomes `ResolveTexture`, returning a `CompositeTexture` either way (a string is a composite with no overlays).
The indexer passes it to `ITextureAtlasAPI.GetOrInsertTexture(CompositeTexture, ...)`, which bakes it, keys the atlas entry by the baked name (so identical composites across walls share one slot), and loads the pixels through `LoadCompositeBitmap`, which adds the `textures/` prefix and `.png` and blends the overlays.
`CompositeTexture.RuntimeBake` looks like the same thing but isn't: it reads the asset by its bare name (so `game:block/wood/planks/oak1` is not found, and every wall fell back to the default shape) and inserts a fresh atlas slot on every call.
Vanilla's `overlays` JSON shorthand (a plain list of texture paths) deserialises into `BlendedOverlays` with blend mode `Normal`, so both the shorthand and the explicit `blendedOverlays` shape are accepted.

**The opacity test judges what renders, not each file.**
For a composite, only the base must be fully opaque: an overlay over an opaque base can't produce a see-through pixel.
Overlays may carry partial alpha.

**Brick family**, key `brick-{type}`: held item `game:burnedbrick-{type}`, 2, texture the cream base plus `{type}1` overlay.
Cream, clinker and fire use their own `four/running/{type}1` with no overlay, hand-authored after checking they're fully opaque, and `material-families`' "explicit entries win" skips them in the family.
The existing explicit `brick` entry keeps red on its legacy texture: the open question below wasn't resolved, so saved red-brick walls don't change.

**Daub changed from the proposal.**
Instead of hand-picking a daub colour per clay, `FinishFamilies."daub-{type}"` matches `game:clay-*` and builds a composite: the clay's own texture `block/clay/{type}clay` as the base, with `daub/browngolden/normal1` blended over it in `Overlay` mode (vanilla's own doors use the same trick), so the daub keeps the colour of the clay it's made from rather than one fixed hue.
The explicit `daub` entry keeps blue, so saved walls don't change.
Not yet checked in game whether the blend actually looks right; see Consequences.

## Alternatives considered
- **Bake opaque copies of the brick textures into this mod.** Ships recoloured copies of vanilla art that drift when vanilla updates them, when vanilla's own composite already works.
- **Render bricks in a transparent pass.** You'd see through the wall, which is the bug.
- **Clay infills in every colour.** Infill is mostly hidden behind finishes; `clay` already exists as the cooling fill.
- **A hand-picked daub colour per clay, one entry each.** The original proposal; blending the daub over the clay's own texture keys the colour to the clay with one family instead of three judgement calls.
- **Plain `{type}clay` texture with no daub overlay.** The fallback if the blended overlay looks muddy in game; not needed yet.

## Consequences & open questions
- Should the explicit red `brick` move to the composite too, for consistency with the other colours? It would change how existing red brick walls look; decide by eye.
- Raw clay as both an infill (`clay`) and a finish (`daub`) is already the case today via `MatchConsumes`' first-match rule on different dictionaries; more daub entries don't change that.
- The opacity test (decision 0007) runs over the expanded entries; its candidate list gained `burnedbrick.json` and `clay.json`.
- The blended daub-over-clay look hasn't been checked in game yet; if it reads muddy, drop the overlay and fall back to the plain clay texture.
