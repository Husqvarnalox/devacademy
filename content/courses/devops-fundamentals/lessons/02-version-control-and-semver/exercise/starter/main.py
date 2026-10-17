import re
import sys

SEMVER = re.compile(
    r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)"
    r"(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?"
    r"(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$"
)  # регулярное выражение с semver.org


def parse(text):
    """Верните (major, minor, patch, [пре-релиз идентификаторы], build или None) либо None."""
    # TODO: используйте SEMVER.match(text); числа приведите к int
    return None


def compare(x, y):
    """-1, 0 или 1 по разделу 11 semver.org; build-метаданные не учитываются."""
    # TODO
    return 0


def bump(kind, v):
    """Новая версия (tuple как у parse) или None для неизвестного kind."""
    # TODO
    return None


def render(v):
    # TODO: "M.m.p[-pre][+build]"
    return ""


def main():
    for line in sys.stdin.read().splitlines():
        p = line.split()
        if not p:
            continue
        # TODO: обработайте команды PARSE, CMP, BUMP и SORT из условия
        pass


main()
