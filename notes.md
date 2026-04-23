# Repo Assist Notes

## Last run: 2026-04-23 (run 24817020698)

### Open PRs
- #1754: HTTP encoding ISO-8859-1 to UTF-8 (breaking change → 8.1.11, Closes #1251) — clean rebase; all CI passing. Awaiting maintainer review/merge decision.
- #1717: HTTP encoding (old branch with protected files) — should be closed (superseded by #1754)
- #1755: Dependabot bump actions/github-script — minor CI dep update, noted to maintainer
- #1756: HtmlParser dead code removal + ToLowerInvariant; all CI passing; awaiting review
- branch repo-assist/test-htmlinference-2026-04-23: 23 tests for HtmlInference.inferListType and inferHeaders; PR created this run; 2980 tests pass

### Open Issues
- #1671: Consider System.Text.Json dependency (no new human activity; commented Feb 2026)
- #1752: Protected files (about PR #1717) — can be closed now #1754 exists
- #1726: April 2026 Monthly Activity Summary (updated this run)

### Backlog
1. Monitor HtmlInference test PR and #1756 for merge
2. PR #1754 (HTTP encoding fix) — awaiting @dsyme decision
3. Close PR #1717 — maintainer action needed
4. Close issue #1752 — maintainer action needed
5. Continue monitoring #1671 for human activity
