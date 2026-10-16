import threading


class Account:
    def __init__(self, id, balance):
        self.id = id
        self.balance = balance
        self.lock = threading.Lock()


def transfer(src, dst, amount):
    """Перевести amount с src на dst, захватывая блокировки в порядке id."""
    if amount <= 0:
        raise ValueError("amount must be positive")
    if src is dst:
        with src.lock:
            if src.balance < amount:
                raise ValueError("insufficient funds")
        return
    first, second = sorted((src, dst), key=lambda a: a.id)
    with first.lock:
        with second.lock:
            if src.balance < amount:
                raise ValueError("insufficient funds")
            src.balance -= amount
            dst.balance += amount
