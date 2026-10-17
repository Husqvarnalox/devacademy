import torch
from torch import nn


class MLP(nn.Module):
    """Полносвязная сеть. sizes = [in, h1, ..., out], минимум два числа (иначе ValueError).

    Между линейными слоями - ReLU, после последнего слоя активации нет.
    Слои должны храниться так, чтобы PyTorch видел их параметры (nn.ModuleList).
    """

    def __init__(self, sizes):
        super().__init__()
        if len(sizes) < 2:
            raise ValueError("нужно минимум два размера: вход и выход")
        self.layers = nn.ModuleList(nn.Linear(a, b) for a, b in zip(sizes[:-1], sizes[1:]))

    def forward(self, x):
        for i, layer in enumerate(self.layers):
            x = layer(x)
            if i < len(self.layers) - 1:
                x = torch.relu(x)
        return x


def count_params(model):
    """Число обучаемых параметров (элементов тензоров с requires_grad=True), int."""
    return sum(p.numel() for p in model.parameters() if p.requires_grad)


def set_constant(model, weight, bias):
    """Заполнить все веса значением weight, все смещения значением bias (на месте, без графа). Вернуть model.

    Слои матриц - параметры с именем, оканчивающимся на 'weight'; смещения - на 'bias'.
    """
    with torch.no_grad():
        for name, p in model.named_parameters():
            p.fill_(weight if name.endswith("weight") else bias)
    return model


def freeze_first_layer(model):
    """Заморозить параметры первого слоя (requires_grad=False). Вернуть число оставшихся обучаемых параметров."""
    for p in model.layers[0].parameters():
        p.requires_grad_(False)
    return count_params(model)
