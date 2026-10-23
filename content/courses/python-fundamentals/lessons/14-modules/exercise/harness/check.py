import json
import math
import os
import random

NONCE = os.environ["DEVACADEMY_NONCE"]

import shop_tools as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def close(actual, expected, call):
    if not isinstance(actual, (int, float)) or isinstance(actual, bool) or abs(actual - expected) > 1e-9:
        return f"{call} вернуло {actual!r}, ожидалось около {expected!r}"
    return None


def exact(actual, expected, call):
    if actual != expected or type(actual) is not type(expected):
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


def t_area():
    for r in (1, 2, 0.5):
        bad = close(m.circle_area(r), math.pi * r * r, f"circle_area({r})")
        if bad:
            return bad


def t_area_zero():
    return close(m.circle_area(0), 0, "circle_area(0)")


def t_pizza_basic():
    for people, want in [(1, 1), (8, 1), (9, 2), (17, 3)]:
        bad = exact(m.pizzas_needed(people), want, f"pizzas_needed({people})")
        if bad:
            return bad


def t_pizza_custom():
    for people, per, want in [(10, 4, 3), (12, 4, 3), (0, 8, 0)]:
        bad = exact(m.pizzas_needed(people, per), want, f"pizzas_needed({people}, {per})")
        if bad:
            return bad
    return exact(m.pizzas_needed(8, slices_per_pizza=6), 2, "pizzas_needed(8, slices_per_pizza=6)")


def t_distance():
    for args, want in [((0, 0, 3, 4), 5.0), ((1, 1, 1, 1), 0.0), ((-1, -1, 2, 3), 5.0), ((0, 0, 1, 1), math.sqrt(2))]:
        bad = close(m.distance(*args), want, f"distance{args}")
        if bad:
            return bad


def t_dice_seed():
    for count, seed in [(5, 1), (3, 42), (10, 7)]:
        random.seed(seed)
        want = [random.randint(1, 6) for _ in range(count)]
        bad = exact(m.roll_dice(count, seed), want, f"roll_dice({count}, {seed})")
        if bad:
            return bad


def t_dice_range():
    result = m.roll_dice(200, 3)
    if len(result) != 200:
        return f"roll_dice(200, 3) вернуло {len(result)} бросков"
    if any(not isinstance(x, int) or x < 1 or x > 6 for x in result):
        return "значения бросков должны быть целыми от 1 до 6"


def t_dice_empty():
    return exact(m.roll_dice(0, 5), [], "roll_dice(0, 5)")


case("area-basic", t_area)
case("pizza-basic", t_pizza_basic)
case("distance-basic", t_distance)
case("dice-seed", t_dice_seed)
case("area-zero", t_area_zero)
case("pizza-custom", t_pizza_custom)
case("dice-range", t_dice_range)
case("dice-empty", t_dice_empty)
