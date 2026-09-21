# Handbook page

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning, after decision 0027

## Summary
A "Guides" page in the survival handbook that teaches the whole build flow — saw in the off hand, the four modes, layer by layer — plus a one-paragraph description on the wall block's own handbook page.

## Context
Nothing in game tells you the mod exists.
The gesture is a plank in the main hand and a saw in the *off* hand (decisions 0005/0006), which nobody guesses; the four tool modes are unlabelled in the UI until the icons land (`mode-icons`); and the peel-one-layer break (0013) and the corner upgrade (0026) are both invisible mechanics.
The mod ships 500-odd material keys and a player who can't find the first click sees none of them.

Vanilla's own format is cheap: `assets/<domain>/config/handbook/NN-slug.json` holding `{ pageCode, title, text }`, two lang keys, and the game's loader picks up `config/handbook` from every domain.
Text is the game's rich subset — `<strong>`, `<br>`, `<i>`, `<hk>shift</hk>` for key names, and `<a href="handbook://block-wall-wall-west">` to link a block page.
Confirmed against 1.22.2's `assets/survival/config/handbook/` and the `config/handbook` asset path inside `VSSurvivalMod.dll`.

## Design
One page, `assets/vssiding/config/handbook/00-siding.json`, `pageCode: "gamemechanicinfo-siding"`, landing in the Guides category alongside vanilla's game-mechanic pages.
Sections, in the order a player needs them:

1. **What a wall is** — framing, infill, two face finishes; a quarter-block panel, not a full block.
2. **Building one** — plank in hand, saw in the off hand, mode picker on the saw, click the cell; then infill, then a finish per face.
3. **The modes** — wall, corner, weatherboard, flat boards, each with its icon once `mode-icons` lands, and corner's in-place upgrade of a bare frame.
4. **Rooms and cellars** — framing plus infill seals; the infill decides cooling (decision 0015). Glazing seals without going dark (0019).
5. **Taking one apart** — breaking peels the outermost layer of the face you hit (0013).
6. **Known quirks** — furniture can't enter the wall's empty three-quarter cell; the two-cell back-to-back partition is the workaround (`furniture-against-thin-walls`).

Plus `blockdesc-wall-*` in `en.json`, the key vanilla reads for a block's handbook blurb, pointing at the guide page.

Prose lives in `en.json` under `vssiding:gamemechanicinfo-siding-title` / `-text`, so translation needs no code.

## Alternatives considered
- **A code-registered `GuiHandbookTextPage`.** More control, a client-side dependency, and no reason: the JSON page is the same thing with no C#.
- **Per-material handbook pages.** Materials are attribute-dictionary entries, not blocks (decision 0001), so there is nothing for the handbook to index. Naming the families in prose is enough.
- **A tutorial-category page with live steps.** Vanilla's tutorial system tracks progress; that's a system to learn for a mod with one gesture.
- **Leaving it to the ModDB page.** Nobody reads the web page while holding a saw.

## Consequences & open questions
- The page hard-codes the mode list and the finish styles, so a new mode means editing prose. Cheap, and the alternative is generating text from attributes, which reads like a spreadsheet.
- Do the `<hk>` tags resolve for a *hotbar* off-hand slot, which is not a bound key? If not, say "off-hand slot" in plain words.
- Handbook text has no images; the mode icons can't be inlined. If that hurts, the ModDB page carries screenshots instead.
