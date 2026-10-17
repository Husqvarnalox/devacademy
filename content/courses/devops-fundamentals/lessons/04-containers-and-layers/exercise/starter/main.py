import sys


def ignored(path, patterns):
    """True, если путь исключён .dockerignore (формы шаблонов - в условии)."""
    # TODO
    return False


def main():
    lines = sys.stdin.read().splitlines()
    cut = lines.index("---")
    # инструкции Dockerfile: пустые строки и комментарии (#) пропускаются; номера с единицы
    instr = [l.strip() for l in lines[:cut] if l.strip() and not l.strip().startswith("#")]
    patterns = []
    for raw in lines[cut + 1:]:
        p = raw.split()
        if not p:
            continue
        # TODO: IGNORE добавляет шаблоны, EDIT <n> <текст> меняет инструкцию n,
        #       BUILD [файлы...] печатает отчёт о сборке (см. условие)
        pass


main()
