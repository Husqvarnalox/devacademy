import numpy as np


def collapse_affine_layers(Ws, bs):
    """Свернуть цепочку слоёв x -> W_i x + b_i (i = 0, 1, ...) в один слой x -> W x + b.

    Ws - список матриц (d_i, d_{i-1}), bs - список векторов (d_i,). Слои применяются в порядке списка.
    Верните кортеж (W, b) из новых массивов float. ValueError, если списки пусты, имеют разную длину
    или формы слоёв не согласуются.
    """
    if len(Ws) == 0 or len(Ws) != len(bs):
        raise ValueError("нужен непустой список матриц и столько же векторов сдвига")
    W = np.array(Ws[0], dtype=float)
    b = np.array(bs[0], dtype=float)
    if W.ndim != 2 or b.shape != (W.shape[0],):
        raise ValueError("слой 0: ожидаются W (d_out, d_in) и b (d_out,)")
    for i in range(1, len(Ws)):
        Wi = np.asarray(Ws[i], dtype=float)
        bi = np.asarray(bs[i], dtype=float)
        if Wi.ndim != 2 or bi.shape != (Wi.shape[0],):
            raise ValueError(f"слой {i}: ожидаются W (d_out, d_in) и b (d_out,)")
        if Wi.shape[1] != W.shape[0]:
            raise ValueError(f"слой {i}: {Wi.shape[1]} входов, а предыдущий слой даёт {W.shape[0]} выходов")
        W, b = Wi @ W, Wi @ b + bi
    return W, b


def markov_distribution(P, p0, steps):
    """Распределение вероятностей через steps шагов марковской цепи: p0 @ P^steps.

    P - матрица переходов (n, n), строки которой суммируются в 1, p0 - вектор (n,) с суммой 1, steps >= 0.
    ValueError, если P не квадратная, суммы строк P или сумма p0 отличаются от 1 больше чем на 1e-9,
    форма p0 не совпадает с P или steps < 0. Верните новый массив float (при steps = 0 - копию p0).
    """
    P = np.asarray(P, dtype=float)
    p0 = np.array(p0, dtype=float)
    if P.ndim != 2 or P.shape[0] != P.shape[1]:
        raise ValueError("P должна быть квадратной матрицей")
    if p0.shape != (P.shape[0],):
        raise ValueError("p0 должен иметь длину, равную размеру P")
    if steps < 0:
        raise ValueError("число шагов должно быть неотрицательным")
    if not np.allclose(P.sum(axis=1), 1.0, rtol=0, atol=1e-9):
        raise ValueError("строки P должны суммироваться в 1")
    if abs(p0.sum() - 1.0) > 1e-9:
        raise ValueError("p0 должен суммироваться в 1")
    return p0 @ np.linalg.matrix_power(P, int(steps))
