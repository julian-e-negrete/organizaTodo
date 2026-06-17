#!/usr/bin/env python3
# /// script
# dependencies = [
#   "pinecone>=8.0.0",
#   "psycopg2-binary>=2.9",
# ]
# ///

"""
Syncs carrefour_products / coto_products from the Shopping (marketdata) Postgres
database into the Pinecone "organizatodo-products" index for semantic search.

Reads DB credentials from POSTGRES_HOST/PORT/USER/PASSWORD/POSTGRES_DB_Shopping
and PINECONE_API_KEY from the environment (e.g. `set -a && source .env && set +a`).

Re-run anytime the catalog is rescraped - upserts are keyed by "{source}:{product_id}"
so re-running overwrites existing records instead of duplicating them.
"""

import os
import sys

import psycopg2
from pinecone import Pinecone

INDEX_NAME = "organizatodo-products"
NAMESPACE = "products"
BATCH_SIZE = 96

TABLES = [
    ("carrefour_products", "Carrefour"),
    ("coto_products", "Coto"),
]


def fetch_rows(conn, table, source):
    has_promo = table == "coto_products"
    promo_col = "promo" if has_promo else "NULL AS promo"
    cur = conn.cursor()
    cur.execute(f"""
        SELECT product_id, sku_id, name, brand, category, price, list_price,
               {promo_col}, available, scraped_at
        FROM {table}
    """)
    for product_id, sku_id, name, brand, category, price, list_price, promo, available, scraped_at in cur:
        yield {
            "_id": f"{source}:{product_id}",
            "chunk_text": f"{name} - {brand} - {category}",
            "name": name or "",
            "brand": brand or "",
            "category": category or "",
            "price": float(price) if price is not None else 0.0,
            "list_price": float(list_price) if list_price is not None else 0.0,
            "promo": promo or "",
            "available": bool(available),
            "source": source,
            "product_id": product_id or "",
            "sku_id": sku_id or "",
            "scraped_at": scraped_at.isoformat() if scraped_at else "",
        }
    cur.close()


def batched(iterable, size):
    batch = []
    for item in iterable:
        batch.append(item)
        if len(batch) >= size:
            yield batch
            batch = []
    if batch:
        yield batch


def main():
    pinecone_api_key = os.environ.get("PINECONE_API_KEY")
    if not pinecone_api_key:
        print("Error: PINECONE_API_KEY environment variable not set", file=sys.stderr)
        sys.exit(1)

    conn = psycopg2.connect(
        host=os.environ["POSTGRES_HOST"],
        port=os.environ["POSTGRES_PORT"],
        user=os.environ["POSTGRES_USER"],
        password=os.environ["POSTGRES_PASSWORD"],
        dbname=os.environ["POSTGRES_DB_Shopping"],
    )

    pc = Pinecone(api_key=pinecone_api_key, source_tag="organizatodo:sync_products")
    idx = pc.Index(INDEX_NAME)

    total = 0
    for table, source in TABLES:
        table_total = 0
        for batch in batched(fetch_rows(conn, table, source), BATCH_SIZE):
            idx.upsert_records(records=batch, namespace=NAMESPACE)
            table_total += len(batch)
        print(f"Upserted {table_total} records from {table} (source={source})")
        total += table_total

    conn.close()
    print(f"Done. Upserted {total} records into '{INDEX_NAME}' (namespace: '{NAMESPACE}')")


if __name__ == "__main__":
    main()
