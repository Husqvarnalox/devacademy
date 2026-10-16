import pandas as pd


def customer_report(orders, customers):
    agg = orders.groupby("customer_id").agg(orders=("order_id", "count"), revenue=("amount", "sum")).reset_index()
    out = customers[["customer_id", "country"]].merge(agg, on="customer_id", how="left", validate="one_to_one")
    out["orders"] = out["orders"].fillna(0).astype("int64")
    out["revenue"] = out["revenue"].fillna(0.0)
    return out.sort_values(["revenue", "customer_id"], ascending=[False, True]).reset_index(drop=True)


def merge_summary(orders, customers):
    m = orders[["order_id", "customer_id"]].merge(customers[["customer_id"]].drop_duplicates(), on="customer_id", how="outer", indicator=True)
    both = int((m["_merge"] == "both").sum())
    orders_only = int((m["_merge"] == "left_only").sum())
    customers_only = int((m["_merge"] == "right_only").sum())
    return {"both": both, "orders_only": orders_only, "customers_only": customers_only}
