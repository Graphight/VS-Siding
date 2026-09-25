# Contributing

## Branches and PRs

**Never commit directly to `main`.**
Branch, push, open a PR, squash-merge.
That holds even for a one-line docs fix and even when working alone: every change gets a diff someone (including future-you) can read after the fact.

```sh
git checkout -b <short-topic-name>
# ... work, in small cherry-pickable commits ...
./build.sh
git push -u origin HEAD
gh pr create
```

Commit subjects are one line and follow [Conventional Commits](https://www.conventionalcommits.org): `type: subject`.

```
feat:     new behaviour someone can use
fix:      a defect in shipped behaviour
docs:     decisions, proposals, READMEs, anything under docs/ or a *.md
chore:    layout, tooling, config; no behaviour change
refactor: same behaviour, different shape
test:     tests only
```

No other prefix, ever.

## Design docs

Design history lives in `docs/`:

- `docs/proposals/`: an idea, not yet acted on. Status: Draft.
- `docs/decisions/`: numbered, what actually shipped or was deliberately rejected. Status: Accepted.

**Read the relevant decisions before changing anything the rest of the code depends on.** They record what was already tried and thrown away.

Accepted decisions are immutable. To change one, add the next-numbered decision and mark the old one `Superseded by NNNN`. Don't rewrite it.

Keep the *why* in `docs/decisions/`, not restated in source comments.

## Before you open a PR

```sh
./build.sh
```

Check the real exit status: a truncated or piped log can look reassuring and still exit non-zero.

## Conventions

- Assert on whole objects in tests, not field-by-field.
- No unrequested comments; a comment earns its place by saying what a reader needs at that line, not by restating what the code does.
