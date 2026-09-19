# Clay and brick finishes

- Status: Accepted
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; decision 0007's brick texture fix; `SidingWallTexSource`; vanilla 1.21 assets (`blocktypes/clay/brickcourse.json`, `itemtypes/resource/burnedbrick.json`, `itemtypes/resource/clay.json`, `itemtypes/resource/daub.json`, `recipes/barrel/daub-dyed.json`, `textures/block/clay/`); playtest of the tinted daub; shipped in `525c2ce`, `b734768`, `50ab6c4`, `ede04bd` and the daub and clay infill commits after it; graduated on branch `clay-and-brick-finishes`

## Summary
Fired bricks in every vanilla colour and raw daub in every vanilla colour become face finishes via `material-families`, and every raw clay becomes an infill.
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

**Daub comes from raw daub, not raw clay.**
Vanilla already makes daub in eleven colours as an item, `daubraw-{color}` (crafted as ash daub from blue clay, soil, grass and sand, then dyed in a barrel), which the proposal missed.
`FinishFamilies."daub-{color}"` matches `game:daubraw-*`, 2 daub, texture `block/clay/daub/{color}/normal1`; all eleven are fully opaque.
The explicit `daub` key keeps its browngolden texture so saved walls look the same, but now consumes and drops `daubraw-browngolden` instead of `clay-blue`, and the family skips browngolden.
Raw clay is infill only: `InfillFamilies."clay-{type}"` adds red and fire clay beside the explicit blue `clay`, all `Soil`, so all three cool.

## Alternatives considered
- **Bake opaque copies of the brick textures into this mod.** Ships recoloured copies of vanilla art that drift when vanilla updates them, when vanilla's own composite already works.
- **Render bricks in a transparent pass.** You'd see through the wall, which is the bug.
- **Raw clay as a finish too, tinted to the clay.** Tried: `{type}clay` with the daub blended over it in `Overlay` mode, vanilla's door trick. It only existed because coloured daub seemed unobtainable; once raw daub turned up it was a shortcut past crafting daub, and the explicit blue `daub` kept blue out of the tint anyway.
- **A hand-picked daub colour per clay, one entry each.** The original proposal; moot once each daub colour is its own item.
- **Plain `{type}clay` as a finish.** Would look identical to clay infill, so a finished face couldn't be told from a bare one.

## Consequences & open questions
- Should the explicit red `brick` move to the composite too, for consistency with the other colours? It would change how existing red brick walls look; decide by eye.
- A daub face built before this change drops `daubraw-browngolden` when peeled, not the `clay-blue` it cost.
- The opacity test (decision 0007) runs over the expanded entries; its candidate list gained `burnedbrick.json`, `clay.json` and `daub.json`.
- Checked in play: bricks in every colour render opaque. Not yet checked: the ten daub colours and red and fire clay infill.
