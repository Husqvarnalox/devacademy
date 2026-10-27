import json
import math


def validate_row(row, schema):
    """Список ошибок валидации строки; пустой список - строка корректна."""
    if not isinstance(row, dict):
        return ["row: expected object"]
    errors = []
    for feature in sorted(schema):
        if feature not in row:
            errors.append(f"{feature}: missing")
            continue
        value = row[feature]
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            errors.append(f"{feature}: expected number")
            continue
        if not math.isfinite(value):
            errors.append(f"{feature}: expected finite number")
            continue
        low = schema[feature].get("min")
        high = schema[feature].get("max")
        if (low is not None and value < low) or (high is not None and value > high):
            errors.append(f"{feature}: out of range")
    for key in sorted(k for k in row if k not in schema):
        errors.append(f"{key}: unknown field")
    return errors


def predict_one(model, row):
    """Оценка вероятности и класс для одной корректной строки."""
    z = model["bias"] + sum(w * row[f] for f, w in zip(model["features"], model["weights"]))
    if z >= 0:
        p = 1.0 / (1.0 + math.exp(-z))
    else:
        e = math.exp(z)
        p = e / (1.0 + e)
    return {"score": round(p, 6), "label": 1 if p >= model["threshold"] else 0}


def predict_batch(model, schema, rows, max_batch_size):
    """Обрабатывает строки пачками; ошибка в одной строке не ломает остальные."""
    if max_batch_size < 1:
        raise ValueError(f"max_batch_size должен быть не меньше 1, получено {max_batch_size}")
    results = []
    batches = 0
    for start in range(0, len(rows), max_batch_size):
        batches += 1
        for index in range(start, min(start + max_batch_size, len(rows))):
            row = rows[index]
            errors = validate_row(row, schema)
            if errors:
                results.append({"index": index, "ok": False, "errors": errors})
            else:
                results.append({"index": index, "ok": True, **predict_one(model, row)})
    return {"model": f"{model['name']}:{model['version']}", "batches": batches, "results": results}


def handle_request(model, schema, body, max_instances=100, max_batch_size=32):
    """Разбирает тело HTTP-запроса и возвращает (статус, ответ)."""
    try:
        payload = json.loads(body)
    except ValueError:
        return 400, {"error": "invalid json"}
    if not isinstance(payload, dict) or not isinstance(payload.get("instances"), list):
        return 400, {"error": 'expected {"instances": [...]}'}
    instances = payload["instances"]
    if len(instances) > max_instances:
        return 413, {"error": "too many instances", "limit": max_instances}
    return 200, predict_batch(model, schema, instances, max_batch_size)
