def add(u, v):
    """Сумма векторов одной длины: новый список [u[0] + v[0], u[1] + v[1], ...].

    Если длины разные, выбросьте ValueError.
    """
    if len(u) != len(v):
        raise ValueError(f"разные длины векторов: {len(u)} и {len(v)}")
    return [a + b for a, b in zip(u, v)]


def scale(c, v):
    """Произведение числа c на вектор v: новый список [c * v[0], c * v[1], ...]."""
    return [c * a for a in v]


def linear_combination(coeffs, vectors):
    """c_1 * v_1 + ... + c_k * v_k для coeffs = [c_1, ..., c_k] и vectors = [v_1, ..., v_k].

    ValueError, если число коэффициентов не равно числу векторов, если векторов нет
    или если у векторов разная длина.
    """
    if len(coeffs) != len(vectors):
        raise ValueError("число коэффициентов не равно числу векторов")
    if not vectors:
        raise ValueError("нужен хотя бы один вектор")
    result = [0.0] * len(vectors[0])
    for c, v in zip(coeffs, vectors):
        result = add(result, scale(c, v))
    return result


def mean_vector(vectors):
    """Покомпонентное среднее непустого списка векторов одной длины (центр группы).

    ValueError для пустого списка или векторов разной длины.
    """
    if not vectors:
        raise ValueError("среднее пустого набора векторов не определено")
    k = len(vectors)
    return linear_combination([1.0 / k] * k, vectors)
