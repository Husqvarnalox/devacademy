import pandas as pd


def load_orders(path):
    return pd.read_csv(path, dtype={"sku": "str"}, parse_dates=["order_date"], na_values=["N/A", "-"])


def missing_report(df):
    counts = df.isna().sum()
    return counts[counts > 0].sort_values(ascending=False)


def clean_orders(df):
    out = df.drop_duplicates().dropna(subset=["qty", "unit_price"])
    out = out.astype({"qty": "int64"})
    return out.reset_index(drop=True)


def fill_price(df):
    out = df.copy()
    out["unit_price"] = out["unit_price"].fillna(out.groupby("product")["unit_price"].transform("median"))
    return out
