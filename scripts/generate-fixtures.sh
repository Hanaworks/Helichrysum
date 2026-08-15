#!/usr/bin/env bash
# Helichrysum 测试夹具生成脚本
# Generates the tests/fixtures/ directory tree with controlled file content and mtime.
set -euo pipefail

FIXTURES_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../tests/fixtures" && pwd)"

echo "Generating fixtures in $FIXTURES_DIR"

# backup1 & backup2: overlapping files for exact duplicate detection
echo "Hello, world!" > "$FIXTURES_DIR/backup1/readme.txt"
echo "Hello, world!" > "$FIXTURES_DIR/backup2/readme.txt"  # identical

echo "Some unique content." > "$FIXTURES_DIR/backup1/notes.txt"
echo "Updated notes here." > "$FIXTURES_DIR/backup2/notes.txt"  # different

touch -t 202401010000 "$FIXTURES_DIR/backup1/readme.txt"
touch -t 202401010000 "$FIXTURES_DIR/backup2/readme.txt"
touch -t 202401010000 "$FIXTURES_DIR/backup1/notes.txt"
touch -t 202406010000 "$FIXTURES_DIR/backup2/notes.txt"

# links: empty for now — will be populated when link handling is implemented
touch "$FIXTURES_DIR/links/.gitkeep"

# archives: empty for now — will be populated when archive handling is implemented
touch "$FIXTURES_DIR/archives/.gitkeep"

echo "Done."