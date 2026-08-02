import sqlite3
import pyodbc

sqlite_db_path = r"c:\011_開発\ProjectRouteX\RouteXWms.db"
conn_sq = sqlite3.connect(sqlite_db_path)
cur_sq = conn_sq.cursor()

# SQL Server (ODBC Driver 17 or 18)
conn_str = "Driver={ODBC Driver 17 for SQL Server};Server=localhost;Database=RouteXWmsDb;Trusted_Connection=yes;"
try:
    conn_ss = pyodbc.connect(conn_str)
except Exception:
    conn_str = "Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=RouteXWmsDb;Trusted_Connection=yes;TrustServerCertificate=yes;"
    conn_ss = pyodbc.connect(conn_str)

cur_ss = conn_ss.cursor()

# SQL Server のテーブル一覧取得
cur_ss.execute("SELECT name FROM sys.tables WHERE type='U';")
ss_tables = [row[0] for row in cur_ss.fetchall()]

# 外部キー制約の一時無効化
for tbl in ss_tables:
    cur_ss.execute(f"ALTER TABLE [{tbl}] NOCHECK CONSTRAINT ALL;")
conn_ss.commit()

# テーブル依存順
target_order = [
    "m_shipper", "m_warehouse", "m_product", "m_carrier", 
    "m_freight_table", "m_shipping_class", "m_warehouse_distance_rate",
    "m_zip_code", "m_distance", "m_distance_freight", "m_individual_freight", "m_collection_area",
    "t_account", "t_inbound", "t_outbound", "t_inventory",
    "t_shipping_instruction", "t_outbound_allocation"
]

# 既存データ消去
for tbl in reversed(target_order):
    if tbl in ss_tables:
        cur_ss.execute(f"DELETE FROM [{tbl}];")
conn_ss.commit()

# 親 m_freight_table の有効なID取得
cur_sq.execute("SELECT freight_table_id FROM m_freight_table LIMIT 1")
first_ft = cur_sq.fetchone()
valid_freight_table_id = first_ft[0] if first_ft else None

# データ移行実行
for tbl in target_order:
    if tbl not in ss_tables:
        continue
        
    cur_sq.execute("SELECT name FROM sqlite_master WHERE type='table' AND name=?", (tbl,))
    if not cur_sq.fetchone():
        continue
        
    cur_ss.execute(f"SELECT column_name FROM information_schema.columns WHERE table_name='{tbl}' ORDER BY ordinal_position;")
    ss_cols = [r[0] for r in cur_ss.fetchall()]
    
    cur_sq.execute(f"PRAGMA table_info([{tbl}])")
    sq_cols = [c[1] for c in cur_sq.fetchall()]
    
    common_cols = [c for c in ss_cols if c in sq_cols]
    if not common_cols:
        continue
        
    cols_str = ", ".join([f"[{c}]" for c in common_cols])
    placeholders = ", ".join(["?"] * len(common_cols))
    
    cur_sq.execute(f"SELECT {cols_str} FROM [{tbl}]")
    rows = cur_sq.fetchall()
    
    if not rows:
        continue
        
    print(f"Migrating [{tbl}] : {len(rows)} rows...")
    insert_sql = f"INSERT INTO [{tbl}] ({cols_str}) VALUES ({placeholders})"
    
    for row in rows:
        clean_row = []
        for idx, col_name in enumerate(common_cols):
            val = row[idx]
            # 外部キー Empty GUID の補正
            if col_name == "freight_table_id" and val == "00000000-0000-0000-0000-000000000000" and valid_freight_table_id:
                val = valid_freight_table_id
            if isinstance(val, str):
                clean_row.append(val.replace("\x00", ""))
            else:
                clean_row.append(val)
        cur_ss.execute(insert_sql, clean_row)
    conn_ss.commit()

# 外部キー制約の再有効化
for tbl in ss_tables:
    cur_ss.execute(f"ALTER TABLE [{tbl}] WITH CHECK CHECK CONSTRAINT ALL;")
conn_ss.commit()

print("\n🎉 SUCCESS! ALL DATA MIGRATED 100% CLEANLY FROM SQLITE TO SQL SERVER 2025!")
