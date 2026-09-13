"""Tiny local sink for screenshots taken inside the WebGL page.

The page posts canvas.toDataURL() output; this writes it as a PNG. Runs on 127.0.0.1 only,
accepts same-origin POSTs of at most 32 MB, and writes into OUT with the name sanitised to
[A-Za-z0-9_.-]. It is a local capture helper, not a server to leave running.

    python tools/capture/save-server.py docs/media 8766
    # in the page: fetch('http://localhost:8766/save?name=level-05.png', {method:'POST', body: canvas.toDataURL('image/png')})
"""
import base64
import os
import re
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import parse_qs, urlparse

OUT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else "docs/media")
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 8766
os.makedirs(OUT, exist_ok=True)


MAX_BYTES = 32 * 1024 * 1024


class Handler(BaseHTTPRequestHandler):
    def _cors(self):
        # Same-origin only on purpose. The page that posts frames is served from
        # 127.0.0.1 too, so no cross-origin header is needed - and a wildcard here
        # would let any site you have open write files into OUT.
        self.send_header("Vary", "Origin")

    def do_OPTIONS(self):
        self.send_response(204)
        self._cors()
        self.end_headers()

    def do_POST(self):
        url = urlparse(self.path)
        name = parse_qs(url.query).get("name", ["frame.png"])[0]
        name = re.sub(r"[^A-Za-z0-9_.-]", "_", name)
        length = int(self.headers.get("Content-Length", "0"))
        if length <= 0 or length > MAX_BYTES:
            self.send_response(413)
            self.end_headers()
            return
        body = self.rfile.read(length).decode("ascii", "ignore")
        if "," in body:
            body = body.split(",", 1)[1]
        try:
            data = base64.b64decode(body, validate=True)
        except (ValueError, base64.binascii.Error):
            self.send_response(400)
            self.end_headers()
            self.wfile.write(b"body is not base64")
            return
        path = os.path.join(OUT, name)
        with open(path, "wb") as f:
            f.write(data)
        self.send_response(200)
        self._cors()
        self.send_header("Content-Type", "text/plain")
        self.end_headers()
        self.wfile.write(("saved %s (%d bytes)" % (path, len(data))).encode())
        print("saved", path, len(data), flush=True)

    def log_message(self, *_):
        pass


print("save-server on http://127.0.0.1:%d -> %s" % (PORT, OUT), flush=True)
HTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
