# Repo Assist Notes

## Last run: 2026-04-01 (run 23832103853)

### Open PRs
- #1665: HTTP encoding ISO-8859-1 → UTF-8 (SUPERSEDED by new PR from this run, should be closed)
- #1714: Coverage FAKE target (open since Mar 26)
- #1715: perf: parseString bulk-append JSON optimization (from Mar 30)
- #1716: docs: clarify dead code branches in JsonConversionsGenerator.fs (from Mar 30)
- New (this run): fix: change HTTP encoding ISO-8859-1 → UTF-8 (rebase of #1665, Closes #1251); branch repo-assist/fix-issue-1251-http-response-default-utf8-rebase
- New (this run): test: fix missing [<Test>] on Multiline comment test + 3 new comment parser tests; branch repo-assist/test-fix-missing-test-attrs

### Open Issues
- New: April 2026 Monthly Activity Summary (just created this run)
- #1707: Expired workflow failure (should be closed by maintainer)
- #1671: STJ consideration (active)

### Test suite
- 2900 tests as of this run (was 2896 before; 4 new tests added this run)
- `Multiline comment is skipped` test was missing [<Test>] - now fixed

### Backlog
- Monitor PRs #1714, #1715, #1716 for merge
- The new PRs from this run (HTTP encoding rebase, test fix) need review
- PR #1665 should be closed (superseded)
- Issue #1707 should be closed (expired)
- Further JSON parser performance (STJ as backing parser discussion in #1671)
