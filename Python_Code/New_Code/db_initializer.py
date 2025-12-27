# utils/db_initializer.py
import sqlite3
import logging

logger = logging.getLogger(__name__)

def init_db_schema(resource_path_func):
    """
    ایجاد کامل schema دیتابیس از ابتدا
    فقط با username (بدون email و بدون مهاجرت)
    """
    db_path = resource_path_func("crm_data.db")
    elements_db_path = resource_path_func("excels_elements.db")
    try:
        conn = sqlite3.connect(db_path, timeout=30)
        cur = conn.cursor()
        cur.execute("PRAGMA journal_mode = WAL;")
        print(f"[DB Init] Initializing database at: {db_path}")

        # ==================================================================
        # 1. جدول roles
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS roles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                role_name TEXT UNIQUE NOT NULL,
                description TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        roles_data = [
            ('device_operator', 'Responsible for uploading raw data files from devices.'),
            ('report_manager', 'Applies corrections and coefficients to data.'),
            ('lab_manager', 'Supervises, approves/rejects changes, or edits data.'),
            ('admin', 'Full access, manages users and system.'),
            ('viewer', 'Can view data but cannot modify.')
        ]
        cur.executemany("INSERT OR IGNORE INTO roles (role_name, description) VALUES (?, ?)", roles_data)

        # ==================================================================
        # 2. جدول users — فقط با username (بدون email)
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT UNIQUE NOT NULL,
                password TEXT NOT NULL,
                full_name TEXT,
                position TEXT,
                role TEXT DEFAULT 'viewer',
                is_active INTEGER DEFAULT 1,
                remember_me INTEGER DEFAULT 0,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')

        # ==================================================================
        # 3. جدول devices
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS devices (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                model TEXT,
                serial_number TEXT UNIQUE,
                calibration_due_date DATE,
                status TEXT DEFAULT 'active',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')

        # نمونه دستگاه‌ها
        sample_devices = [
            (1, 'OES 715', '715', 'SN001'),
            (2, 'OES 735 1', '735', 'SN002'),
            (3, 'OES 735 2', '735', 'SN003'),
            (4, 'Mass elan9000 1', 'elan9000', 'SN004'),
            (5, 'Mass elan9000 2', 'elan9000', 'SN005'),
        ]
        for dev in sample_devices:
            cur.execute("""
                INSERT OR IGNORE INTO devices (id, name, model, serial_number) 
                VALUES (?, ?, ?, ?)
            """, dev)

        # ==================================================================
        # 4. جدول uploaded_files
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS uploaded_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                original_filename TEXT NOT NULL,
                clean_filename TEXT,
                file_path TEXT NOT NULL,
                device_id INTEGER REFERENCES devices(id),
                uploaded_by INTEGER REFERENCES users(id),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                is_archived INTEGER DEFAULT 0
            )
        ''')

        # اضافه کردن ستون‌های اختیاری
        for col, definition in [
            ('file_type', 'TEXT'),
            ('description', 'TEXT'),
            ('contracts', 'TEXT')
        ]:
            try:
                cur.execute(f"ALTER TABLE uploaded_files ADD COLUMN {col} {definition}")
            except sqlite3.OperationalError:
                pass  # ستون از قبل وجود دارد

        # ==================================================================
        # 5. جدول changes_log
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS changes_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                user_id INTEGER REFERENCES users(id),
                action TEXT NOT NULL,
                entity_type TEXT,
                entity_id INTEGER,
                file_path TEXT,
                column_name TEXT,
                solution_label TEXT,
                original_value TEXT,
                new_value TEXT,
                details TEXT,
                stage TEXT,
                pivot_index INTEGER
            )
        ''')

        # ==================================================================
        # 6. جدول permissions
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS permissions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                role_id INTEGER NOT NULL,
                action TEXT NOT NULL,
                allowed INTEGER DEFAULT 1,
                FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE,
                UNIQUE (role_id, action)
            )
        ''')

        # دریافت role_idها
        role_map = {name: cur.execute("SELECT id FROM roles WHERE role_name=?", (name,)).fetchone()[0]
                    for name in ['device_operator', 'report_manager', 'lab_manager', 'admin']}

        permissions_data = [
            (role_map['device_operator'], 'upload_file', 1),
            (role_map['report_manager'], 'apply_correction', 1),
            (role_map['lab_manager'], 'approve_change', 1),
            (role_map['lab_manager'], 'edit_data', 1),
            (role_map['admin'], 'manage_users', 1),
            (role_map['admin'], 'upload_file', 1),
            (role_map['admin'], 'apply_correction', 1),
            (role_map['admin'], 'approve_change', 1),
            (role_map['admin'], 'edit_data', 1),
        ]
        cur.executemany("INSERT OR IGNORE INTO permissions (role_id, action, allowed) VALUES (?, ?, ?)", permissions_data)

        # ==================================================================
        # 7. جدول approvals
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS approvals (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                change_id INTEGER REFERENCES changes_log(id) ON DELETE CASCADE,
                approved_by INTEGER REFERENCES users(id),
                status TEXT CHECK(status IN ('approved', 'rejected')) NOT NULL,
                comments TEXT,
                approved_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')

        # ==================================================================
        # 8. جدول notifications
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS notifications (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER REFERENCES users(id),
                message TEXT NOT NULL,
                type TEXT CHECK(type IN ('approval_needed', 'change_approved', 'change_rejected', 'system')) NOT NULL,
                related_entity_id INTEGER,
                is_read INTEGER DEFAULT 0,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
        ''')

        # ==================================================================
        # 9. جدول measurements
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS measurements (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_id INTEGER REFERENCES uploaded_files(id) ON DELETE CASCADE,
                element TEXT NOT NULL,
                sample_id TEXT NOT NULL,
                current_value REAL,
                UNIQUE(file_id, element, sample_id)
            )
        ''')

        # ==================================================================
        # 10. جدول measurement_versions
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS measurement_versions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                measurement_id INTEGER REFERENCES measurements(id) ON DELETE CASCADE,
                version_number INTEGER NOT NULL,
                value REAL,
                changed_by INTEGER REFERENCES users(id),
                change_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                stage TEXT,
                reason TEXT,
                UNIQUE(measurement_id, version_number)
            )
        ''')

        # ==================================================================
        # 11. جدول crm_selections
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS crm_selections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_id INTEGER REFERENCES uploaded_files(id),
                solution_label TEXT NOT NULL,
                selected_crm_key TEXT NOT NULL,
                selected_by INTEGER REFERENCES users(id),
                selected_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(file_id, solution_label)
            )
        ''')

        # ==================================================================
        # 12. جدول crm_data برای qc
        # ==================================================================
        cur.execute('''
            CREATE TABLE IF NOT EXISTS crm_data (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                crm_id TEXT,
                solution_label TEXT,
                element TEXT,
                value REAL,
                file_name TEXT,
                folder_name TEXT,
                date TEXT
            )
        ''')
        # ==================================================================
        # حذف جدول منسوخ
        # ==================================================================
        cur.execute("DROP TABLE IF EXISTS files")

        conn.commit()
        conn.close()

        # Initialize excels_elements.db
        conn_elements = sqlite3.connect(elements_db_path, timeout=30)
        cur_elements = conn_elements.cursor()
        cur_elements.execute("PRAGMA journal_mode = WAL;")
        print(f"[DB Init] Initializing elements database at: {elements_db_path}")

        # Create elements_data table (base columns; elements added dynamically)
        cur_elements.execute('''
            CREATE TABLE IF NOT EXISTS elements_data (
                sample_id TEXT,
                file_name TEXT
            )
        ''')

        conn_elements.commit()
        conn_elements.close()

        print("[DB Init] Database initialized successfully with username-based schema!")
        return True

    except Exception as e:
        logger.error(f"[DB Init] Failed: {e}")
        print(f"[DB Init] ERROR: {e}")
        return False