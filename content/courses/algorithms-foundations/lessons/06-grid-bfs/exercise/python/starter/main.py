import sys


def shortest_exit(grid, n, m):
    """Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1."""
    # TODO: поиск в ширину по состояниям (клетка, использована ли уже пробивка стены), O(n * m)
    return -1


def main():
    data = sys.stdin.read().split()
    n, m = int(data[0]), int(data[1])
    grid = data[2:2 + n]
    print(shortest_exit(grid, n, m))


main()
