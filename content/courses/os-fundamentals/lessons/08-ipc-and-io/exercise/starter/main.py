import sys


def parse(text):
    """Return (capacity, writes, reads). Items are ints or "." (the process does other work)."""
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
    # TODO: alternate turns, producer first. Keep: fill, closed (writer side), reader_gone,
    # the remaining bytes of the current write, the size of the current read request,
    # and "already announced that I am blocked" flags so a block is printed once.
    # Print the lines described in the task, then the final totals line.
    out.append("bytes written: 0, bytes read: 0")
    print("\n".join(out))


main()
