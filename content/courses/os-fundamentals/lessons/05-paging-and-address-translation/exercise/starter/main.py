import sys
from collections import OrderedDict


def main():
    tokens = sys.stdin.read().split()
    page_size = int(tokens[0])       # decimal, a power of two
    tlb_size = int(tokens[1])        # decimal, 0 means "no TLB"
    n = int(tokens[2])
    pos = 3
    table = {}                       # vpn -> pfn, or None for an invalid entry
    for _ in range(n):
        vpn, pfn = tokens[pos], tokens[pos + 1]
        pos += 2
        table[int(vpn, 16)] = None if pfn == "-" else int(pfn, 16)
    q = int(tokens[pos])
    pos += 1
    tlb = OrderedDict()              # vpn -> pfn, least recently used first
    hits = misses = 0
    out = []
    for _ in range(q):
        addr = int(tokens[pos], 16)
        pos += 1
        # TODO: vpn, offset = divmod(addr, page_size); consult the TLB, then the page table;
        # append "0x.. -> 0x.. [TLB hit|miss]" or "0x.. -> PAGE FAULT [TLB miss]".
        out.append(hex(addr))
    out.append(f"TLB hits: {hits}, misses: {misses}")
    print("\n".join(out))


main()
