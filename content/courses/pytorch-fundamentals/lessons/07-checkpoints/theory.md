<!-- section: simple -->
## Как не потерять обученную модель

Обучение занимает время, а результат — набор чисел в памяти. Закрыли программу — всё пропало. Поэтому нужно уметь **сохранять** обученную модель в файл и **загружать** её обратно.

В PyTorch модель сохраняют не целиком, а как словарь **state_dict**: имя параметра → тензор. Чтобы загрузить, сначала создают точно такую же модель (код архитектуры у вас есть), а потом наполняют её сохранёнными числами. У оптимизатора тоже есть `state_dict` (например, моменты Adam); если сохранить и его, обучение можно **продолжить** так, будто перерыва не было.

Вторая половина урока про то, что некоторые слои ведут себя по-разному при обучении и при предсказании:

* **Dropout** при обучении случайно «выключает» часть значений, чтобы сеть не запоминала данные; при предсказании он ничего не делает.
* **BatchNorm** при обучении нормирует значения по текущему пакету и обновляет накопленные оценки среднего и дисперсии; при предсказании использует накопленные.

Переключатели называются `model.train()` и `model.eval()`. Забыть `eval()` перед предсказанием — частая ошибка: ответы будут случайными.

<!-- section: precise -->
## state_dict, режимы и затухание весов

**state_dict модели** содержит параметры (обучаемые, `requires_grad=True`) и **буферы** (необучаемые, но часть состояния): у `BatchNorm1d` это `running_mean`, `running_var` и `num_batches_tracked`. Ключи имеют вид `'0.weight'`, `'1.running_mean'` и т. д. — по именам модулей в `Sequential`.

**Сохранение.** `torch.save({'model': model.state_dict(), 'optimizer': opt.state_dict(), 'epoch': epoch}, path)`. **Загрузка:** `state = torch.load(path)`, затем `model.load_state_dict(state['model'])`, `opt.load_state_dict(state['optimizer'])`. Начиная с PyTorch 2.6 `torch.load` по умолчанию загружает только тензоры и простые типы (`weights_only=True`), чего хватает для контрольных точек такого вида. Не загружайте файлы из ненадёжных источников. Если файла нет, будет `FileNotFoundError`; прятать его не нужно.

**Режимы.** `model.train()` и `model.eval()` рекурсивно выставляют флаг `training` во всех подмодулях. Они **не** отключают градиенты: для этого оберните вычисления в `torch.no_grad()`. Для предсказания нужны оба: `eval()` и `no_grad()`.

**Dropout(p).** При `train` обнуляет элементы с вероятностью `p` и умножает остальные на `1/(1-p)`, при `eval` — тождественная операция. Результат при обучении случаен, поэтому для воспроизводимости ставьте `torch.manual_seed`.

**BatchNorm.** При `train` используются среднее и дисперсия пакета, а `running_mean/var` обновляются; при `eval` — накопленные значения, ответ не зависит от остальных объектов пакета.

**Затухание весов (weight decay).** Параметр `weight_decay=wd` в оптимизаторе добавляет `wd · p` к градиенту каждого параметра: `p ← p - lr (g + wd · p)`. Это L2-регуляризация, она тянет веса к нулю, уменьшает их норму, ухудшает ошибку на обучении и может улучшить ошибку на новых данных, когда данных мало. В отличие от этого, у `Adam` и `AdamW` затухание реализовано по-разному; в упражнении используется простой `SGD`.

<!-- section: examples -->
## Примеры

**Пример 1. Что лежит в state_dict.**

```python run
import torch
from torch import nn

torch.manual_seed(0)
net = nn.Sequential(nn.Linear(4, 8), nn.BatchNorm1d(8), nn.ReLU(), nn.Linear(8, 2))
for name, t in net.state_dict().items():
    print(name, tuple(t.shape))

params = {n for n, _ in net.named_parameters()}
print("буферов вне параметров:", len(net.state_dict()) - len(params))
```
```output
0.weight (8, 4)
0.bias (8,)
1.weight (8,)
1.bias (8,)
1.running_mean (8,)
1.running_var (8,)
1.num_batches_tracked ()
3.weight (2, 8)
3.bias (2,)
буферов вне параметров: 3
```

**Пример 2. Сохранение, загрузка и продолжение обучения.**

```python run
import torch
from torch import nn

def make():
    torch.manual_seed(0)
    return nn.Sequential(nn.Linear(3, 4), nn.ReLU(), nn.Linear(4, 1))

g = torch.Generator().manual_seed(1)
X = torch.randn(16, 3, generator=g)
y = X.sum(dim=1)

def step(model, opt):
    opt.zero_grad()
    nn.functional.mse_loss(model(X).squeeze(-1), y).backward()
    opt.step()

a = make()
opt_a = torch.optim.Adam(a.parameters(), lr=0.05)
for _ in range(5):
    step(a, opt_a)
torch.save({"model": a.state_dict(), "optimizer": opt_a.state_dict()}, "demo.pt")
for _ in range(5):
    step(a, opt_a)

b = make()
opt_b = torch.optim.Adam(b.parameters(), lr=0.05)
state = torch.load("demo.pt")
b.load_state_dict(state["model"])
opt_b.load_state_dict(state["optimizer"])
for _ in range(5):
    step(b, opt_b)

same = all(torch.allclose(p, q, atol=1e-6) for p, q in zip(a.parameters(), b.parameters()))
print("продолжение совпало с непрерывным обучением:", same)

c = make()
opt_c = torch.optim.Adam(c.parameters(), lr=0.05)
c.load_state_dict(state["model"])
for _ in range(5):
    step(c, opt_c)
lost = all(torch.allclose(p, q, atol=1e-6) for p, q in zip(a.parameters(), c.parameters()))
print("без состояния оптимизатора совпадения нет:", not lost)
```
```output
продолжение совпало с непрерывным обучением: True
без состояния оптимизатора совпадения нет: True
```

**Пример 3. Dropout и BatchNorm в двух режимах.**

```python run
import torch
from torch import nn

torch.manual_seed(0)
drop = nn.Dropout(0.5)
x = torch.ones(1, 1000)

drop.train()
out = drop(x)
print("при train значения только 0 и 2:", sorted(set(out.flatten().tolist())))
print("доля нулей около половины:", 0.4 < (out == 0).float().mean().item() < 0.6)
drop.eval()
print("при eval вход не меняется:", bool(torch.equal(drop(x), x)))

bn = nn.BatchNorm1d(1)
batch = torch.arange(10, dtype=torch.float32).reshape(-1, 1) + 100.0
bn.train()
bn(batch)
print("running_mean сдвинулся к среднему пакета:", 5.0 < bn.running_mean.item() < 15.0)
before = bn.running_mean.clone()
bn.eval()
bn(batch)
print("при eval статистики не меняются:", bool(torch.equal(bn.running_mean, before)))
```
```output
при train значения только 0 и 2: [0.0, 2.0]
доля нулей около половины: True
при eval вход не меняется: True
running_mean сдвинулся к среднему пакета: True
при eval статистики не меняются: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Предсказывать в режиме `train`.** Dropout выключает случайные значения, BatchNorm меняет скользящие статистики; ответы случайны.
* **Думать, что `eval()` отключает градиенты.** Для экономии памяти добавляйте `torch.no_grad()`.
* **Не вернуть режим.** После предсказания внутри цикла обучения забыли `model.train()`, и Dropout больше не работает.
* **Сохранить только модель.** Обучение продолжится с чистым состоянием Adam: траектория изменится (пример 2).
* **Загружать в модель другой архитектуры.** `load_state_dict` выбросит ошибку о несовпадающих ключах или формах; создавайте модель тем же кодом.
* **Хранить `state_dict()` без копии как лучшую версию.** Он ссылается на живые тензоры; делайте `deepcopy`.
* **Загружать чужие файлы без проверки.** Pickle может исполнить код; доверяйте только своим файлам.
* **Применять weight_decay к смещениям, не подумав.** В PyTorch по умолчанию затухание касается всех параметров, включая смещения; на практике смещения часто исключают отдельной группой параметров.

<!-- section: summary -->
## Итоги

* Модель сохраняют как `state_dict`; вместе с ним сохраняют состояние оптимизатора и номер эпохи.
* Для загрузки создают модель тем же кодом и вызывают `load_state_dict`.
* `train()` включает случайность Dropout и пакетные статистики BatchNorm, `eval()` отключает их; градиенты отключает только `no_grad()`.
* Предсказание: `eval()` + `no_grad()` + возврат прежнего режима.
* `weight_decay` тянет веса к нулю и действует как L2-регуляризация.

Вы прошли основы PyTorch: тензоры, autograd, модули, цикл обучения, классификацию, данные и сохранение. Поиск архитектур и ускорение на видеокартах - следующий шаг.

<!-- section: deep-dive -->
## Сравнение с другими курсами

В курсе про [логистическую регрессию на NumPy](lesson:ml.numpy.logistic-regression) вы добавляли L2-штраф в потерю и в градиент вручную; `weight_decay` в SGD делает то же самое прямо в обновлении, потому что градиент штрафа `0.5 · wd · p²` равен `wd · p`. Структура модели как набора именованных параметров знакома из [урока про nn.Module](lesson:ml.pytorch.modules): именно имена модулей становятся ключами `state_dict`.
