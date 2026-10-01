"""V5 M7: serve a WebGL build to phones on the local network.

Usage:  python plan/v5/tools/serve-webgl.py ["Pirate Game Unity/Builds/WebGL-Mobile/RoguePirates"] [port] [--cert cert.pem --key key.pem]

Serves the folder with the headers Unity's Brotli and gzip files need. Phones on the same Wi-Fi open the printed URL.
Plain HTTP works for play; HTTPS (some browser features, such as fullscreen and orientation lock, need it) needs a
certificate: make one with mkcert (free: `mkcert -install`, then `mkcert <your LAN IP>`) and pass --cert and --key.
"""
import http.server
import os
import socket
import ssl
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'Pirate Game Unity'))


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        path = self.path.split('?')[0]
        if path.endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        elif path.endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()

    def guess_type(self, path):
        base = path[:-3] if path.endswith(('.br', '.gz')) else path
        if base.endswith('.wasm'):
            return 'application/wasm'
        if base.endswith('.js'):
            return 'application/javascript'
        if base.endswith('.data'):
            return 'application/octet-stream'
        return super().guess_type(base)


def lan_ip():
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.connect(('8.8.8.8', 80))
        return s.getsockname()[0]
    except OSError:
        return '127.0.0.1'
    finally:
        s.close()


def main():
    args = [a for a in sys.argv[1:]]
    cert = key = None
    if '--cert' in args:
        cert = args[args.index('--cert') + 1]
        key = args[args.index('--key') + 1]
        for flag in ('--cert', '--key'):
            i = args.index(flag)
            del args[i:i + 2]
    folder = args[0] if args else os.path.join(ROOT, "Builds", "WebGL-Mobile", "RoguePirates")
    port = int(args[1]) if len(args) > 1 else 8443 if cert else 8080
    os.chdir(folder if os.path.isabs(folder) else os.path.join(os.getcwd(), folder))
    server = http.server.ThreadingHTTPServer(('0.0.0.0', port), Handler)
    scheme = 'http'
    if cert:
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        ctx.load_cert_chain(cert, key)
        server.socket = ctx.wrap_socket(server.socket, server_side=True)
        scheme = 'https'
    print(f'Serving {os.getcwd()} at {scheme}://{lan_ip()}:{port}/ (Ctrl+C to stop)')
    server.serve_forever()


if __name__ == '__main__':
    main()
