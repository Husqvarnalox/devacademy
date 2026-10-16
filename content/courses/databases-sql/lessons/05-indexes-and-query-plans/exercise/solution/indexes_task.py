INDEXES = [
    # customer_id слева: Q1; (customer_id, status): Q2; + total: Q4 читается только из индекса
    "CREATE INDEX idx_orders_customer_status_total ON orders(customer_id, status, total)",
    # status слева: Q5; (status, created_at): Q3 (равенство, затем диапазон)
    "CREATE INDEX idx_orders_status_created ON orders(status, created_at)",
]
