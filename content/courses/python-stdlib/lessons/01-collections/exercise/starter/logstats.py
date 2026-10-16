from collections import Counter, defaultdict, deque, namedtuple

Entry = None  # TODO: namedtuple с полями date, level, service, message


def parse(line):
    """Разобрать строку 'ДАТА УРОВЕНЬ СЕРВИС сообщение...' в Entry."""
    # TODO
    return None


def count_levels(lines):
    """Counter по уровням."""
    # TODO
    return None


def top_error_services(lines, n):
    """n самых частых сервисов среди ERROR: [(сервис, число), ...]."""
    # TODO
    return None


def messages_by_service(lines):
    """Обычный dict: сервис -> список сообщений в порядке появления."""
    # TODO
    return None


def last_lines(lines, n):
    """Последние n строк (deque с maxlen)."""
    # TODO
    return None
