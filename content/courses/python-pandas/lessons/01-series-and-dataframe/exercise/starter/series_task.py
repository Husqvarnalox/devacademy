import pandas as pd


def align_sum(a, b):
    """Сложите две Series по меткам так, чтобы метка, которой нет в одной из них, считалась нулём.

    Верните Series с объединением меток (без NaN).
    """
    # TODO
    return None


def add_by_position(a, b):
    """Сложите две Series одинаковой длины ПО ПОЗИЦИЯМ (метки игнорируются).

    Верните Series с индексом a. Если длины разные, бросьте ValueError.
    """
    # TODO
    return None


def label_and_position(s, label, pos):
    """Верните кортеж (значение по метке label, значение по позиции pos)."""
    # TODO
    return None


def slice_by_labels(s, start, stop):
    """Верните часть Series от метки start до метки stop ВКЛЮЧИТЕЛЬНО (индекс отсортирован)."""
    # TODO
    return None
