import sys
from collections import OrderedDict


def main():
    tokens = sys.stdin.read().split()
    pos = 0
    page_size = int(tokens[0])
    tlb_size = int(tokens[1])
    n = int(tokens[2])
    pos = 3
    table = {}
    for _ in range(n):
        vpn, pfn = tokens[pos], tokens[pos + 1]
        pos += 2
        table[int(vpn, 16)] = None if pfn == "-" else int(pfn, 16)
    q = int(tokens[pos])
    pos += 1
    tlb = OrderedDict()               # vpn -> pfn, least recently used first
    hits = misses = 0
    out = []
    for _ in range(q):
        text = tokens[pos]
        pos += 1
        addr = int(text, 16)
        vpn, offset = divmod(addr, page_size)
        if vpn in tlb:
            tlb.move_to_end(vpn)
            hits += 1
            out.append(f"{hex(addr)} -> {hex(tlb[vpn] * page_size + offset)} [TLB hit]")
            continue
        misses += 1
        pfn = table.get(vpn)
        if pfn is None:
            out.append(f"{hex(addr)} -> PAGE FAULT [TLB miss]")
            continue
        if tlb_size > 0:
            if len(tlb) >= tlb_size:
                tlb.popitem(last=False)
            tlb[vpn] = pfn
        out.append(f"{hex(addr)} -> {hex(pfn * page_size + offset)} [TLB miss]")
    out.append(f"TLB hits: {hits}, misses: {misses}")
    print("\n".join(out))


main()
