def count_words(words):
    """Словарь «слово -> сколько раз встретилось» для списка слов."""
    counts = {}
    for word in words:
        counts[word] = counts.get(word, 0) + 1
    return counts


def unique_sorted(items):
    """Отсортированный список значений без повторов."""
    return sorted(set(items))


def common_items(first, second):
    """Отсортированный список значений, которые есть в обоих списках (без повторов)."""
    return sorted(set(first) & set(second))


def cart_total(prices, cart):
    """Сумма цен товаров из списка cart по словарю prices «название -> цена». Неизвестные товары стоят 0."""
    total = 0
    for name in cart:
        total = total + prices.get(name, 0)
    return total
