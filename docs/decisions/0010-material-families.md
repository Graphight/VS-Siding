# Material families

- Status: Accepted
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; `wall.json` as of 71c1d9c; vanilla 1.21 assets (`itemtypes/resource/plank.json`, `textures/block/wood/planks/`, `worldproperties/block/wood.json`); graduated on branch `material-families` at f7c609b

## Summary
A material dictionary entry can be a template that expands once per matching item: one `{wood}` entry becomes an `oak`, `birch`, `pine`, ... entry at load.
First use: framing and plank finishes for every wood type.

## Context
Only `oak` framing exists.
Holding `plank-birch` with a saw does nothing, silently.
The same starter-set gap applies to `Infills` and `Finishes`.

The backlog weighed two directions:
- **Hand-author one entry per wood.** Simple to reason about, but vanilla has around fifteen plank woods, times framing and finish, and every third-party wood mod needs its own compat patch.
- **Auto-derive from any installed item.** Needs a generic idea of what a "material" is, including its texture and drops, for an arbitrary item. Nobody has that.

There's a middle: vanilla already groups materials into **families** that share a code pattern and a texture pattern.
`plank-{wood}` items all have a `block/wood/planks/{wood}1` texture.
`cobblestone-{rock}` blocks all have `block/stone/cobblestone/{rock}1`.
A template names the pattern once; the load step fills in the variants that actually exist.
The explicit knowledge (which texture, how many planks) stays in JSON, where decision 0001 wants it.

## Design

**Templates live in a sibling dictionary, `attributes.FramingFamilies` (and `InfillFamilies`, `FinishFamilies`).**
```json
FramingFamilies: {
	"{wood}": {
		Match: { type: "item", code: "game:plank-*", variant: "wood" },
		DisplayName: "vssiding:framing-{wood}",
		Texture: "game:block/wood/planks/{wood}1",
		Consumes: { type: "item", code: "game:plank-{wood}", quantity: 2 },
		Drops: [ { type: "item", code: "game:plank-{wood}", quantity: { avg: 2, var: 0 } } ]
	}
}
```
`Match` finds the items; `variant` names which of each item's variant values fills the placeholder.
`type` is `item` or `block`: expansion enumerates both the world's items and its blocks and filters on it, because the second consumer (`masonry-finishes`, `cobblestone-{rock}`) matches blocks.
The key and every string in the entry get the placeholder replaced.
Keeping templates out of `Framings` itself means `MatchConsumes`, `ComputeRetention`, drops, and the texture source never see a template, so none of them change.

**Explicit entries win.**
Expansion skips a generated key that already exists, and skips an item that an explicit entry's `Consumes` already matches.
That keeps today's `oak` framing and `planks` finish exactly as saved in existing worlds, and lets a single awkward wood be hand-authored over the template.

**Expansion runs once, server side, in `SidingModSystem.AssetsFinalize`**, writing into `Block.Attributes.Token` for every `vssiding:wall-*` block.
First thing to verify: that block attributes reach the client from the server after `AssetsFinalize`, so the client's texture source sees the generated entries.
If they don't, run the same pure expansion in `SidingWallBlock.OnLoaded` on both sides instead; it's deterministic over the same item list.

**Pure function, tested without a game:** `MaterialFamilies.Expand(JObject families, JObject explicitEntries, IEnumerable<(string type, AssetLocation code, IDictionary<string,string> variants)> candidates) → JObject`.
Assert the whole resulting `JObject` against an expected one.

**Display names are our own lang keys** (`vssiding:framing-{wood}`, `vssiding:finish-planks-{wood}`), one hand-written `en.json` line per vanilla wood.
A third-party wood with no lang line shows the raw key; nothing reads `DisplayName` yet, so that costs nothing today.
Reusing vanilla's `game:item-plank-{wood}` was the free alternative, but it would name a framing "Birch Plank".

**Plank finishes use the same template**, keyed `planks-{wood}`, with the `Elements` from decision 0007.
The existing explicit `planks` entry keeps oak.

## Alternatives considered
- **Hand-authored entry per wood.** Fine for vanilla, but every wood mod then needs a patch from someone, and the entries are fifteen copies differing by one word.
- **Derive textures by reading the matched item's own texture or the matching `planks-*` block's texture.** More robust for odd mods, but "which of a block's textures is the representative one" is a guess; a path pattern is explicit and wrong loudly (placeholder texture) rather than subtly.
- **`variantgroups` on the wall.** Decision 0001.
- **Expanding at mesh-build time instead of load time.** Every lookup would pay for it, and `MatchConsumes` would need template awareness.

## Consequences & open questions
- `veryaged` planks only have textures under `planks/aged/`, so the template path misses it and it would render decision 0004's unknown-texture placeholder. It gets hand-authored `Framings.veryaged` and `Finishes.planks-veryaged` entries, which pre-empt the generated ones. (`aged1.png` does exist at the template path.)
- Third-party woods using the `game:` plank item code are picked up for free; ones in their own domain need a one-entry family patch pointing at their domain's code and texture path. That's still one patch per mod, not one per wood.
- The texture opacity test must run over the expanded entries, not only the JSON on disk, or a new wood with a partially transparent texture sneaks past it.
- `masonry-finishes` is the second consumer and the proof the mechanism is general; don't generalise past what those two need.
- Save stability: a generated key is the variant value, so an uninstalled wood mod leaves walls with an unknown key, which decision 0003 already renders as missing and restores on reinstall.
