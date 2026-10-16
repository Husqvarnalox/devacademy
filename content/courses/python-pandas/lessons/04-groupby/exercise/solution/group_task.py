import pandas as pd


def region_summary(df):
    out = df.groupby("region", dropna=False).agg(
        orders=("sale_id", "count"),
        units=("qty", "sum"),
        revenue=("revenue", "sum"),
        avg_ticket=("revenue", "mean"),
    )
    return out.sort_values("revenue", ascending=False)


def add_share(df):
    out = df.copy()
    total = out.groupby("region", dropna=False)["revenue"].transform("sum")
    out["region_share"] = out["revenue"] / total
    return out


def top_product(df):
    sums = df.groupby(["region", "product"], dropna=False)["revenue"].sum().reset_index()
    best = sums.loc[sums.groupby("region", dropna=False)["revenue"].idxmax()]
    return best.set_index("region")["product"].rename("product")
