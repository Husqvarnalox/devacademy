import hashlib
import json


def canonical_json(obj):
    """Каноническая JSON-строка: ключи по алфавиту, без пробелов, кириллица как есть, NaN и бесконечности запрещены."""
    return json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False)


FORMAT = "linear-v1"
REQUIRED_FIELDS = ("name", "features", "weights", "bias", "threshold", "metadata")


def serialize_model(model):
    """Модель -> байты: каноническая JSON-запись с добавленным полем format."""
    return canonical_json({"format": FORMAT, **model}).encode("utf-8")


def deserialize_model(blob, expected_sha256=None):
    """Байты -> модель (без поля format). Проверяет контрольную сумму, формат и согласованность размеров."""
    if expected_sha256 is not None:
        actual = hashlib.sha256(blob).hexdigest()
        if actual != expected_sha256:
            raise ValueError(f"checksum mismatch: ожидалось {expected_sha256}, получено {actual}")
    data = json.loads(blob.decode("utf-8"))
    if not isinstance(data, dict) or data.get("format") != FORMAT:
        found = data.get("format") if isinstance(data, dict) else type(data).__name__
        raise ValueError(f"неподдерживаемый формат модели: {found!r}, ожидался {FORMAT!r}")
    missing = [field for field in REQUIRED_FIELDS if field not in data]
    if missing:
        raise ValueError(f"в модели нет полей: {missing}")
    if len(data["weights"]) != len(data["features"]):
        raise ValueError(f"число весов ({len(data['weights'])}) не равно числу признаков ({len(data['features'])})")
    del data["format"]
    return data


class ModelRegistry:
    STAGES = ("None", "Staging", "Production", "Archived")

    def __init__(self):
        self._models = {}  # имя -> {версия: запись}

    def _record(self, name, version):
        try:
            return self._models[name][version]
        except KeyError:
            raise KeyError(f"нет модели {name!r} версии {version!r}") from None

    def _view(self, name, version):
        rec = self._record(name, version)
        return {
            "name": name,
            "version": version,
            "stage": rec["stage"],
            "sha256": rec["sha256"],
            "metrics": dict(rec["metrics"]),
            "run_id": rec["run_id"],
        }

    def register(self, name, blob, metrics, run_id):
        """Регистрирует новую версию модели; возвращает её номер (1, 2, 3... для каждого имени)."""
        versions = self._models.setdefault(name, {})
        version = len(versions) + 1
        versions[version] = {
            "blob": bytes(blob),
            "sha256": hashlib.sha256(blob).hexdigest(),
            "metrics": dict(metrics),
            "run_id": run_id,
            "stage": "None",
        }
        return version

    def get(self, name, version):
        """Описание версии без самого blob; KeyError, если версии нет."""
        return self._view(name, version)

    def transition(self, name, version, stage):
        """Переводит версию в стадию; новая Production-версия отправляет прежнюю в Archived."""
        if stage not in self.STAGES:
            raise ValueError(f"неизвестная стадия {stage!r}, допустимы {self.STAGES}")
        rec = self._record(name, version)
        if stage == "Production":
            for other_version, other in self._models[name].items():
                if other_version != version and other["stage"] == "Production":
                    other["stage"] = "Archived"
        rec["stage"] = stage

    def latest(self, name, stage="Production"):
        """Описание версии с наибольшим номером в стадии или None."""
        versions = [v for v, rec in self._models.get(name, {}).items() if rec["stage"] == stage]
        return self._view(name, max(versions)) if versions else None

    def promote_if_better(self, name, version, metric, mode="max", min_delta=0.0):
        """Переводит версию в Production, если она лучше текущей Production больше чем на min_delta."""
        if mode not in ("max", "min"):
            raise ValueError(f"mode должен быть max или min, получено {mode!r}")
        candidate = self._record(name, version)
        if metric not in candidate["metrics"]:
            raise ValueError(f"у версии {version} нет метрики {metric!r}")
        current = self.latest(name, "Production")
        if current is not None:
            if metric not in current["metrics"]:
                raise ValueError(f"у Production-версии {current['version']} нет метрики {metric!r}")
            if mode == "max":
                improvement = candidate["metrics"][metric] - current["metrics"][metric]
            else:
                improvement = current["metrics"][metric] - candidate["metrics"][metric]
            if not improvement > min_delta:
                return False
        self.transition(name, version, "Production")
        return True

    def load(self, name, stage="Production"):
        """Загружает модель выбранной стадии, проверяя контрольную сумму; LookupError, если версии в стадии нет."""
        found = self.latest(name, stage)
        if found is None:
            raise LookupError(f"нет модели {name!r} в стадии {stage!r}")
        rec = self._models[name][found["version"]]
        return deserialize_model(rec["blob"], expected_sha256=found["sha256"])
