# Clay and brick finishes

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; decision 0007's brick texture fix; `SidingWallTexSource`; vanilla 1.21 assets (`blocktypes/clay/brickcourse.json`, `itemtypes/resource/burnedbrick.json`, `itemtypes/resource/clay.json`, `textures/block/clay/`)

## Summary
Fired bricks in every vanilla colour, and daub in every clay colour, become face finishes via `material-families`.
Coloured bricks need composite textures (a base plus an overlay), which the texture source can't do yet.

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

**`SidingWallTexSource` builds a `CompositeTexture` when `Texture` is an object** and passes it to the `ITextureAtlasAPI.GetOrInsertTexture(CompositeTexture, ...)` overload, which exists for exactly this.
`ResolveTexturePath` becomes `ResolveTexture`, returning a `CompositeTexture` either way (a string is a composite with no overlays), so there's one atlas call.
First thing to verify: whether the overload needs the composite `Bake`d first, and that two walls with the same composite share one atlas entry rather than inserting twice.

**The opacity test judges what renders, not each file.**
For a composite, only the base must be fully opaque: an overlay over an opaque base can't produce a see-through pixel.
Overlays may carry partial alpha.

**Brick family**, key `brick-{type}`: held item `game:burnedbrick-{type}`, 2, texture the cream base plus `{type}1` overlay.
Cream and clinker use their own `four/running/{type}1` with no overlay, and fire bricks aren't in `brickcourse.json` at all (`brick.json` uses `four/running/fire*` directly); all three are hand-authored exceptions, after checking their alpha, and `material-families`' "explicit entries win" skips them in the family.
The existing explicit `brick` entry keeps red on its legacy texture, so saved walls don't change.

**Daub**: vanilla has daub textures in eleven colours (`block/clay/daub/{color}/normal1`), but they don't map one-to-one onto the three raw clays (`clay-blue`, `clay-red`, `clay-fire`).
Proposed: one daub finish per raw clay, each picking the nearest-looking daub colour, hand-authored (three entries isn't a family).
The other daub colours wait for a real in-game way to make coloured daub.

## Alternatives considered
- **Bake opaque copies of the brick textures into this mod.** Ships recoloured copies of vanilla art that drift when vanilla updates them, when vanilla's own composite already works.
- **Render bricks in a transparent pass.** You'd see through the wall, which is the bug.
- **Clay infills in every colour.** Infill is mostly hidden behind finishes; `clay` already exists as the cooling fill.

## Consequences & open questions
- Should the explicit red `brick` move to the composite too, for consistency with the other colours? It would change how existing red brick walls look; decide by eye.
- Raw clay as both an infill (`clay`) and a finish (`daub`) is already the case today via `MatchConsumes`' first-match rule on different dictionaries; more daub entries don't change that.
