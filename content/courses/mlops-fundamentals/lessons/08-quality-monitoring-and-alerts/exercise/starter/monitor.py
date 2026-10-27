def windowed_accuracy(events, window):
    """Точность по окнам времени среди событий с известной меткой.

    events: [{"ts": int, "pred": 0/1, "label": 0/1 или None}], порядок произвольный.
    Возвращает [{"window": индекс, "n": число размеченных, "accuracy": round(доля верных, 4)}]
    по возрастанию индекса, только для окон, где есть хотя бы одно размеченное событие.
    """
    # TODO: ValueError при window < 1; индекс окна ts // window; события с label None пропускаются
    return []


def evaluate_rule(values, threshold, for_windows, direction="below"):
    """Правило тревоги в стиле Prometheus `for`: события [индекс, "firing" | "resolved"]."""
    # TODO: проверка direction и for_windows; None - нет данных, пропускать без изменения состояния;
    #       считать подряд идущие «плохие» значения, на for_windows-м - firing, при хорошем значении
    #       во время тревоги - resolved; хорошее значение сбрасывает счётчик
    return []


def retraining_decision(drift_rows, accuracy_rows, min_accuracy, min_samples=30):
    """Решение о переобучении и причины: дрейф признаков и падение точности."""
    # TODO: reasons "drift:<признак>" для строк со статусом alert, затем причина по качеству
    #       из ПОСЛЕДНЕЙ строки с n >= min_samples или "quality:insufficient-data"
    return {"retrain": False, "reasons": []}
