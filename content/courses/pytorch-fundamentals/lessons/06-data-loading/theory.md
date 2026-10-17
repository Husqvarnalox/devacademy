<!-- section: simple -->
## Данные приходят пакетами

До сих пор мы подавали в сеть все данные сразу. На практике данные не помещаются в память целиком или их удобнее давать порциями: **пакетами** (batches). PyTorch делит эту работу на две части:

* **Dataset** отвечает на вопрос «что лежит под номером i?» и «сколько всего образцов?»;
* **DataLoader** берёт такой набор, сам перемешивает номера, собирает образцы в пакеты и отдаёт их по одному.

Второе важное умение — не обманывать себя. Если проверять сеть на тех же данных, на которых она училась, она может просто запомнить ответы. Поэтому данные делят на **обучающую** (train) и **валидационную** (val) части: сеть учится на первой и проверяется на второй.

Проверка на валидации показывает **переобучение**: потеря на обучении продолжает падать, а на валидации после какого-то момента начинает расти. Тогда обучение разумно остановить и вернуть лучшую версию сети. Это называется **ранней остановкой**.

<!-- section: precise -->
## Устройство и параметры

**Dataset в стиле map.** Класс наследуется от `torch.utils.data.Dataset` и реализует `__len__` и `__getitem__(i)`. Обычно `__getitem__` возвращает кортеж `(x, y)`.

**DataLoader.** Основные параметры: `batch_size`; `shuffle=True` перемешивает порядок в каждую эпоху; `generator` задаёт случайность перемешивания (для воспроизводимости); `drop_last` отбрасывает неполный последний пакет; `num_workers=0` загружает данные в главном процессе (в нашем окружении этого достаточно). Пакет — это кортеж тензоров с новой первой размерностью: образцы `(2,)` в пакете размера 4 превращаются в `(4, 2)`.

**Число пакетов** при `drop_last=False` равно `ceil(n / batch_size)`. Шагов оптимизатора за эпоху ровно столько же ([урок про цикл обучения](lesson:ml.pytorch.training-loop)).

**Разбиение.** Простейший способ: перемешать индексы `torch.randperm(n, generator=g)` и отрезать долю. Индексы не должны пересекаться. Для классификации важно, чтобы доля классов в частях не слишком отличалась, а валидация не использовалась для обучения вообще.

**Ранняя остановка.** После каждой эпохи считаем потерю на валидации. Если она **строго** лучше прежней — запоминаем эпоху и копию весов (`copy.deepcopy(model.state_dict())`; без копии словарь ссылается на живые тензоры и изменится при следующем шаге). Если улучшения нет `patience` эпох подряд, цикл прерывается; в модель возвращаются лучшие веса.

**Режимы.** Перед обучением `model.train()`, перед оценкой `model.eval()` и `torch.no_grad()`. Для нашей сети без Dropout и BatchNorm различий нет, но привычка важна ([урок 7](lesson:ml.pytorch.checkpoints)).

<!-- section: examples -->
## Примеры

**Пример 1. Dataset и DataLoader.**

```python run
import torch
from torch.utils.data import DataLoader, Dataset

class Squares(Dataset):
    def __init__(self, n):
        self.x = torch.arange(n, dtype=torch.float32)

    def __len__(self):
        return len(self.x)

    def __getitem__(self, i):
        return self.x[i], self.x[i] ** 2

ds = Squares(10)
print("длина:", len(ds), "элемент 3:", [v.item() for v in ds[3]])

loader = DataLoader(ds, batch_size=4, shuffle=False)
print("пакетов:", len(loader))
for xb, yb in loader:
    print(tuple(xb.shape), xb.tolist())
```
```output
длина: 10 элемент 3: [3.0, 9.0]
пакетов: 3
(4,) [0.0, 1.0, 2.0, 3.0]
(4,) [4.0, 5.0, 6.0, 7.0]
(2,) [8.0, 9.0]
```

**Пример 2. Воспроизводимое перемешивание и разбиение.**

```python run
import torch
from torch.utils.data import DataLoader, TensorDataset

ds = TensorDataset(torch.arange(8, dtype=torch.float32), torch.arange(8))

def order(seed):
    g = torch.Generator().manual_seed(seed)
    loader = DataLoader(ds, batch_size=4, shuffle=True, generator=g)
    return [int(i) for _, yb in loader for i in yb]

print("seed 1 повторяется:", order(1) == order(1))
print("seed 1 и 2 различаются:", order(1) != order(2))
print("все объекты на месте:", sorted(order(1)) == list(range(8)))

g = torch.Generator().manual_seed(0)
perm = torch.randperm(10, generator=g)
train_idx, val_idx = perm[2:], perm[:2]
print("размеры:", len(train_idx), len(val_idx))
print("пересечение пусто:", set(train_idx.tolist()).isdisjoint(val_idx.tolist()))
```
```output
seed 1 повторяется: True
seed 1 и 2 различаются: True
все объекты на месте: True
размеры: 8 2
пересечение пусто: True
```

**Пример 3. Переобучение на шумных данных.**

```python run
import torch
from torch import nn

torch.set_num_threads(1)
g = torch.Generator().manual_seed(0)
X = torch.randn(90, 2, generator=g)
y = ((X[:, 0] + 1.5 * torch.randn(90, generator=g)) > 0).long()
Xtr, ytr, Xva, yva = X[:54], y[:54], X[54:], y[54:]

torch.manual_seed(0)
model = nn.Sequential(nn.Linear(2, 64), nn.ReLU(), nn.Linear(64, 64), nn.ReLU(), nn.Linear(64, 2))
opt = torch.optim.Adam(model.parameters(), lr=0.02)
loss_fn = nn.CrossEntropyLoss()
tr, va = [], []
for epoch in range(200):
    opt.zero_grad()
    loss = loss_fn(model(Xtr), ytr)
    loss.backward()
    opt.step()
    tr.append(loss.item())
    with torch.no_grad():
        va.append(loss_fn(model(Xva), yva).item())

print("потеря на обучении упала:", tr[-1] < 0.5 * tr[0])
print("лучшая потеря на валидации раньше конца:", va.index(min(va)) < 100)
print("в конце валидация хуже лучшей:", va[-1] > min(va) + 0.1)
```
```output
потеря на обучении упала: True
лучшая потеря на валидации раньше конца: True
в конце валидация хуже лучшей: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Оценка на обучающих данных.** Точность близка к идеальной, а на новых данных качество низкое.
* **Пересечение train и val.** Если индексы повторяются, валидация перестаёт быть честной (утечка данных).
* **Хранить в `best_state` сам `state_dict()`.** Он ссылается на живые тензоры и изменяется вместе с моделью; нужна `deepcopy`.
* **Сдвиг на единицу в ранней остановке.** Пересчитывайте остановку как `epoch - best_epoch >= patience`; проверяйте на маленьком примере.
* **Нестрогое сравнение.** `v <= best` сдвигает лучшую эпоху при равенстве и ломает счёт терпения.
* **Забыть `shuffle=True` при обучении.** Пакеты идут в одном порядке, обучение хуже; для валидации перемешивание не нужно.
* **Случайность без seed.** Порядок пакетов меняется между запусками, результаты нельзя сравнить.
* **Использовать валидацию для подбора слишком многого.** Если многократно подбирать параметры по одной и той же валидации, она тоже «переобучается»; для финальной оценки нужна отдельная тестовая выборка.

<!-- section: summary -->
## Итоги

* `Dataset` отвечает за `__len__` и `__getitem__`, `DataLoader` — за пакеты и перемешивание.
* `generator` и `manual_seed` делают порядок и разбиение воспроизводимыми.
* Обучение, валидация и (в конце) тест — разные данные.
* Переобучение видно по расхождению потерь; ранняя остановка с patience и возвратом лучших весов защищает от него.
* Копируйте `state_dict` через `deepcopy`.

Дальше: [сохранение моделей и режимы train/eval](lesson:ml.pytorch.checkpoints).

<!-- section: deep-dive -->
## Валидация и метрики

Выбор метрики на валидации — отдельный вопрос: для несбалансированных классов точность вводит в заблуждение, и нужны точность и полнота ([метрики классификации](lesson:ml.foundations.classification-metrics)). Разбиение на train и val в [курсе по NumPy](lesson:ml.numpy.logistic-regression) делалось вручную; здесь это те же идеи, но с инструментами PyTorch. Обратите внимание: ранняя остановка уже использует валидацию для выбора эпохи, поэтому итоговую оценку лучше делать на третьей, ни разу не использованной выборке.
