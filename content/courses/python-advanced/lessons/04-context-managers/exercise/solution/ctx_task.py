import contextlib

_MISSING = object()


class Transaction:
    """with Transaction(store) as s: при исключении store возвращается к исходному состоянию."""

    def __init__(self, store):
        self.store = store
        self._snapshot = None

    def __enter__(self):
        self._snapshot = dict(self.store)
        return self.store

    def __exit__(self, exc_type, exc, tb):
        if exc_type is not None:
            self.store.clear()
            self.store.update(self._snapshot)
        return False


class Ignore:
    """Подавляет исключения перечисленных типов; подавленное лежит в .caught."""

    def __init__(self, *exc_types):
        self.types = exc_types
        self.caught = None

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, tb):
        if exc_type is not None and self.types and issubclass(exc_type, self.types):
            self.caught = exc
            return True
        return False


@contextlib.contextmanager
def temp_attr(obj, name, value):
    """Контекстный менеджер: временно устанавливает obj.name = value."""
    old = getattr(obj, name, _MISSING)
    setattr(obj, name, value)
    try:
        yield obj
    finally:
        if old is _MISSING:
            delattr(obj, name)
        else:
            setattr(obj, name, old)


@contextlib.contextmanager
def enter_all(*managers):
    """Контекстный менеджер: входит во все managers, отдаёт список значений."""
    with contextlib.ExitStack() as stack:
        yield [stack.enter_context(m) for m in managers]
