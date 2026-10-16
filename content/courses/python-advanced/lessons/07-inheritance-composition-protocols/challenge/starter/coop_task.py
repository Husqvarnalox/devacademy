class Entity:
    """Конец цепочки: проверяет, что неизвестных аргументов не осталось."""

    def __init__(self, **kwargs):
        # TODO: TypeError при непустом kwargs
        pass


class NamedMixin(Entity):
    # TODO: __init__(self, *, name, **kwargs)
    pass


class TimestampedMixin(Entity):
    # TODO: __init__(self, *, created=0, **kwargs)
    pass


class TaggedMixin(Entity):
    # TODO: __init__(self, *, tags=(), **kwargs)
    pass


class Document(NamedMixin, TimestampedMixin, TaggedMixin):
    pass


def mro_names(cls):
    """Имена классов из MRO без object."""
    # TODO
    return []
