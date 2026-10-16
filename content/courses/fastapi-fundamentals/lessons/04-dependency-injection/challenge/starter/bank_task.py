from typing import Annotated

from fastapi import Depends, FastAPI, HTTPException
from pydantic import BaseModel, Field

app = FastAPI()


class Transaction:
    """Работает с копией счетов; commit записывает копию в базу, rollback отбрасывает."""

    def __init__(self, db: "Database") -> None:
        self._db = db
        self.accounts = dict(db.accounts)
        self.committed = False
        self.rolled_back = False

    def commit(self) -> None:
        self._db.accounts = dict(self.accounts)
        self.committed = True

    def rollback(self) -> None:
        self.rolled_back = True


class Database:
    def __init__(self) -> None:
        self.accounts: dict[str, int] = {}
        self.transactions: list[Transaction] = []

    def begin(self) -> Transaction:
        tx = Transaction(self)
        self.transactions.append(tx)
        return tx


DB = Database()

# TODO: unit_of_work (зависимость с yield), модель тела и POST /transfer
