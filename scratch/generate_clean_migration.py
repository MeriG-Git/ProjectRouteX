import sqlite3
import subprocess
import os
import re

sqlite_db_path = r"c:\011_開発\ProjectRouteX\RouteXWms.db"
conn_sqlite = sqlite3.connect(sqlite_db_path)
cursor_sqlite = conn_sqlite.cursor()

# SQL Server のテーブル名およびカラム一覧を sqlcmd で取得
get_cols_sql = "SELECT t.name AS tbl, c.name AS col FROM sys.tables t INNER JOIN sys.columns c ON t.object_id = c.object_id ORDER BY t.name, c.column_id;"
res = subprocess.run(["sqlcmd", "-S", "localhost", "-E", "-d", "RouteXWmsDb", "-Q", get_cols_sql, "-h", "-1", "-W"], capture_output=True, text=True)

sql_schema = {}
for line in res.stdout.splitlines():
    parts = line.strip().split()
    if len(parts) >= 2:
        tbl, col = parts[0], parts[1]
        if tbl not in sql_schema:
            sql_schema[tbl] = []
        sql_schema[tbl].append(col)

# 依存関係順序
target_order = [
    "m_shipper", "m_warehouse", "m_product", "m_carrier", 
    "m_freight_table", "m_shipping_class", "m_warehouse_distance_rate",
    "m_zip_code", "m_distance", "m_distance_freight", "m_individual_freight", "m_collection_area",
    "t_account", "t_inbound", "t_outbound", "t_inventory",
    "t_shipping_instruction", "t_outbound_allocation"
]

sql_file_path = r"c:\011_開発\ProjectRouteX\scratch\clean_migration.sql"
with open(sql_file_path, "w", encoding="utf-8") as f:
    f.write("USE [RouteXWmsDb];\nGO\n")
    
    # 全テーブルのFK個別無効化
    for tbl in sql_schema.keys():
        f.write(f"ALTER TABLE [{tbl}] NOCHECK CONSTRAINT ALL;\n")
    f.write("GO\n")
    
    # 全テーブルの既存データクリア
    for tbl in reversed(target_order):
        if tbl in sql_schema:
            f.write(f"DELETE FROM [{tbl}];\n")
    f.write("GO\n")
            
    for tbl in target_order:
        if tbl not in sql_schema:
            continue
            
        cursor_sqlite.execute("SELECT name FROM sqlite_master WHERE type='table' AND name=?", (tbl,))
        if not cursor_sqlite.fetchone():
            continue
            
        cursor_sqlite.execute(f"PRAGMA table_info([{tbl}])")
        sqlite_cols = [c[1] for c in cursor_sqlite.fetchall()]
        
        common_cols = [c for c in sql_schema[tbl] if c in sqlite_cols]
        if not common_cols:
            continue
            
        col_select = ", ".join([f"[{c}]" for c in common_cols])
        cursor_sqlite.execute(f"SELECT {col_select} FROM [{tbl}]")
        rows = cursor_sqlite.fetchall()
        
        if not rows:
            continue
            
        col_names_str = ", ".join([f"[{c}]" for c in common_cols])
        
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
                    # 改行文字や制御文字の完全除去とシングルクォートのエスケープ
                    s = str(val).replace("'", "''")
                    s = re.sub(r'[\r\n\t\x00-\x1f]', '', s)
                    val_strs.append(f"N'{s}'")
            vals_str = ", ".join(val_strs)
            f.write(f"INSERT INTO [{tbl}] ({col_names_str}) VALUES ({vals_str});\n")
        f.write("GO\n")
        
    for tbl in sql_schema.keys():
        f.write(f"ALTER TABLE [{tbl}] WITH CHECK CHECK CONSTRAINT ALL;\n")
    f.write("GO\n")

print("Clean Migration SQL (Strictly Sanitized) generated successfully.")
