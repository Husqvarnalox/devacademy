import torch
from torch import nn


class MLP(nn.Module):
    """Полносвязная сеть. sizes = [in, h1, ..., out], минимум два числа (иначе ValueError).

    Между линейными слоями - ReLU, после последнего слоя активации нет.
    Слои должны храниться так, чтобы PyTorch видел их параметры (nn.ModuleList).
    """

    def __init__(self, sizes):
        super().__init__()
        # TODO: создайте слои nn.Linear и сохраните их в self.layers

    def forward(self, x):
        # TODO
        return x


def count_params(model):
    """Число обучаемых параметров (элементов тензоров с requires_grad=True), int."""
    # TODO
    return 0


def set_constant(model, weight, bias):
    """Заполнить все веса значением weight, все смещения значением bias (на месте, без графа). Вернуть model.

    Слои матриц - параметры с именем, оканчивающимся на 'weight'; смещения - на 'bias'.
    """
    # TODO
    return model


def freeze_first_layer(model):
    """Заморозить параметры первого слоя (requires_grad=False). Вернуть число оставшихся обучаемых параметров."""
    # TODO
    return 0
