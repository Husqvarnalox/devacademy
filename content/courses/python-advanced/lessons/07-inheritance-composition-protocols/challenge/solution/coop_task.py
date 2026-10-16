class Entity:
    """Конец цепочки: проверяет, что неизвестных аргументов не осталось."""

    def __init__(self, **kwargs):
        if kwargs:
            raise TypeError(f"неизвестные аргументы: {sorted(kwargs)}")


class NamedMixin(Entity):
    def __init__(self, *, name, **kwargs):
        self.name = name
        super().__init__(**kwargs)


class TimestampedMixin(Entity):
    def __init__(self, *, created=0, **kwargs):
        self.created = created
        super().__init__(**kwargs)


class TaggedMixin(Entity):
    def __init__(self, *, tags=(), **kwargs):
        self.tags = tuple(tags)
        super().__init__(**kwargs)


class Document(NamedMixin, TimestampedMixin, TaggedMixin):
    pass


def mro_names(cls):
    """Имена классов из MRO без object."""
    return [c.__name__ for c in cls.__mro__ if c is not object]
