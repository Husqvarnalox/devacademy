from decimal import Decimal
from typing import Annotated

from pydantic import (
    BaseModel,
    BeforeValidator,
    ConfigDict,
    Field,
    StringConstraints,
    ValidationError,
    computed_field,
    field_validator,
    model_validator,
)


def _clean_sku(value):
    return value.strip().upper() if isinstance(value, str) else value


Sku = Annotated[str, BeforeValidator(_clean_sku), StringConstraints(pattern=r"^[A-Z0-9-]{3,12}$")]


class Address(BaseModel):
    model_config = ConfigDict(extra="forbid", str_strip_whitespace=True)

    street: str = Field(min_length=1)
    city: str = Field(min_length=1)
    zip: str = Field(pattern=r"^\d{5}$")


class OrderItem(BaseModel):
    model_config = ConfigDict(extra="forbid", str_strip_whitespace=True)

    sku: Sku
    qty: int = Field(ge=1, le=100)
    unit_price: Decimal = Field(gt=0, decimal_places=2)


class Order(BaseModel):
    model_config = ConfigDict(extra="forbid", str_strip_whitespace=True)

    items: list[OrderItem] = Field(min_length=1, max_length=50)
    address: Address
    coupon: str | None = None
    tags: list[str] = Field(default_factory=list)

    @field_validator("coupon")
    @classmethod
    def coupon_prefix(cls, value):
        if value is not None and not value.startswith("SALE-"):
            raise ValueError("coupon must start with SALE-")
        return value

    @model_validator(mode="after")
    def unique_skus(self):
        skus = [item.sku for item in self.items]
        if len(skus) != len(set(skus)):
            raise ValueError("duplicate sku in order")
        return self

    @computed_field
    @property
    def total(self) -> Decimal:
        return sum((item.qty * item.unit_price for item in self.items), Decimal(0))


def parse_or_errors(data: dict) -> dict:
    try:
        return {"ok": True, "order": Order.model_validate(data)}
    except ValidationError as exc:
        return {
            "ok": False,
            "errors": [
                {"loc": ".".join(str(p) for p in err["loc"]), "type": err["type"]}
                for err in exc.errors()
            ],
        }


def to_payload(order: Order) -> dict:
    return order.model_dump(mode="json", exclude_none=True)
