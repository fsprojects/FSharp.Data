# Repo Assist Notes

## Last run: 2026-04-03 (run 23933778819)

### Open PRs
- #1717: HTTP encoding ISO-8859-1 → UTF-8 (CI failing with Windows port binding; the new test fix PR below should fix this when rebased)
- New (Task 4): test: use TcpListener(0) for reliable free-port selection in HTTP test server
- New (Task 5): perf: use HashSet<char> and String.exists for adorner detection in TextConversions

### Closed this run context
- March 2026 Monthly Summary (#1684): closed this run
- April 2026 Monthly Summary: created this run

### Open Issues
- April 2026 Monthly Activity Summary: just created (this run)
- #1671: STJ consideration (active, enhancement)

### Test suite
- 2896 tests as of this run

### Backlog
- Monitor PR #1717 for merge (HTTP encoding fix, endorsed by @dsyme)
  - Windows CI failure on this PR is a flaky port binding issue; the Task 4 PR (TcpListener fix) addresses this
  - If #1717 is rebased to include the Task 4 test fix, CI should pass
- Consider STJ investigation from #1671
