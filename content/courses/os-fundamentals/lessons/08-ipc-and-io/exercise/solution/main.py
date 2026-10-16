import sys


def parse(text):
    lines = [ln.strip() for ln in text.split("\n") if ln.strip()]
    cap = int(lines[0])
    writes, reads = [], []
    for ln in lines[1:]:
        name, _, rest = ln.partition(":")
        nums = [x if x == "." else int(x) for x in rest.split()]
        if name.strip() == "producer":
            writes = nums
        elif name.strip() == "consumer":
            reads = nums
    return cap, writes, reads


def main():
    cap, writes, reads = parse(sys.stdin.read())
    out = []
    fill = 0
    closed = False          # writer closed its end
    reader_gone = False     # reader closed its end
    p_done = c_done = False
    p_blocked = c_blocked = False
    wi = ri = 0             # next write / read request
    rem = None              # bytes left of current write
    want = None             # size of current read request
    written = read = 0

    def pipe():
        return f"(pipe {fill}/{cap})"

    turn = 0
    while not (p_done and c_done) and turn < 100000:
        who = "producer" if turn % 2 == 0 else "consumer"
        turn += 1
        if who == "producer" and not p_done:
            if rem is None:
                if wi >= len(writes):
                    closed = True
                    p_done = True
                    out.append("producer closes pipe")
                    continue
                item = writes[wi]
                wi += 1
                if item == ".":
                    out.append("producer works")
                    continue
                rem = item
            if reader_gone:
                out.append("producer SIGPIPE: reader closed the pipe")
                p_done = True
                closed = True
                continue
            free = cap - fill
            if free == 0:
                if not p_blocked:
                    out.append("producer blocks: pipe full")
                    p_blocked = True
                continue
            p_blocked = False
            w = min(rem, free)
            fill += w
            rem -= w
            written += w
            out.append(f"producer write {w} {pipe()}")
            if rem > 0:
                out.append("producer blocks: pipe full")
                p_blocked = True
            else:
                rem = None
                if wi >= len(writes):
                    closed = True
                    p_done = True
                    out.append("producer closes pipe")
        elif who == "consumer" and not c_done:
            if want is None:
                if ri >= len(reads):
                    c_done = True
                    reader_gone = True
                    out.append("consumer done")
                    continue
                item = reads[ri]
                ri += 1
                if item == ".":
                    out.append("consumer works")
                    continue
                want = item
            if fill == 0:
                if closed:
                    out.append("consumer read 0 (EOF)")
                    c_done = True
                    reader_gone = True
                    out.append("consumer done")
                elif not c_blocked:
                    out.append("consumer blocks: pipe empty")
                    c_blocked = True
                continue
            c_blocked = False
            r = min(want, fill)
            fill -= r
            read += r
            want = None
            out.append(f"consumer read {r} {pipe()}")
            if ri >= len(reads):
                c_done = True
                reader_gone = True
                out.append("consumer done")
    out.append(f"bytes written: {written}, bytes read: {read}")
    print("\n".join(out))


main()
