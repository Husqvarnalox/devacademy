import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import bpe_task as m


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


CORPUS = (
    "мама мыла раму. мама мыла раму снова.\n"
    "the cat sat on the mat. the cat sat on the hat.\n"
    "токенизатор делит текст на токены, а модель видит только номера токенов.\n"
)


def t_merge_basic():
    ids = [1, 2, 3, 1, 2]
    got = m.merge(ids, (1, 2), 99)
    if got != [99, 3, 99]:
        return f"merge([1, 2, 3, 1, 2], (1, 2), 99): ожидалось [99, 3, 99], получили {got!r}"
    if ids != [1, 2, 3, 1, 2]:
        return "merge не должна изменять входной список"
    if m.merge([4, 5], (1, 2), 99) != [4, 5]:
        return "если пары нет, список не меняется"


def t_train_classic():
    merges = m.train("aaabdaaabac", 259)
    want = {(97, 97): 256, (256, 97): 257, (257, 98): 258}
    if merges != want:
        return f"train('aaabdaaabac', 259): ожидалось {want}, получили {merges!r}"
    if list(merges.values()) != [256, 257, 258]:
        return "слияния должны идти в порядке обучения: 256, 257, 258"
    got = m.encode("aaabdaaabac", merges)
    if got != [258, 100, 258, 97, 99]:
        return f"encode обучающей строки: ожидалось [258, 100, 258, 97, 99], получили {got!r}"


def t_roundtrip_basic():
    merges = m.train(CORPUS, 300)
    ids = m.encode(CORPUS, merges)
    if m.decode(ids, merges) != CORPUS:
        return "decode(encode(text)) должен вернуть исходный текст"
    n_bytes = len(CORPUS.encode("utf-8"))
    if not len(ids) < n_bytes * 0.6:
        return f"после 44 слияний текст должен стать заметно короче: {len(ids)} токенов против {n_bytes} байтов"


def t_merge_overlap():
    if m.merge([5, 5, 5], (5, 5), 9) != [9, 5]:
        return "в [5, 5, 5] пара (5, 5) заменяется слева направо без перекрытий: [9, 5]"
    if m.merge([5, 5, 5, 5], (5, 5), 9) != [9, 9]:
        return "[5, 5, 5, 5] -> [9, 9]"
    if m.merge([], (5, 5), 9) != [] or m.merge([5], (5, 5), 9) != [5]:
        return "пустой список и список из одного элемента не меняются"


def t_train_stops():
    try:
        m.train("abc", 255)
    except ValueError:
        pass
    else:
        return "при vocab_size < 256 ожидался ValueError"
    if m.train("abcd", 1000) != {}:
        return "если каждая пара встречается один раз, сливать нечего: ожидался пустой словарь"
    if m.train("", 300) != {} or m.train("ab ab", 256) != {}:
        return "пустой текст или vocab_size = 256 дают пустой словарь слияний"
    merges = m.train("abab abab", 10_000)
    if len(merges) >= 10_000 - 256 or any(v != 256 + i for i, v in enumerate(merges.values())):
        return "обучение останавливается, когда самая частая пара встречается меньше 2 раз; id идут подряд с 256"


def t_train_cyrillic():
    merges = m.train("мама мыла раму", 260)
    first = next(iter(merges), None)
    if first != tuple("м".encode("utf-8")):
        return f"первое слияние на русском тексте склеивает два байта UTF-8 буквы «м» {tuple('м'.encode('utf-8'))}, получили {first!r}"
    if m.decode([256], merges) != "м":
        return "decode([256]) должен вернуть «м»"


def t_encode_priority():
    merges = {(98, 99): 256, (97, 98): 257}
    got = m.encode("abc", merges)
    if got != [97, 256]:
        return f"encode применяет слияния в порядке обучения (наименьший id первым), а не слева направо: ожидалось [97, 256], получили {got!r}"
    if m.encode("", merges) != []:
        return "encode('') должен вернуть []"


def t_roundtrip_unicode():
    merges = m.train(CORPUS, 320)
    for text in ["", "ёж", "Привет, мир! 👋", "naïve café", "строка\nс переводом\tи табом", "aaaaaaa", "токены 🙂🙂"]:
        ids = m.encode(text, merges)
        if any(not isinstance(i, int) for i in ids):
            return f"id должны быть int: {ids!r}"
        back = m.decode(ids, merges)
        if back != text:
            return f"decode(encode({text!r})) вернул {back!r}"


def t_decode_partial():
    if m.decode([0xD0], {}) != "�":
        return "один байт 0xD0 - неполный символ UTF-8: decode должен вернуть «\\ufffd» (errors='replace'), а не бросать исключение"
    merges = {(0xD0, 0xBC): 256, (256, 0xD0): 257}
    if m.decode([257, 0xB0], merges) != "ма":
        return "decode разворачивает вложенные слияния рекурсивно: [257, 0xB0] -> «ма»"


case("merge-basic", t_merge_basic)
case("train-classic", t_train_classic)
case("roundtrip-basic", t_roundtrip_basic)
case("merge-overlap", t_merge_overlap)
case("train-stops", t_train_stops)
case("train-cyrillic", t_train_cyrillic)
case("encode-priority", t_encode_priority)
case("roundtrip-unicode", t_roundtrip_unicode)
case("decode-partial", t_decode_partial)
