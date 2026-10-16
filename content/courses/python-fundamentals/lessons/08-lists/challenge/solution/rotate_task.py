def rotate_in_place(items, k):
    """Циклически сдвигает items вправо на k позиций на месте (k может быть любым целым)."""
    if not items:
        return None
    k %= len(items)
    if k:
        items[:] = items[-k:] + items[:-k]
    return None
