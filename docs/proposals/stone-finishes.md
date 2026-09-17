# Stone finishes

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; vanilla 1.21 assets (`blocktypes/stone/cobble/cobblestone.json`, `blocktypes/stone/polished/polishedrock.json`)

## Summary
Cobblestone and polished stone become face finishes, one per rock type, using `material-families` templates.
Texture-only; no new geometry.

## Context
Decision 0003's finish catalogue lists cobblestone and polished stone as texture-swap finishes, and nothing has shipped for either.
Both come in one variant per rock, which is exactly the family shape `material-families` introduces, and a second family is what proves that mechanism isn't secretly wood-shaped.

Depends on `material-families`.

## Design

**Two `FinishFamilies` templates in `wall.json`:**

| Key | Held (consumed and dropped) | Texture |
|---|---|---|
| `cobblestone-{rock}` | block `game:cobblestone-{rock}`, 1 | `game:block/stone/cobblestone/{rock}1` |
| `polished-{rock}` | block `game:rockpolished-{rock}`, 1 | `game:block/stone/polishedrock/{rock}` |

The held thing is a whole block, so the quantity is 1: one block of stone faces one wall cell.
`MatchConsumes` compares codes only, so a block works as a held finish with no code change; confirm `ConsumeHeld` takes one block off the stack in play.

**Plain right-click with a saw, same as every other finish (decision 0006).**
The wall's `OnBlockInteractStart` gets first refusal, so holding cobblestone and clicking a wall finishes it instead of placing a cobblestone block.
Clicking anything else places cobblestone as normal.

**Finishes stay cosmetic.**
A stone face doesn't make a cooling wall; the infill does (decision 0003).

## Alternatives considered
- **Consume loose `stone-{rock}` items instead of blocks.** Cheaper per wall, but a face that looks like mortared cobblestone should cost about what a cobblestone block costs.
- **Hand-authored entries per rock.** Around twenty rocks times two finishes; this is what `material-families` exists to avoid.
- **Plaster** (also in decision 0003's catalogue). Vanilla has plaster blocks but no raw plaster material a player holds and spreads, so there's nothing natural to consume. Parked until there is, or until someone decides a plaster block is the right cost.
- **A stone-rubble cooling infill.** `clay` already covers the cellar case; add a second cooling infill when a player asks for one.

## Consequences & open questions
- Some rock variants may not have both a cobblestone and a polished form; the family only expands over items that exist, so that's handled, but check no generated texture path misses (a miss renders the atlas placeholder).
- The texture opacity test (decision 0007) runs over the expanded entries; stone textures are expected to pass.
- Both families need a lang pattern if `DisplayName` is ever shown; nothing in the code reads it today.
