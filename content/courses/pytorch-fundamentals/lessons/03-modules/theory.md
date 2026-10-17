<!-- section: simple -->
## Модель как объект, который хранит параметры

Модель — это функция с настраиваемыми числами. В коде NumPy мы хранили веса в отдельных переменных и таскали их по функциям. В PyTorch для этого есть класс `nn.Module`: объект, который **знает свои параметры** и умеет вычислять выход. У него два обязательных элемента:

* `__init__` — создаёт слои и сохраняет их как атрибуты;
* `forward(x)` — описывает вычисление; вызывается как `model(x)`.

Готовые слои (`nn.Linear`, `nn.ReLU`, `nn.Dropout`, `nn.BatchNorm1d`) сами являются модулями; модуль может содержать другие модули — получается дерево. Благодаря этому у любой модели есть единая точка доступа к параметрам: `model.parameters()`. Её получает оптимизатор, её обходит код сохранения, её мы проверяем в тестах.

Самый важный совет урока: **слои, которые должны обучаться, нужно регистрировать**. Для списка слоёв используйте `nn.ModuleList` или `nn.Sequential`, а не обычный `list`.

<!-- section: precise -->
## Устройство nn.Module

**Регистрация.** Когда вы присваиваете `self.fc = nn.Linear(...)` внутри `__init__`, модуль замечает, что значение — подмодуль, и заносит его в служебный словарь. Параметры (`nn.Parameter`) регистрируются так же. Всё, что лежит в обычных `list` и `dict`, **не регистрируется**. Контейнеры:

* `nn.Sequential(layer1, layer2, ...)` — последовательная цепочка; `forward` уже написан;
* `nn.ModuleList([...])` — список модулей без готового `forward`; вы сами решаете, как их применять;
* `nn.ModuleDict({...})` — то же для словаря.

**Параметры и имена.** `named_parameters()` возвращает пары `(имя, параметр)`; имя собирается из пути атрибутов: `layers.0.weight`, `layers.0.bias`, `layers.1.weight`. Тот же путь используется как ключ в `state_dict()` (см. [урок о сохранении](lesson:ml.pytorch.checkpoints)). Для `nn.Linear(n_in, n_out)` вес имеет форму `(n_out, n_in)`, смещение — `(n_out,)`, всего `n_in * n_out + n_out` параметров; вычисление `x @ W.T + b`.

**Инициализация.** Слои создаются со случайными начальными значениями, поэтому перед созданием модели фиксируйте `torch.manual_seed`. Перезаписывать веса вручную нужно **под `torch.no_grad()`** (параметр — лист графа, запись в него «в графе» запрещена): `p.fill_(0.5)` или `nn.init.zeros_(p)`.

**Заморозка.** `p.requires_grad_(False)` исключает параметр из графа: градиент по нему не считается, `p.grad` остаётся `None`. Если оптимизатор получил замороженные параметры, он их просто пропустит (у них нет градиента).

**Режимы.** `model.train()` и `model.eval()` переключают поведение слоёв вроде `Dropout` и `BatchNorm`; на градиенты они не влияют. Подробнее — в [последнем уроке](lesson:ml.pytorch.checkpoints).

<!-- section: examples -->
## Примеры

**Пример 1. Линейный слой и число параметров.**

```python run
import torch
from torch import nn

torch.manual_seed(0)
layer = nn.Linear(3, 2)
print("вес:", tuple(layer.weight.shape), "смещение:", tuple(layer.bias.shape))
print("параметров:", sum(p.numel() for p in layer.parameters()))

x = torch.tensor([[1.0, 2.0, 3.0]])
manual = x @ layer.weight.T + layer.bias
print("совпадает с ручной формулой:", bool(torch.allclose(layer(x), manual)))
```
```output
вес: (2, 3) смещение: (2,)
параметров: 8
совпадает с ручной формулой: True
```

**Пример 2. Свой Module и Sequential.**

```python run
import torch
from torch import nn


class Net(nn.Module):
    def __init__(self):
        super().__init__()
        self.fc1 = nn.Linear(2, 4)
        self.fc2 = nn.Linear(4, 1)

    def forward(self, x):
        return self.fc2(torch.relu(self.fc1(x)))


net = Net()
print("имена:", [n for n, _ in net.named_parameters()])
print("выход для батча из 5:", tuple(net(torch.zeros(5, 2)).shape))

seq = nn.Sequential(nn.Linear(2, 4), nn.ReLU(), nn.Linear(4, 1))
print("ключи Sequential:", list(seq.state_dict().keys()))
print("параметров в обеих:", sum(p.numel() for p in net.parameters()), sum(p.numel() for p in seq.parameters()))
```
```output
имена: ['fc1.weight', 'fc1.bias', 'fc2.weight', 'fc2.bias']
выход для батча из 5: (5, 1)
ключи Sequential: ['0.weight', '0.bias', '2.weight', '2.bias']
параметров в обеих: 17 17
```

**Пример 3. Ловушка обычного списка и заморозка.**

```python run
import torch
from torch import nn


class Broken(nn.Module):
    def __init__(self):
        super().__init__()
        self.layers = [nn.Linear(2, 2), nn.Linear(2, 1)]     # обычный list: слои не зарегистрированы


class Fine(nn.Module):
    def __init__(self):
        super().__init__()
        self.layers = nn.ModuleList([nn.Linear(2, 2), nn.Linear(2, 1)])


print("Broken параметров:", len(list(Broken().parameters())))
fine = Fine()
print("Fine параметров:", len(list(fine.parameters())))

for p in fine.layers[0].parameters():
    p.requires_grad_(False)
out = fine.layers[1](fine.layers[0](torch.ones(1, 2))).sum()
out.backward()
print("градиент замороженного слоя:", fine.layers[0].weight.grad)
print("градиент второго слоя есть:", fine.layers[1].weight.grad is not None)
```
```output
Broken параметров: 0
Fine параметров: 4
градиент замороженного слоя: None
градиент второго слоя есть: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Слои в обычном списке.** Параметры невидимы для `parameters()`, оптимизатор их не обновляет, `state_dict` их не сохраняет; модель «молча» не учится. Всегда `nn.ModuleList`/`nn.Sequential`.
* **Забыть `super().__init__()`.** Без него регистрация атрибутов не работает, и PyTorch бросит ошибку при присваивании подмодуля.
* **Вызывать `model.forward(x)` вместо `model(x)`.** Прямой вызов обходит хуки; принято писать `model(x)`.
* **Активация после последнего слоя.** Для регрессии и для классификации с `CrossEntropyLoss` выход должен быть «сырыми» значениями (логитами). Лишний ReLU обрезает отрицательные значения.
* **Менять веса вне `no_grad`.** Запись в лист графа вызывает ошибку; используйте `with torch.no_grad():` и `fill_`/`copy_`.
* **Считать, что `eval()` замораживает веса.** Он меняет поведение `Dropout`/`BatchNorm`, но градиенты по-прежнему считаются.
* **Не фиксировать seed.** Начальные веса случайны, и результаты экспериментов нельзя будет воспроизвести.

<!-- section: summary -->
## Итоги

* `nn.Module` хранит слои и параметры; вычисление описывается в `forward`, вызывается как `model(x)`.
* Параметры находит `parameters()`/`named_parameters()`; регистрируются только атрибуты-модули и контейнеры `ModuleList`, `Sequential`, `ModuleDict`.
* `nn.Linear(n_in, n_out)`: вес `(n_out, n_in)`, смещение `(n_out,)`, всего `n_in * n_out + n_out` параметров.
* Вручную менять веса нужно под `torch.no_grad()`; заморозка — `requires_grad_(False)`.
* Выход последнего слоя — сырые значения без активации (логиты или предсказание регрессии).

Дальше: [цикл обучения](lesson:ml.pytorch.training-loop).

<!-- section: deep-dive -->
## Сравнение с GoMLX

В [маленькой нейросети на GoMLX](lesson:go.ml.gomlx.tiny-mlp) параметры были явными тензорами, которые мы передавали в функцию графа, и обновляли мы их сами. В PyTorch `nn.Module` берёт на себя учёт параметров: их собирает `parameters()`, их сохраняет `state_dict()`, их же получает оптимизатор. Взамен появляется неочевидное правило регистрации, из-за которого и случается самая частая ошибка новичков: слой, спрятанный в обычный список.
