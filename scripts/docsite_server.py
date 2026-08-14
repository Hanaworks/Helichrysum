#!/usr/bin/env python3
"""Helichrysum docs-site 静态服务器：始终返回 no-cache 头，避免浏览器缓存旧内容"""
import http.server
import socketserver
import os
import sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 3000
DIRECTORY = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else '.'
os.chdir(DIRECTORY)

class NoCacheHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DIRECTORY, **kwargs)

    def end_headers(self):
        # 禁用一切缓存，确保 docsify 每次都拿到最新文档
        self.send_header('Cache-Control', 'no-store, no-cache, must-revalidate, max-age=0')
        self.send_header('Pragma', 'no-cache')
        self.send_header('Expires', '0')
        super().end_headers()

    def log_message(self, fmt, *args):
        pass  # 安静模式

with socketserver.ThreadingTCPServer(("0.0.0.0", PORT), NoCacheHandler) as httpd:
    httpd.allow_reuse_address = True
    print(f"Serving {DIRECTORY} at http://0.0.0.0:{PORT} (no-cache)")
    httpd.serve_forever()