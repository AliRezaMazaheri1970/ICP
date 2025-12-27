# screens/management/statistics_tab.py
import sqlite3
import pandas as pd
import logging
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel, QPushButton, QComboBox,
    QDateEdit, QFrame, QGridLayout, QScrollArea, QMessageBox
)
from PyQt6.QtCore import Qt, QDate, QTimer
from PyQt6.QtGui import QFont, QColor
import jdatetime
from datetime import datetime, timedelta
import pyqtgraph as pg

logger = logging.getLogger(__name__)

class StatisticsTab(QWidget):
    def __init__(self, app):
        super().__init__()
        self.app = app
        self.db_path = app.resource_path("crm_data.db")
        self.setup_ui()
        QTimer.singleShot(300, self.refresh_all)

    def setup_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(32, 32, 32, 32)
        main_layout.setSpacing(28)

        # === Top Circular Summary Cards ===
        cards_frame = QFrame()
        cards_frame.setStyleSheet("background: white; border-radius: 28px; padding: 32px; box-shadow: 0 12px 40px rgba(0,0,0,0.1);")
        cards_layout = QHBoxLayout(cards_frame)
        cards_layout.setSpacing(40)
        cards_layout.addStretch()

        self.value_labels = []

        stats_info = [
            ("Uploaded Files", "#6366f1"),
            ("CRM Reports", "#8b5cf6"),
            ("Changes Logged", "#ec4899"),
            ("Pending Approvals", "#f59e0b"),
            ("Active Files", "#10b981"),
            ("Total Devices", "#06b6d4"),
            ("Total Contracts", "#f43f5e")
        ]

        for title, color in stats_info:
            card = self.create_circular_card(title, "0", color)
            cards_layout.addWidget(card)
            self.value_labels.append(card.findChild(QLabel, "value_label"))

        cards_layout.addStretch()
        main_layout.addWidget(cards_frame)

        # === Filters ===
        filter_card = QFrame()
        filter_card.setStyleSheet("background: white; border-radius: 24px; padding: 24px; box-shadow: 0 10px 35px rgba(0,0,0,0.1);")
        fl = QHBoxLayout(filter_card)
        fl.setSpacing(20)

        fl.addWidget(QLabel("<b>Period:</b>"))
        self.period_combo = QComboBox()
        self.period_combo.addItems(["Today", "Last 7 Days", "Last 30 Days", "Last 90 Days", "Year to Date", "Custom"])
        self.period_combo.setStyleSheet("padding: 14px 24px; border-radius: 14px; border: 2px solid #e2e8f0; font-size: 15px;")
        self.period_combo.currentTextChanged.connect(self.on_period_changed)
        fl.addWidget(self.period_combo)

        self.start_date = QDateEdit(calendarPopup=True)
        self.start_date.setDate(QDate.currentDate().addDays(-30))
        self.start_date.setVisible(False)
        self.start_date.setStyleSheet("padding: 12px; border-radius: 12px; border: 2px solid #e2e8f0;")
        fl.addWidget(self.start_date)

        self.end_date = QDateEdit(calendarPopup=True)
        self.end_date.setDate(QDate.currentDate())
        self.end_date.setVisible(False)
        self.end_date.setStyleSheet("padding: 12px; border-radius: 12px; border: 2px solid #e2e8f0;")
        fl.addWidget(self.end_date)

        fl.addWidget(QLabel("<b>Device:</b>"))
        self.device_combo = QComboBox()
        self.device_combo.addItem("All Devices")
        self.device_combo.setStyleSheet("padding: 14px 20px; border-radius: 14px; border: 2px solid #e2e8f0;")
        fl.addWidget(self.device_combo)

        refresh_btn = QPushButton("Refresh")
        refresh_btn.setStyleSheet("background: #4f46e5; color: white; padding: 14px 36px; border-radius: 14px; font-weight: bold;")
        refresh_btn.clicked.connect(self.refresh_all)
        fl.addWidget(refresh_btn)

        fl.addStretch()
        main_layout.addWidget(filter_card)

        # === Charts Container (2 Columns) ===
        self.charts_container = QWidget()
        self.charts_grid = QGridLayout(self.charts_container)
        self.charts_grid.setSpacing(28)
        self.charts_grid.setContentsMargins(10, 10, 10, 40)

        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setStyleSheet("border: none;")
        scroll.setWidget(self.charts_container)
        main_layout.addWidget(scroll)

        self.load_devices()

    def create_circular_card(self, title, value, color):
        card = QFrame()
        card.setFixedSize(170, 170)  # اندازه عالی برای 6 کارت در یک ردیف
        darker = QColor(color).darker(140).name()

        card.setStyleSheet(f"""
            QFrame {{
                background: qlineargradient(x1:0, y1:0, x2:1, y2:1,
                    stop:0 {color}, 
                    stop:1 {darker});
                border-radius: 85px;
                border: none;
            }}
            QLabel {{
                background: transparent !important;
                border: none !important;
                padding: 0px !important;
                margin: 0px !important;
            }}
        """)

        layout = QVBoxLayout(card)
        layout.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.setContentsMargins(8, 12, 8, 8)   # پدینگ خیلی کم (فقط 8 پیکسل از اطراف)
        layout.setSpacing(4)

        # عدد بزرگ
        value_lbl = QLabel(str(value))
        value_lbl.setObjectName("value_label")
        value_lbl.setFont(QFont("Segoe UI", 38, QFont.Weight.ExtraBold))
        value_lbl.setStyleSheet("color: white;")
        value_lbl.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(value_lbl)

        # عنوان (نزدیک به عدد و بدون پس‌زمینه)
        title_lbl = QLabel(title)
        title_lbl.setFont(QFont("Segoe UI", 11, QFont.Weight.Medium))
        title_lbl.setStyleSheet("color: rgba(255, 255, 255, 0.95);")
        title_lbl.setAlignment(Qt.AlignmentFlag.AlignCenter)
        title_lbl.setWordWrap(True)
        layout.addWidget(title_lbl)

        return card

    def on_period_changed(self, text):
        visible = text == "Custom"
        self.start_date.setVisible(visible)
        self.end_date.setVisible(visible)
        if text != "Custom":
            self.refresh_all()

    def get_date_range(self):
        period = self.period_combo.currentText()
        today = datetime.today().date()

        if period == "Today":
            start = end = today
        elif period == "Last 7 Days":
            start = today - timedelta(days=6)
            end = today
        elif period == "Last 30 Days":
            start = today - timedelta(days=29)
            end = today
        elif period == "Last 90 Days":
            start = today - timedelta(days=89)
            end = today
        elif period == "Year to Date":
            start = today.replace(month=1, day=1)
            end = today
        else:  # Custom
            start = self.start_date.date().toPyDate()
            end = self.end_date.date().toPyDate()
            if start > end:
                start, end = end, start

        return start.strftime("%Y-%m-%d"), end.strftime("%Y-%m-%d")

    def miladi_to_jalali(self, miladi):
        if not miladi or len(str(miladi)) < 10:
            return "—"
        try:
            y, m, d = map(int, str(miladi)[:10].split('-'))
            return jdatetime.date.fromgregorian(year=y, month=m, day=d).strftime("%Y/%m/%d")
        except:
            return str(miladi)[:10]

    def load_devices(self):
        try:
            conn = sqlite3.connect(self.db_path)
            df = pd.read_sql_query("SELECT name FROM devices ORDER BY name", conn)
            conn.close()
            for name in df['name']:
                self.device_combo.addItem(name)
        except Exception as e:
            logger.error(f"Load devices error: {e}")

    def refresh_all(self):
        # پاک کردن نمودارهای قبلی
        while self.charts_grid.count():
            child = self.charts_grid.takeAt(0)
            if child.widget():
                child.widget().deleteLater()

        start, end = self.get_date_range()
        device = self.device_combo.currentText() if self.device_combo.currentText() != "All Devices" else None

        try:
            conn = sqlite3.connect(self.db_path)

            # آپدیت کارت‌ها
            counts = [
                self.count_uploaded_files(conn, start, end, device),
                self.count_crm_records(conn, start, end),
                self.count_changes(conn, start, end),
                self.count_pending_approvals(conn),
                self.count_active_files(conn),
                self.count_devices(conn),
                self.count_total_contracts(conn, start, end, device)
            ]
            for lbl, val in zip(self.value_labels, counts):
                lbl.setText(str(val))

            # نمودار ۱: آپلودها در روز
            upload_chart = self.create_fixed_chart("Daily Uploads", "#6366f1")
            self.plot_bar_fixed(conn, upload_chart, """
                SELECT DATE(created_at) as d, COUNT(*) as c 
                FROM uploaded_files 
                WHERE DATE(created_at) BETWEEN ? AND ? 
                GROUP BY d ORDER BY d
            """, (start, end))
            self.charts_grid.addWidget(upload_chart, 0, 0)

            # نمودار 2: توزیع دستگاه‌ها
            pie_chart = self.create_fixed_chart("Device Distribution", "#10b981")
            self.plot_device_pie_fixed(conn, pie_chart, start, end)
            self.charts_grid.addWidget(pie_chart,0,1)  # دو ستون

            conn.close()

        except Exception as e:
            logger.error(f"Dashboard error: {e}")
            QMessageBox.critical(self, "Error", str(e))

    def create_fixed_chart(self, title, accent_color):
        frame = QFrame()
        frame.setFixedSize(520, 360)
        frame.setStyleSheet(f"""
            background: white;
            border-radius: 24px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.1);
            border: 1px solid #e2e8f0;  /* فقط یک خط دور قاب */
        """)
        layout = QVBoxLayout(frame)
        layout.setContentsMargins(20, 20, 20, 20)

        title_lbl = QLabel(title)
        title_lbl.setFont(QFont("Segoe UI", 18, QFont.Weight.Bold))
        title_lbl.setStyleSheet("color: #1e293b; margin-bottom: 12px;")
        layout.addWidget(title_lbl)

        plot = pg.PlotWidget()
        plot.setBackground("white")
        plot.showGrid(x=True, y=True, alpha=0.2)
        plot.setMouseEnabled(x=False, y=False)  # غیرفعال کردن زوم و اسکرول
        plot.setMenuEnabled(False)
        layout.addWidget(plot)

        # اضافه کردن عنوان حتی اگر داده‌ای نباشد (عنوان همیشه هست، اما برای "No data" هم حفظ می‌شود)
        return frame

    def plot_bar_fixed(self, conn, frame, query, params):
        plot = frame.layout().itemAt(1).widget()
        df = pd.read_sql_query(query, conn, params=params)

        plot.clear()  # پاک کردن محتوای قبلی

        # همیشه عنوان وجود دارد (از create_fixed_chart)

        if df.empty:
            no_data_text = pg.TextItem("No data", color="#94a3b8", anchor=(0.5, 0.5))
            no_data_text.setPos(0.5, 0.5)  # وسط قرار دادن
            plot.addItem(no_data_text)
            return

        x = range(len(df))
        bars = pg.BarGraphItem(x=x, height=df.iloc[:,1], width=0.7, brush=frame.styleSheet().split("border: 1px solid ")[1].split(";")[0])
        plot.addItem(bars)

        jalali_dates = [self.miladi_to_jalali(d) for d in df.iloc[:,0]]
        plot.getAxis('bottom').setTicks([list(enumerate(jalali_dates))])
        plot.setLabel('left', 'Count', color='#1e293b')
        plot.setLabel('bottom', 'Date (Jalali)', color='#1e293b')

        # تنظیم محدوده برای جلوگیری از زوم بیش از حد
        plot.setXRange(-0.5, len(df) - 0.5, padding=0.1)
        plot.setYRange(0, df.iloc[:,1].max() * 1.1, padding=0)

        # غیرفعال کردن مجدد زوم
        plot.setMouseEnabled(x=False, y=False)

    def count_total_contracts(self, conn, start, end, device=None):
        """شمارش تعداد قراردادهای یکتا در بازه زمانی (بدون تکرار)"""
        query = """
            SELECT COUNT(DISTINCT TRIM(contracts)) 
            FROM uploaded_files 
            WHERE contracts IS NOT NULL 
              AND TRIM(contracts) != '' 
              AND TRIM(contracts) != '[]'
              AND DATE(created_at) BETWEEN ? AND ?
        """
        params = [start, end]
        
        if device:
            query += " AND device_id = (SELECT id FROM devices WHERE name = ?)"
            params.append(device)
            
        result = conn.execute(query, params).fetchone()[0]
        return result if result else 0
    def plot_device_pie_fixed(self, conn, frame, start, end):
        plot = frame.layout().itemAt(1).widget()
        query = """
            SELECT 
                CASE 
                    WHEN name LIKE '%mass%' THEN 'Mass'
                    WHEN name LIKE '%oes%' OR name LIKE '%OES%' THEN 'OES'
                    WHEN name LIKE '%fire%' THEN 'Fire'
                    ELSE 'Other'
                END as type,
                COUNT(uf.id) as cnt
            FROM devices d
            LEFT JOIN uploaded_files uf ON d.id = uf.device_id 
                AND DATE(uf.created_at) BETWEEN ? AND ?
            GROUP BY type
        """
        df = pd.read_sql_query(query, conn, params=(start, end))

        plot.clear()  # پاک کردن محتوای قبلی

        # همیشه عنوان وجود دارد (از create_fixed_chart)

        if df.empty or df['cnt'].sum() == 0:
            no_data_text = pg.TextItem("No data", color="#94a3b8", anchor=(0.5, 0.5))
            no_data_text.setPos(0.5, 0.5)  # وسط قرار دادن
            plot.addItem(no_data_text)
            return

        colors = ["#f59e0b", "#3b82f6", "#ef4444", "#6b7280"]
        legend = plot.addLegend(offset=(10, 10))

        total = df['cnt'].sum()
        start_angle = 0
        for i, row in df.iterrows():
            angle = row['cnt'] / total * 360
            color = colors[i % len(colors)]
            arc = pg.QtWidgets.QGraphicsEllipseItem(-90, -90, 180, 180)
            arc.setStartAngle(int(start_angle * 16))
            arc.setSpanAngle(int(angle * 16))
            arc.setBrush(QColor(color))
            arc.setPen(pg.mkPen(width=0))
            plot.addItem(arc)
            legend.addItem(pg.ScatterPlotItem(brush=color, size=12), f"{row['type']}: {row['cnt']}")
            start_angle += angle

        # تنظیم محدوده برای pie chart (دایره کامل بدون زوم بیش از حد)
        plot.setXRange(-100, 100, padding=0)
        plot.setYRange(-100, 100, padding=0)
        plot.hideAxis('bottom')
        plot.hideAxis('left')

        # غیرفعال کردن مجدد زوم
        plot.setMouseEnabled(x=False, y=False)

    # Counter Methods (بدون تغییر)
    def count_uploaded_files(self, c, s, e, d=None):
        q = "SELECT COUNT(*) FROM uploaded_files WHERE DATE(created_at) BETWEEN ? AND ?"
        p = [s, e]
        if d:
            q += " AND device_id=(SELECT id FROM devices WHERE name=?)"
            p.append(d)
        return c.execute(q, p).fetchone()[0]

    def count_crm_records(self, c, s, e, d=None):
        return c.execute("SELECT COUNT(*) FROM crm_data WHERE date BETWEEN ? AND ?", (s, e)).fetchone()[0]

    def count_changes(self, c, s, e):
        return c.execute("SELECT COUNT(*) FROM changes_log WHERE DATE(timestamp) BETWEEN ? AND ?", (s, e)).fetchone()[0]

    def count_pending_approvals(self, c):
        return c.execute("SELECT COUNT(*) FROM changes_log LEFT JOIN approvals a ON changes_log.id = a.change_id WHERE a.id IS NULL").fetchone()[0]

    def count_active_files(self, c):
        return c.execute("SELECT COUNT(*) FROM uploaded_files WHERE is_archived = 0").fetchone()[0]

    def count_devices(self, c):
        return c.execute("SELECT COUNT(*) FROM devices").fetchone()[0]