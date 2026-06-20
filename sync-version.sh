#!/bin/bash
# Nong.Cli.Net version sync — updates ALL version references across code + docs + csproj
# Usage: ./sync-version.sh 12.2.0
set -euo pipefail
NEW="${1:?Usage: $0 <version>  e.g.  $0 12.2.0}"
MAJOR_MINOR=$(echo "$NEW" | cut -d. -f1,2)
ROOT="$(cd "$(dirname "$0")" && pwd)"
echo "=== Nong.Cli.Net → $NEW ==="

# ── 1. All 24 csproj <Version> tags ──
while IFS= read -r f; do
    old=$(grep -oP '<Version>\K[0-9.]+(?=</Version>)' "$f" | head -1)
    [ -z "$old" ] && continue
    sed -i "s|<Version>$old</Version>|<Version>$NEW</Version>|" "$f"
    echo "  csproj: $(basename "$(dirname "$f")")/$(basename "$f")  $old → $NEW"
done < <(find "$ROOT" -name "*.csproj" -not -path "*/.git/*" -not -path "*/bin/*" -not -path "*/obj/*")

# ── 2. CliVersion.cs ──
sed -i "s|Current = \"[0-9.]*\"|Current = \"$NEW\"|" "$ROOT/Cli/Common/CliVersion.cs"
echo "  CliVersion.cs → $NEW"

# ── 3. UserAgent strings ──
sed -i "s|Nong-Aminer/[0-9.]*|Nong-Aminer/$MAJOR_MINOR|" "$ROOT/Aminer/AminerClient.cs"
sed -i "s|Nong-Metaso/[0-9.]*|Nong-Metaso/$MAJOR_MINOR|" "$ROOT/Metaso/MetasoClient.cs"
echo "  UserAgent → $MAJOR_MINOR"

# ── 4. SearchCommands hardcoded version ──
sed -i "s|\"version\": \"[0-9.]*\"|\"version\": \"$NEW\"|" "$ROOT/Cli/Commands/SearchCommands.cs"
echo "  SearchCommands.cs → $NEW"

# ── 5. LiteratureVersion ──
sed -i "s|LiteratureVersion.*=.*\"[0-9.]*\"|LiteratureVersion { get; set; } = \"$NEW\"|" "$ROOT/Literature/Providers/ProviderHttpClientFactory.cs"
echo "  LiteratureVersion → $NEW"

# ── 6. README files ──
for f in "$ROOT/README.md" "$ROOT/README.zh-CN.md"; do
    [ -f "$f" ] || continue
    sed -i "s|\"version\": \"[0-9.]*\"|\"version\": \"$NEW\"|g" "$f"
    sed -i "s|Nong\.Cli\.Net [0-9.]* / [0-9]*|Nong.Cli.Net $NEW / 332|g" "$f"
    echo "  $(basename "$f")"
done

# ── 7. Imaging README ──
sed -i "s|Nong CLI [0-9.]*|Nong CLI $NEW|" "$ROOT/Imaging/README.md" 2>/dev/null && echo "  Imaging/README.md"

echo ""
echo "done. Verify: grep -rn '\"[0-9.]*\"' --include='*.cs' --include='*.md' Cli/ | grep -v vendor"
