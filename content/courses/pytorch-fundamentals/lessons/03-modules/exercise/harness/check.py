import json
import os

import torch
from torch import nn

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import module_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() возвращает None при успехе или строку с причиной провала; исключения - провал."""
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def t_structure():
    model = m.MLP([2, 4, 3, 1])
    if not isinstance(model, nn.Module):
        return "MLP должен наследоваться от nn.Module"
    shapes = sorted(tuple(p.shape) for p in model.parameters())
    want = sorted([(4, 2), (4,), (3, 4), (3,), (1, 3), (1,)])
    if shapes != want:
        return f"ожидались параметры форм {want}, получили {shapes}; храните слои в nn.ModuleList, а не в списке Python"
    names = [n for n, _ in model.named_parameters()]
    if not all(n.startswith("layers.") for n in names):
        return f"слои должны лежать в атрибуте layers; имена параметров: {names}"


def t_forward_shape():
    torch.manual_seed(0)
    model = m.MLP([2, 5, 3])
    out = model(torch.randn(7, 2))
    if tuple(out.shape) != (7, 3):
        return f"для входа (7, 2) ожидался выход (7, 3), получили {tuple(out.shape)}"
    if not out.requires_grad:
        return "выход модели должен быть частью графа (requires_grad=True)"


def t_count_params():
    got = m.count_params(m.MLP([2, 4, 3, 1]))
    want = 2 * 4 + 4 + 4 * 3 + 3 + 3 * 1 + 1
    if got != want or not isinstance(got, int):
        return f"ожидалось {want} (int), получили {got!r}"


def t_constant_forward():
    model = m.set_constant(m.MLP([2, 3, 1]), 0.5, 0.1)
    out = model(torch.tensor([[1.0, 2.0]]))
    # скрытый слой: relu(0.5 * (1 + 2) + 0.1) = 1.6 в каждой из 3 единиц; выход: 0.5 * 3 * 1.6 + 0.1 = 2.5
    if not torch.allclose(out, torch.tensor([[2.5]]), atol=1e-6):
        return f"ожидалось [[2.5]], получили {out.tolist()}"
    if any(p.grad_fn is not None for p in model.parameters()):
        return "параметры должны остаться листьями графа: заполняйте их под torch.no_grad()"
    if not all(p.requires_grad for p in model.parameters()):
        return "заполнение не должно отключать requires_grad"


def t_relu_only_hidden():
    model = m.set_constant(m.MLP([1, 2, 1]), 1.0, 0.0)
    with torch.no_grad():
        model.layers[1].weight.fill_(-1.0)
    out = model(torch.tensor([[2.0]]))
    # скрытый слой: relu(2) = 2 в двух единицах; выход без активации: -1 * 2 + -1 * 2 = -4
    if not torch.allclose(out, torch.tensor([[-4.0]])):
        return f"после последнего слоя активации быть не должно: ожидалось [[-4.0]], получили {out.tolist()}"
    neg = model(torch.tensor([[-3.0]]))
    if not torch.allclose(neg, torch.tensor([[0.0]])):
        return f"между слоями должен стоять ReLU: для входа -3 ожидалось [[0.0]], получили {neg.tolist()}"


def t_freeze():
    model = m.MLP([2, 4, 3, 1])
    left = m.freeze_first_layer(model)
    if left != 4 * 3 + 3 + 3 * 1 + 1 or m.count_params(model) != left:
        return f"после заморозки первого слоя должно остаться {4 * 3 + 3 + 3 + 1} обучаемых параметров, получили {left!r}"
    if any(p.requires_grad for p in model.layers[0].parameters()):
        return "параметры первого слоя должны быть заморожены"
    loss = model(torch.randn(3, 2)).sum()
    loss.backward()
    if model.layers[0].weight.grad is not None or model.layers[1].weight.grad is None:
        return "у замороженного слоя не должно быть градиента, а у остальных - должен быть"


def t_validation_and_state_dict():
    try:
        m.MLP([3])
    except ValueError:
        pass
    else:
        return "MLP([3]) должен бросать ValueError"
    keys = list(m.MLP([2, 3, 1]).state_dict().keys())
    want = ["layers.0.weight", "layers.0.bias", "layers.1.weight", "layers.1.bias"]
    if keys != want:
        return f"ключи state_dict: ожидали {want}, получили {keys}"


case("structure", t_structure)
case("forward-shape", t_forward_shape)
case("count-params", t_count_params)
case("constant-forward", t_constant_forward)
case("relu-only-hidden", t_relu_only_hidden)
case("freeze", t_freeze)
case("validation-and-state-dict", t_validation_and_state_dict)
