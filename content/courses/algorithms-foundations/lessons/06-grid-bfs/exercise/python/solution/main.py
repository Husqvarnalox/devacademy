import sys


def shortest_exit(grid, n, m):
    """Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1."""
    flat = "".join(grid)
    start = flat.index("S")
    end = flat.index("E")
    wall = [ch == "#" for ch in flat]
    # состояние = (клетка, сколько стен уже пробито: 0 или 1), кодируем числом cell * 2 + used
    dist = [-1] * (2 * n * m)
    dist[start * 2] = 0
    queue = [start * 2]
    head = 0                              # очередь на списке с указателем: pop(0) был бы O(n)
    while head < len(queue):
        state = queue[head]
        head += 1
        cell, used = state >> 1, state & 1
        d = dist[state] + 1
        r, c = divmod(cell, m)
        for nr, nc in ((r + 1, c), (r - 1, c), (r, c + 1), (r, c - 1)):
            if 0 <= nr < n and 0 <= nc < m:
                ncell = nr * m + nc
                nused = used + wall[ncell]
                if nused <= 1:
                    nstate = ncell * 2 + nused
                    if dist[nstate] < 0:
                        dist[nstate] = d
                        queue.append(nstate)
    options = [x for x in (dist[end * 2], dist[end * 2 + 1]) if x >= 0]
    return min(options) if options else -1


def main():
    data = sys.stdin.read().split()
    n, m = int(data[0]), int(data[1])
    grid = data[2:2 + n]
    print(shortest_exit(grid, n, m))


main()
