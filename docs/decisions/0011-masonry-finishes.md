# Masonry finishes

- Status: Accepted
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; vanilla 1.21 assets (`blocktypes/stone/`, `itemtypes/resource/stone.json`, `itemtypes/resource/stonebrick.json`, `textures/block/stone/`); shipped in #15 (8195d65); graduated on branch `stone-infill`

## Summary
Every stone material a player can hold becomes a face finish, once per rock type, using `material-families` templates.
Texture-only; no new geometry.
Clay and brick finishes are `clay-and-brick-finishes`.

## Context
Decision 0003's finish catalogue lists cobblestone and polished stone, and nothing has shipped for either.
Vanilla stone comes as several held materials, each in one variant per rock, which is exactly the family shape `material-families` introduces.
A second, bigger family is what proves that mechanism isn't secretly wood-shaped.

Depends on `material-families`.

## Design

**One `FinishFamilies` template per held stone material:**

| Key | Held (consumed and dropped) | Looks like | Texture |
|---|---|---|---|
| `drystone-{rock}` | item `game:stone-{rock}`, 4 | dry-laid rubble | `game:block/stone/drystone/{rock}1` |
| `cobblestone-{rock}` | block `game:cobblestone-{rock}`, 1 | mortared cobble | `game:block/stone/cobblestone/{rock}1` |
| `ashlar-{rock}` | item `game:stonebrick-{rock}`, 2 | dressed stone blocks | `game:block/stone/brick/{rock}1` |
| `polished-{rock}` | block `game:rockpolished-{rock}`, 1 | smooth stone | `game:block/stone/polishedrock/{rock}` |

The ladder of cost follows the ladder of work: loose stones are cheapest and roughest, cut and polished stone the most finished.
Quantities are starting guesses, tuned in playtest.
`MatchConsumes` compares codes only, so held blocks work as finishes with no code change; confirm `ConsumeHeld` takes the right count off a block stack in play.

**Plain right-click with a saw, same as every other finish (decision 0006).**
The wall gets first refusal, so holding cobblestone and clicking a wall finishes it instead of placing a block; clicking anything else places cobblestone as normal.
A wall face the held block can't finish (the wrong face, or one already finished) also places it as normal (`heldPlaces` in `SidingWallBlock.OnBlockInteractStart`).

**Finishes stay cosmetic.**
A stone face doesn't make a cooling wall; the infill does (decision 0003).

## Alternatives considered
- **Hand-authored entries per rock.** Around twenty rocks times four finishes; this is what `material-families` exists to avoid.
- **One `stone` finish with a single texture.** Loses the per-rock colour that makes vanilla stone building look good.
- **Plaster** (also in decision 0003's catalogue). Vanilla has plaster blocks but no raw plaster material a player holds and spreads. Parked until there's something natural to consume.
- **A stone-rubble cooling infill.** Asked for; see decision 0012.

## Consequences & open questions
- Not every rock has every form (polished rock and stone bricks both list only some rocks). Families expand over items that exist, so that's handled.
- Travertine and meteoric iron stones have no drystone texture, so the drystone `Match` is a regex that leaves them out: `game:@stone-(?!travertine$|meteorite-iron$).*`.
- Dry-laid stone uses `block/stone/drystone/{rock}1`; no rubble texture was needed.
- All four families shipped in one session.
- The texture opacity test (decision 0007) runs over the expanded entries; its candidate list gained `cobblestone.json`, `polishedrock.json`, `stonebrick.json`, and `stone.json`.
- Verified in play: `ConsumeHeld` takes the right count off a held block stack (a cobblestone finish takes one block off a stack of ten).
