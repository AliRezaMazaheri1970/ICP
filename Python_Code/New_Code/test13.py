import pandas as pd
import sqlite3

# ███████ تنظیمات — فقط این ۳ خط را تغییر بده ██████████████████
excel_file   = "new_data.xlsx"          # ← اسم فایل اکسلت رو اینجا بنویس
db_file      = "crm_data.db"       # ← اسم فایل دیتابیس sqlite ات
# █████████████████████████████████████████████████████████████████

# ۱. خواندن داده‌های جدید از اکسل
df_new = pd.read_excel(excel_file)

# ۲. اتصال به دیتابیس SQLite
con = sqlite3.connect(db_file)

# ۳. اضافه کردن ردیف‌های جدید به جدول pivot_crm
#    (اگر ستون‌ها دقیقاً هم‌نام اکسل و دیتابیس باشن، خودش همه چیز رو درست می‌ریزه)
df_new.to_sql("pivot_crm", con, if_exists="append", index=False)

# ۴. (اختیاری) همزمان دیتابیس کامل رو به اکسل به‌روز شده خروجی بگیر
df_all = pd.read_sql("SELECT * FROM pivot_crm", con)
df_all.to_excel("pivot_crm_FULL_updated.xlsx", index=False)

con.close()

print(f"تعداد {len(df_new)} ردیف جدید با موفقیت از فایل اکسل اضافه شد!")
print(f"فایل کامل به‌روز شده هم ذخیره شد: pivot_crm_FULL_updated.xlsx")