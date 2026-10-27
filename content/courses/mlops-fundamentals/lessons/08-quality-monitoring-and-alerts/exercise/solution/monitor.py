def windowed_accuracy(events, window):
    """Точность по окнам времени среди событий с известной меткой."""
    if window < 1:
        raise ValueError(f"window должен быть не меньше 1, получено {window}")
    stats = {}
    for event in events:
        if event["label"] is None:
            continue
        index = event["ts"] // window
        n, correct = stats.get(index, (0, 0))
        stats[index] = (n + 1, correct + (1 if event["pred"] == event["label"] else 0))
    return [
        {"window": index, "n": n, "accuracy": round(correct / n, 4)}
        for index, (n, correct) in sorted(stats.items())
    ]


def evaluate_rule(values, threshold, for_windows, direction="below"):
    """Правило тревоги в стиле Prometheus `for`: события [индекс, "firing" | "resolved"]."""
    if direction not in ("below", "above"):
        raise ValueError(f"direction должен быть 'below' или 'above', получено {direction!r}")
    if for_windows < 1:
        raise ValueError(f"for_windows должен быть не меньше 1, получено {for_windows}")
    events = []
    streak = 0
    firing = False
    for i, value in enumerate(values):
        if value is None:
            continue
        bad = value < threshold if direction == "below" else value > threshold
        if bad:
            streak += 1
            if streak >= for_windows and not firing:
                firing = True
                events.append([i, "firing"])
        else:
            streak = 0
            if firing:
                firing = False
                events.append([i, "resolved"])
    return events


def retraining_decision(drift_rows, accuracy_rows, min_accuracy, min_samples=30):
    """Решение о переобучении и причины: дрейф признаков и падение точности."""
    reasons = [f"drift:{row['feature']}" for row in drift_rows if row["status"] == "alert"]
    eligible = [row for row in accuracy_rows if row["n"] >= min_samples]
    if eligible:
        last = eligible[-1]
        if last["accuracy"] < min_accuracy:
            reasons.append(f"quality:{last['accuracy']:.4f}<{min_accuracy:.4f}")
    else:
        reasons.append("quality:insufficient-data")
    retrain = any(r.startswith("drift:") or (r.startswith("quality:") and r != "quality:insufficient-data") for r in reasons)
    return {"retrain": retrain, "reasons": reasons}
