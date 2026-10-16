import pandas as pd


def join_orders(orders, customers):
    return orders.merge(customers, on="customer_id", how="left", validate="many_to_one")


def find_orphans(orders, customers):
    m = orders.merge(customers[["customer_id"]].drop_duplicates(), on="customer_id", how="left", indicator=True)
    return sorted(m.loc[m["_merge"] == "left_only", "order_id"].unique().tolist())


def revenue_pivot(joined):
    return joined.pivot_table(index="country", columns="segment", values="amount", aggfunc="sum", fill_value=0)


def unpivot(wide):
    long = wide.reset_index().melt(id_vars="country", var_name="segment", value_name="revenue")
    return long.sort_values(["country", "segment"]).reset_index(drop=True)
