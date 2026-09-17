# Clay and brick finishes

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; decision 0007's brick texture fix; vanilla 1.21 assets (`itemtypes/resource/burnedbrick.json`, `itemtypes/resource/clay.json`, `textures/block/clay/`)

## Summary
Fired bricks in every vanilla colour, and daub in every clay colour, become face finishes via `material-families`.
The hard part isn't the templates; it's that most vanilla brick textures render see-through on our walls.

## Context
Only `brick` (red, `burnedbrick-red`) and `daub` (browngolden, from `clay-blue`) exist.
Vanilla fires `burnedbrick-{type}` in nine colours: fire, black, brown, cream, gray, orange, red, tan, clinker.

Decision 0007 found that the modern brick textures (`block/clay/brick/four/running/*`) are mostly alpha 163, which renders as x-ray on our opaque geometry.
Its fix pointed red at `game:legacy/clay/brick/red1`, which is fully opaque.
But `legacy/clay/brick/` only covers a few colours (red, brown, fire, ...), not all nine.
So "a family over `burnedbrick-*`" hits the same bug for most colours, and the opacity test will say so.

Kept separate from `masonry-finishes` because of that risk: stone textures are expected to just work, bricks aren't.

## Design

**Step one is the texture question, time-boxed.**
Decision 0007 couldn't find how vanilla renders `four/running/*` solid.
Look once more, specifically at the `claybricks` block's JSON (`blocktypes/clay/brickcourse.json`) for a texture overlay, `blendMode`, or a second texture layer the partial alpha is composited onto.
If that's it, the finish entry grows the same thing and every colour works.
If it isn't found within the session's first stretch, fall back: family over the colours with an opaque legacy texture only, and list the rest as open.

**Brick family**, key `brick-{type}`: held item `game:burnedbrick-{type}`, 2, texture per the step above.
The existing explicit `brick` entry keeps red, per `material-families`' "explicit entries win".

**Daub family**, key `daub-{color}`: vanilla has daub textures in eleven colours (`block/clay/daub/{color}/normal1`), but they don't map one-to-one onto the three raw clays (`clay-blue`, `clay-red`, `clay-fire`).
Proposed: one daub finish per raw clay, each picking the nearest-looking daub colour, hand-authored (three entries isn't a family).
The other daub colours wait for a real in-game way to make coloured daub.

## Alternatives considered
- **Bake opaque copies of the brick textures into this mod.** Works for certain, but ships recoloured copies of vanilla art that drift when vanilla updates them. The fallback if the vanilla mechanism can't be found and players want every colour.
- **Render bricks in a transparent pass.** You'd see through the wall, which is the bug.
- **Clay infills in every colour.** Infill is mostly hidden behind finishes; `clay` already exists as the cooling fill.

## Consequences & open questions
- The session might end with only some brick colours. That's a fine outcome if the rest are written down.
- Clinker bricks have their own textures (`block/clay/brick/clinker*`); check their alpha separately.
- Raw clay as both an infill (`clay`) and a finish (`daub`) is already the case today via `MatchConsumes`' first-match rule on different dictionaries; more daub entries don't change that.
