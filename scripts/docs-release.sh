#!/usr/bin/env bash
# Helichrysum 文档站发布脚本
#
# 用法:
#   ./scripts/docs-release.sh              # 从模板构建 index.html（注入 git hash）
#   ./scripts/docs-release.sh --serve      # 构建 + 重启本地文档服务器
#
# 原理: docs-site/index.template.html 是模板（含 @GIT_HASH@ 占位符），
# 每次发布时用当前 git commit hash 替换，产出 docs-site/index.html。
# 文档提交后 hash 变化 → docsify 的 .md 请求带新 ?v= 参数 →
# 强制 CDN/浏览器绕过缓存重新拉取。无需手动维护版本号。
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TEMPLATE="$ROOT_DIR/docs-site/index.template.html"
OUTPUT="$ROOT_DIR/docs-site/index.html"
MARKER='@GIT_HASH@'

if [[ ! -f "$TEMPLATE" ]]; then
  echo "错误: 模板文件 $TEMPLATE 不存在"
  exit 1
fi
if ! grep -q "$MARKER" "$TEMPLATE"; then
  echo "错误: 模板文件中找不到 $MARKER 占位符"
  exit 1
fi

# 取当前 git commit hash（12 位短 hash；无 commit 时用 no-commit）
if git rev-parse --git-dir >/dev/null 2>&1; then
  GIT_HASH="$(git rev-parse --short=12 HEAD 2>/dev/null || echo 'no-commit')"
else
  GIT_HASH='no-commit'
fi

echo "GIT_HASH: $GIT_HASH"

# 模板 → 产物（占位符替换）
sed "s/$MARKER/$GIT_HASH/g" "$TEMPLATE" > "$OUTPUT"
echo "已生成: $OUTPUT (DOC_VERSION = $GIT_HASH)"

if [[ "${1:-}" == "--serve" ]]; then
  PORT="${2:-3000}"
  echo "重启文档服务器 (:$PORT)..."
  pkill -f "docsite_server.py $PORT" 2>/dev/null || true
  sleep 1
  setsid python3 "$ROOT_DIR/scripts/docsite_server.py" "$PORT" "$ROOT_DIR/docs-site" \
    >/tmp/opencode/docs-site.log 2>&1 < /dev/null &
  disown
  sleep 2
  ss -tlnp 2>/dev/null | grep ":$PORT" && echo "文档站已启动: http://0.0.0.0:$PORT"
fi

echo "完成。"