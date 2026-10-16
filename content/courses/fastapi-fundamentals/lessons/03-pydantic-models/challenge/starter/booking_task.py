from datetime import date

from pydantic import BaseModel, ConfigDict, Field

# TODO: ApiModel (camelCase-псевдонимы), Booking, booking_from_json, booking_to_json


def booking_from_json(text: str):
    raise NotImplementedError


def booking_to_json(booking) -> str:
    raise NotImplementedError
