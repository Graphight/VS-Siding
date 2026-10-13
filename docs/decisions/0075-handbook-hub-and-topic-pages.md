# 0075 — Handbook hub and topic pages

- Status: Accepted
- Created: 2026-10-12
- Reflects: branch `handbook-hub-pages`; `VSSiding/assets/vssiding/config/handbook/*.json`, the `gamemechanicinfo-siding-*` keys of `lang/en.json`; `HandbookPagesTests`, `HandbookScenarios`; decisions 0029, 0040, 0058; decompiled 1.22 `VtmlUtil.Richtextify`, `VtmlParser`, `IconComponent`; played on 2026-10-12

## Summary
The handbook is eight pages: the Guide as a hub, and seven topic pages it links to.
Pages use a small fixed set of tags, and two tests guard the keys, links, icons and item codes.

## Context
The three guide pages grew a sentence per feature PR.
They reached about 4,000, 2,400 and 1,600 characters of mostly unbroken paragraphs, using only `<strong>`, `<i>`, `<br>` and `<a>`.
The handbook was a gate for 1.4.0.

## Design
Eight pages, one file each under `config/handbook/`, every title prefixed `VS Siding Mod: `.

| File | pageCode | Title |
|---|---|---|
| `00-siding.json` | `gamemechanicinfo-siding` | Guide |
| `01-siding-walls.json` | `gamemechanicinfo-siding-walls` | Layers and materials |
| `02-siding-floors.json` | `gamemechanicinfo-siding-floors` | Floors and decks |
| `03-siding-modes.json` | `gamemechanicinfo-siding-modes` | Picker modes |
| `04-siding-corners.json` | `gamemechanicinfo-siding-corners` | Corners and junctions |
| `05-siding-rooms.json` | `gamemechanicinfo-siding-rooms` | Rooms, cellars and light |
| `06-siding-furniture.json` | `gamemechanicinfo-siding-furniture` | Furniture and lights in walls |
| `07-siding-stoneage.json` | `gamemechanicinfo-siding-stoneage` | Starting in the stone age |

The Guide is a hub: an opening, the three layers, a numbered first wall, and a contents list.
The three original page codes are kept, because `blockdesc-wall-*` and `blockdesc-floor-*` link to them.
No C# outside the test projects.

**The tag kit.**
- Page title, first line: `<strong>Title</strong>`.
- Section heading: `<font scale="120%"><strong>Heading</strong></font>` on its own line, a blank line above it.
- A material: its icon then its name, `<itemstack type="item">plank-oak</itemstack> <i>planks</i>`, with several codes joined by `|` where a family is meant.
- A picker option: `<font scale="150%"><icon path="vssiding:icons/wall.svg"></icon></font> <i>Wall</i>`.
- The picker's key: `<hk>toolmode</hk>`.
- A list item: `• ` then one sentence, one idea.
- Last line: a link back to the Guide, `<a href="handbook://gamemechanicinfo-siding">Guide</a>`.

**What the renderer does.**
Read from decompiled 1.22 `VtmlUtil.Richtextify`, `VtmlParser` and `IconComponent`:
- `<br>` is the only tag the parser accepts unclosed.
- `<icon path>` draws an SVG under `textures/`, tinted to the font colour, at 0.7 of the font size.
- An `<itemstack>` with an unknown code draws block 0 and logs nothing.

**Two guards.**
- `VSSiding.Tests/HandbookPagesTests.cs` checks that the keys exist, links resolve, every page links back to the Guide and the Guide links to every page, icon paths are real files, and tags are known and closed.
- `VSSiding.E2E.Tests/HandbookScenarios.cs` checks that every `<itemstack>` code resolves to a real item or block on the headless server, since an unknown code fails silently.
  The scenario was checked by misspelling one code, which made it fail naming that code.

**Content rule.**
Facts were moved between pages, not dropped.
The one added fact is that a new frame gets a deck while Deck is picked, which the picker already implies.

**What this supersedes in 0029.**
Decision 0029 dropped `<hk>` because nothing was a bound key.
The picker now opens on the tool mode hotkey (decisions 0040, 0058), so the pages use `<hk>toolmode</hk>`.
Decision 0029 recorded that the picker icons could not be inlined.
`<icon path>` inlines them.

## Alternatives considered
- **Keeping three pages and only reformatting them.** The scrolls stay longer; the user chose the hub.
- **Screenshots through a custom tag registered with `TagConverters`.** It needs C# and assets; the user chose icons only.
- **A code-registered page.** Decision 0029 already rejected it.

## Consequences & open questions
- Played on 2026-10-12, as the PR's checklist records: all eight pages read at the default GUI scale and one larger, the heading size, the picker icon size beside text, the tool mode key drawing, every item icon and link, and where the pages sort in the Guides list.
  The build played was the one before the last copy sweep, which changed sentences and no tags.
  Each size is a one-line change in `en.json`.
- The pages hard-code material and mode lists, so a new mode or material still means editing prose, now on a named page.
