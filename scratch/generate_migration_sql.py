import sqlite3
import subprocess
import os

sqlite_db_path = r"c:\011_開発\ProjectRouteX\RouteXWms.db"

if not os.path.exists(sqlite_db_path):
    print(f"Error: SQLite DB not found at {sqlite_db_path}")
    exit(1)

conn_sqlite = sqlite3.connect(sqlite_db_path)
cursor_sqlite = conn_sqlite.cursor()

# SQLiteから全テーブル取得
cursor_sqlite.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';")
tables = [row[0] for row in cursor_sqlite.fetchall()]

print(f"Found tables in SQLite: {tables}")

# FK依存関係を考慮したインポート順序
target_order = [
    "m_shipper", "m_warehouse", "m_product", "m_carrier", 
    "m_freight_table", "m_shipping_class", "m_warehouse_distance_rate",
    "m_zip_code", "m_distance", "m_distance_freight", "m_individual_freight", "m_collection_area",
    "t_account", "t_inbound", "t_outbound", "t_inventory",
    "t_shipping_instruction", "t_outbound_allocation"
]

# その他のテーブル
for t in tables:
    if t not in target_order:
        target_order.append(t)

# 新SQL Serverのテーブルデータをクリーンアップ＆一括移行SQL生成
sql_script_path = r"c:\011_開発\ProjectRouteX\scratch\migration_script.sql"
os.makedirs(os.path.dirname(sql_script_path), exist_ok=True)

with open(sql_script_path, "w", encoding="utf-8") as f:
    f.write("USE [RouteXWmsDb];\nGO\n")
    f.write("EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';\nGO\n")
    
    # 既存データの全削除
    for table in reversed(target_order):
        if table in tables:
            f.write(f"DELETE FROM [{table}];\nGO\n")
            
    for table in target_order:
        if table not in tables:
            continue
        cursor_sqlite.execute(f"PRAGMA table_info([{table}])")
        columns_info = cursor_sqlite.fetchall()
        cols = [col[1] for col in columns_info]
        
        cursor_sqlite.execute(f"SELECT * FROM [{table}]")
        rows = cursor_sqlite.fetchall()
        
        if not rows:
            continue
            
        print(f"Migrating table {table}: {len(rows)} rows")
        
        col_names_str = ", ".join([f"[{c}]" for c in cols])
        f.write(f"SET IDENTITY_INSERT [{table}] ON;\nGO\n" if "id" in [c.lower() for c in cols] else "")
        
        for row in rows:
            val_strs = []
            for val in row:
                if val is None:
                    val_strs.append("NULL")
                elif isinstance(val, bool):
                    val_strs.append("1" if val else "0")
                elif isinstance(val, (int, float)):
                    val_strs.append(str(val))
                else:
                    # エスケープ処理
                    val_str = str(val).replace("'", "''")
                    val_strs.append(f"N'{val_str}'")
            values_clause = ", ".join(val_strs)
            f.write(f"INSERT INTO [{table}] ({col_names_str}) VALUES ({values_clause});\n")
        f.write("GO\n")
        
    f.write("EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';\nGO\n")

print("Migration SQL script generated successfully.")
