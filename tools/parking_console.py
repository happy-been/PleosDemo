"""기존 Pleos 콘솔을 전달하고 P단에서도 ACCEL 입력을 올리도록 바꾼다.

Python 표준 라이브러리만 사용한다. AVD 서버는 8088, 브라우저는 8090을 사용한다.
"""
import argparse
import http.client
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit


ORIGINAL_ACCEL = "const accelTarget=pedals.accelerator&&isDriveGear()?100:0;"
PARKING_ACCEL = "const accelTarget=pedals.accelerator?100:0;"
HOP_HEADERS = {
    "connection", "keep-alive", "proxy-authenticate", "proxy-authorization",
    "te", "trailer", "transfer-encoding", "upgrade", "content-length",
    "host", "accept-encoding",
}


def parking_html(body):
    html = body.decode("utf-8")
    if html.count(ORIGINAL_ACCEL) != 1:
        raise ValueError("콘솔의 가속 처리 코드가 달라졌습니다. AVD 버전을 확인해 주세요.")
    html = html.replace(ORIGINAL_ACCEL, PARKING_ACCEL, 1)
    html = html.replace("Select D + hold ACCEL", "P 유지 · ACCEL로 앱 가속")
    html = html.replace("<title>Pleos Connect Virtual Console</title>",
                        "<title>Parking Lab · P단 페달 콘솔</title>", 1)
    # 포커스를 잃으면 페달을 놓아, 다른 창으로 이동한 뒤 입력이 남지 않게 한다.
    release = """<script>
function releaseParkingPedals(){
  pedals.accelerator=pedals.brake=false;
  pedalControl.accelerator=pedalControl.brake=true;
  vehicle.accelerator_pct=vehicle.brake_pct=0;
  document.getElementById('acceleratorPad').classList.remove('pressed');
  document.getElementById('brakePad').classList.remove('pressed');
  markLocalControl();renderAll();queueCommit();
}
window.addEventListener('blur',releaseParkingPedals);
document.addEventListener('visibilitychange',()=>{if(document.hidden)releaseParkingPedals();});
</script>"""
    return html.replace("</body>", release + "</body>", 1).encode("utf-8")


class ConsoleProxy(BaseHTTPRequestHandler):
    upstream_port = 8088

    def do_GET(self):
        if urlsplit(self.path).path == "/__parking_lab/health":
            data = json.dumps({"app": "parking-console", "p_accelerator": True}).encode()
            self.reply(200, data, "application/json")
        else:
            self.forward()

    def do_HEAD(self):
        self.forward()

    def do_POST(self):
        self.forward()

    def do_PUT(self):
        self.forward()

    def forward(self):
        connection = http.client.HTTPConnection("127.0.0.1", self.upstream_port, timeout=5)
        try:
            route = urlsplit(self.path)
            target = route.path or "/"
            if route.query:
                target += "?" + route.query
            headers = {key: value for key, value in self.headers.items()
                       if key.lower() not in HOP_HEADERS}
            headers["Accept-Encoding"] = "identity"
            length = int(self.headers.get("Content-Length", "0"))
            body = self.rfile.read(length) if length else None
            connection.request(self.command, target, body=body, headers=headers)
            response = connection.getresponse()
            data = response.read()
            patched = response.status == 200 and route.path in ("", "/") and self.command == "GET"
            if patched:
                data = parking_html(data)
            self.send_response(response.status)
            for key, value in response.getheaders():
                if key.lower() not in HOP_HEADERS and key.lower() not in {"content-encoding", "etag"}:
                    self.send_header(key, value)
            self.send_header("Content-Length", str(len(data)))
            if patched:
                self.send_header("Cache-Control", "no-store")
            self.end_headers()
            if self.command != "HEAD":
                self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError):
            pass
        except Exception as error:
            self.reply(502, ("AVD 콘솔 연결 오류: " + str(error)).encode("utf-8"), "text/plain; charset=utf-8")
        finally:
            connection.close()

    def reply(self, status, body, content_type):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format_string, *args):
        if len(args) > 1 and str(args[1]).startswith(("4", "5")):
            super().log_message(format_string, *args)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Parking Lab P단 페달 콘솔")
    parser.add_argument("--port", type=int, default=8090)
    parser.add_argument("--upstream-port", type=int, default=8088)
    options = parser.parse_args()
    ConsoleProxy.upstream_port = options.upstream_port
    server = ThreadingHTTPServer(("127.0.0.1", options.port), ConsoleProxy)
    print("Parking Lab: http://127.0.0.1:%d/ (Ctrl+C to stop)" % options.port, flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
