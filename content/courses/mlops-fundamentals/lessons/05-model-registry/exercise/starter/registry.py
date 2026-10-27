import hashlib
import json


def canonical_json(obj):
    """Каноническая JSON-строка: ключи по алфавиту, без пробелов, кириллица как есть, NaN и бесконечности запрещены."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


FORMAT = "linear-v1"


def serialize_model(model):
    """Модель -> байты: каноническая JSON-запись с добавленным полем format."""
    # TODO: canonical_json({"format": FORMAT, **model}) и .encode("utf-8")
    return b""


def deserialize_model(blob, expected_sha256=None):
    """Байты -> модель (без поля format). Проверяет контрольную сумму, формат и согласованность размеров."""
    # TODO: если expected_sha256 задан и не совпал с hashlib.sha256(blob).hexdigest() - ValueError("checksum mismatch ...")
    # TODO: json.loads(blob.decode("utf-8")); format должен быть FORMAT, len(weights) == len(features), иначе ValueError
    # TODO: вернуть словарь без поля format
    return {}


class ModelRegistry:
    STAGES = ("None", "Staging", "Production", "Archived")

    def __init__(self):
        self._models = {}  # TODO: придумайте хранение: имя -> версии -> запись (blob, sha256, metrics, run_id, stage)

    def register(self, name, blob, metrics, run_id):
        """Регистрирует новую версию модели; возвращает её номер (1, 2, 3... для каждого имени)."""
        # TODO: номер = число уже зарегистрированных версий этого имени + 1; stage = "None"; sha256 считается от blob
        return 0

    def get(self, name, version):
        """Описание версии без самого blob; KeyError, если версии нет."""
        # TODO: словарь {"name", "version", "stage", "sha256", "metrics", "run_id"} (metrics - копия)
        return {}

    def transition(self, name, version, stage):
        """Переводит версию в стадию; новая Production-версия отправляет прежнюю в Archived."""
        # TODO: ValueError для неизвестной стадии, KeyError для неизвестной версии
        # TODO: при переводе в "Production" все остальные Production-версии этого имени становятся "Archived"
        return None

    def latest(self, name, stage="Production"):
        """Описание версии с наибольшим номером в стадии или None."""
        # TODO: выбрать максимальный номер среди версий в нужной стадии, вернуть то же, что get
        return None

    def promote_if_better(self, name, version, metric, mode="max", min_delta=0.0):
        """Переводит версию в Production, если она лучше текущей Production больше чем на min_delta."""
        # TODO: ValueError, если у кандидата нет метрики (и у Production-версии - тоже) или mode не max/min
        # TODO: нет Production - повышаем (True); иначе улучшение = кандидат - текущая (max) или текущая - кандидат (min)
        # TODO: повышаем только при улучшении СТРОГО больше min_delta, иначе False и стадии не меняются
        return False

    def load(self, name, stage="Production"):
        """Загружает модель выбранной стадии, проверяя контрольную сумму; LookupError, если версии в стадии нет."""
        # TODO: найти latest(name, stage), при отсутствии - LookupError; иначе deserialize_model(blob, expected_sha256=...)
        return {}
