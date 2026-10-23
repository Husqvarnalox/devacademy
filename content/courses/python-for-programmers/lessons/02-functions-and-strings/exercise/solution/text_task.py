def collect(item, bucket=None):
    """Добавить item в bucket (или в новый список) и вернуть список."""
    if bucket is None:
        bucket = []
    bucket.append(item)
    return bucket


def slugify(title):
    """Превратить заголовок в адрес вида 'privet-mir'."""
    cleaned = "".join(ch if ch.isalnum() else " " for ch in title.lower())
    return "-".join(cleaned.split())


def money(cents, currency="USD"):
    """Показать сумму в центах как '1,234.56 USD'."""
    sign = "-" if cents < 0 else ""
    units, rest = divmod(abs(cents), 100)
    return f"{sign}{units:,}.{rest:02d} {currency}"


def initials(full_name):
    """Вернуть заглавные инициалы с точками: 'А.П.И.'."""
    return "".join(word[0].upper() + "." for word in full_name.split())


def join_nonempty(*parts, sep=" "):
    """Склеить непустые (после strip) части через sep."""
    return sep.join(p.strip() for p in parts if p.strip())
