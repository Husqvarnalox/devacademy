import sys


class BodyError(Exception):
    pass


def read_line(data, pos):
    end = data.find(b"\n", pos)
    if end < 0:
        raise BodyError("truncated")
    line = data[pos:end]
    if line.endswith(b"\r"):
        line = line[:-1]
    return line, end + 1


def decode_chunked(data, pos):
    body = bytearray()
    chunks = 0
    while True:
        line, pos = read_line(data, pos)
        size_text = line.split(b";", 1)[0].strip()
        if not size_text or any(c not in b"0123456789abcdefABCDEF" for c in size_text):
            raise BodyError("bad chunk size")
        size = int(size_text, 16)
        if size == 0:
            break
        if pos + size > len(data):
            raise BodyError("truncated")
        body += data[pos:pos + size]
        pos += size
        if data[pos:pos + 2] == b"\r\n":
            pos += 2
        elif data[pos:pos + 1] == b"\n":
            pos += 1
        elif pos >= len(data):
            raise BodyError("truncated")
        else:
            raise BodyError("bad chunk terminator")
        chunks += 1
    trailers = []
    while True:
        line, pos = read_line(data, pos)
        if not line:
            break
        name, _, value = line.partition(b":")
        trailers.append((name.decode("latin-1").strip().lower(), value.decode("latin-1").strip()))
    return bytes(body), chunks, trailers


def main():
    data = sys.stdin.buffer.read()
    try:
        status_line, pos = read_line(data, 0)
        parts = status_line.decode("latin-1").split(" ", 2)
        if len(parts) < 2 or not parts[0].startswith("HTTP/") or not parts[1].isdigit():
            raise BodyError("bad status line")
        version, status = parts[0], int(parts[1])
        reason = parts[2] if len(parts) > 2 else ""
        headers = {}
        while True:
            line, pos = read_line(data, pos)
            if not line:
                break
            name, colon, value = line.partition(b":")
            if not colon:
                raise BodyError("bad header")
            key = name.decode("latin-1").strip().lower()
            text = value.decode("latin-1").strip()
            headers[key] = headers[key] + ", " + text if key in headers else text
        trailers = []
        chunks = 0
        if "chunked" in headers.get("transfer-encoding", "").lower():
            body, chunks, trailers = decode_chunked(data, pos)
        elif "content-length" in headers:
            length_text = headers["content-length"]
            if not length_text.isdigit():
                raise BodyError("bad content-length")
            length = int(length_text)
            if pos + length > len(data):
                raise BodyError("truncated")
            body = data[pos:pos + length]
        else:
            body = data[pos:]
    except BodyError as error:
        print(f"error: {error}")
        return
    print(f"version={version}")
    print(f"status={status}")
    print(f"reason={reason}")
    print(f"chunks={chunks}")
    for name, value in trailers:
        print(f"trailer {name}={value}")
    print(f"body_bytes={len(body)}")
    print("---")
    print(body.decode("utf-8", "replace"))


main()
