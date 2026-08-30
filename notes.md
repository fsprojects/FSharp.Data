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
- August 2026: created this run (2026-08-30)

## 2026-08-30 Run Notes
- Selected tasks: 4 (Engineering), 3 (Issue Fix), 5 (Coding Improvements)
- Task 3: no issues labelled bug/help-wanted/good-first-issue exist; nothing fixable found
- Task 4+5 (combined, low-risk): PR bumping Fantomas 7.0.1→7.0.6 and fixing stale
  "this hash is v1.1.0" comment in push-master.yml (actual SHA is v1.2.0, per Dependabot
  history — comment went stale after commit 40023654)
- Remaining open substantive issues: #1781 (JSON CsvProvider column type request),
  #1671 (System.Text.Json investigation) — both already have Repo Assist comments,
  no new human activity, not re-engaged (anti-spam)
