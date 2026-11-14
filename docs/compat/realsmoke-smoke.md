# Real Smoke against siding walls and floors

Mod version 2.0.0-pre.13, game version 1.22.7, probed 2026-11-14.

## Where smoke goes

Each row lights a firepit in a stone box whose only way out is the block under test, and reads the smoke Real Smoke holds in a second stone box the block opens onto, for 102 seconds of game time.
`smoke passes` means some smoke reached the second box, with the most it held at one reading; `smoke held` means none did.
The open rows are the controls: smoke crosses open air and stone stops it.
A wall's room face is the one it claims, so a fire on it is a fire inside the room.
The last column is what the block under test answers for the face towards the fire and the face towards the second box.

| Arrangement | Smoke | Faces |
| --- | --- | --- |
| control: open cell | smoke passes (60) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| control: stone | smoke held (0) | fire side barrier 1, side solid true; far side barrier 1, side solid true |
| wall, bare frame, fire on the outer face | smoke passes (52) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| wall, clay infill, fire on the outer face | smoke held (0) | fire side barrier 0, side solid false; far side barrier 1, side solid true |
| wall, wattle infill, fire on the outer face | smoke held (0) | fire side barrier 0, side solid false; far side barrier 1, side solid true |
| vent wall, trapdoor infill, fire on the outer face | smoke passes (70) | fire side barrier 0, side solid false; far side barrier 1, side solid false |
| wall, glass infill, fire on the outer face | smoke held (0) | fire side barrier 0, side solid false; far side barrier 1, side solid true |
| wall, clay infill hosting a lantern, fire on the outer face | smoke held (0) | fire side barrier 0, side solid false; far side barrier 1, side solid true |
| wall, bare frame, fire on the room face | smoke passes (60) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| wall, clay infill, fire on the room face | smoke held (0) | fire side barrier 1, side solid true; far side barrier 0, side solid false |
| wall, wattle infill, fire on the room face | smoke held (0) | fire side barrier 1, side solid true; far side barrier 0, side solid false |
| vent wall, trapdoor infill, fire on the room face | smoke passes (44) | fire side barrier 1, side solid false; far side barrier 0, side solid false |
| wall, glass infill, fire on the room face | smoke held (0) | fire side barrier 1, side solid true; far side barrier 0, side solid false |
| wall, clay infill hosting a lantern, fire on the room face | smoke held (0) | fire side barrier 1, side solid true; far side barrier 0, side solid false |
| control: open cell above | smoke passes (154) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| control: stone above | smoke held (0) | fire side barrier 1, side solid true; far side barrier 1, side solid true |
| floor, bare joists | smoke held (0) | fire side barrier 0, side solid false; far side barrier 1, side solid true |
| floor, clay infill | smoke held (0) | fire side barrier 0, side solid false; far side barrier 1, side solid true |
| wall deck, bare | smoke held (0) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| wall deck, clay infill | smoke held (0) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| vent floor, trapdoor | smoke passes (102) | fire side barrier 0, side solid false; far side barrier 1, side solid true |
| vent wall deck, trapdoor | smoke passes (162) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| roof of a wooden trapdoor | smoke held (0) | fire side barrier 1, side solid true; far side barrier 0, side solid false |
| roof of a wooden window trapdoor | smoke held (0) | fire side barrier 0, side solid true; far side barrier 0, side solid false |
| roof of a barred trapdoor | smoke held (0) | fire side barrier 0, side solid true; far side barrier 0, side solid false |
| roof of a stick layer | smoke passes (100) | fire side barrier 0, side solid false; far side barrier 0, side solid false |
| chimney course over a clay floor | smoke held (0) | fire side barrier 0, side solid false; far side barrier 1, side solid true |

## Room retention under a flue

A one-cell room with clay-filled walls and a planks floor, its roof planks except the cell above the room, which holds the block named.

| Roof cell | Room | Counts |
| --- | --- | --- |
| planks roof (control) | sealed | exits 0, skylights 0, warm walls 2 |
| no roof cell | leaks | exits 1033, skylights 225, warm walls 203 |
| vent floor, trapdoor | sealed | exits 0, skylights 1, warm walls 6 |
| wooden trapdoor | sealed | exits 0, skylights 0, warm walls 2 |
| wooden window trapdoor | leaks | exits 1029, skylights 225, warm walls 226 |
| barred trapdoor | leaks | exits 1029, skylights 225, warm walls 226 |
| stick layer | leaks | exits 1049, skylights 225, warm walls 146 |

## Fires hosted in a wall's cell

Each is placed into the cell of a framed and clay-filled wall by a click, as furniture is. The firepit is lit and burns for the whole run.

| Block | Result |
| --- | --- |
| firepit | hosts, wall layers intact |
| forge | hosts, wall layers intact |
| clayoven | refused: solid side |
