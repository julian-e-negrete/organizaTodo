#!/usr/bin/env python3
# /// script
# dependencies = [
#   "pinecone>=8.0.0",
#   "psycopg2-binary>=2.9",
# ]
# ///

"""
Syncs financial line-item records from the main organizaTodo Postgres database
into the Pinecone "organizatodo-financial" index for semantic search.

Reads DB credentials from POSTGRES_HOST/PORT/USER/PASSWORD/POSTGRES_DB and
PINECONE_API_KEY from the environment (e.g. `set -a && source .env && set +a`).

Hard exclusions, never queried or embedded: Users (PasswordHash/Email),
PasswordResetTokens (Token), ShoppingLists/LedgerTags/LedgerTransactionTags
(containers/junctions, no content), MockProducts (superseded by the real
catalog in organizatodo-products).

Re-run anytime records change - upserts are keyed "{entity_type}:{Id}" so
re-running overwrites existing records instead of duplicating them.
"""

import os
import sys

import psycopg2
from pinecone import Pinecone

INDEX_NAME = "organizatodo-financial"
NAMESPACE = "records"
BATCH_SIZE = 96

QUERIES = {
    "housing_service": """
        SELECT "Id", "UserId", "Name", "Amount", "DueDay", "Periodicity",
               "IsPaid", "PaidDate", "IsActive", "CreatedAt"
        FROM "HousingServices"
    """,
    "credit_card_purchase": """
        SELECT "Id", "UserId", "Description", "TotalAmount", "Installments",
               "CurrentInstallment", "InterestRate", "PurchaseDate", "IsActive"
        FROM "CreditCardPurchases"
    """,
    "fixed_liability": """
        SELECT "Id", "UserId", "Name", "MonthlyAmount", "DueDay", "IsActive",
               "IsPaid", "PaidDate", "CreatedAt"
        FROM "FixedLiabilities"
    """,
    "income": """
        SELECT "Id", "UserId", "Description", "Amount", "IncomeDate",
               "Category", "Month", "Year"
        FROM "Income"
    """,
    "other_expense": """
        SELECT "Id", "UserId", "Description", "Amount", "ExpenseDate",
               "Month", "Year"
        FROM "OtherExpenses"
    """,
    "saving": """
        SELECT "Id", "UserId", "Amount", "Month", "Year", "Notes",
               "IsInitialBalance", "CreatedAt"
        FROM "Savings"
    """,
    "ledger_transaction": """
        SELECT "Id", "UserId", "Amount", "Type", "Description",
               "TransactionDate", "BalanceAfter", "GeneratesInterest",
               "InterestRate"
        FROM "LedgerTransactions"
    """,
    "shopping_list_item": """
        SELECT i."Id", l."UserId", i."ProductName", i."Quantity",
               i."EstimatedPrice", i."Supermarket", i."Priority",
               l."Month", l."Year"
        FROM "ShoppingListItems" i
        JOIN "ShoppingLists" l ON l."Id" = i."ShoppingListId"
    """,
}


def iso(value):
    return value.isoformat() if value is not None else ""


def to_record(entity_type, row):
    if entity_type == "housing_service":
        id_, user_id, name, amount, due_day, periodicity, is_paid, paid_date, is_active, created_at = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{name} - servicio del hogar - {periodicity}",
            "entity_type": entity_type, "user_id": user_id, "name": name,
            "amount": float(amount), "due_day": due_day, "periodicity": periodicity,
            "is_paid": bool(is_paid), "paid_date": iso(paid_date),
            "is_active": bool(is_active), "created_at": iso(created_at),
        }
    if entity_type == "credit_card_purchase":
        id_, user_id, description, total_amount, installments, current_installment, interest_rate, purchase_date, is_active = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{description} - compra con tarjeta de crédito",
            "entity_type": entity_type, "user_id": user_id, "description": description,
            "total_amount": float(total_amount), "installments": installments,
            "current_installment": current_installment, "interest_rate": float(interest_rate),
            "purchase_date": iso(purchase_date), "is_active": bool(is_active),
        }
    if entity_type == "fixed_liability":
        id_, user_id, name, monthly_amount, due_day, is_active, is_paid, paid_date, created_at = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{name} - gasto fijo mensual",
            "entity_type": entity_type, "user_id": user_id, "name": name,
            "monthly_amount": float(monthly_amount), "due_day": due_day or 0,
            "is_active": bool(is_active), "is_paid": bool(is_paid),
            "paid_date": iso(paid_date), "created_at": iso(created_at),
        }
    if entity_type == "income":
        id_, user_id, description, amount, income_date, category, month, year = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{description} - ingreso - {category or ''}",
            "entity_type": entity_type, "user_id": user_id, "description": description,
            "amount": float(amount), "category": category or "",
            "income_date": iso(income_date), "month": month, "year": year,
        }
    if entity_type == "other_expense":
        id_, user_id, description, amount, expense_date, month, year = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{description} - otro gasto",
            "entity_type": entity_type, "user_id": user_id, "description": description,
            "amount": float(amount), "expense_date": iso(expense_date),
            "month": month, "year": year,
        }
    if entity_type == "saving":
        id_, user_id, amount, month, year, notes, is_initial_balance, created_at = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{notes or 'Ahorro'} - ahorro mensual",
            "entity_type": entity_type, "user_id": user_id, "amount": float(amount),
            "month": month, "year": year, "notes": notes or "",
            "is_initial_balance": bool(is_initial_balance), "created_at": iso(created_at),
        }
    if entity_type == "ledger_transaction":
        id_, user_id, amount, type_, description, transaction_date, balance_after, generates_interest, interest_rate = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{description} - {type_}",
            "entity_type": entity_type, "user_id": user_id, "amount": float(amount),
            "type": type_, "description": description, "transaction_date": iso(transaction_date),
            "balance_after": float(balance_after), "generates_interest": bool(generates_interest),
            "interest_rate": float(interest_rate) if interest_rate is not None else 0.0,
        }
    if entity_type == "shopping_list_item":
        id_, user_id, product_name, quantity, estimated_price, supermarket, priority, month, year = row
        return {
            "_id": f"{entity_type}:{id_}",
            "chunk_text": f"{product_name}",
            "entity_type": entity_type, "user_id": user_id, "product_name": product_name,
            "quantity": quantity, "estimated_price": float(estimated_price) if estimated_price is not None else 0.0,
            "supermarket": supermarket or "", "priority": priority, "month": month, "year": year,
        }
    raise ValueError(f"Unknown entity_type: {entity_type}")


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
        dbname=os.environ["POSTGRES_DB"],
    )

    pc = Pinecone(api_key=pinecone_api_key, source_tag="organizatodo:sync_financial")
    idx = pc.Index(INDEX_NAME)

    total = 0
    for entity_type, sql in QUERIES.items():
        cur = conn.cursor()
        cur.execute(sql)
        records = [to_record(entity_type, row) for row in cur.fetchall()]
        cur.close()

        entity_total = 0
        for batch in batched(records, BATCH_SIZE):
            idx.upsert_records(records=batch, namespace=NAMESPACE)
            entity_total += len(batch)
        print(f"Upserted {entity_total} records for entity_type={entity_type}")
        total += entity_total

    conn.close()
    print(f"Done. Upserted {total} records into '{INDEX_NAME}' (namespace: '{NAMESPACE}')")


if __name__ == "__main__":
    main()
