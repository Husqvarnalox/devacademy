from decimal import Decimal

from pydantic import BaseModel, ConfigDict, Field, ValidationError

# TODO: Address, OrderItem, Order (Pydantic v2: model_config = ConfigDict(...), без class Config)


def parse_or_errors(data: dict) -> dict:
    raise NotImplementedError


def to_payload(order) -> dict:
    raise NotImplementedError
