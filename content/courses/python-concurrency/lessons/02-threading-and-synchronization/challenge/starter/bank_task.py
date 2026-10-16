import threading


class Account:
    def __init__(self, id, balance):
        self.id = id
        self.balance = balance
        self.lock = threading.Lock()


def transfer(src, dst, amount):
    """Перевести amount с src на dst, захватывая блокировки в порядке id."""
    # TODO
    raise NotImplementedError
