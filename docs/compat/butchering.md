# Butchering against a siding wall

Mod version 1.14.3, game version 1.22.7, probed 2026-11-08.
Each row is one block type of the mod, tried in the cell of a wall built for it.
`hosts` means every wall the block took became its guest and came back when the block broke; a block taking more than one wall gives the count.
`refused` names the rule that keeps the block out of a wall's cell.

| Block | Verdict |
| --- | --- |
| butcherhook | hosts |
| butcherhookceiling | hosts |
| butchertable | hosts (3 cells) |
| ivoryblock | refused: solid side |
| smokingrack | hosts |
