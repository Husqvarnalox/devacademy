from datetime import date

from pydantic import BaseModel, ConfigDict, Field, computed_field, model_validator
from pydantic.alias_generators import to_camel


class ApiModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="forbid")


class Booking(ApiModel):
    guest_name: str = Field(min_length=1)
    check_in: date
    check_out: date

    @model_validator(mode="after")
    def check_order(self):
        if self.check_out <= self.check_in:
            raise ValueError("check_out must be after check_in")
        return self

    @computed_field
    @property
    def nights(self) -> int:
        return (self.check_out - self.check_in).days


def booking_from_json(text: str) -> Booking:
    return Booking.model_validate_json(text)


def booking_to_json(booking: Booking) -> str:
    return booking.model_dump_json(by_alias=True)
