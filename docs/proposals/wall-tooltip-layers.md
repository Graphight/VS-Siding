# Wall tooltip names its layers

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning, after decision 0028

## Summary
Override `GetPlacedBlockInfo` so looking at a wall names its framing, its infill and each finished face, and tells you what the next click would add.

## Design
`SidingWallBlock` has no `GetPlacedBlockInfo` override, so the selection tooltip says "Siding Wall" and nothing else.
The infill is behind a finish once a face is boarded, so there is no way to tell a wattle wall from a stone-rubble one — which is the difference between a cellar and a warm room (decision 0015) — short of breaking it.

The entity already holds every key (`Framing`/`Infill`/`Front`/`Back`/`SecondFront`) and every key already has a `DisplayName` in the attribute dictionaries.
So: read the entity, `Lang.Get` each key's `DisplayName`, print the layers present, and name the first gap ("no infill", "front unfinished").
Order it as the build order, so the tooltip doubles as the next-step hint.

One line about sealing — sealed or not, cooling or not — comes free from the same keys via `ComputeRetention`, and is the thing a cellar builder actually wants to read.

## Alternatives considered
- **`GetHeldItemInfo` on the plank instead.** That's the saw's job and the mode picker already names the mode; the information missing is about the wall in the world, not the stack in hand.
- **A `/sidingwall` command.** `sidingroom` exists for room debugging; a tooltip is for players, and a command nobody runs teaches nobody.
- **Showing the raw keys.** `oak`/`wattle` are internal; the dictionaries carry display names precisely so they need not leak.
- **Nothing, and let the handbook explain.** The handbook explains the system. The tooltip answers "what is *this* wall", which no static page can.

## Consequences & open questions
- Tooltip length: framing, infill, two faces and a seal line is five lines, which is long beside vanilla's one. Probably collapse the finishes onto one line and only name a face when it differs from its opposite.
- `cornerout` has three finishable faces (`Front`, `SecondFront`, `Back`), so the face naming must read in terms a player sees, not the field names.
- Whether the client's copy of the entity is populated at tooltip time on a fresh chunk load is worth a look; if not, an unfinished-looking wall would be a lie for a frame or two.
