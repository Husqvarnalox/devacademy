import contextlib


class Transaction:
    """with Transaction(store) as s: при исключении store возвращается к исходному состоянию."""

    def __init__(self, store):
        self.store = store

    # TODO: __enter__ и __exit__


class Ignore:
    """Подавляет исключения перечисленных типов; подавленное лежит в .caught."""

    def __init__(self, *exc_types):
        self.types = exc_types
        self.caught = None

    # TODO: __enter__ и __exit__


def temp_attr(obj, name, value):
    """Контекстный менеджер: временно устанавливает obj.name = value."""
    # TODO: через @contextlib.contextmanager
    raise NotImplementedError


def enter_all(*managers):
    """Контекстный менеджер: входит во все managers, отдаёт список значений."""
    # TODO: contextmanager + ExitStack
    raise NotImplementedError
