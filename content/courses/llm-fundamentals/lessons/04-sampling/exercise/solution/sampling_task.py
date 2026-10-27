import numpy as np


def softmax(logits, temperature=1.0):
    """Устойчивый softmax(logits / temperature) для 1-D массива.

    temperature <= 0 - ValueError. Значения -inf получают вероятность 0; для логитов вроде [1000, 0] предупреждений нет.
    """
    if not temperature > 0:
        raise ValueError("temperature должна быть больше 0")
    z = np.asarray(logits, dtype=np.float64) / temperature
    e = np.exp(z - np.max(z))
    return e / e.sum()


def top_k_filter(logits, k):
    """Копия logits, где все значения, кроме k наибольших, заменены на -inf.

    Ничьи на границе решает np.argsort(-logits, kind="stable")[:k] (меньший индекс выигрывает).
    k is None или k >= len(logits) - копия без изменений; k <= 0 - ValueError.
    """
    z = np.array(logits, dtype=np.float64)
    if k is None:
        return z
    if k <= 0:
        raise ValueError("k должно быть положительным")
    if k >= z.size:
        return z
    keep = np.argsort(-z, kind="stable")[:k]
    out = np.full_like(z, -np.inf)
    out[keep] = z[keep]
    return out


def top_p_filter(probs, p):
    """Оставить наименьший префикс токенов (по убыванию вероятности, ничьи - меньший индекс), чья сумма >= p.

    Остальные обнулить, результат перенормировать. Минимум один токен остаётся. p вне (0, 1] - ValueError.
    """
    if not 0.0 < p <= 1.0:
        raise ValueError("p должно быть в диапазоне (0, 1]")
    probs = np.asarray(probs, dtype=np.float64)
    order = np.argsort(-probs, kind="stable")
    cum = np.cumsum(probs[order])
    n_keep = min(int(np.searchsorted(cum, p, side="left")) + 1, probs.size)
    out = np.zeros_like(probs)
    out[order[:n_keep]] = probs[order[:n_keep]]
    return out / out.sum()


def sample_next(logits, rng, temperature=1.0, top_k=None, top_p=None):
    """Выбрать следующий токен.

    temperature == 0: int(np.argmax(logits)), top_k и top_p игнорируются, rng не используется.
    Иначе строго по шагам: z = logits / temperature -> top_k_filter(z, top_k) -> softmax(z) ->
    top_p_filter(probs, top_p), если top_p не None -> int(rng.choice(len(probs), p=probs)).
    """
    logits = np.asarray(logits, dtype=np.float64)
    if temperature == 0:
        return int(np.argmax(logits))
    if temperature < 0:
        raise ValueError("temperature не может быть отрицательной")
    z = top_k_filter(logits / temperature, top_k)
    probs = softmax(z)
    if top_p is not None:
        probs = top_p_filter(probs, top_p)
    return int(rng.choice(len(probs), p=probs))


def generate(logit_table, start_id, end_id, rng, max_new_tokens, temperature=1.0, top_k=None, top_p=None):
    """Сгенерировать список id без start_id и без end_id.

    Следующие логиты - logit_table[последний токен]. Генерация останавливается, когда выбран end_id
    (он не добавляется) или после max_new_tokens токенов.
    """
    out = []
    last = start_id
    for _ in range(max_new_tokens):
        token = sample_next(logit_table[last], rng, temperature, top_k, top_p)
        if token == end_id:
            break
        out.append(token)
        last = token
    return out
