# 0001 — Wall material is a JSON attribute dictionary on one block class, not a block variant

- Status: Accepted
- Created: 2026-09-13
- Reflects: reverse-engineering `vsroofing_1.7.2.zip` (assets only, no decompiled DLL) during initial repo scaffolding, no code yet

## Summary
Framing, insulation, and exterior are each a *key into a JSON dictionary* read by one shared block class at mesh-build time, not a `variantgroups` axis on the block. This is how the Roofing mod avoids a combinatorial explosion of block IDs, and we're copying the pattern.

## Context
The starting worry (raised before any code existed): framing x insulation x exterior x thickness is a combinatorial mess if each combination is its own block, or even if each is its own vanilla-style variant axis (`game:planks-oak` vs `game:planks-pine` scales fine for one axis, badly for three crossed together).

The Roofing mod (mods.vintagestory.at/show/mod/30143) solves the same problem for roof pitch x material and ships no public source, so the only way to see the approach was to download the released zip and read the assets it ships (JSON is plain text; nothing here required decompiling `vsroofing.dll`).

## Design
What `assets/vsroofing/blocktypes/roof.json` actually contains:
- **One block**: `"code": "roof"`, `"class": "vsroofing.RoofBlock"`, `"entityclass": "vsroofing.AutoRoofEntity"`.
- **One real variant axis**: `side`, loaded from the vanilla `game:abstract/horizontalorientation` group. That's it — orientation is the only thing that's a block variant.
- **Material lives in `attributes.Roofs`**, a dictionary keyed by material name (`straw`, `straw-aged`, ...). Each entry carries its own `Shape` (per-slope mesh refs), `Textures`, `Sounds`, `Materials` (crafting cost), `BlockMaterial`. The block entity (`AutoRoofEntity`) presumably stores which key was selected per placed block and the block class reads the matching dict entry to build the mesh/texture at runtime.
- **Extensibility is JSON-patch, not code.** `compatibility/wool/patches/roof.json` adds a whole new material (`leather`) to another mod's cloth by `addeach`/`addmerge`-patching into `/attributes/Roofs/leather/...` on the base file. No new block, no rebuild of vsroofing itself, no recompiled variant list.

Applied to us: one `SidingWallBlock` class (name TBD), material choice for framing/insulation/exterior each keyed into their own attribute dictionary (`attributes.Framings`, `attributes.Insulations`, `attributes.Exteriors`), selection stored on the block entity, shape/texture picked at mesh-build time. Adding a material later — including one added by a compatibility patch from another mod — is a JSON entry, never a new block ID.

## Alternatives considered
- **One block per material combination.** Rejected outright — the whole reason this decision exists.
- **Vanilla-style `variantgroups` per axis** (`game:siding-oak-wool-brick` as three crossed variant groups). Works for one axis; for three crossed axes VS still has to instantiate every combination as a distinct block at asset-load time, so the block count still multiplies out even though the JSON authoring is a grid instead of a flat list. Rejected for the same reason as the fully manual case, just a smaller multiplier.
- **Decompiling `vsroofing.dll` to copy the block-entity/mesh logic directly.** Not done. The released assets already answer the question that mattered (where does material selection live), and copying compiled logic from a closed-source mod is a different (and murkier) thing than reading the JSON contract it publishes for compatibility mods to extend.

## Consequences & open questions
- We still need our own answer for *where* the selected material is stored per block — block entity data is the strong guess (matches `AutoRoofEntity`) but unconfirmed without either decompiling or testing against the live mod. First real spike should place a roof block and inspect its block entity via `.we` or a debug command, rather than guess.
- Three independent attribute dictionaries (framing/insulation/exterior) crossed at mesh time is more mesh-assembly work per block than the roofing mod's single dictionary — the mod only ever swaps one material into one shape. We're compositing three. That's new territory this decision doesn't resolve.
- Collision/selection box thinness (the actual point of the mod) isn't addressed by this decision at all — it only settles how *material choice* avoids combinatorics, not how a sub-block-thick wall shape works. Separate proposal needed.
