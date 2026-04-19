# Repo Assist Notes

## Last run: 2026-04-19 (run 24621084715)

### Open PRs
- #1717: HTTP encoding ISO-8859-1 to UTF-8 (breaking change → 9.0.0, Closes #1251) — merged main into branch this run; all 3740 tests pass. Awaiting maintainer review/merge decision.
- #1750: ci: automatically create GitHub releases on push to main (addresses #1742); opened by @dsyme — awaiting review
- #1751: release: bump version to 8.1.9 — SHA1 disposal, WorldBank retry, TypeProviders.SDK update — created 2026-04-18

### Open Issues
- #1671: Consider System.Text.Json dependency (no new human activity; commented Feb 2026)
- #1726: April 2026 Monthly Activity Summary (updated this run)
- #1742: Automate GitHub releases — active discussion; @dsyme questioning whether needed; last Repo Assist comment 2026-04-18

### Recently merged (since 8.1.8)
- #1749: fix: WorldBank retry delay (merged 2026-04-17 by @dsyme)
- #1745: fix: SHA1 disposal in Caching.hashString (merged 2026-04-17 by @dsyme)
- #1747: test: 37 StructuralInference tests + CI SDK version from global.json (merged 2026-04-17 by @dsyme)
- #1743: Dependabot: Bump actions/upload-artifact 7.0.0→7.0.1 (merged 2026-04-17 by @dsyme)
- #1741: eng: update FSharp.TypeProviders.SDK to 75ac6119 (merged 2026-04-17 by @dsyme)
- #1739: improve: fast-path in niceCamelName/capitalizeFirstLetter/Pluralizer (merged 2026-04-17 by @dsyme)

### Backlog
1. Monitor release PR #1751 for merge
2. Monitor GitHub releases PR #1750 - may be declined given @dsyme's questions
3. PR #1717 (HTTP encoding fix, breaking 9.0.0) — now up to date with main; await @dsyme decision
4. Continue monitoring #1671 for human activity
