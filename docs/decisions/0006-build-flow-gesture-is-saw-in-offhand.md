# Build flow gesture is saw-in-offhand, not shift-right-click

- Status: Accepted
- Created: 2026-09-15
- Reflects: manual playtesting on branch `in-world-build-flow`, decompiling the shipped `VintagestoryAPI.dll`/`VSSurvivalMod.dll`/`VintagestoryLib.dll` to trace the actual click dispatch and `ItemClay`'s clay-forming trigger; supersedes decision 0005's "shift held" gesture for adding layers

## Summary
Adding infill and finishes to a placed wall no longer requires shift-right-click.
It now uses the same gesture frame placement already uses: a plain right-click while a saw sits in the off hand.
Shift-right-click is dropped entirely for this mod's build flow.

## Context
Playtesting decision 0005 found that shift-right-clicking a wall with a clay lump held, near ground level, sometimes triggered vanilla's clay-forming instead of applying the layer.

Tracing it down (`ItemClay.OnHeldInteractStart`, decompiled): clay's clay-forming creation is gated purely on the shift key.
It does not check what block was clicked at all — it checks whether the cell *adjacent* to the clicked face (offset by `blockSel.Face`) is empty and has solid ground underneath, and if so drops a `clayform` block there.
Any shift-right-click with clay in hand near solid ground is eligible, regardless of what's actually being clicked.

`SidingWallBlock.OnBlockInteractStart` returning `true` does correctly short-circuit the vanilla dispatch chain (traced through `SystemMouseInWorldInteractions`) and prevents the held item's own interact from running *for that same click* — confirmed via decompile and server-side logging that our handler was in fact being reached and succeeding.
But that only helps for clicks that land on the wall in the first place.
Because clay-forming's own trigger is unconditional on shift, there is no return value or flag on our side that can route around it: the same shift-right-click gesture is claimed by both, and there's no reliable way to always win that race from our side.

Frame placement (decision 0005) already solved a materially identical problem — telling this mod's plank behavior apart from Roofing's — with a physical off-hand item rather than a modifier key.
That mechanism doesn't share a gesture with any known vanilla behavior, because a plain right-click always gives the targeted block first refusal (`Block.OnBlockInteractStart` is tried before any held-item interact whenever shift isn't held — no `PlacedPriorityInteract` needed), and no vanilla per-item logic branches on off-hand contents.

## Design
`SidingWallBlock.OnBlockInteractStart` now checks for a saw in the off hand instead of `byPlayer.Entity.Controls.ShiftKey`.
Held-item matching (`MatchConsumes` against `Infills`/`Finishes`), face resolution, affordability, and re-finish guards are all unchanged from decision 0005 — only the gesture that unlocks them moved.

The saw-in-offhand check itself (`WildcardUtil.Match` against `game:saw-*`, since saws come in per-metal variants and there is no bare `game:saw` item) is pulled into a shared `SidingWallBlock.HasSawInOffhand(IPlayer)` helper, used by both `SidingWallBlock.OnBlockInteractStart` and `PlaceWallFrame.OnHeldInteractStart` — one saw check for the whole build flow, framing and layering alike.

`PlacedPriorityInteract` is dropped from `wall.json`. It only mattered for arbitrating shift-right-clicks, which this mod's wall block no longer treats specially at all — a plain right-click already gives the block first refusal without it, and shift-right-clicking a wall now does nothing on our side by design, falling straight through to whatever else the held item would normally do.

## Alternatives considered
- **Find a way to intercept clay-forming.** Its trigger lives in `ItemClay`, a vanilla class this mod doesn't own and has no supported hook into. A JSON patch could only add or reorder *our own* behaviors, not alter another item's compiled logic gated purely on a keypress.
- **Keep shift, and also require the saw.** Doesn't help — clay-forming's trigger never inspects the off-hand at all, so it fires regardless of what's equipped there. Shift itself is the collision, not the missing saw check.
- **A different modifier key (ctrl, alt).** Decision 0005 already ruled out ctrl (Roofing's own base-block modifier). Any keyboard modifier just moves the collision risk to a different vanilla or modded behavior instead of removing the category of risk.

## Consequences & open questions
- Discoverability regresses slightly: sneak-to-build was a convention players already know from Roofing (decision 0005's whole rationale for choosing it). A saw permanently in the off hand while building is a new pattern specific to this mod, and needs the handbook page (already flagged as owed in decision 0005) more than ever.
- Frame placement and layering now share one mental model end to end: saw in the off hand means "I am building a wall," full stop, for every step. This is arguably more coherent than decision 0005's split (saw for framing, shift for everything after), not just a defensive fix.
- Untested: whether some *other* vanilla or modded item's own `OnHeldInteractStart` also triggers unconditionally on a plain right-click near a wall, the way clay's does on shift. Nothing found so far, but the search was specific to clay, not exhaustive.
