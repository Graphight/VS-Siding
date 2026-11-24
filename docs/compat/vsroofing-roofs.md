# VS Roofing Mod roofs against siding walls

Mod version 2.1.1, game version 1.22.7, probed 2026-11-23.
Nothing here is asserted: each row is what the two mods did on this run.
Every click goes through vanilla's right-click order, the clicked block's interaction first and then the held item's, run on the server with a hammer in the off hand for Roofing and a saw for Siding.
`Took the click` names the step that handled it.
In-game errors are the codes the server sent the player; an error raised on the client alone does not reach this run.

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

## A Roofing frame click aimed at a wall's cell

A wall framed in oak, bare or with clay infill, claims its west face, with granite under it and north of it.
The player stands three blocks south with planks in hand, so a roof built there slopes along the wall's plane.
`Admitted` rows mark every roof block hostable in Siding's table for the run, which no shipped patch does.
`Roof at` is where a roof block stands after the click, from the wall's cell, and `Wall` whether that cell still holds the wall, holds it as a guest or neither.
`Roof broken` is the wall's cell after a roof in it is broken.

| Wall | Click on | Admitted | Took the click | Roof at | Wall | In-game errors | Roof broken |
| --- | --- | --- | --- | --- | --- | --- | --- |
| bare frame | the top of the block below | no | nothing | nowhere | stays | none |  |
| bare frame | the wall's room face | no | the held item | west | stays | none |  |
| bare frame | the wall's outer face | no | the held item | east | stays | none |  |
| bare frame | the wall's top | no | the held item | above | stays | none |  |
| bare frame | the side neighbour's face | no | nothing | nowhere | stays | none |  |
| clay infill | the top of the block below | no | nothing | nowhere | stays | none |  |
| clay infill | the wall's room face | no | the held item | west | stays | none |  |
| clay infill | the wall's outer face | no | the held item | east | stays | none |  |
| clay infill | the wall's top | no | the held item | above | stays | none |  |
| clay infill | the side neighbour's face | no | nothing | nowhere | stays | none |  |
| bare frame | the top of the block below | yes | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's room face | yes | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's outer face | yes | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the wall's top | yes | the held item | the cell | guest, layers kept | none | wall back |
| bare frame | the side neighbour's face | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the top of the block below | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's room face | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's outer face | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the wall's top | yes | the held item | the cell | guest, layers kept | none | wall back |
| clay infill | the side neighbour's face | yes | the held item | the cell | guest, layers kept | none | wall back |

## A siding frame click aimed at a roof's cell

A roof frame built as in the first table, bare or thatched, with granite under it and east of it, and a siding wall frame clicked at its cell with planks.

| Roof | Click on | Took the click | Siding block at | Roof cell after | In-game errors |
| --- | --- | --- | --- | --- | --- |
| vsroofing:roof-bare-north | the top of the block below | the held item | nowhere | vsroofing:roof-bare-north | none |
| vsroofing:roof-bare-north | the roof's top | the held item | above | vsroofing:roof-bare-north | none |
| vsroofing:roof-bare-north | the side neighbour's face | the held item | nowhere | vsroofing:roof-bare-north | none |
| vsroofing:roof-filled-north | the top of the block below | the held item | nowhere | vsroofing:roof-filled-north | none |
| vsroofing:roof-filled-north | the roof's top | the held item | above | vsroofing:roof-filled-north | none |
| vsroofing:roof-filled-north | the side neighbour's face | the held item | nowhere | vsroofing:roof-filled-north | none |
