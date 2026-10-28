import numpy as np


def softmax(logits, temperature=1.0):
    """Устойчивый softmax(logits / temperature) для 1-D массива.

    temperature <= 0 - ValueError. Значения -inf получают вероятность 0; для логитов вроде [1000, 0] предупреждений нет.
    """
    # TODO
    e = np.exp(np.asarray(logits, dtype=np.float64))
    return e / e.sum()


def top_k_filter(logits, k):
    """Копия logits, где все значения, кроме k наибольших, заменены на -inf.

    Ничьи на границе решает np.argsort(-logits, kind="stable")[:k] (меньший индекс выигрывает).
    k is None или k >= len(logits) - копия без изменений; k <= 0 - ValueError.
    """
    # TODO
    return np.array(logits, dtype=np.float64)


def top_p_filter(probs, p):
    """Оставить наименьший префикс токенов (по убыванию вероятности, ничьи - меньший индекс), чья сумма >= p.

    Остальные обнулить, результат перенормировать. Минимум один токен остаётся. p вне (0, 1] - ValueError.
    """
    # TODO
    return np.array(probs, dtype=np.float64)


def sample_next(logits, rng, temperature=1.0, top_k=None, top_p=None):
    """Выбрать следующий токен.

    temperature == 0: int(np.argmax(logits)), top_k и top_p игнорируются, rng не используется.
    Иначе строго по шагам: z = logits / temperature -> top_k_filter(z, top_k) -> softmax(z) ->
    top_p_filter(probs, top_p), если top_p не None -> int(rng.choice(len(probs), p=probs)).
    """
    # TODO
    return int(np.argmax(logits))


def generate(logit_table, start_id, end_id, rng, max_new_tokens, temperature=1.0, top_k=None, top_p=None):
    """Сгенерировать список id без start_id и без end_id.

    Следующие логиты - logit_table[последний токен]. Генерация останавливается, когда выбран end_id
    (он не добавляется) или после max_new_tokens токенов.
    """
    # TODO
    return []
