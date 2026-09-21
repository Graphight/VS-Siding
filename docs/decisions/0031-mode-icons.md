# 0031 — Mode icons

- Status: Accepted
- Created: 2026-09-21
- Reflects: branch `mode-icons`, PR #37, after decision 0030

## Summary
Four SVG icons for the saw's tool modes, loaded with `IGuiAPI.LoadSvgWithPadding` and hung on the `SkillItem`s in `PlaceWallFrame`.

## Context
`PlaceWallFrame` built four `SkillItem`s with a `Code` and a `Name` and no icon, so the mode picker was four blank tiles you had to hover to read.
Vanilla's own mode pickers are all icons: single-colour SVG paths under `assets/*/textures/icons/`.

## Design
`assets/vssiding/textures/icons/{wall,corner,weatherboard,boards}.svg` — `viewBox="0 0 32 32"`, one black fill, filled paths only, since strokes vanish at tile size.
Each file is named after its mode's `Code`, so the wiring is one `Select` over the existing array, and `ModeIconsTests` asserts the two sets match.

The call is `LoadSvgWithPadding(loc, 48, 48, 5, -1)`, character for character what every vanilla tool-mode call site does.
`-1` tints white, and a `null` colour is wrong twice over: `SvgLoader.rasterizeSvg` keeps the source fill *and* skips the alpha premultiplication `GuiElementSkillItemGrid` renders with.
The source art stays black, like vanilla's.

Disposal lives in `SidingModSystem.Dispose`, which now keeps the `ICoreClientAPI` from `StartClientSide`.
`CollectibleBehavior.OnUnloaded` does exist, but `PlaceWallFrame` is patched onto every plank variant, so ~14 instances share one cached array and would each dispose it.
`ClientMain.Dispose` runs mod systems before its item loop, so this frees the icons once, first.

## Alternatives considered
- **Inline icons in the handbook guide**, via the built-in `<icon path="...">` VTML tag. Built, tried in game, reverted: `IconComponent` draws at `sizeMulSvg = 0.7` of the font, roughly 14px, where the lap and board gaps fall below a pixel and grey out. Serving 48px and 14px needs two sets of files, which is the drift that sharing one file existed to avoid.
- **Cairo `DrawSkillIconDelegate`**, which vanilla also uses. Code you have to run to see; an SVG is a file you can open.
- **PNGs.** They blur at other GUI scales, and the game has an SVG loader already.
- **Rendering the block into the tile.** Every mode would show the same bare frame, because the mode picks what you build *next*.

## Consequences & open questions
- `VintagestoryAPI.xml` lists only documented members, so it can never prove one absent: `CollectibleBehavior.OnUnloaded` is in the DLL and not the XML, and this work drafted a comment claiming it did not exist. Review caught it before merge. That is decision 0020's rule; reflect over the assembly instead.
- Icons are one colour, so anything relying on shading flattens. Design for silhouette.
- `modicon.png` for the mod list is separate art, and belongs with `release-readiness`.
