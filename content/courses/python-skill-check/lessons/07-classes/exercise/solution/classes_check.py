from dataclasses import dataclass, replace


@dataclass(frozen=True, order=True)
class Version:
    """Версия ПО: major.minor.patch."""

    major: int
    minor: int = 0
    patch: int = 0

    @classmethod
    def parse(cls, text):
        """Создать версию из строки '1.2.3'."""
        parts = text.split(".")
        if len(parts) != 3 or not all(p.isascii() and p.isdigit() for p in parts):
            raise ValueError(f"некорректная версия: {text!r}")
        return cls(*(int(p) for p in parts))

    def bump(self, part):
        """Вернуть новую версию с увеличенной частью 'major', 'minor' или 'patch'."""
        if part == "major":
            return Version(self.major + 1)
        if part == "minor":
            return Version(self.major, self.minor + 1)
        if part == "patch":
            return replace(self, patch=self.patch + 1)
        raise ValueError(f"неизвестная часть версии: {part!r}")

    def __str__(self):
        return f"{self.major}.{self.minor}.{self.patch}"


def latest(versions):
    """Наибольшая версия или None, если версий нет."""
    return max(versions, default=None)
