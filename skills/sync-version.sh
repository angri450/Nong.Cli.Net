#!/bin/bash
# Toolkit.Net version sync — zero dependencies (bash + sed only)
# Updates all references to the Nong.Cli.Net version across docs.
# Does NOT touch plugin.json versions (those are independent marketplace versions).
#
# Usage: ./sync-version.sh 12.1.0
set -euo pipefail
NEW="${1:?Usage: $0 <version>  e.g.  $0 12.1.0}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
echo "=== Toolkit.Net → sync to Nong.Cli.Net $NEW ==="

# 1. CLAUDE.md — version line + transition description
sed -i "s|适配 [0-9.]*|适配 $NEW|g; s|\(v[0-9.]*→\)v[0-9.]*|\1v$NEW|g" "$ROOT/CLAUDE.md"
echo "  CLAUDE.md"

# 2. README files — CLI version + command count line
for f in "$ROOT/README.md" "$ROOT/README.zh-CN.md"; do
    [ -f "$f" ] || continue
    sed -i "s|Nong\.Cli\.Net [0-9.]* / [0-9]*|Nong.Cli.Net $NEW / 332|g" "$f"
    echo "  $(basename "$f")"
done

# 3. Reference docs with inline examples mentioning CLI version
for f in "$ROOT/excel/references/read-excel.md" "$ROOT/ocr/references/ocr-local.md"; do
    [ -f "$f" ] || continue
    sed -i "s|\"version\": \"[0-9.]*\"|\"version\": \"$NEW\"|g; s|Nong [0-9.]*, expect|Nong $NEW, expect|g" "$f"
    echo "  $(basename "$f")"
done

echo ""
echo "done. Plugin versions (plugin.json) are NOT touched — they track marketplace releases independently."
echo "CLI version refs updated. Verify: grep -rn '$NEW' --include='*.md' ."
