import hashlib
import sys

ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"


def encode(n):
    if n == 0:
        return "0"
    out = []
    while n:
        n, r = divmod(n, 62)
        out.append(ALPHABET[r])
    return "".join(reversed(out))


def decode(s):
    n = 0
    for ch in s:
        n = n * 62 + ALPHABET.index(ch)
    return n


def hash_code(url, attempt, length):
    data = url if attempt == 0 else f"{url}#{attempt}"
    digest = int(hashlib.md5(data.encode()).hexdigest(), 16)
    return encode(digest % 62 ** length).rjust(length, "0")


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    mode, param = lines[0].split()
    param = int(param)
    by_code, by_url = {}, {}
    counter = param
    collisions = 0
    for line in lines[1:]:
        cmd, arg = line.split()
        if cmd == "ENCODE":
            print(f"ENCODE {arg} {encode(int(arg))}")
        elif cmd == "DECODE":
            print(f"DECODE {arg} {decode(arg)}")
        elif cmd == "RESOLVE":
            print(f"RESOLVE {arg} {by_code.get(arg, 'NOT_FOUND')}")
        elif cmd == "SHORTEN":
            if arg in by_url:
                print(f"SHORTEN {arg} {by_url[arg]} existing")
                continue
            attempt = 0
            if mode == "counter":
                code = encode(counter)
                counter += 1
            else:
                while True:
                    code = hash_code(arg, attempt, param)
                    if code not in by_code:
                        break
                    attempt += 1
                    collisions += 1
            by_code[code] = arg
            by_url[arg] = code
            print(f"SHORTEN {arg} {code} attempts={attempt + 1}")
    print(f"urls={len(by_url)} collisions={collisions}")


main()
