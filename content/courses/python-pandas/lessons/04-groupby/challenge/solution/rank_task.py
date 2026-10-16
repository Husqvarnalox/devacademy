import pandas as pd


def zscore_within_region(df):
    g = df.groupby("region", dropna=False)["revenue"]
    return (df["revenue"] - g.transform("mean")) / g.transform("std")


def top_n_per_region(df, n):
    ordered = df.sort_values(["region", "revenue", "sale_id"], ascending=[True, False, True], na_position="last")
    out = ordered.groupby("region", dropna=False).head(n)
    return out[["sale_id", "region", "revenue"]].reset_index(drop=True)
