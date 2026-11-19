# 0103 — Wallpaper finish

- Status: Accepted
- Created: 2026-11-19
- Reflects: issue #169; branch `169-wallpaper-finish`; `config/materials.json`'s `FinishFamilies["wallpaper-{type}"]`, `SidingWallBlock.CanAttachBlockAt`, `MaterialDisplayNameTests`, `MaterialTextureOpacityTests`, `WallpaperScenarios`; vanilla `wallpaper.json` and `BlockBehaviorDecor`; decisions 0001, 0010, 0064

## Summary
Vanilla's wallpaper is a decor.
On a siding wall it either refuses (the room side) or redraws the whole wall shape with paper on both sides (the claimed side).
Wallpaper is now a finish, drawn by the wall's own mesh in the clicked face's slot, and a wall refuses vanilla decor on its side faces.

## Context
`SidingWallBlock.CanAttachOf` refuses decor on a wall's room (unclaimed) face.
On the claimed face it goes on, and `JsonTesselator.AddDecor` redraws the wall's whole shape with the decor texture, so both faces show paper.
A decor cannot draw on one face of a JSON-shaped block.

A plain right-click already reaches the wall's `OnBlockInteractStart` before vanilla placement, so a wallpaper in hand lands as a finish.
A sneak right-click runs vanilla placement first: `BlockBehaviorDecor.TryPlaceBlock` calls `CanAttachBlockAt` on the wall and fails with `decorrequiressolid` when it answers false.

## Design
**One finish family.**
`FinishFamilies["wallpaper-{type}"]` matches `game:wallpaper-*` on the variant `type`, with texture `game:block/wallpaper/normal/{type}` and `BlockMaterial: "Cloth"`, so fire skips it (only a wood top layer burns).
It consumes and drops one `game:wallpaper-{type}` block per face.
`Cloth` is a new layer material, so `wall.json`, `floor.json` and their vent copies gain a `Cloth` row in `LayerSounds` (vanilla's cloth sounds) and in `LayerResistance` (0.5, as glass).
Vanilla's nine types come along: blue4, green1, grey1, brown, walnut, darkpurple, yellow, pink, lightgreen.
The textures are 32x32 and fully opaque.
The mod ships its own lang key per type, since vanilla's keys are wildcards (`block-wallpaper-blue*`) and `SidingWallBlock.LangEntry` does not resolve wildcards.

**One finish per face.**
Boards on one side and paper on the other is the panelling-and-wallpaper answer.
There is no paper-over-boards layering.

**Flat on every surface.**
The entry has no `Elements` or `Styles`, so a floor's top, a ceiling and a deck's faces take it with no extra code, as plaster does.

**Peeling gives the paper back.**
One wallpaper per face, as every finish does, though vanilla's decor drops nothing.

**Walls refuse decor on side faces.**
`SidingWallBlock.CanAttachBlockAt` answers false for a block with a `BlockBehaviorDecor` on a horizontal face, `HasBehavior` seeing block behaviours.
UP (rugs on a deck) and DOWN are unchanged.
The test is `SidingWallBlock.RefusesDecor`, and `GuestSealingPatches.CanAttachPostfix` applies it too.
In a cell that hosts furniture the furniture answers `CanAttachBlockAt`, and that postfix rewrites a claimed side face's answer from the guest wall's retention.
Without the test there, wallpaper went on a hosted cell as decor, and a plain click reached vanilla placement too, since `PanelInteractionPatches` passes a panel hit on.
`CanAttachOf` is unchanged.

## Alternatives considered
- **Paper layered over boards.** Two finishes on a face breaks the one-finish-per-face rule every other finish follows.
- **Dropping nothing on peel**, as vanilla's decor does. Every other finish returns its material.
- **Any mod's wallpaper.** The template matches `game:wallpaper-*`; another mod's blocks would need their own match and texture path.
- **Aged or peeled paper styles.** Not built.
- **Refusing decor on a deck's underside, or converting wallpaper already placed as decor.** Not built. Wallpaper already on a wall as decor is left as it is.

## Consequences & open questions
Known gap: a decor with `canAttachToAnything` (decorBehaviorFlags 0x40; vanilla worldgen overlays such as moss, grime and banners) skips `CanAttachBlockAt` on a JSON-drawn block, so it still goes on.
