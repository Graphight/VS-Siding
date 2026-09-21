# Wall tooltip names its layers

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning, after decision 0028

## Summary
Override `GetPlacedBlockInfo` so looking at a wall names its framing, its infill and each finished face, and tells you what the next click would add.

## Context
`SidingWallBlock` has no `GetPlacedBlockInfo` override, so the selection tooltip says "Siding Wall" and nothing else.
Once a face is boarded the infill is hidden, so there is no way short of breaking the wall to tell wattle from stone rubble — the difference between a cellar and a warm room (decision 0015).

Nothing in `VSSiding/*.cs` or the tests reads `DisplayName` today: the mesh path wants `Texture`, drops want `Drops`.
So 500-odd entries carry a display name that has never been resolved, and this would be the first code to render one.

## Design
The entity already holds every key (`Framing`/`Infill`/`Front`/`Back`/`SecondFront`), and every key has a `DisplayName`.
So: read the entity, `Lang.Get` each key's `DisplayName`, print the layers present in build order — which makes the tooltip double as the next-step hint — and name the first gap ("no infill", "front unfinished").
One line about sealing, cooling or not, comes free from the same keys via `ComputeRetention`, and is what a cellar builder wants to read.

**Fall back when the lang key is missing.** `MaterialFamilies.Expand` matches `Match: { code: "game:plank-*" }` against the live registry and substitutes into a template (`"vssiding:framing-{wood}"`), while `lang/en.json` lists today's vanilla woods and rocks by hand.
A wood added by a game update, or patched into the `game` domain by another mod, yields an entry whose lang key does not exist and `Lang.Get` hands back the raw key.
Title-case the material key instead of printing `vssiding:framing-foo` at a player.

## Alternatives considered
- **`GetHeldItemInfo` on the plank instead.** The mode picker already names the mode; what's missing is about the wall in the world, not the stack in hand.
- **A `/sidingwall` command.** `sidingroom` exists for room debugging, but a command nobody runs teaches nobody.
- **Showing the raw keys.** `oak`/`wattle` are internal; the dictionaries carry display names so they need not leak.
- **Nothing, and let the handbook explain.** The handbook explains the system. The tooltip answers "what is *this* wall", which no static page can.

## Consequences & open questions
- Framing, infill, two faces and a seal line is five lines against vanilla's one. Probably collapse the finishes onto one line and only name a face when it differs from its opposite.
- `cornerout` has three finishable faces (`Front`, `SecondFront`, `Back`), so the naming must read in terms a player sees, not the field names.
- Whether the client's copy of the entity is populated at tooltip time on a fresh chunk load is worth a look; if not, an unfinished-looking wall would be a lie for a frame or two.
- A missing lang key is worth a startup warning too, beside the ones `MaterialFamilies.Expand` already logs, so an unmapped material shows in the log rather than only in a tooltip.
