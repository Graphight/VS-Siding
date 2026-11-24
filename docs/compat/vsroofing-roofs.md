# VS Roofing Mod roofs against siding walls

Mod version 2.1.1, game version 1.22.7, probed 2026-11-24.
Nothing here is asserted: each row is what the two mods did on this run.
Every click goes through vanilla's right-click order, the clicked block's interaction first and then the held item's, run on the server with a hammer in the off hand for Roofing and a saw for Siding.
`Took the click` names the step that handled it; `nothing` means vanilla would next place a held block against the clicked face.
In-game errors are the codes the server sent the player; an error raised on the client alone does not reach this run.

## Roof block overrides

The roof's block class and each class above it up to `Block`, with every member it declares over its base, read by reflection.

- `VSRoofing.RoofBlock`: `AllowSnowCoverage`, `CanAcceptFallOnto`, `CanAttachBlockAt`, `CanCreatureSpawnOn`, `DisplacesLiquids`, `DoEmitSideAoByFlag`, `DoEmitSideAo`, `DoPartialSelection`, `DoPlaceBlock`, `GetAttributes`, `GetBlockMaterial`, `GetCollisionBoxes`, `GetColorWithoutTint`, `GetDecal`, `GetDrops`, `GetLightAbsorption`, `GetLightHsv`, `GetLiquidBarrierHeightOnSide`, `GetParticleCollisionBoxes`, `GetPlacedBlockInfo`, `GetPlacedBlockInteractionHelp`, `GetPlacedBlockName`, `GetRandomColor`, `GetRetention`, `GetRotatedBlockCode`, `GetSelectionBoxes`, `GetSelectionColor`, `GetSnowCoveredVariant`, `GetSnowLevel`, `GetSounds`, `OnBlockBroken`, `OnBlockInteractStart`, `OnBlockPlaced`, `OnBlockRemoved`, `OnFallOnto`, `OnLoaded`, `OnNeighbourBlockChange`, `OnPickBlock`, `OnServerGameTick`, `OnUnloaded`, `PerformSnowLevelUpdate`, `ShouldReceiveServerGameTicks`, `SideIsSolid`

## Tool modes

GetToolModes takes the client's player and Roofing builds its list only there, so each pick is the index SetToolMode takes from a fresh stack of planks, with a second index for a pick on a page the first opens.
The stack attributes it writes name it. A page opener and a page's empty slots write nothing of their own and have no row.
Each frame is one plank click on the top of a granite block in open air, with the player three blocks south.
`Sides solid` is SideIsSolid per face, north, east, south, west, up and down, with a dash where the face is not solid; `Heat retention` is GetRetention per face in the same order.
`Wall may replace` is IsReplacableBy for `vssiding:wall-wall-west`.

| Pick | Stack attributes | Took the click | Block | Entity keys | Sides solid | Heat retention | Wall may replace |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | roofType 0, eaveVariant 0, roofMode 0 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 1 | roofType 0, eaveVariant 1, roofMode 1 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 1, RoofInfo[10], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 2 | roofType 0, eaveVariant 2, roofMode 2 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 2, RoofInfo[10], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 0 | page 1, roofType 0, slope 1, placement 0, advancedToolMode 0 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 1 | page 1, roofType 0, slope 1, placement 1, advancedToolMode 1 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 2 | page 1, roofType 0, slope 1, placement 2, advancedToolMode 2 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 3 | page 1, roofType 0, slope 2, placement 0, advancedToolMode 3 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 5 | page 1, roofType 0, slope 2, placement 2, advancedToolMode 5 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 7 | page 1, roofType 0, slope 3, placement 1, advancedToolMode 7 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 9 | page 1, roofType 0, slope 4, placement 0, advancedToolMode 9 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 11 | page 1, roofType 0, slope 4, placement 2, advancedToolMode 11 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 12 | page 1, roofType 0, slope 5, placement 0, advancedToolMode 12 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 13 | page 1, roofType 0, slope 5, placement 1, advancedToolMode 13 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 3 then 14 | page 1, roofType 0, slope 5, placement 2, advancedToolMode 14 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 4 | roofType 1, eaveVariant 0, roofMode 4 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 5 | roofType 1, eaveVariant 1, roofMode 5 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 1, RoofInfo[10], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 6 | roofType 1, eaveVariant 2, roofMode 6 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 2, RoofInfo[10], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 0 | page 1, roofType 1, slope 1, placement 0, advancedToolMode 0 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 1 | page 1, roofType 1, slope 1, placement 1, advancedToolMode 1 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 2 | page 1, roofType 1, slope 1, placement 2, advancedToolMode 2 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 3 | page 1, roofType 1, slope 2, placement 0, advancedToolMode 3 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 5 | page 1, roofType 1, slope 2, placement 2, advancedToolMode 5 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 7 | page 1, roofType 1, slope 3, placement 1, advancedToolMode 7 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 9 | page 1, roofType 1, slope 4, placement 0, advancedToolMode 9 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 11 | page 1, roofType 1, slope 4, placement 2, advancedToolMode 11 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 12 | page 1, roofType 1, slope 5, placement 0, advancedToolMode 12 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 13 | page 1, roofType 1, slope 5, placement 1, advancedToolMode 13 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 7 then 14 | page 1, roofType 1, slope 5, placement 2, advancedToolMode 14 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 1, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 8 | roofType 2, eaveVariant 0, roofMode 8 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Type 2, RidgeInfo[28], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 9 then 0 | page 2, roofType 2, advancedSlopesToolMode 0, slope 1 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 2, RidgeInfo[24], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 9 then 1 | page 2, roofType 2, advancedSlopesToolMode 1, slope 2 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 2, RidgeInfo[24], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 9 then 2 | page 2, roofType 2, advancedSlopesToolMode 2, slope 3 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Type 2, RidgeInfo[28], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 9 then 3 | page 2, roofType 2, advancedSlopesToolMode 3, slope 4 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 2, RidgeInfo[24], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 9 then 4 | page 2, roofType 2, advancedSlopesToolMode 4, slope 5 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Type 2, RidgeInfo[24], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 10 | orientation 0 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 11 | orientation 1 | the held item | vsroofing:roof-bare-east | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 12 | orientation 2 | the held item | vsroofing:roof-bare-south | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |
| 13 | orientation 3 | the held item | vsroofing:roof-bare-west | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | ------ | 0 0 0 0 0 0 | false |

## Boxes

In sixteenths of a block, (x1,y1,z1)-(x2,y2,z2), z running south.
`Thatched` is after a sneak click with thatch on the frame, `Filled` after a ctrl click with granite on the thatched roof.

### Collision boxes

| Pick | Frame | Thatched | Filled |
| --- | --- | --- | --- |
| 0 | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) |
| 1 | (0,0,8)-(2,8,16) (15,0,8)-(16,8,16) (0,8,0)-(2,16,8) (15,8,0)-(16,16,8) | (0,0,8)-(8,8,16) (0,8,0)-(8,16,8) | (0,0,8)-(8,8,16) (0,8,0)-(8,16,8) |
| 2 | (0,0,8)-(1,8,16) (14,0,8)-(16,8,16) (0,8,0)-(1,16,8) (14,8,0)-(16,16,8) | (8,0,8)-(16,8,16) (8,8,0)-(16,16,8) | (8,0,8)-(16,8,16) (8,8,0)-(16,16,8) |
| 3 then 0 | (0,0,8)-(2,2.7,16) (14,0,8)-(16,2.7,16) (0,2.7,0)-(2,5.3,8) (14,2.7,0)-(16,5.3,8) | (0,0,8)-(16,2.7,16) (0,2.7,0)-(16,5.3,8) | (0,0,8)-(16,2.7,16) (0,2.7,0)-(16,5.3,8) |
| 3 then 1 | (0,5.3,8)-(2,8,16) (14,5.3,8)-(16,8,16) (0,8,0)-(2,10.7,8) (14,8,0)-(16,10.7,8) | (0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8) | (0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8) |
| 3 then 2 | (0,10.7,8)-(2,13.3,16) (14,10.7,8)-(16,13.3,16) (0,13.3,0)-(2,16,8) (14,13.3,0)-(16,16,8) | (0,10.7,8)-(16,13.3,16) (0,13.3,0)-(16,16,8) | (0,10.7,8)-(16,13.3,16) (0,13.3,0)-(16,16,8) |
| 3 then 3 | (0,0,8)-(2,4,16) (14,0,8)-(16,4,16) (0,4,0)-(2,8,8) (14,4,0)-(16,8,8) | (0,0,8)-(16,4,16) (0,4,0)-(16,8,8) | (0,0,8)-(16,4,16) (0,4,0)-(16,8,8) |
| 3 then 5 | (0,8,8)-(2,12,16) (14,8,8)-(16,12,16) (0,12,0)-(2,16,8) (14,12,0)-(16,16,8) | (0,8,8)-(16,12,16) (0,12,0)-(16,16,8) | (0,8,8)-(16,12,16) (0,12,0)-(16,16,8) |
| 3 then 7 | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) |
| 3 then 9 | (0,0,12)-(2,8,16) (14,0,12)-(16,8,16) (0,8,8)-(2,16,12) (14,8,8)-(16,16,12) | (0,0,12)-(16,8,16) (0,8,8)-(16,16,12) | (0,0,12)-(16,8,16) (0,8,8)-(16,16,12) |
| 3 then 11 | (0,0,4)-(2,8,8) (14,0,4)-(16,8,8) (0,8,0)-(2,16,4) (14,8,0)-(16,16,4) | (0,0,4)-(16,8,8) (0,8,0)-(16,16,4) | (0,0,4)-(16,8,8) (0,8,0)-(16,16,4) |
| 3 then 12 | (0,0,13.3)-(2,8,16) (14,0,13.3)-(16,8,16) (0,8,10.7)-(2,16,13.3) (14,8,10.7)-(16,16,13.3) | (0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3) | (0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3) |
| 3 then 13 | (0,0,8)-(2,8,10.7) (14,0,8)-(16,8,10.7) (0,8,5.3)-(2,16,8) (14,8,5.3)-(16,16,8) | (0,0,8)-(16,8,10.7) (0,8,5.3)-(16,16,8) | (0,0,8)-(16,8,10.7) (0,8,5.3)-(16,16,8) |
| 3 then 14 | (0,0,2.7)-(2,8,5.3) (14,0,2.7)-(16,8,5.3) (0,8,0)-(2,16,2.7) (14,8,0)-(16,16,2.7) | (0,0,2.7)-(16,8,5.3) (0,8,0)-(16,16,2.7) | (0,0,2.7)-(16,8,5.3) (0,8,0)-(16,16,2.7) |
| 4 | (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,8,0)-(16,16,8) | (0,8,0)-(16,16,8) |
| 5 | (0,8,0)-(2,16,8) (15,8,0)-(16,16,8) | (0,8,0)-(8,16,8) | (0,8,0)-(8,16,8) |
| 6 | (0,8,0)-(1,16,8) (14,8,0)-(16,16,8) | (8,8,0)-(16,16,8) | (8,8,0)-(16,16,8) |
| 7 then 0 | (0,2.7,0)-(2,5.3,8) (14,2.7,0)-(16,5.3,8) | (0,2.7,0)-(16,5.3,8) | (0,2.7,0)-(16,5.3,8) |
| 7 then 1 | (0,8,0)-(2,10.7,8) (14,8,0)-(16,10.7,8) | (0,8,0)-(16,10.7,8) | (0,8,0)-(16,10.7,8) |
| 7 then 2 | (0,13.3,0)-(2,16,8) (14,13.3,0)-(16,16,8) | (0,13.3,0)-(16,16,8) | (0,13.3,0)-(16,16,8) |
| 7 then 3 | (0,4,0)-(2,8,8) (14,4,0)-(16,8,8) | (0,4,0)-(16,8,8) | (0,4,0)-(16,8,8) |
| 7 then 5 | (0,12,0)-(2,16,8) (14,12,0)-(16,16,8) | (0,12,0)-(16,16,8) | (0,12,0)-(16,16,8) |
| 7 then 7 | (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,8,0)-(16,16,8) | (0,8,0)-(16,16,8) |
| 7 then 9 | (0,8,8)-(2,16,12) (14,8,8)-(16,16,12) | (0,8,8)-(16,16,12) | (0,8,8)-(16,16,12) |
| 7 then 11 | (0,8,0)-(2,16,4) (14,8,0)-(16,16,4) | (0,8,0)-(16,16,4) | (0,8,0)-(16,16,4) |
| 7 then 12 | (0,8,10.7)-(2,16,13.3) (14,8,10.7)-(16,16,13.3) | (0,8,10.7)-(16,16,13.3) | (0,8,10.7)-(16,16,13.3) |
| 7 then 13 | (0,8,5.3)-(2,16,8) (14,8,5.3)-(16,16,8) | (0,8,5.3)-(16,16,8) | (0,8,5.3)-(16,16,8) |
| 7 then 14 | (0,8,0)-(2,16,2.7) (14,8,0)-(16,16,2.7) | (0,8,0)-(16,16,2.7) | (0,8,0)-(16,16,2.7) |
| 8 | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) |
| 9 then 0 | (0,0,0)-(16,2.7,16) | (0,0,0)-(16,2.7,16) | (0,0,0)-(16,2.7,16) |
| 9 then 1 | (0,0,0)-(16,4,16) | (0,0,0)-(16,4,16) | (0,0,0)-(16,4,16) |
| 9 then 2 | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) |
| 9 then 3 | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) |
| 9 then 4 | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) |
| 10 | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) |
| 11 | (0,0,0)-(8,8,2) (0,0,14)-(8,8,16) (8,8,0)-(16,16,2) (8,8,14)-(16,16,16) | (0,0,0)-(8,8,16) (8,8,0)-(16,16,16) | (0,0,0)-(8,8,16) (8,8,0)-(16,16,16) |
| 12 | (14,0,0)-(16,8,8) (0,0,0)-(2,8,8) (14,8,8)-(16,16,16) (0,8,8)-(2,16,16) | (0,0,0)-(16,8,8) (0,8,8)-(16,16,16) | (0,0,0)-(16,8,8) (0,8,8)-(16,16,16) |
| 13 | (8,0,14)-(16,8,16) (8,0,0)-(16,8,2) (0,8,14)-(8,16,16) (0,8,0)-(8,16,2) | (8,0,0)-(16,8,16) (0,8,0)-(8,16,16) | (8,0,0)-(16,8,16) (0,8,0)-(8,16,16) |

### Selection boxes

| Pick | Frame | Thatched | Filled |
| --- | --- | --- | --- |
| 0 | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 1 | (0,0,8)-(8,8,16) (0,8,0)-(8,16,8) | (0,0,8)-(8,8,16) (0,8,0)-(8,16,8) | (0,0,8)-(8,8,16) (0,8,0)-(8,16,8) (0,0,0)-(16,16,16) |
| 2 | (8,0,8)-(16,8,16) (8,8,0)-(16,16,8) | (8,0,8)-(16,8,16) (8,8,0)-(16,16,8) | (8,0,8)-(16,8,16) (8,8,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 3 then 0 | (0,0,8)-(16,2.7,16) (0,2.7,0)-(16,5.3,8) | (0,0,8)-(16,2.7,16) (0,2.7,0)-(16,5.3,8) | (0,0,8)-(16,2.7,16) (0,2.7,0)-(16,5.3,8) (0,0,0)-(16,16,16) |
| 3 then 1 | (0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8) | (0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8) | (0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8) (0,0,0)-(16,16,16) |
| 3 then 2 | (0,10.7,8)-(16,13.3,16) (0,13.3,0)-(16,16,8) | (0,10.7,8)-(16,13.3,16) (0,13.3,0)-(16,16,8) | (0,10.7,8)-(16,13.3,16) (0,13.3,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 3 then 3 | (0,0,8)-(16,4,16) (0,4,0)-(16,8,8) | (0,0,8)-(16,4,16) (0,4,0)-(16,8,8) | (0,0,8)-(16,4,16) (0,4,0)-(16,8,8) (0,0,0)-(16,16,16) |
| 3 then 5 | (0,8,8)-(16,12,16) (0,12,0)-(16,16,8) | (0,8,8)-(16,12,16) (0,12,0)-(16,16,8) | (0,8,8)-(16,12,16) (0,12,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 3 then 7 | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 3 then 9 | (0,0,12)-(16,8,16) (0,8,8)-(16,16,12) | (0,0,12)-(16,8,16) (0,8,8)-(16,16,12) | (0,0,12)-(16,8,16) (0,8,8)-(16,16,12) (0,0,0)-(16,16,16) |
| 3 then 11 | (0,0,4)-(16,8,8) (0,8,0)-(16,16,4) | (0,0,4)-(16,8,8) (0,8,0)-(16,16,4) | (0,0,4)-(16,8,8) (0,8,0)-(16,16,4) (0,0,0)-(16,16,16) |
| 3 then 12 | (0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3) | (0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3) | (0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3) (0,0,0)-(16,16,16) |
| 3 then 13 | (0,0,8)-(16,8,10.7) (0,8,5.3)-(16,16,8) | (0,0,8)-(16,8,10.7) (0,8,5.3)-(16,16,8) | (0,0,8)-(16,8,10.7) (0,8,5.3)-(16,16,8) (0,0,0)-(16,16,16) |
| 3 then 14 | (0,0,2.7)-(16,8,5.3) (0,8,0)-(16,16,2.7) | (0,0,2.7)-(16,8,5.3) (0,8,0)-(16,16,2.7) | (0,0,2.7)-(16,8,5.3) (0,8,0)-(16,16,2.7) (0,0,0)-(16,16,16) |
| 4 | (0,8,0)-(16,16,8) | (0,8,0)-(16,16,8) | (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 5 | (0,8,0)-(8,16,8) | (0,8,0)-(8,16,8) | (0,8,0)-(8,16,8) (0,0,0)-(16,16,16) |
| 6 | (8,8,0)-(16,16,8) | (8,8,0)-(16,16,8) | (8,8,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 7 then 0 | (0,2.7,0)-(16,5.3,8) | (0,2.7,0)-(16,5.3,8) | (0,2.7,0)-(16,5.3,8) (0,0,0)-(16,16,16) |
| 7 then 1 | (0,8,0)-(16,10.7,8) | (0,8,0)-(16,10.7,8) | (0,8,0)-(16,10.7,8) (0,0,0)-(16,16,16) |
| 7 then 2 | (0,13.3,0)-(16,16,8) | (0,13.3,0)-(16,16,8) | (0,13.3,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 7 then 3 | (0,4,0)-(16,8,8) | (0,4,0)-(16,8,8) | (0,4,0)-(16,8,8) (0,0,0)-(16,16,16) |
| 7 then 5 | (0,12,0)-(16,16,8) | (0,12,0)-(16,16,8) | (0,12,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 7 then 7 | (0,8,0)-(16,16,8) | (0,8,0)-(16,16,8) | (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 7 then 9 | (0,8,8)-(16,16,12) | (0,8,8)-(16,16,12) | (0,8,8)-(16,16,12) (0,0,0)-(16,16,16) |
| 7 then 11 | (0,8,0)-(16,16,4) | (0,8,0)-(16,16,4) | (0,8,0)-(16,16,4) (0,0,0)-(16,16,16) |
| 7 then 12 | (0,8,10.7)-(16,16,13.3) | (0,8,10.7)-(16,16,13.3) | (0,8,10.7)-(16,16,13.3) (0,0,0)-(16,16,16) |
| 7 then 13 | (0,8,5.3)-(16,16,8) | (0,8,5.3)-(16,16,8) | (0,8,5.3)-(16,16,8) (0,0,0)-(16,16,16) |
| 7 then 14 | (0,8,0)-(16,16,2.7) | (0,8,0)-(16,16,2.7) | (0,8,0)-(16,16,2.7) (0,0,0)-(16,16,16) |
| 8 | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) (0,0,0)-(16,16,16) |
| 9 then 0 | (0,0,0)-(16,2.7,16) | (0,0,0)-(16,2.7,16) | (0,0,0)-(16,2.7,16) (0,0,0)-(16,16,16) |
| 9 then 1 | (0,0,0)-(16,4,16) | (0,0,0)-(16,4,16) | (0,0,0)-(16,4,16) (0,0,0)-(16,16,16) |
| 9 then 2 | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) (0,0,0)-(16,16,16) |
| 9 then 3 | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) (0,0,0)-(16,16,16) |
| 9 then 4 | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) (0,0,0)-(16,16,16) |
| 10 | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) |
| 11 | (0,0,0)-(8,8,16) (8,8,0)-(16,16,16) | (0,0,0)-(8,8,16) (8,8,0)-(16,16,16) | (0,0,0)-(8,8,16) (8,8,0)-(16,16,16) (0,0,0)-(16,16,16) |
| 12 | (0,0,0)-(16,8,8) (0,8,8)-(16,16,16) | (0,0,0)-(16,8,8) (0,8,8)-(16,16,16) | (0,0,0)-(16,8,8) (0,8,8)-(16,16,16) (0,0,0)-(16,16,16) |
| 13 | (8,0,0)-(16,8,16) (0,8,0)-(8,16,16) | (8,0,0)-(16,8,16) (0,8,0)-(8,16,16) | (8,0,0)-(16,8,16) (0,8,0)-(8,16,16) (0,0,0)-(16,16,16) |

## Sealing after thatch and fill

SideIsSolid and GetRetention as in the tool modes table, after the thatch and then the granite fill.

| Pick | Thatched sides solid | Thatched heat retention | Filled sides solid | Filled heat retention |
| --- | --- | --- | --- | --- |
| 0 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 1 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 2 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 0 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 1 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 2 | N----D | 1 0 0 0 0 1 | NE-W-D | 1 0 0 0 0 1 |
| 3 then 3 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 5 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 7 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 9 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 11 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 12 | N----D | 1 0 0 0 0 1 | NE-W-D | 1 0 0 0 0 1 |
| 3 then 13 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 3 then 14 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 4 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 5 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 6 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 0 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 1 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 2 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 3 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 5 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 7 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 9 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 11 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 12 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 13 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 7 then 14 | ------ | 1 0 0 0 0 1 | ------ | 1 0 0 0 0 1 |
| 8 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 9 then 0 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 9 then 1 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 9 then 2 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 9 then 3 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 9 then 4 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 10 | N----D | 1 0 0 0 0 1 | N----D | 1 0 0 0 0 1 |
| 11 | -E---D | 0 1 0 0 0 1 | -E---D | 0 1 0 0 0 1 |
| 12 | --S--D | 0 0 1 0 0 1 | --S--D | 0 0 1 0 0 1 |
| 13 | ---W-D | 0 0 0 1 0 1 | ---W-D | 0 0 0 1 0 1 |

## Gables

Each gable is one slice across, running north and south: the pieces south of the ridge's column rise north, and those north of it rise south.
Each piece is one plank click on granite put under its cell and broken after, built from the upper piece down with the orientation pick, then the manual slope and placement pick.
The ridge goes on last with Roofing's own ridge pick, one level over the upper pieces.
`Cell` is in blocks from the ridge's column at the upper pieces' level, (x,y,z); every roof block within four blocks has a row, and a click whose cell holds no roof has one too.
Boxes are in sixteenths of a block, as above.

| Gable | Cell | Pick | Took the click | Block | Entity keys | Collision | Selection |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 3x1 | (0,0,-3) | 12 then 3 then 0 | the held item | vsroofing:roof-bare-south | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (14,0,0)-(16,2.7,8) (0,0,0)-(2,2.7,8) (14,2.7,8)-(16,5.3,16) (0,2.7,8)-(2,5.3,16) | (0,0,0)-(16,2.7,8) (0,2.7,8)-(16,5.3,16) |
| 3x1 | (0,0,-2) | 12 then 3 then 1 | the held item | vsroofing:roof-bare-south | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[8], SnowCovered 0, BuildNumber 1 | (14,5.3,0)-(16,8,8) (0,5.3,0)-(2,8,8) (14,8,8)-(16,10.7,16) (0,8,8)-(2,10.7,16) | (0,5.3,0)-(16,8,8) (0,8,8)-(16,10.7,16) |
| 3x1 | (0,0,-1) | 12 then 3 then 2 | the held item | vsroofing:roof-bare-south | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (14,10.7,0)-(16,13.3,8) (0,10.7,0)-(2,13.3,8) (14,13.3,8)-(16,16,16) (0,13.3,8)-(2,16,16) | (0,10.7,0)-(16,13.3,8) (0,13.3,8)-(16,16,16) |
| 3x1 | (0,0,1) | 10 then 3 then 2 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (0,10.7,8)-(2,13.3,16) (14,10.7,8)-(16,13.3,16) (0,13.3,0)-(2,16,8) (14,13.3,0)-(16,16,8) | (0,10.7,8)-(16,13.3,16) (0,13.3,0)-(16,16,8) |
| 3x1 | (0,0,2) | 10 then 3 then 1 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[8], SnowCovered 0, BuildNumber 1 | (0,5.3,8)-(2,8,16) (14,5.3,8)-(16,8,16) (0,8,0)-(2,10.7,8) (14,8,0)-(16,10.7,8) | (0,5.3,8)-(16,8,16) (0,8,0)-(16,10.7,8) |
| 3x1 | (0,0,3) | 10 then 3 then 0 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (0,0,8)-(2,2.7,16) (14,0,8)-(16,2.7,16) (0,2.7,0)-(2,5.3,8) (14,2.7,0)-(16,5.3,8) | (0,0,8)-(16,2.7,16) (0,2.7,0)-(16,5.3,8) |
| 3x1 | (0,1,0) | 10 then 8 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Type 2, RidgeInfo[26], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | (0,0,0)-(16,2.7,16) | (0,0,0)-(16,2.7,16) |
| 1x1 | (0,-1,-2) | 12 then 3 then 7 | the held item | vsroofing:roof-bare-south | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (14,0,0)-(16,8,8) (0,0,0)-(2,8,8) (14,8,8)-(16,16,16) (0,8,8)-(2,16,16) | (0,0,0)-(16,8,8) (0,8,8)-(16,16,16) |
| 1x1 | (0,-1,2) | 10 then 3 then 7 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) |
| 1x1 | (0,0,-1) | 12 then 3 then 7 | the held item | vsroofing:roof-bare-south | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | (14,0,0)-(16,8,8) (0,0,0)-(2,8,8) (14,8,8)-(16,16,16) (0,8,8)-(2,16,16) | (0,0,0)-(16,8,8) (0,8,8)-(16,16,16) |
| 1x1 | (0,0,1) | 10 then 3 then 7 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) |
| 1x1 | (0,1,0) | 10 then 8 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Type 2, RidgeInfo[30], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | (0,0,0)-(16,8,16) | (0,0,0)-(16,8,16) |
| 1x3 | (0,-2,-1) | 12 then 3 then 12 | the held item | vsroofing:roof-bare-south | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (14,0,0)-(16,8,2.7) (0,0,0)-(2,8,2.7) (14,8,2.7)-(16,16,5.3) (0,8,2.7)-(2,16,5.3) | (0,0,0)-(16,8,2.7) (0,8,2.7)-(16,16,5.3) |
| 1x3 | (0,-2,1) | 10 then 3 then 12 | the held item | vsroofing:roof-bare-north | Locked true, Info[2], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (0,0,13.3)-(2,8,16) (14,0,13.3)-(16,8,16) (0,8,10.7)-(2,16,13.3) (14,8,10.7)-(16,16,13.3) | (0,0,13.3)-(16,8,16) (0,8,10.7)-(16,16,13.3) |
| 1x3 | (0,-1,-1) | 12 then 3 then 13 | the held item | vsroofing:roof-bare-south | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[8], SnowCovered 0, BuildNumber 1 | (14,0,5.3)-(16,8,8) (0,0,5.3)-(2,8,8) (14,8,8)-(16,16,10.7) (0,8,8)-(2,16,10.7) | (0,0,5.3)-(16,8,8) (0,8,8)-(16,16,10.7) |
| 1x3 | (0,-1,1) | 10 then 3 then 13 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[8], SnowCovered 0, BuildNumber 1 | (0,0,8)-(2,8,10.7) (14,0,8)-(16,8,10.7) (0,8,5.3)-(2,16,8) (14,8,5.3)-(16,16,8) | (0,0,8)-(16,8,10.7) (0,8,5.3)-(16,16,8) |
| 1x3 | (0,0,-1) | 12 then 3 then 14 | the held item | vsroofing:roof-bare-south | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (14,0,10.7)-(16,8,13.3) (0,0,10.7)-(2,8,13.3) (14,8,13.3)-(16,16,16) (0,8,13.3)-(2,16,16) | (0,0,10.7)-(16,8,13.3) (0,8,13.3)-(16,16,16) |
| 1x3 | (0,0,1) | 10 then 3 then 14 | the held item | vsroofing:roof-bare-north | Locked true, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (0,0,2.7)-(2,8,5.3) (14,0,2.7)-(16,8,5.3) (0,8,0)-(2,16,2.7) (14,8,0)-(16,16,2.7) | (0,0,2.7)-(16,8,5.3) (0,8,0)-(16,16,2.7) |
| 1x3 | (0,1,0) | 10 then 8 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], Type 2, RidgeInfo[26], RidgeConnectInfo[8], RidgeRoofInfo[8], RidgeShapeResolved true, Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | (0,0,0)-(16,16,16) | (0,0,0)-(16,16,16) |

## Corners

Two perpendicular roofs, each one Standard pick then an orientation pick, built as the gables are: a roof rising north at (0,0,0), then a roof rising east north of it (behind, uphill) for the outer corner or south of it (in front, downhill) for the inner one.
Every roof block within two blocks has a row.

| Corner | Cell | Pick | Took the click | Block | Entity keys | Collision | Selection |
| --- | --- | --- | --- | --- | --- | --- | --- |
| outer | (0,0,-1) | 0 then 11 | the held item | vsroofing:roof-bare-east | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[8], SnowCovered 0, BuildNumber 1 | (0,0,0)-(8,8,2) (0,0,15)-(8,8,16) (8,8,0)-(16,16,2) (8,8,15)-(16,16,16) | (0,0,0)-(8,8,16) (8,8,0)-(16,16,16) |
| outer | (0,0,0) | 0 then 10 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], CornerType 1, CornerInfo[10], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[14], SnowCovered 0, BuildNumber 1 | (0,0,8)-(16,8,16) (0,0,0)-(8,8,16) (8,8,0)-(16,16,8) (8,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,0,0)-(8,8,16) (8,8,0)-(16,16,8) (8,8,0)-(16,16,8) |
| inner | (0,0,0) | 0 then 10 | the held item | vsroofing:roof-bare-north | Locked false, Info[4], CornerType 2, CornerInfo[10], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[12], SnowCovered 0, BuildNumber 1 | (0,0,8)-(8,8,16) (0,0,8)-(8,8,16) (0,8,0)-(16,16,8) (8,8,0)-(16,16,16) | (0,0,8)-(8,8,16) (0,0,8)-(8,8,16) (0,8,0)-(16,16,8) (8,8,0)-(16,16,16) |
| inner | (0,0,1) | 0 then 11 | the held item | vsroofing:roof-bare-east | Locked false, Info[4], Frame 3830, Tool 5, SideEaveVariant 0, RoofInfo[10], SnowCovered 0, BuildNumber 1 | (0,0,0)-(8,8,2) (0,0,14)-(8,8,16) (8,8,0)-(16,16,2) (8,8,14)-(16,16,16) | (0,0,0)-(8,8,16) (8,8,0)-(16,16,16) |

## A Roofing frame click aimed at a wall's cell

A wall framed in oak, bare or with clay infill, claims its west face, with granite under it and north of it.
The player stands three blocks south with planks in hand, so a roof built there slopes along the wall's plane.
`Admitted` rows mark every roof block hostable in Siding's table for the run; the others leave the table as the mod built it.
`Roof at` is where a roof block stands after the click, from the wall's cell, and `Wall` whether that cell still holds the wall, holds it as a guest or neither.
`Roof broken` is the wall's cell after a roof in it is broken.

| Wall | Click on | Admitted | Took the click | Roof at | Wall | In-game errors | Roof broken |
| --- | --- | --- | --- | --- | --- | --- | --- |
| bare frame | the top of the block below | no | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's room face | no | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's outer face | no | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's top | no | the held item | above | stays | none |  |
| bare frame | the side neighbour's face | no | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the top of the block below | no | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's room face | no | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's outer face | no | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's top | no | the held item | above | stays | none |  |
| clay infill | the side neighbour's face | no | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the top of the block below | yes | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's room face | yes | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's outer face | yes | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's top | yes | the held item | above | stays | none |  |
| bare frame | the side neighbour's face | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the top of the block below | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's room face | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's outer face | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's top | yes | the held item | above | stays | none |  |
| clay infill | the side neighbour's face | yes | the held item | the cell | guest, layers kept | none | wall back |

## Base fill on a hosted roof

A Standard roof frame from a plank click on granite under the cell, in open air or over a wall framed in oak with clay infill claiming its west face, the roof admitted as hostable for the run; then thatch with a sneak click on the roof's south face, or not; then a ctrl click with granite.
The ctrl click lands on the roof's first selection box, on its south face, or on the guest wall's first panel box, on its west face.
A hosted roof takes no off-panel shift, so its own boxes span the cell over the panel's.
`Wall before` is the wall's cell once the roof is built and thatched, and `Key changes` the saved tree's keys the ctrl click added (+), dropped (-) or changed.

| Roof | Wall before | Click on | Took the click | Roof after | Key changes | Collision after | Selection after | Wall after | In-game errors |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| vsroofing:roof-bare-north | no wall | the roof's south face | the clicked block | vsroofing:roof-filled-north | +materials [11812], +decorRot 0, +rotation 368640, +cuboids [7991296 11925504 13892608 15859200 9959520 4059296 6026400 2093280], +emitSideAo [33], +sideSolid [33], +sideAlmostSolid [33], +blockName fill, +availMaterialQuantities [4096] | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) | no wall | none |
| vsroofing:roof-bare-north | guest, layers kept | the roof's south face | the clicked block | vsroofing:roof-filled-north | +materials [11812], +decorRot 0, +rotation 368640, +cuboids [7991296 11925504 13892608 15859200 9959520 4059296 6026400 2093280], +emitSideAo [33], +sideSolid [33], +sideAlmostSolid [33], +blockName fill, +availMaterialQuantities [4096] | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | guest, layers kept | none |
| vsroofing:roof-bare-north | guest, layers kept | the panel's west face | nothing | vsroofing:roof-bare-north | none | (0,0,8)-(2,8,16) (14,0,8)-(16,8,16) (0,8,0)-(2,16,8) (14,8,0)-(16,16,8) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | guest, layers kept | none |
| vsroofing:roof-filled-north | no wall | the roof's south face | the clicked block | vsroofing:roof-filled-north | +materials [11812], +decorRot 0, +rotation 368640, +cuboids [7991296 11925504 13892608 15859200 9959520 4059296 6026400 2093280], +emitSideAo [33], +sideSolid [33], +sideAlmostSolid [33], +blockName fill, +availMaterialQuantities [4096] | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) | no wall | none |
| vsroofing:roof-filled-north | guest, layers kept | the roof's south face | the clicked block | vsroofing:roof-filled-north | +materials [11812], +decorRot 0, +rotation 368640, +cuboids [7991296 11925504 13892608 15859200 9959520 4059296 6026400 2093280], +emitSideAo [33], +sideSolid [33], +sideAlmostSolid [33], +blockName fill, +availMaterialQuantities [4096] | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(16,16,16) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | guest, layers kept | none |
| vsroofing:roof-filled-north | guest, layers kept | the panel's west face | nothing | vsroofing:roof-filled-north | none | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | (0,0,8)-(16,8,16) (0,8,0)-(16,16,8) (0,0,0)-(4,15,2) (0,0,2)-(4,13,4) (0,0,4)-(4,11,6) (0,0,6)-(4,9,8) (0,0,8)-(4,7,10) (0,0,10)-(4,5,12) (0,0,12)-(4,3,14) (0,0,14)-(4,1,16) | guest, layers kept | none |

## A siding frame click aimed at a roof's cell

A roof frame built as in the tool modes table, bare or thatched, with granite under it and east of it, and a siding wall frame clicked at its cell with planks.
`Guest` is the wall the roof's cell holds as its guest after the click.

| Roof | Click on | Took the click | Siding block at | Guest | Roof cell after | In-game errors |
| --- | --- | --- | --- | --- | --- | --- |
| vsroofing:roof-bare-north | the top of the block below | the held item | nowhere | vssiding:wall-wall-north, framing oak | vsroofing:roof-bare-north | none |
| vsroofing:roof-bare-north | the roof's top | the held item | above | none | vsroofing:roof-bare-north | none |
| vsroofing:roof-bare-north | the side neighbour's face | the held item | nowhere | vssiding:wall-wall-north, framing oak | vsroofing:roof-bare-north | none |
| vsroofing:roof-filled-north | the top of the block below | the held item | nowhere | vssiding:wall-wall-north, framing oak | vsroofing:roof-filled-north | none |
| vsroofing:roof-filled-north | the roof's top | the held item | above | none | vsroofing:roof-filled-north | none |
| vsroofing:roof-filled-north | the side neighbour's face | the held item | nowhere | vssiding:wall-wall-north, framing oak | vsroofing:roof-filled-north | none |
