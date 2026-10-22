from dataclasses import dataclass


class Version:
    """Версия ПО: major.minor.patch (сделайте неизменяемым dataclass с упорядочиванием)."""

    @classmethod
    def parse(cls, text):
        """Создать версию из строки '1.2.3'."""
        pass

    def bump(self, part):
        """Вернуть новую версию с увеличенной частью 'major', 'minor' или 'patch'."""
        pass


def latest(versions):
    """Наибольшая версия или None, если версий нет."""
    pass
