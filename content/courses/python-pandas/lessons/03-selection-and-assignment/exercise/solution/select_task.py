import pandas as pd


def select_orders(df, regions, min_qty):
    mask = df["region"].isin(regions) & (df["qty"] >= min_qty) & (df["status"] != "cancelled")
    return df.loc[mask].copy()


def bulk_discount(df):
    out = df.copy()
    mask = (out["status"] == "shipped") & (out["qty"] >= 10)
    out.loc[mask, "unit_price"] = out.loc[mask, "unit_price"] * 0.9
    return out


def cancel_orders(df, ids):
    out = df.copy()
    out.loc[out["order_id"].isin(ids), "status"] = "cancelled"
    return out


def expensive(df, threshold):
    return df.assign(total=df["qty"] * df["unit_price"]).query("total > @threshold")[["order_id", "total"]]
