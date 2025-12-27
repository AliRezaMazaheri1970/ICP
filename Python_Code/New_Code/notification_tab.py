# screens/notifications/notification_tab.py
import sqlite3
import pandas as pd
import logging
from PyQt6.QtWidgets import (
    QVBoxLayout, QWidget, QLabel, QPushButton,
    QTableView, QHeaderView, QHBoxLayout, QMessageBox
)
from PyQt6.QtCore import Qt
from PyQt6.QtGui import QStandardItemModel, QStandardItem, QColor

logger = logging.getLogger(__name__)

class NotificationTab(QWidget):
    def __init__(self, app):
        super().__init__()
        self.app = app
        self.db_path = app.resource_path("crm_data.db")
        self.user_id = app.user_id
        self.setup_ui()
        self.load_notifications()

    def setup_ui(self):
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(10)

        header = QHBoxLayout()
        refresh_btn = QPushButton("Refresh")
        refresh_btn.clicked.connect(self.load_notifications)
        header.addWidget(refresh_btn)
        self.notif_count_label = QLabel("")
        self.notif_count_label.setStyleSheet("color: #dc2626; font-weight: bold;")
        header.addWidget(self.notif_count_label)
        header.addStretch()
        layout.addLayout(header)

        self.notif_table = QTableView()
        self.notif_table.setSelectionBehavior(QTableView.SelectionBehavior.SelectRows)
        self.notif_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        layout.addWidget(self.notif_table)

        mark_read_btn = QPushButton("Mark as Read")
        mark_read_btn.clicked.connect(self.mark_selected_as_read)
        layout.addWidget(mark_read_btn)

    def load_notifications(self):
            """بارگذاری نوتیفیکیشن‌ها از دیتابیس"""
            try:
                conn = sqlite3.connect(self.db_path, timeout=30)
                query = """
                    SELECT 
                        n.id,
                        n.message,
                        n.type,
                        n.created_at,
                        n.is_read,
                        -- فرستنده
                        COALESCE(
                            u_from.full_name,
                            u_from.username,
                            'System'
                        ) AS sender,
                        -- گیرنده
                        COALESCE(
                            u_to.full_name,
                            u_to.username,
                            'Unknown'
                        ) AS recipient
                    FROM notifications n
                    LEFT JOIN changes_log cl ON n.related_entity_id = cl.id AND n.type IN ('approval_needed', 'change_approved', 'change_rejected')
                    LEFT JOIN approvals a ON cl.id = a.change_id
                    LEFT JOIN users u_from ON 
                        (n.type = 'approval_needed' AND cl.user_id = u_from.id) OR
                        (n.type IN ('change_approved', 'change_rejected') AND a.approved_by = u_from.id)
                    LEFT JOIN users u_to ON n.user_id = u_to.id
                    WHERE n.user_id = ?
                    ORDER BY n.created_at DESC
                """
                df = pd.read_sql_query(query, conn, params=(self.user_id,))
                conn.close()

                model = QStandardItemModel()
                model.setHorizontalHeaderLabels([
                    "ID", "Message", "Type", "Time", "Read", "From", "To"
                ])

                unread_count = 0
                for _, row in df.iterrows():
                    read_status = "Yes" if row['is_read'] else "No"
                    if not row['is_read']:
                        unread_count += 1

                    items = [
                        QStandardItem(str(row['id'])),
                        QStandardItem(row['message']),
                        QStandardItem(row['type'].replace('_', ' ').title()),
                        QStandardItem(row['created_at'][:16].replace('T', ' ')),
                        QStandardItem(read_status),
                        QStandardItem(row['sender']),
                        QStandardItem(row['recipient'])
                    ]

                    # رنگ قرمز + بولد برای خوانده‌نشده
                    if not row['is_read']:
                        for item in items:
                            item.setForeground(QColor("#dc2626"))
                            item.setFont(QFont("Segoe UI", 9, QFont.Weight.Bold))

                    model.appendRow(items)

                self.notif_table.setModel(model)
                self.notif_table.resizeColumnsToContents()
                self.notif_count_label.setText(f"{unread_count} Unread")

            except Exception as e:
                logger.error(f"Notifications load error: {e}")
                QMessageBox.critical(self, "Error", f"Failed to load notifications:\n{e}")

    def mark_selected_as_read(self):
        """علامت‌گذاری نوتیفیکیشن انتخاب‌شده به عنوان خوانده‌شده"""
        index = self.notif_table.currentIndex()
        if not index.isValid():
            QMessageBox.warning(self, "Warning", "Please select a notification to mark as read.")
            return

        notif_id = self.notif_table.model().index(index.row(), 0).data()
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            cur = conn.cursor()
            cur.execute("UPDATE notifications SET is_read = 1 WHERE id = ?", (notif_id,))
            conn.commit()
            conn.close()

            self.load_notifications()
            QMessageBox.information(self, "Success", "Notification marked as read.")

            # رفرش تب مدیریت (اگر باز باشد)
            if hasattr(self.app, 'management_tab') and self.app.management_tab:
                self.app.management_tab.load_notifications()

        except Exception as e:
            logger.error(f"Mark as read failed: {e}")
            QMessageBox.critical(self, "Error", f"Failed to update:\n{e}")