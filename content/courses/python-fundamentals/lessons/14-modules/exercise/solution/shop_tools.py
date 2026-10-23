import math
import random


def circle_area(radius):
    return math.pi * radius ** 2


def pizzas_needed(people, slices_per_pizza=8):
    return math.ceil(people / slices_per_pizza)


def distance(x1, y1, x2, y2):
    return math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2)


def roll_dice(count, seed):
    random.seed(seed)
    rolls = []
    for _ in range(count):
        rolls.append(random.randint(1, 6))
    return rolls
