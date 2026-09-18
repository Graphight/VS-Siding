# Stone infill

- Status: Accepted
- Created: 2026-09-18
- Reflects: branch `masonry-finishes` at 71b8139; `SidingWallBlock.ComputeRetention`; vanilla 1.21 assets (`itemtypes/resource/stone.json`, `textures/block/stone/drystone/`); graduated on branch `stone-infill` at 8195d65

## Summary
Loose stones (`stone-{rock}`) pack a frame as a cooling infill, once per rock, via an `InfillFamilies` template.
Same cellar behaviour as `clay`; no code change.

## Context
`clay` is the only cooling infill, so a cellar wall means digging clay even in stone country where loose stones are everywhere.
Decision 0011 parked a stone-rubble infill until a player asked; this is the ask.

## Design

**One `InfillFamilies` template, matching loose stones only.**
```json
InfillFamilies: {
	"stone-{rock}": {
		Match: { type: "item", code: "game:@stone-(?!travertine$|meteorite-iron$).*", variant: "rock" },
		DisplayName: "vssiding:infill-stone-{rock}",
		Texture: "game:block/stone/drystone/{rock}1",
		BlockMaterial: "Stone",
		Consumes: { type: "item", code: "game:stone-{rock}", quantity: 4 },
		Drops: [ { type: "item", code: "game:stone-{rock}", quantity: { avg: 4, var: 0 } } ]
	}
}
```
Only `stone-*`: cobblestone, bricks, and polished rock are finishes, not fill.
Four stones matches the other infills and the drystone finish.

**Cooling comes for free.**
`ComputeRetention` already treats an infill whose `BlockMaterial` is `Stone` as cooling, the same path `clay` takes with `Soil` (decisions 0002, 0003).
A stone-packed wall and a clay-packed wall behave identically for room retention.

**Texture is the drystone texture, with drystone's exclusion.**
Vanilla has no per-rock rubble texture, and dry-laid stone is what packed loose stone looks like.
Travertine and meteoric iron stones have no drystone texture, so the same regex `Match` as the drystone finish leaves them out.

**Infill before finish, by build order.**
The same `stone-{rock}` item is also the drystone finish.
The build flow offers infill first while a frame is empty, so the first click packs the frame and later clicks finish its faces.
`clay-blue` already works this way as both `clay` infill and `daub` finish.

## Alternatives considered
- **A single `stone` infill with one texture.** Loses the per-rock look, and the family costs no more.
- **Accept cobblestone too.** A full block as fill for a quarter-block wall is a poor trade, and cobblestone already has a finish.
- **Better insulation than clay.** Out of scope: retention only has a sign today, not a magnitude (decision 0002).

## Consequences & open questions
- The opacity test must see the infill candidates; `stone.json` is already in its candidate list, so the template is covered once it's added.
- The regex is now in two places; if a third stone family needs it, consider whether it belongs in one shared spot.
