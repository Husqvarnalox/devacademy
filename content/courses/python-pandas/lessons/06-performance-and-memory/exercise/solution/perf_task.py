import numpy as np
import pandas as pd

RATES = {"ok": 0.10, "warn": 0.15, "fault": 0.30}


def add_cost(df):
    out = df.copy()
    rate = out["state"].map(RATES).astype("float64")
    out["cost"] = out["load_pct"] * rate + np.where(out["site"] == "warehouse", 2.0, 0.0)
    return out


def shrink(df):
    out = df.copy()
    for col in out.columns:
        dt = out[col].dtype
        if pd.api.types.is_string_dtype(dt) or dt == object:
            if out[col].nunique() / max(len(out), 1) <= 0.5:
                out[col] = out[col].astype("category")
        elif pd.api.types.is_integer_dtype(dt):
            out[col] = pd.to_numeric(out[col], downcast="integer")
    return out


def memory_bytes(df):
    return int(df.memory_usage(deep=True).sum())


def site_report(df):
    return (
        df.query("state == 'ok'")
        .assign(temp_f=lambda d: d["temp_c"] * 9 / 5 + 32)
        .groupby("site")
        .agg(readings=("reading_id", "count"), mean_temp_f=("temp_f", "mean"))
        .round({"mean_temp_f": 2})
        .reset_index()
    )
