# Repo Assist Notes

## Last run: 2026-03-30 (run 23728027771)

### Open PRs
- #1665: HTTP encoding ISO-8859-1 → UTF-8 (open since Feb 26)
- #1714: Coverage FAKE target (open since Mar 26)
- New (unnamed): perf: parseString bulk-append JSON optimization
- New (unnamed): docs: clarify dead code branches in JsonConversionsGenerator.fs

### Open Issues
- #1684: Monthly Activity Summary (active)
- #1707: Expired workflow failure (should be closed by maintainer)
- #1671: STJ consideration (active)

### Dead code analysis completed
- JsonConversionsGenerator.fs TypeWrapper.Nullable branches: dead code (JSON inference never produces Nullable)
- JsonGenerator.fs ConvertOptionalProperty branch: dead code (requires InferedType.Json(optional=true) which provider never produces)
- TypeWrapper.Option, false: structurally possible but practically unreachable due to null-stripping

### Backlog
- Monitor #1665 and #1714 for merge
- Further JSON parser performance (STJ as backing parser discussion in #1671)
