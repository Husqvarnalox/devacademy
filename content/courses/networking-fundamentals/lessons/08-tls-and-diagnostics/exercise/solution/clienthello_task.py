import sys

VERSIONS = {0x0300: "SSL 3.0", 0x0301: "TLS 1.0", 0x0302: "TLS 1.1", 0x0303: "TLS 1.2", 0x0304: "TLS 1.3"}
SUITES = {
    0x1301: "TLS_AES_128_GCM_SHA256",
    0x1302: "TLS_AES_256_GCM_SHA384",
    0x1303: "TLS_CHACHA20_POLY1305_SHA256",
    0xC02B: "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256",
    0xC02C: "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384",
    0xC02F: "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256",
    0xC030: "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384",
    0xCCA8: "TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256",
    0xCCA9: "TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256",
    0x00FF: "TLS_EMPTY_RENEGOTIATION_INFO_SCSV",
}
EXTENSIONS = {
    0: "server_name",
    10: "supported_groups",
    11: "ec_point_formats",
    13: "signature_algorithms",
    16: "application_layer_protocol_negotiation",
    21: "padding",
    43: "supported_versions",
    45: "psk_key_exchange_modes",
    51: "key_share",
}


class HelloError(Exception):
    pass


class Reader:
    def __init__(self, data):
        self.data = data
        self.pos = 0

    def take(self, count):
        if self.pos + count > len(self.data):
            raise HelloError("truncated")
        chunk = self.data[self.pos:self.pos + count]
        self.pos += count
        return chunk

    def u8(self):
        return self.take(1)[0]

    def u16(self):
        return int.from_bytes(self.take(2), "big")

    def u24(self):
        return int.from_bytes(self.take(3), "big")


def is_grease(value):
    return value & 0x0F0F == 0x0A0A and (value >> 8) == (value & 0xFF)


def version_name(value):
    return VERSIONS.get(value, "unknown")


def parse(data):
    out = []
    rec = Reader(data)
    content_type = rec.u8()
    record_version = rec.u16()
    record_length = rec.u16()
    if content_type != 22:
        raise HelloError("not a handshake record")
    out.append("record_type=22 (handshake)")
    out.append(f"record_version=0x{record_version:04x} ({version_name(record_version)})")
    out.append(f"record_length={record_length}")
    body = Reader(rec.take(record_length))
    handshake_type = body.u8()
    handshake_length = body.u24()
    if handshake_type != 1:
        raise HelloError("not a ClientHello")
    hello = Reader(body.take(handshake_length))
    out.append("handshake_type=1 (client_hello)")
    out.append(f"handshake_length={handshake_length}")
    client_version = hello.u16()
    out.append(f"client_version=0x{client_version:04x} ({version_name(client_version)})")
    hello.take(32)
    session_id = hello.u8()
    hello.take(session_id)
    out.append(f"session_id_length={session_id}")
    suites_raw = Reader(hello.take(hello.u16()))
    suites = []
    while suites_raw.pos < len(suites_raw.data):
        value = suites_raw.u16()
        suites.append("GREASE" if is_grease(value) else SUITES.get(value, f"0x{value:04x}"))
    out.append(f"cipher_suites={len(suites)}: {', '.join(suites)}")
    methods = hello.take(hello.u8())
    out.append(f"compression_methods={len(methods)}")
    sni = "-"
    versions = "-"
    alpn = "-"
    names = []
    if hello.pos < len(hello.data):
        extensions = Reader(hello.take(hello.u16()))
        while extensions.pos < len(extensions.data):
            ext_type = extensions.u16()
            ext = Reader(extensions.take(extensions.u16()))
            names.append("GREASE" if is_grease(ext_type) else EXTENSIONS.get(ext_type, f"ext_{ext_type}"))
            if ext_type == 0:
                lst = Reader(ext.take(ext.u16()))
                while lst.pos < len(lst.data):
                    name_type = lst.u8()
                    name = lst.take(lst.u16())
                    if name_type == 0 and sni == "-":
                        sni = name.decode("ascii", "replace")
            elif ext_type == 43:
                raw = Reader(ext.take(ext.u8()))
                found = []
                while raw.pos < len(raw.data):
                    value = raw.u16()
                    found.append("GREASE" if is_grease(value) else version_name(value))
                versions = ", ".join(found)
            elif ext_type == 16:
                raw = Reader(ext.take(ext.u16()))
                found = []
                while raw.pos < len(raw.data):
                    found.append(raw.take(raw.u8()).decode("ascii", "replace"))
                alpn = ",".join(found)
    out.append(f"extensions={len(names)}: {', '.join(names) if names else '-'}")
    out.append(f"sni={sni}")
    out.append(f"supported_versions={versions}")
    out.append(f"alpn={alpn}")
    return out


def main():
    try:
        data = bytes.fromhex("".join(sys.stdin.read().split()))
    except ValueError:
        print("error: bad hex")
        return
    try:
        print("\n".join(parse(data)))
    except HelloError as error:
        print(f"error: {error}")


main()
