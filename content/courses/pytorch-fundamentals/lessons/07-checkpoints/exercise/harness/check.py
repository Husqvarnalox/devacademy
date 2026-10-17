import json
import os

import torch
from torch import nn

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import ckpt_task as m


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


def data(seed=0, n=32):
    g = torch.Generator().manual_seed(seed)
    X = torch.randn(n, 4, generator=g)
    y = (X[:, 0] + X[:, 1] > 0).long()
    return X, y


def net(seed):
    torch.manual_seed(seed)
    return m.make_net()


def step(model, opt, X, y):
    opt.zero_grad()
    nn.functional.cross_entropy(model(X), y).backward()
    opt.step()


def t_make_net():
    model = net(0)
    kinds = [type(c) for c in model.children()]
    want = [nn.Linear, nn.BatchNorm1d, nn.ReLU, nn.Dropout, nn.Linear]
    if kinds != want:
        return f"ожидалась цепочка Linear, BatchNorm1d, ReLU, Dropout, Linear; получили {[k.__name__ for k in kinds]}"
    if model[0].in_features != 4 or model[0].out_features != 8 or model[4].out_features != 2 or model[3].p != 0.5:
        return "размеры слоёв: 4 -> 8 -> 2, Dropout(0.5)"
    if not model.training:
        return "новая модель по умолчанию находится в режиме train"


def t_roundtrip():
    X, y = data()
    a = net(0)
    opt = torch.optim.Adam(a.parameters(), lr=0.05)
    for _ in range(3):
        step(a, opt, X, y)
    m.save_checkpoint(a, opt, 7, "ckpt.pt")
    if not os.path.exists("ckpt.pt"):
        return "save_checkpoint должен создать файл по пути path"
    b = net(1)
    optb = torch.optim.Adam(b.parameters(), lr=0.05)
    epoch = m.load_checkpoint(b, optb, "ckpt.pt")
    if epoch != 7 or not isinstance(epoch, int):
        return f"load_checkpoint должен вернуть номер эпохи 7 (int), получили {epoch!r}"
    a.eval()
    b.eval()
    with torch.no_grad():
        if not torch.allclose(a(X), b(X), atol=1e-6):
            return "после загрузки выходы в режиме eval должны совпасть с исходной моделью"


def t_predict_basic():
    X, y = data(seed=2)
    model = net(0)
    out = m.predict(model, X)
    if out.dtype != torch.int64 or tuple(out.shape) != (32,):
        return f"ожидался int64 формы (32,), получили {out.dtype} {tuple(out.shape)}"
    if bool(out.requires_grad):
        return "результат predict не должен иметь градиента"


def t_predict_restores_mode():
    X, _ = data()
    model = net(0)
    model.train()
    m.predict(model, X)
    if not model.training:
        return "если модель была в режиме train, predict должен вернуть её в train"
    model.eval()
    m.predict(model, X)
    if model.training:
        return "если модель была в режиме eval, она и должна остаться в eval"


def t_predict_deterministic():
    X, _ = data(seed=3)
    model = net(0)
    a = m.predict(model, X)
    b = m.predict(model, X)
    if not torch.equal(a, b):
        return "в режиме eval Dropout отключён: два вызова predict должны совпасть"
    model.eval()
    with torch.no_grad():
        want = model(X).argmax(dim=1)
    if not torch.equal(a, want):
        return "predict должен совпадать с argmax логитов в режиме eval"


def t_predict_keeps_batchnorm():
    X, _ = data(seed=4)
    model = net(0)
    bn = model[1]
    before_mean = bn.running_mean.clone()
    before_var = bn.running_var.clone()
    before_n = int(bn.num_batches_tracked)
    m.predict(model, X)
    if not (torch.equal(bn.running_mean, before_mean) and torch.equal(bn.running_var, before_var)) or int(bn.num_batches_tracked) != before_n:
        return "в режиме eval BatchNorm не должен обновлять скользящие статистики: переключитесь в eval до forward"


def t_resume_matches_continuous():
    X, y = data(seed=5)
    a = net(0)
    opta = torch.optim.Adam(a.parameters(), lr=0.05)
    for _ in range(3):
        step(a, opta, X, y)
    m.save_checkpoint(a, opta, 3, "resume.pt")
    torch.manual_seed(11)
    for _ in range(2):
        step(a, opta, X, y)
    b = net(9)
    optb = torch.optim.Adam(b.parameters(), lr=0.05)
    m.load_checkpoint(b, optb, "resume.pt")
    torch.manual_seed(11)
    for _ in range(2):
        step(b, optb, X, y)
    for pa, pb in zip(a.state_dict().values(), b.state_dict().values()):
        if pa.dtype.is_floating_point and not torch.allclose(pa, pb, atol=1e-6):
            return "после загрузки продолжение обучения должно давать те же веса: сохраняйте и загружайте состояние оптимизатора вместе с моделью"


def t_missing_file():
    model = net(0)
    opt = torch.optim.Adam(model.parameters())
    try:
        m.load_checkpoint(model, opt, "no-such-file.pt")
    except FileNotFoundError:
        return None
    return "загрузка несуществующего файла должна привести к FileNotFoundError"


case("make-net", t_make_net)
case("roundtrip", t_roundtrip)
case("predict-basic", t_predict_basic)
case("predict-restores-mode", t_predict_restores_mode)
case("predict-deterministic", t_predict_deterministic)
case("predict-keeps-batchnorm", t_predict_keeps_batchnorm)
case("resume-matches-continuous", t_resume_matches_continuous)
case("missing-file", t_missing_file)
