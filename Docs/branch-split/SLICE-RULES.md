# Slice porting rules

Rules every session (human or AI) follows when porting a slice from the source branch onto its own `split/NN-*`
branch. The plan and status live in [`../branch-split-assessment.md`](../branch-split-assessment.md).

## Locations

| What | Where |
|---|---|
| Source branch (read-only) | `experimental/game-completion-candidate-vibecoded-borobongo`, checked out detached at `Gothic-workspace/GU-split-00-source` |
| Slice worktrees | `Gothic-workspace/GU-split-NN-name` on branch `split/NN-name` (based on `origin/main` for Wave 1) |
| Tools | `Gothic-workspace/_split-tools/` (outside the repo) |
| Commit → slice map | `_split-tools/slice_map.txt` (`<commit> <primary slice> [secondary slices]`, `xx` = not ported) |
| Line ownership per file | `_split-tools/ownership.tsv` (from `python blame_slices.py table`) |
| PR descriptions | `Gothic-Unity/Docs/branch-split/pr/NN-name.md` (untracked planning files) |

Each worktree has `Assets/HurricaneVR` as a junction to the main checkout and a copy of `GameSettings.dev.json`, so it
opens in Unity as is (the first import builds its own `Library/`).

## Tool gotchas (learned in Wave 1)

- Lines labeled `MRG` by `blame_slices.py` were written while resolving merge conflicts on the source branch. They
  are **not** on `main`. Assign them by hand.
- Git Bash `sed -i` turns CRLF files into LF. Use the Edit tool, or `_split-tools/edit_crlf.py`, which keeps the
  line endings and BOM.

## Workflow per slice

1. **Find the slice's code.** Read the slice rows in `slice_map.txt` (primary and secondary) and in `ownership.tsv`.
   `python _split-tools/blame_slices.py show <NN> <file>` prints a file in its final state with this slice's lines
   marked `>>` and other slices' lines marked with their id. Lines a slice *removed* from `main` don't show in blame:
   check `git diff origin/main...<source> -- <file>` and the slice's commits (`git show <commit> -- <file>`).
2. **Port the final state, not the history.** Files that only this slice touches (or creates) are taken whole:
   `git checkout <source> -- <file> <file>.meta`. Never forget the `.meta` of a new file or folder.
3. **Shared files: take only this slice's hunks.** Leave lines of other slices out. Lines from a later slice that
   this slice's code needs are a sign the line is mis-attributed: then it belongs to this slice (note it in the
   PR file).
4. **Secondary slices.** A commit with a secondary slice (e.g. `a47917da 09 04,11,20`) is split by content: each
   slice takes the part that matches its theme. If unsure, take the smallest working part and note it.
5. **No references to later slices.** The result compiles and works on its own. If the ported code needs
   something from a not-yet-ported slice, cut the dependent bit out (prefer removing over stubbing). Mark a
   deliberate gap with `// TODO(split: NN)` only if the later slice must re-add something at that exact spot.
6. **Compile:** `python _split-tools/compile_check.py <worktree>` must print `RESULT: OK` (builds Core, VR,
   Flat, G1, G2, Lab, PlayMode tests, VR editor). Never touch other worktrees.
7. **Commit** in the worktree in a few logical commits (`feat:`/`fix:`/`chore:` style like the repo), each ending
   with two co-author lines, in this order: `Co-Authored-By: BoroBongo <borobongo@gmail.com>` (author of the source
   branch), then `Co-Authored-By: Claude …`. **Never add a `Claude-Session:` line or session URL** (public repo, maintainer's
   decision 2026-10-11). Do **not** push and do not open PRs.
8. **Write the PR description** to `Gothic-Unity/Docs/branch-split/pr/NN-name.md` (template below).

## Code quality bar (human code review follows)

Follow `CLAUDE.md` and the wiki Coding Style Guide (`Gothic-Unity.wiki/Get-Started/Coding Style Guide.md`):
Allman braces, 4 spaces, CRLF, max 120 chars per line, `_camelCase` private fields, `PascalCase` public, booleans
with verb prefix, `[Tooltip]` instead of field comments, no regions, one class per file, namespaces = folders,
`Logger.Log(message, LogCat.X)` for all logs, comments explain *why* (link OpenGothic / scripts where it helps).

Port behavior faithfully (the source was play-tested), but fix these maintainability issues inside the slice when
cheap and behavior-preserving:
- lines over 120 characters (except generated tables like Bink's),
- several types in one file → one file per type,
- static `_instance` singletons → DI (`[Inject]` / registration in the scene installer) when the class is in the slice,
- fully qualified type names where a `using` fits, unused `using`s, leftover debug logs ("diag", spam),
- dead code, commented-out code, and code paths only reachable through flags that this slice drops (see below).
Do **not** do structural refactors (splitting god classes, moving logic out of big adapters); those are follow-up
PRs after the feature is on `main`. Exception: a new external group may go into a new file if that keeps the
shared file from growing (note it).

### Flag policy (decided 2026-10-08, can be revisited)

`DeveloperConfig` got 116 new fields. In the split:
- **Keep** flags that switch a *feature* or a *VR design choice* on/off, flags that are an escape hatch for a known
  crash or for performance (e.g. `EnableMusic`), and all tunables (distances, multipliers, sizes, timings).
- **Drop** flags that only guard a *bug fix / engine-parity fix* whose "on" path is what `Production.asset` uses.
  Keep the "on" code path only. This removes dead branches before they reach `main`.
- Every kept flag gets a `[Tooltip]`, sits under one `[Separator("…")]` block per slice, and its `Production.asset`
  value matches the source branch's.
- List kept and dropped flags in the PR description.

## PR description template

```markdown
# <NN> <title>

Branch: `split/NN-name` (worktree `GU-split-NN-name`), based on `<base>`.

## What
<bullets: user-visible behavior, grouped>

## Why
<one paragraph: the problem in main / engine parity reference>

## Config
Kept flags: … · Dropped flags (fix always on): … · Production.asset: …

## How to test (VR, ~10-15 min)
1. …

## Review notes
- Source commits: <list>
- Hunks left for other slices / judgement calls: …
- Deviations from the source (maintainability fixes): …
- Known limitations / risks: …
```
