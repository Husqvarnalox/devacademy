import functools
import inspect
import types
import typing
from typing import Any, Literal, Union, get_args, get_origin, get_type_hints


def check_type(value, tp):
    """True, если value соответствует аннотации tp (во время выполнения)."""
    # TODO: get_origin / get_args
    return True


def bad_arguments(func, *args, **kwargs):
    """Имена параметров func (в порядке сигнатуры), значения которых не подходят по аннотации."""
    # TODO: inspect.signature(...).bind и get_type_hints
    return []


def strict(func):
    """Декоратор: проверяет аргументы и результат по аннотациям, иначе TypeError."""
    # TODO
    return func


def first(items, default):
    # TODO: перепишите как обобщённую функцию first[T](items: list[T], default: T) -> T (PEP 695)
    return default


def matches_typeddict(data, td):
    """True, если dict data соответствует TypedDict td."""
    # TODO
    return True
