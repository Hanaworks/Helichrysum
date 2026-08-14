#!/usr/bin/env bash
# Helichrysum 文档站发布脚本
#
# 用法:
#   ./scripts/docs-release.sh              # 注入 git hash 版本号
#   ./scripts/docs-release.sh --serve      # 注入 + 重启本地文档服务器
#
# 原理: 将 git commit hash 注入 docs-site/index.html 的 @GIT_HASH@ 占位符。
# 文档每次提交后 hash 变化 → docsify 的 .md 请求带新 ?v= 参数 →
# 强制 CDN/浏览器绕过缓存重新拉取。无需手动维护版本号。
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
INDEX_HTML="$ROOT_DIR/docs-site/index.html"
TEMPLATE_MARKER='@GIT_HASH@'

# 取当前 git commit hash（短 hash，12 位足够唯一；无 commit 时用 'no-commit'）
if git rev-parse --git-dir >/dev/null 2>&1; then
  GIT_HASH="$(git rev-parse --short=12 HEAD 2>/dev/null || echo 'no-commit')"
else
  GIT_HASH='no-commit'
fi

echo "GIT_HASH: $GIT_HASH"

if ! grep -q "$TEMPLATE_MARKER" "$INDEX_HTML"; then
  echo "错误: $INDEX_HTML 中找不到 $TEMPLATE_MARKER 占位符（已被注入过？反复运行脚本会重复注入 hash）。"
  echo "提示: 请先从 git 恢复模板，或检查是否已注入。"
  exit 1
fi

# 注入（使用临时文件避免 sed 转义问题）
sed "s/$TEMPLATE_MARKER/$GIT_HASH/g" "$INDEX_HTML" > "$INDEX_HTML.tmp"
mv "$INDEX_HTML.tmp" "$INDEX_HTML"

echo "已注入: $INDEX_HTML (DOC_VERSION = $GIT_HASH)"

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