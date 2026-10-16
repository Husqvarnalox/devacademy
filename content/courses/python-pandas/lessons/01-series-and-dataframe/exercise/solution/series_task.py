import pandas as pd


def align_sum(a, b):
    return a.add(b, fill_value=0)


def add_by_position(a, b):
    if len(a) != len(b):
        raise ValueError("длины не совпадают")
    return pd.Series(a.to_numpy() + b.to_numpy(), index=a.index)


def label_and_position(s, label, pos):
    return s.loc[label], s.iloc[pos]


def slice_by_labels(s, start, stop):
    return s.loc[start:stop]
