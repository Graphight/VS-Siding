# Design docs

Feature-level design notes: enough structure to capture *why* something is shaped the way it is, without becoming a chore for a one-person mod.

Two stages, split by directory so a doc's status is obvious from where it lives:

- **`proposals/`**: an idea, not yet acted on. Mutable. Edit freely, argue in the doc, change your mind.
- **`decisions/`**: numbered, what actually shipped (or was deliberately rejected). Accepted decisions are immutable.

A proposal **graduates** into a decision when we act on it: give it the next `NNNN`, move it to `decisions/`, set its status to `Accepted`, delete the proposal file.

## What goes here

The reasoning behind a feature, and above all what we deliberately *didn't* do. Point to the code for *how* something works; the code is what stays up to date.

What does **not** go here: current-state "how it works" docs (those belong next to the code, or in `CLAUDE.md`/`README.md`), and ticket/handoff/scratch notes.

## Avoiding staleness

- Once a decision is **Accepted, don't rewrite it.** If the choice changes, write a new decision and mark the old one `Superseded by NNNN`.
- The `Reflects` header says what evidence/session/commits the doc describes.

## Conventions

- One file per feature/idea.
  - Decisions: `decisions/NNNN-slug.md`, numbered in order, permanent.
  - Proposals: `proposals/slug.md`, no number (numbering happens on graduation).
- Status: `Draft` | `Accepted` | `Superseded by NNNN` | `Withdrawn`.
- `docs/proposals/README.md` lists what's open, in build order - the only index maintained by hand. Decisions are not indexed anywhere; `ls docs/decisions/` is the list.

## Template

```markdown
# NNNN — Title

- Status: Draft | Accepted | Superseded by NNNN | Withdrawn
- Created: YYYY-MM-DD
- Reflects: <branch / commits / session this describes>

## Summary
One or two sentences: what this is.

## Context
The problem and the constraints. Why we're touching this at all.

## Design
The decisions made and the *why* behind each. Link to code for the *how*.

## Alternatives considered
What was rejected and why. The highest-value section for future readers.

## Consequences & open questions
What this enables, what it costs, what might bite us later.
```
