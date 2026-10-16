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


def unit_of_work():
    tx = DB.begin()
    try:
        yield tx
    except Exception:
        tx.rollback()
        raise
    else:
        tx.commit()


class TransferIn(BaseModel):
    src: str
    dst: str
    amount: int = Field(gt=0)


@app.post("/transfer")
def transfer(body: TransferIn, tx: Annotated[Transaction, Depends(unit_of_work)]):
    if body.src not in tx.accounts or body.dst not in tx.accounts:
        raise HTTPException(status_code=404, detail="account not found")
    tx.accounts[body.src] -= body.amount
    tx.accounts[body.dst] += body.amount
    if tx.accounts[body.src] < 0:
        raise HTTPException(status_code=409, detail="insufficient funds")
    return {"src": tx.accounts[body.src], "dst": tx.accounts[body.dst]}
