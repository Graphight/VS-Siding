# Siding - Layered Wall Building for Vintage Story

![external-logo](docs/screenshots/ExternalLogo.jpg)

A Vintage Story mod that replaces solid one-block walls and floors with thin, layered ones: pick a framing material, an infill material, and a finish for each face, and build walls and floors that read like actual construction instead of a stack of full blocks.

Inspired by how the [Roofing](https://mods.vintagestory.at/show/mod/30143) mod handles pitched roofs: one shared shape, material swapped in via config, instead of hand-authoring every combination.

## Building a wall

Put a saw or a stone in your off-hand slot, hold the material in your main hand, and right-click.
Every layer goes on the same way: planks or sticks raise the framing, an infill fills it, a finish faces each side.

A stone works from the first day: it is never used up, and sticks build the frame.
Sticks are also the wattle infill, so right-click a face of a bare stick frame to fill it, and its top or an end to stack or extend it.
Bones build a frame too, two to a frame, and a pelt or a cloth fills one.

The saw or stone carries a tool mode picker, and the mode decides what you build: a flat **wall**, an outside **corner**, a **diagonal** across the block, a **post** in one corner of the block, a thin **floor**, or a plank finish on one **face** of any solid block, and whether plank and log finishes go on as weatherboard, boards, shakes, logs or bark.
The in-game handbook has the whole thing: start at *VS Siding Mod: Guide*, which links to a page for each topic.

## What a wall is made of

- **Framing**: planks, in any wood the game or another mod adds, sticks, bones, or bamboo stakes. Bone does not burn.
- **Infill**: wattle, bamboo poles, straw, clay, packed earth, rammed earth, mud brick, pelt, cloth, rubble stone, glass, leaded glass, a glass pane from IndustrialStory, rammed earth or mud brick from Material Needs, or a wooden trapdoor as a louvred vent.
- **Finish**: appearance only, per face, in wood, clay, packed earth, rammed earth, mud brick, brick, stone, metal, wallpaper, tiles laid four to a face, Material Needs rammed earth, mud brick or plaster, or grass and dirt on a floor or deck top.

Framing plus infill is a complete wall, and seals a room the way solid blocks do.
The infill decides whether it counts as a cooling (cellar) wall: rubble stone, clay, packed earth, rammed earth and mud brick do, wattle, bamboo poles, straw, pelt and cloth do not.
With [Roofing](https://mods.vintagestory.at/show/mod/30143), a wall can share a block with a roof, cut to the roof's slope, so a gable end fills right up to it.
Glass seals the same way but lets daylight through.

## Floors

Pick **floor** on the saw and right-click the side of a block with planks to lay joists.
Fill a floor the way you fill a wall, then finish the top with floorboards and the underside with a ceiling.
A filled floor seals the room below it, glass floors let light through, and a lantern or chandelier hangs from the underside.
A wall's deck takes the same layers, so a floor runs right up to the wall.

## Screenshots

The framing:

![framing-screenshot](docs/screenshots/Framing.jpg)

Saw Mode picker:

![saw-mode-picker](docs/screenshots/SawModePicker.jpg)

The finishes:

![finishes-inside](docs/screenshots/FinishingsInside.jpg)
![finishes-3D-texture](docs/screenshots/Finishings3DTexture.jpg)
![finishes-corner](docs/screenshots/FinishingsCorner.jpg)

Stick and bone frames, and a bone and pelt tent:

![primitive-siding](docs/screenshots/PrimitiveSiding.jpg)
![primitive-tent](docs/screenshots/PrimitiveTent.jpg)

Posts, at the end of a wall and in the inner corner of an L, and a corner with a different finish on each inner face:

![corner-post-finished](docs/screenshots/CornerPostFinished.jpg)
![corners-with-post](docs/screenshots/CornersWithPost.jpg)

Bamboo frames and pole infill on bamboo stilts:

![bamboo-stilts](docs/screenshots/BambooStilts.jpg)

A stairwell with capped wall tops and steps that line up:

![stair-hosting](docs/screenshots/StairHosting.jpg)

A room with tiles and wallpaper:

![fit-out](docs/screenshots/FitOut.jpg)

Glass at every merge setting, a hatch over a ladder, and a kitchen vent:

![glass-merge](docs/screenshots/GlassMerge.jpg)
![hatch-ladder](docs/screenshots/HatchLadder.jpg)
![vent-smoke](docs/screenshots/VentSmoke.jpg)

A diagonal decked on both halves:

![diagonal-outer-deck](docs/screenshots/DiagonalOuterDeck.jpg)

Grass and dirt on a deck, the grass matching the field:

![dirt-and-grass](docs/screenshots/DirtAndGrass.jpg)

A mud brick gable under a Roofing roof:

![roofing-gable](docs/screenshots/RoofingGable.jpg)

Floors, from above and below:

![floors-above](docs/screenshots/FloorsAbove.jpg)
![floors-below](docs/screenshots/FloorsBelow.jpg)

Interior wall sharing:

![interior-wall-sharing](docs/screenshots/InteriorPartition.jpg)

Handbook:

![handbook-entry](docs/screenshots/HandbookEntry.jpg)

---

## Building from Source

```bash
just build    # build only
just deploy   # build, then install into the game's Mods folder
```

Plain `just` runs `just deploy`.
Needs [`just`](https://github.com/casey/just).
Without it, `./build.sh` (or `build.ps1` on Windows) runs the same build.

Requires the `VINTAGE_STORY` environment variable to point at your Vintage Story install (or a `Directory.Build.props.user` copied from `Directory.Build.props.user.example`).

## License

[MIT](./LICENSE)
