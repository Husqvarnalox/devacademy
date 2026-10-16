import functools
import inspect
import types
import typing
from typing import Any, Literal, Union, get_args, get_origin, get_type_hints


def check_type(value, tp):
    """True, если value соответствует аннотации tp (во время выполнения)."""
    if tp is Any:
        return True
    if tp is None or tp is type(None):
        return value is None
    origin = get_origin(tp)
    if origin is None:
        return isinstance(value, tp)
    args = get_args(tp)
    if origin is Union or origin is types.UnionType:
        return any(check_type(value, a) for a in args)
    if origin is Literal:
        return any(value == a and type(value) is type(a) for a in args)
    if not isinstance(value, origin):
        return False
    if origin in (list, set, frozenset):
        return not args or all(check_type(v, args[0]) for v in value)
    if origin is dict:
        return not args or all(check_type(k, args[0]) and check_type(v, args[1]) for k, v in value.items())
    if origin is tuple:
        if not args:
            return True
        if len(args) == 2 and args[1] is Ellipsis:
            return all(check_type(v, args[0]) for v in value)
        return len(value) == len(args) and all(check_type(v, a) for v, a in zip(value, args))
    return True


def bad_arguments(func, *args, **kwargs):
    """Имена параметров func (в порядке сигнатуры), значения которых не подходят по аннотации."""
    sig = inspect.signature(func)
    bound = sig.bind(*args, **kwargs)
    hints = get_type_hints(func)
    bad = []
    for name, value in bound.arguments.items():
        kind = sig.parameters[name].kind
        if kind in (inspect.Parameter.VAR_POSITIONAL, inspect.Parameter.VAR_KEYWORD):
            continue
        if name in hints and not check_type(value, hints[name]):
            bad.append(name)
    return bad


def strict(func):
    """Декоратор: проверяет аргументы и результат по аннотациям, иначе TypeError."""
    @functools.wraps(func)
    def wrapper(*args, **kwargs):
        bad = bad_arguments(func, *args, **kwargs)
        if bad:
            raise TypeError("неверный тип аргументов: " + ", ".join(bad))
        result = func(*args, **kwargs)
        hints = get_type_hints(func)
        if "return" in hints and not check_type(result, hints["return"]):
            raise TypeError(f"неверный тип результата: {type(result).__name__}")
        return result

    return wrapper


def first[T](items: list[T], default: T) -> T:
    return items[0] if items else default


def matches_typeddict(data, td):
    """True, если dict data соответствует TypedDict td."""
    hints = get_type_hints(td)
    return (
        isinstance(data, dict)
        and set(data) <= set(hints)
        and td.__required_keys__ <= set(data)
        and all(check_type(v, hints[k]) for k, v in data.items())
    )
