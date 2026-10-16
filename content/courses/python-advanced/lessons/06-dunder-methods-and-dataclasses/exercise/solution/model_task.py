import functools
from dataclasses import dataclass, field


@functools.total_ordering
class Money:
    """Деньги в минимальных единицах (центах) с валютой."""

    def __init__(self, cents, currency="USD"):
        self.cents = cents
        self.currency = currency

    def __repr__(self):
        return f"Money({self.cents!r}, {self.currency!r})"

    def __str__(self):
        return f"{self.cents / 100:.2f} {self.currency}"

    def __eq__(self, other):
        if not isinstance(other, Money):
            return NotImplemented
        return (self.cents, self.currency) == (other.cents, other.currency)

    def __hash__(self):
        return hash((self.cents, self.currency))

    def __lt__(self, other):
        if not isinstance(other, Money) or other.currency != self.currency:
            return NotImplemented
        return self.cents < other.cents


class Playlist:
    """Плейлист-контейнер над списком."""

    def __init__(self, tracks):
        self._tracks = list(tracks)

    def __len__(self):
        return len(self._tracks)

    def __getitem__(self, index):
        result = self._tracks[index]
        if isinstance(index, slice):
            return Playlist(result)
        return result

    def __contains__(self, track):
        return track in self._tracks


@dataclass(frozen=True, order=True)
class Version:
    major: int
    minor: int = 0
    patch: int = 0

    def __str__(self):
        return f"{self.major}.{self.minor}.{self.patch}"


@dataclass(slots=True)
class Cart:
    owner: str
    items: list[str] = field(default_factory=list)
    discount: float = 0.0

    def add(self, item):
        self.items.append(item)
