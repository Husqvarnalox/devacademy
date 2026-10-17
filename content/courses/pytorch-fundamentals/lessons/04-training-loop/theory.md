<!-- section: simple -->
## Одно и то же пять действий

Любое обучение в PyTorch — это повторение одного и того же небольшого цикла. В курсе [«Основы машинного обучения»](lesson:ml.foundations.linear-regression) мы писали его вручную: вычислить предсказания, потерю, градиенты, сдвинуть веса. В PyTorch те же действия называются иначе, но их ровно пять:

1. **обнулить градиенты**: `optimizer.zero_grad()`;
2. **прямой проход**: `pred = model(x)`;
3. **потеря**: `loss = loss_fn(pred, y)`;
4. **обратный проход**: `loss.backward()` — градиенты оказываются в `.grad` параметров;
5. **шаг оптимизатора**: `optimizer.step()` — параметры сдвигаются.

Оптимизатор — это объект, которому вы один раз отдаёте `model.parameters()` и скорость обучения `lr`. Простейший, **SGD**, делает ровно `w ← w - lr · grad` — то же, что мы писали в NumPy. **Adam** использует тот же градиент, но подбирает размер шага для каждого параметра отдельно и обычно сходится с меньшей настройкой.

Раз цикл один и тот же, он выносится в функцию `train_step`; от задачи к задаче меняются только модель, потеря и данные.

<!-- section: precise -->
## Потери и оптимизаторы

**Потери** лежат в `torch.nn`: `MSELoss` (среднее квадрата ошибок, регрессия), `CrossEntropyLoss` (классификация, [следующий урок](lesson:ml.pytorch.classification)), `L1Loss`. По умолчанию они усредняют по пакету (`reduction='mean'`), поэтому масштаб градиента не зависит от размера пакета. Формы предсказания и цели должны совпадать: если слой выдаёт `(n, 1)`, а цель имеет форму `(n,)`, `MSELoss` сделает broadcasting до `(n, n)` и посчитает **другую величину** (PyTorch выдаёт предупреждение). Приведите формы: `pred.squeeze(-1)`.

**Оптимизаторы** лежат в `torch.optim`: `SGD(params, lr, momentum=0.0, weight_decay=0.0)`, `Adam(params, lr=1e-3)`. Они хранят ссылки на параметры; `step()` читает `.grad` и обновляет `.data` вне графа, поэтому писать `w -= lr * w.grad` самим не нужно.

**Порядок шага.** Градиенты накапливаются ([урок об autograd](lesson:ml.pytorch.autograd)), поэтому `zero_grad()` вызывают либо в начале шага, либо сразу после `step()`; главное — до очередного `backward()`. По умолчанию `zero_grad()` присваивает `.grad = None`.

**Что считать историей.** Сохраняйте `loss.item()` — обычное число. Если сложить сами тензоры `loss`, каждый удержит свой граф и память вырастет.

**Оценка.** Потеря на валидации считается под `torch.no_grad()`: граф не нужен, `.grad` не меняется. Для моделей с `Dropout`/`BatchNorm` добавляется `model.eval()` ([урок 7](lesson:ml.pytorch.checkpoints)).

**Связь с NumPy.** Для линейной модели с `MSELoss` и `SGD` градиентом служит `(2/n) Xᵀ (Xw - y)`. Поэтому обучение `nn.Linear` из нулей воспроизводит [ручной градиентный спуск](lesson:ml.numpy.linear-regression) шаг в шаг, а при достаточном числе эпох приходит к решению `lstsq`.

<!-- section: examples -->
## Примеры

**Пример 1. Обучение nn.Linear и проверка против lstsq.**

```python run
import torch
from torch import nn

torch.manual_seed(0)
g = torch.Generator().manual_seed(0)
X = torch.randn(100, 3, generator=g)
y = X @ torch.tensor([2.0, -1.0, 0.5]) + 1.5 + 0.05 * torch.randn(100, generator=g)

model = nn.Linear(3, 1)
opt = torch.optim.SGD(model.parameters(), lr=0.1)
loss_fn = nn.MSELoss()

history = []
for epoch in range(300):
    opt.zero_grad()
    loss = loss_fn(model(X).squeeze(-1), y)
    loss.backward()
    opt.step()
    history.append(loss.item())

Xb = torch.cat([torch.ones(100, 1), X], dim=1)
ref = torch.linalg.lstsq(Xb, y.unsqueeze(1)).solution.squeeze(1)
print("потеря упала:", round(history[0], 2), "->", round(history[-1], 4))
print("веса:", [round(v, 2) for v in model.weight.detach().reshape(-1).tolist()])
print("смещение:", round(model.bias.item(), 2))
print("совпало с lstsq:", bool(torch.allclose(model.weight.detach().reshape(-1), ref[1:], atol=1e-3)))
```
```output
потеря упала: 9.08 -> 0.0038
веса: [2.0, -1.0, 0.49]
смещение: 1.51
совпало с lstsq: True
```

**Пример 2. Что ломает пропущенный zero_grad.**

```python run
import torch
from torch import nn

def run(zero):
    torch.manual_seed(0)
    g = torch.Generator().manual_seed(1)
    X = torch.randn(64, 2, generator=g)
    y = X @ torch.tensor([1.0, -2.0]) + 0.5
    model = nn.Linear(2, 1)
    opt = torch.optim.SGD(model.parameters(), lr=0.1)
    for _ in range(40):
        if zero:
            opt.zero_grad()
        loss = nn.functional.mse_loss(model(X).squeeze(-1), y)
        loss.backward()
        opt.step()
    return loss.item()

print("с zero_grad потеря мала:", run(True) < 1e-3)
print("без zero_grad потеря огромна:", run(False) > 1.0)
```
```output
с zero_grad потеря мала: True
без zero_grad потеря огромна: True
```

**Пример 3. SGD и Adam на одной задаче.**

```python run
import torch
from torch import nn

g = torch.Generator().manual_seed(2)
X = torch.randn(200, 4, generator=g)
y = (X ** 2).sum(dim=1) - 2.0                      # нелинейная цель

def fit(make_opt, epochs=200):
    torch.manual_seed(0)
    model = nn.Sequential(nn.Linear(4, 16), nn.ReLU(), nn.Linear(16, 1))
    opt = make_opt(model.parameters())
    for _ in range(epochs):
        opt.zero_grad()
        loss = nn.functional.mse_loss(model(X).squeeze(-1), y)
        loss.backward()
        opt.step()
    return loss.item()

print("потеря SGD(0.01) < исходной:", fit(lambda p: torch.optim.SGD(p, lr=0.01)) < y.var().item())
print("потеря Adam(0.01) меньше, чем у SGD(0.01):",
      fit(lambda p: torch.optim.Adam(p, lr=0.01)) < fit(lambda p: torch.optim.SGD(p, lr=0.01)))
```
```output
потеря SGD(0.01) < исходной: True
потеря Adam(0.01) меньше, чем у SGD(0.01): True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Забыть `optimizer.zero_grad()`.** Градиенты складываются, шаги растут, потеря «взрывается» без единой ошибки в логах (пример 2).
* **Рассогласованные формы в `MSELoss`.** Выход `(n, 1)` против цели `(n,)` даёт broadcasting до `(n, n)` и неверную потерю; приводите формы явно.
* **Хранить в истории тензор `loss`.** Сохраняйте `loss.item()`.
* **Поставить `step()` перед `backward()`.** Оптимизатор прочитает старые или пустые градиенты.
* **Считать потерю на валидации с графом.** Оберните в `no_grad()` — иначе расходуется память и можно случайно обучиться на валидации.
* **Передать оптимизатору не те параметры.** Если слой не зарегистрирован в модуле ([урок 3](lesson:ml.pytorch.modules)), он не обновляется.
* **Выбрать слишком большой `lr`.** Потеря растёт или даёт `nan`; для SGD на линейной модели условие сходимости зависит от собственных чисел `XᵀX`, для Adam типичный диапазон — `1e-3 ... 1e-2`.
* **Менять оптимизатор на полпути, не пересоздав его.** Состояние Adam (моменты) привязано к параметрам; новый оптимизатор начинает с нуля.

<!-- section: summary -->
## Итоги

* Цикл обучения: `zero_grad`, прямой проход, потеря, `backward`, `step`; потери в историю кладите как числа.
* `SGD` повторяет ручной градиентный спуск (`w ← w - lr · g`); `Adam` адаптирует шаг для каждого параметра.
* Для линейной модели с MSE обучение `nn.Linear` из нулей сходится к решению наименьших квадратов, и это удобная проверка кода.
* Оценка идёт под `no_grad`; формы предсказания и цели должны совпадать.
* Скорость обучения подбирается и для SGD, и для Adam.

Дальше: [классификация небольшой сетью](lesson:ml.pytorch.classification).

<!-- section: deep-dive -->
## Сравнение с NumPy и GoMLX

В [курсе по NumPy](lesson:ml.numpy.linear-regression) градиент и обновление были двумя строками, которые вы выводили сами; здесь первую строку делает `backward()`, вторую — `step()`. В [GoMLX](lesson:go.ml.gomlx.linear-regression-gradient-descent) шаг обучения описывается как граф и вызывается как одна скомпилированная функция. Идея везде одинакова: потеря, градиент, сдвиг против градиента. Для проверки собственного кода полезно держать в голове эталон: линейная регрессия обязана сойтись к `lstsq`.
