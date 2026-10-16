import functools
from dataclasses import dataclass, field


class Money:
    """Деньги в минимальных единицах (центах) с валютой."""

    def __init__(self, cents, currency="USD"):
        self.cents = cents
        self.currency = currency

    # TODO: __repr__, __str__, __eq__, __hash__, __lt__ (+ total_ordering)


class Playlist:
    """Плейлист-контейнер над списком."""

    def __init__(self, tracks):
        self._tracks = list(tracks)

    # TODO: __len__, __getitem__ (индекс и срез), __contains__


class Version:
    # TODO: превратите в dataclass(frozen=True, order=True) с полями major, minor=0, patch=0
    pass


class Cart:
    # TODO: dataclass(slots=True): owner, items (default_factory=list), discount=0.0, метод add
    pass
