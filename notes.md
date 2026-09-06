# Repo Assist Notes

## Key Open PRs (as of 2026-05-01)

- **#1762** (PRIORITY): fix OpenTelemetry.Api >= 1.15.1 — MERGE THIS FIRST; unblocks all other PR CI
- #1758: HTTP response default encoding fix (Closes #1251)
- #1759: 48 StringExtensions tests
- #1763: FS1182 warning fix in JsonSchema
- #1764: 39 XmlRuntime tests
- #1765: net8.0 multi-targeting (fixes NETSDK1212 warnings)
- #1766: JsonValue.WriteTo InvariantCulture fix
- #1768: HtmlNode.serialize perf (CI fixed 2026-05-01)
- #1769: HtmlNode.ToString tests (CI fixed 2026-05-01)
- #1770: JsonExtensions.InnerText tests (CI fixed 2026-05-01)
- #1771: CsvFile transformation tests (CI fixed 2026-05-01)
- #1767: Dependabot NuGet/login 1.1.0 → 1.2.0

## PRs to Close
- #1717, #1754: superseded by #1758

## Issues to Close
- #1752: protected-files resolved
- #1760: superseded by Dependabot PR #1767

## Monthly Summary
- April 2026: issue #1726 (CLOSED)
- May 2026: issue #1772 (CLOSED)
- July 2026: issue #1788 (CLOSED)
- August 2026: issue #1797 (CLOSED 2026-09-06)
- September 2026: created this run (2026-09-06)

## 2026-09-06 Run Notes
- Selected tasks: 1 (Labelling, N/A - 0 unlabelled issues, substituted Task 2),
  4 (Engineering), 10 (Take Repo Forward)
- Task 1: 0 unlabelled issues/PRs exist — substituted with Task 2 style triage:
  reviewed #1781, #1671 for new human activity since last Repo Assist comment —
  none found, not re-engaged (anti-spam)
- Task 4: ran `dotnet paket outdated` — Test group transitive deps significantly
  behind (OpenTelemetry 1.15->1.18, several Microsoft.Extensions.*/System.* patch
  versions). Ran `dotnet paket update group Test`, reverted unrelated CRLF-only
  diff in .paket/Paket.Restore.targets, verified Build + RunTests (3145 tests)
  pass. Created PR (branch repo-assist/eng-paket-lockfile-update-2026-09-06).
- Task 10: reviewed in-progress items — #1781/#1671 both fully answered with no
  implementation started; left as future work since no clear next step without
  maintainer direction on approach.
- #1796 (Fantomas+comment fix PR from 2026-08-30) still open, awaiting review.
- Remaining open substantive issues: #1781, #1671 — both up to date, no new
  human activity, not re-engaged.

## 2026-08-30 Run Notes
- Selected tasks: 4 (Engineering), 3 (Issue Fix), 5 (Coding Improvements)
- Task 3: no issues labelled bug/help-wanted/good-first-issue exist; nothing fixable found
- Task 4+5 (combined, low-risk): PR bumping Fantomas 7.0.1→7.0.6 and fixing stale
  "this hash is v1.1.0" comment in push-master.yml (actual SHA is v1.2.0, per Dependabot
  history — comment went stale after commit 40023654)
- Remaining open substantive issues: #1781 (JSON CsvProvider column type request),
  #1671 (System.Text.Json investigation) — both already have Repo Assist comments,
  no new human activity, not re-engaged (anti-spam)
