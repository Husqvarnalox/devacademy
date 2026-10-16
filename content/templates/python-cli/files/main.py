"""Минимальная консольная программа: python main.py [имя]"""
import argparse


def greet(name: str) -> str:
    return f"Hello, {name}!"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Приветствует по имени.")
    parser.add_argument("name", nargs="?", default="world", help="кого приветствовать")
    args = parser.parse_args(argv)
    print(greet(args.name))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
