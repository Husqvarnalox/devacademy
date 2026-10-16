def rotate_in_place(items, k):
    """Циклически сдвигает items вправо на k позиций на месте (k может быть любым целым)."""
    # TODO: в таком виде функция строит новый список и не меняет переданный - исправьте
    k %= len(items) if items else 1
    rotated = items[-k:] + items[:-k] if k else items[:]
    return rotated
