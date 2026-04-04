# Repo Assist Notes

## Last run: 2026-04-04 (run 23971245027)

### Open PRs
- #1717: HTTP encoding ISO-8859-1 → UTF-8 (Closes #1251; endorsed by dsyme; rebased with TcpListener fix merged from main - Windows CI should pass now)
- New CSV PR (~#1727): perf: reuse StringBuilder and ResizeArray in CSV parser; 2896 tests pass

### Recently merged
- #1724: test: use TcpListener(0) for free-port selection (merged 2026-04-03)
- #1725: perf: HashSet+String.exists for adorner detection (merged 2026-04-03)
- #1723: fsdocs 22.0.0-alpha.3 + SDK 10.0.201 (merged 2026-04-03)
- #1716: JSON codegen dead-code comment cleanup (merged 2026-04-01)

### Open Issues
- April 2026 Monthly Activity Summary #1726: updated this run
- #1671: Consider System.Text.Json dependency (enhancement; Repo Assist commented previously; no new human comments)

### Test suite
- 2896 tests as of this run

### Backlog
- Monitor PR #1717 for merge (HTTP encoding fix, endorsed by @dsyme; Windows CI should now pass after rebase)
- Monitor new CSV PR (~#1727) for merge
- Consider STJ investigation from #1671
