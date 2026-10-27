import math


def dot(u, v):
    """Скалярное произведение u_1 * v_1 + ... + u_n * v_n (число типа float).

    Если длины векторов разные, выбросьте ValueError.
    """
    if len(u) != len(v):
        raise ValueError(f"разные длины векторов: {len(u)} и {len(v)}")
    return float(sum(a * b for a, b in zip(u, v)))


def norm(v, p=2):
    """Норма вектора: p = 1 (сумма модулей), p = 2 (длина), p = math.inf (максимум модулей).

    Для пустого вектора верните 0.0. Для любого другого p выбросьте ValueError.
    """
    if p == 1:
        return float(sum(abs(a) for a in v))
    if p == 2:
        return math.sqrt(sum(a * a for a in v))
    if p == math.inf:
        return float(max((abs(a) for a in v), default=0.0))
    raise ValueError(f"неподдерживаемая норма p = {p!r}")


def cosine_similarity(u, v):
    """Косинусное сходство (u . v) / (|u| * |v|), число от -1 до 1.

    ValueError, если длины разные или хотя бы один из векторов нулевой.
    """
    if len(u) != len(v):
        raise ValueError(f"разные длины векторов: {len(u)} и {len(v)}")
    nu, nv = norm(u), norm(v)
    if nu == 0.0 or nv == 0.0:
        raise ValueError("косинус не определён для нулевого вектора")
    return dot(u, v) / (nu * nv)


def top_k_similar(query, vectors, k):
    """Индексы k векторов из vectors, самых похожих на query по косинусу.

    Порядок: по убыванию сходства, при равенстве - по возрастанию индекса.
    Нулевые векторы в vectors пропускаются. Если подходящих векторов меньше k,
    верните все. Для k <= 0 верните []. Нулевой query - ValueError.
    """
    if norm(query) == 0.0:
        raise ValueError("запрос не должен быть нулевым вектором")
    if k <= 0:
        return []
    scored = []
    for i, vec in enumerate(vectors):
        if norm(vec) == 0.0:
            continue
        scored.append((-cosine_similarity(query, vec), i))
    scored.sort()
    return [i for _, i in scored[:k]]
