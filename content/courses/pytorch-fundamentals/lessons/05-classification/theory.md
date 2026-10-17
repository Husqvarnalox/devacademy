<!-- section: simple -->
## От чисел к классам

В регрессии сеть отвечает числом. В классификации она должна выбрать один вариант из нескольких: к какому из трёх облаков относится точка. Сеть сначала выдаёт для каждого класса одно число, **логит** («сырую оценку»), и чем он больше, тем увереннее сеть в этом классе. Предсказанный класс — это номер наибольшего логита. О том, как оценивать классификаторы, подробно рассказано в уроке про [метрики классификации](lesson:ml.foundations.classification-metrics).

Чтобы учить сеть, нужна потеря, которая наказывает за низкую уверенность в правильном классе. В PyTorch это `nn.CrossEntropyLoss`. Она берёт логиты и номер верного класса и сама превращает логиты в вероятности (softmax), а затем возвращает минус логарифм вероятности верного класса. Поэтому сеть возвращает **логиты**, а softmax вы сами не применяете.

Наша сеть небольшая: `Linear → ReLU → Linear`. Между слоями стоит нелинейность `ReLU` (ноль для отрицательных чисел и само число для остальных). Без неё два линейных слоя были бы равны одному, и сеть умела бы лишь проводить прямые границы между классами.

<!-- section: precise -->
## Что именно требует PyTorch

* **Логиты:** тензор формы `(N, C)` типа `float32`, где `N` — объекты, `C` — классы.
* **Метки:** тензор формы `(N,)` типа `int64` с номерами классов `0 ... C-1`, а не вероятности и не one-hot.
* **Потеря:** `CrossEntropyLoss()(logits, y)` — скаляр; внутри `log_softmax` и среднее отрицательное логарифмическое правдоподобие. На равномерных логитах она равна `ln C`: для трёх классов это около 1.10.
* **Предсказание:** `logits.argmax(dim=1)`. Если нужны вероятности для показа, применяйте `softmax` отдельно и только при выводе.
* **Точность:** `(pred == y).float().mean().item()`.
* **Число параметров** `Linear(in, out)`: `in · out + out`. Для `2 → 8 → 3` это `2·8 + 8 + 8·3 + 3 = 51`.

**Воспроизводимость.** Начальные веса слоёв берутся из глобального генератора, поэтому `torch.manual_seed(seed)` нужно вызвать **до** создания сети. Данные лучше генерировать отдельным `torch.Generator`, чтобы вызов обучения не менял данные.

**Две точности.** Сеть оптимизируется на обучающих данных, поэтому точность на них завышена. Настоящее качество показывает выборка, которой не было при обучении ([это же вы делали в курсе по NumPy](lesson:ml.numpy.logistic-regression)). Для оценки оборачивайте вычисления в `torch.no_grad()`.

<!-- section: examples -->
## Примеры

**Пример 1. Что делает CrossEntropyLoss.**

```python run
import torch
from torch import nn

logits = torch.tensor([[2.0, 0.5, -1.0], [0.1, 0.2, 0.3]])
y = torch.tensor([0, 2])

loss = nn.CrossEntropyLoss()(logits, y)
manual = -torch.log_softmax(logits, dim=1)[torch.arange(2), y].mean()
uniform = nn.CrossEntropyLoss()(torch.zeros(1, 3), torch.tensor([1]))

print("потеря:", round(loss.item(), 4))
print("совпало с ручным расчётом:", bool(torch.allclose(loss, manual)))
print("равномерные логиты дают ln 3:", round(uniform.item(), 4))
print("предсказания:", logits.argmax(dim=1).tolist())
```
```output
потеря: 0.6216
совпало с ручным расчётом: True
равномерные логиты дают ln 3: 1.0986
предсказания: [0, 2]
```

**Пример 2. Обучение MLP на трёх облаках.**

```python run
import torch
from torch import nn

torch.set_num_threads(1)
g = torch.Generator().manual_seed(0)
centers = torch.tensor([[0.0, 0.0], [3.0, 0.0], [0.0, 3.0]])
X = (centers[:, None, :] + 0.6 * torch.randn(3, 50, 2, generator=g)).reshape(-1, 2)
y = torch.arange(3).repeat_interleave(50)

torch.manual_seed(0)
model = nn.Sequential(nn.Linear(2, 16), nn.ReLU(), nn.Linear(16, 3))
opt = torch.optim.Adam(model.parameters(), lr=0.05)
loss_fn = nn.CrossEntropyLoss()

for epoch in range(100):
    opt.zero_grad()
    loss = loss_fn(model(X), y)
    loss.backward()
    opt.step()
    if epoch in (0, 99):
        print("эпоха", epoch, "потеря", round(loss.item(), 2))

with torch.no_grad():
    acc = (model(X).argmax(dim=1) == y).float().mean().item()
print("точность на обучении выше 0.95:", acc > 0.95)
print("параметров:", sum(p.numel() for p in model.parameters()))
```
```output
эпоха 0 потеря 1.34
эпоха 99 потеря 0.01
точность на обучении выше 0.95: True
параметров: 99
```

**Пример 3. Softmax перед CrossEntropyLoss портит обучение.**

```python run
import torch
from torch import nn

g = torch.Generator().manual_seed(0)
centers = torch.tensor([[0.0, 0.0], [3.0, 0.0], [0.0, 3.0]])
X = (centers[:, None, :] + 0.6 * torch.randn(3, 50, 2, generator=g)).reshape(-1, 2)
y = torch.arange(3).repeat_interleave(50)

def fit(with_softmax):
    torch.manual_seed(0)
    layers = [nn.Linear(2, 16), nn.ReLU(), nn.Linear(16, 3)]
    if with_softmax:
        layers.append(nn.Softmax(dim=1))
    model = nn.Sequential(*layers)
    opt = torch.optim.Adam(model.parameters(), lr=0.05)
    for _ in range(60):
        opt.zero_grad()
        loss = nn.CrossEntropyLoss()(model(X), y)
        loss.backward()
        opt.step()
    return loss.item()

print("потеря с логитами меньше 0.2:", fit(False) < 0.2)
print("потеря с лишним softmax больше 0.5:", fit(True) > 0.5)
```
```output
потеря с логитами меньше 0.2: True
потеря с лишним softmax больше 0.5: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Softmax на выходе сети при `CrossEntropyLoss`** (пример 3): потеря не опускается ниже ограниченного значения, градиенты слабее.
* **Метки типа `float` или формы `(N, 1)`.** Нужны `int64` формы `(N,)`; иначе ошибка или молчаливая неверная работа.
* **Путаница размерностей `argmax`.** Для логитов `(N, C)` берите `dim=1`; `dim=0` выбирает лучший объект для каждого класса.
* **`seed` после создания сети.** Начальные веса уже взяты из генератора; результаты будут различаться между запусками.
* **Оценка только на обучающих данных.** Это завышает качество; держите отдельную выборку.
* **Нет нелинейности между слоями.** Сеть остаётся линейной.
* **Забыли `no_grad` при оценке.** Работает, но тратит память и тащит граф.

<!-- section: summary -->
## Итоги

* Сеть выдаёт логиты `(N, C)`, метки — `int64` формы `(N,)`.
* `CrossEntropyLoss` включает softmax; на равномерных логитах равна `ln C`.
* Предсказание — `argmax(dim=1)`; точность — доля совпадений.
* `Linear(in, out)` имеет `in·out + out` параметров; между слоями нужна нелинейность.
* Данные и сеть инициализируйте с seed, оценивайте на новых данных.

Дальше: [Dataset, DataLoader и переобучение](lesson:ml.pytorch.data-loading).

<!-- section: deep-dive -->
## Логит и сигмоида

Для двух классов можно брать один логит и функцию `BCEWithLogitsLoss`: это то, что вы видели в [логистической регрессии на GoMLX](lesson:go.ml.gomlx.logistic-regression). Для `C` классов естественное обобщение сигмоиды — softmax, а потеря превращается в перекрёстную энтропию. Работа с логитами, а не с вероятностями, численно устойчивее: `log_softmax` вычисляется без переполнения даже при больших значениях.
