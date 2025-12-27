# screens/management/management_tab.py
import sqlite3
import pandas as pd
import logging
from PyQt6.QtWidgets import (
    QVBoxLayout, QTabWidget, QWidget, QLabel, QPushButton,
    QTableView, QHeaderView, QHBoxLayout, QMessageBox,
    QInputDialog, QLineEdit, QComboBox, QDateEdit, QDialog, QTextEdit, QFileDialog,QToolTip
)
from PyQt6.QtCore import Qt, QDate
from PyQt6.QtGui import QStandardItemModel, QStandardItem, QColor, QFont,QCursor
import pyqtgraph as pg
import numpy as np
import os
import re
from .statistics_tab import StatisticsTab

logger = logging.getLogger(__name__)

class ManagementTab(QWidget):
    def __init__(self, app, results_frame):
        super().__init__()
        self.app = app
        self.results_frame = results_frame
        self.db_path = app.resource_path("crm_data.db")
        self.user_id = app.user_id  # مستقیم از MainWindow
        self.statistics_widget = StatisticsTab(self.app)
        self.setup_ui()
        self.load_files_combo()
        self.load_archived_files()
        self.load_notifications()
        self.load_workflow()

    def setup_ui(self):
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(20, 20, 20, 20)
        main_layout.setSpacing(15)

        # عنوان اصلی
        title = QLabel("<h1 style='color: #1e40af;'>Laboratory Management Center</h1>")
        title.setAlignment(Qt.AlignmentFlag.AlignCenter)
        title.setStyleSheet("font-weight: bold; margin-bottom: 15px;")
        main_layout.addWidget(title)

        # تب‌ها
        self.tabs = QTabWidget()
        self.tabs.setStyleSheet("""
            QTabWidget::pane {
                border: 1px solid #d1d5db;
                border-radius: 8px;
                background-color: white;
            }
            QTabBar::tab {
                background: #f3f4f6;
                border: 1px solid #d1d5db;
                border-bottom: none;
                border-top-left-radius: 6px;
                border-top-right-radius: 6px;
                padding: 8px 16px;
                margin: 2px 2px 0 0;
                font-size: 13px;
                font-weight: bold;
                color: #4b5563;
            }
            QTabBar::tab:selected {
                background: white;
                border: 1px solid #d1d5db;
                border-bottom: 1px solid white;
                color: #1e40af;
            }
            QTabBar::tab:hover {
                background: #e5e7eb;
            }
        """)

        self.versioning_widget = self.create_versioning_tab()
        self.archive_widget = self.create_archive_tab()
        self.notifications_widget = self.create_notifications_tab()
        self.workflow_widget = self.create_workflow_tab()

        self.tabs.addTab(self.versioning_widget, "Data Versioning")
        self.tabs.addTab(self.archive_widget, "Archived Files")
        self.tabs.addTab(self.notifications_widget, "Notifications")
        self.tabs.addTab(self.workflow_widget, "File Workflow")
        self.tabs.addTab(self.statistics_widget, "statistics")
        main_layout.addWidget(self.tabs)

        # استایل کلی
        self.setStyleSheet("""
            QWidget {
                background-color: #f8fafc;
                font-family: 'Segoe UI', sans-serif;
            }
            QTableView {
                background: white;
                gridline-color: #e5e7eb;
                font-size: 13px;
                border: none;
            }
            QHeaderView::section {
                background: #f3f4f6;
                padding: 4px;
                border: 1px solid #e5e7eb;
                font-weight: bold;
                color: #4b5563;
            }
            QPushButton {
                background-color: #3b82f6;
                color: white;
                padding: 6px 12px;
                border: none;
                border-radius: 4px;
                font-size: 12px;
                font-weight: bold;
            }
            QPushButton:hover {
                background-color: #2563eb;
            }
            QPushButton#approve { background: #16a34a; }
            QPushButton#approve:hover { background: #15803d; }
            QPushButton#reject { background: #dc2626; }
            QPushButton#reject:hover { background: #b91c1c; }
            QPushButton#restore { background: #7c3aed; }
            QPushButton#restore:hover { background: #6d28d9; }
            QPushButton#archive { background: #f59e0b; }
            QPushButton#archive:hover { background: #d97706; }
            QLabel {
                color: #4b5563;
                font-size: 13px;
            }
            QComboBox, QDateEdit, QLineEdit {
                padding: 4px;
                border: 1px solid #d1d5db;
                border-radius: 4px;
                background: white;
                font-size: 13px;
            }
            QComboBox::drop-down {
                width: 20px;
            }
        """)

    # ────────────────────────────────────────
    # 1. Data Versioning & Restore
    # ────────────────────────────────────────
    def create_versioning_tab(self):
        widget = QWidget()
        layout = QVBoxLayout(widget)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(10)

        controls = QHBoxLayout()
        controls.addWidget(QLabel("File:"))
        self.file_combo = QComboBox()
        controls.addWidget(self.file_combo, 1)
        btn_load = QPushButton("Load Versions")
        btn_load.clicked.connect(self.load_file_versions)
        controls.addWidget(btn_load)
        layout.addLayout(controls)

        self.versions_table = QTableView()
        layout.addWidget(self.versions_table)

        buttons_layout = QHBoxLayout()
        restore_btn = QPushButton("Restore Selected Version")
        restore_btn.setObjectName("restore")
        restore_btn.clicked.connect(self.restore_version)
        buttons_layout.addWidget(restore_btn)

        archive_btn = QPushButton("Archive Selected File")
        archive_btn.setObjectName("archive")
        archive_btn.clicked.connect(self.archive_file)
        buttons_layout.addWidget(archive_btn)

        layout.addLayout(buttons_layout)
        return widget

    def load_files_combo(self):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            df = pd.read_sql_query("SELECT id, file_path FROM uploaded_files WHERE is_archived = 0", conn)
            conn.close()
            self.file_combo.clear()
            for _, row in df.iterrows():
                self.file_combo.addItem(row['file_path'], row['id'])
        except Exception as e:
            logger.error(f"Files combo error: {e}")

    def load_file_versions(self):
        file_id = self.file_combo.currentData()
        if not file_id:
            return
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            query = """
                SELECT mv.id, mv.version_number, mv.value, COALESCE(u.full_name, u.username),
                       mv.change_date, mv.stage, mv.reason
                FROM measurement_versions mv
                JOIN users u ON mv.changed_by = u.id
                JOIN measurements m ON mv.measurement_id = m.id
                WHERE m.file_id = ?
                ORDER BY mv.change_date DESC
            """
            df = pd.read_sql_query(query, conn, params=(file_id,))
            conn.close()

            model = QStandardItemModel()
            model.setHorizontalHeaderLabels([
                "Version ID", "Version #", "Value", "Changed By", "Date", "Stage", "Reason"
            ])
            for _, row in df.iterrows():
                items = [QStandardItem(str(v) if v is not None else '') for v in row]
                items[4].setText(row['change_date'][:16].replace('T', ' '))
                model.appendRow(items)
            self.versions_table.setModel(model)
            self.versions_table.resizeColumnsToContents()
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to load versions:\n{e}")

    def restore_version(self):
        index = self.versions_table.currentIndex()
        if not index.isValid():
            QMessageBox.warning(self, "Warning", "Please select a version to restore.")
            return
        version_id = self.versions_table.model().index(index.row(), 0).data()
        value = self.versions_table.model().index(index.row(), 2).data()
        reply = QMessageBox.question(
            self, "Restore Version",
            f"Restore value <b>{value}</b> to current data?",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
        )
        if reply != QMessageBox.StandardButton.Yes:
            return
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            cur = conn.cursor()
            cur.execute("SELECT measurement_id FROM measurement_versions WHERE id = ?", (version_id,))
            meas_id = cur.fetchone()[0]
            cur.execute("UPDATE measurements SET current_value = ? WHERE id = ?", (value, meas_id))
            cur.execute("""
                INSERT INTO changes_log (user_id, action, entity_type, entity_id, details, stage)
                VALUES (?, 'restore', 'measurement', ?, ?, 'versioning')
            """, (self.user_id, meas_id, f"Restored from version {version_id}"))

            cur.execute("SELECT file_id FROM measurements WHERE id = ?", (meas_id,))
            file_id = cur.fetchone()[0]
            cur.execute("SELECT file_path FROM uploaded_files WHERE id = ?", (file_id,))
            file_path = cur.fetchone()[0] if cur.rowcount > 0 else 'unknown'

            message = f"User {self.app.user_name or 'unknown'} restored a version on file '{file_path}'."
            cur.execute("SELECT id FROM users WHERE role = 'lab_manager'")
            for (manager_id,) in cur.fetchall():
                cur.execute("""
                    INSERT INTO notifications (user_id, message, type, related_entity_id)
                    VALUES (?, ?, ?, ?)
                """, (manager_id, message, "system", meas_id))
            conn.commit()
            conn.close()
            QMessageBox.information(self, "Success", "Data restored successfully!")
            self.app.results_frame.data_changed()
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Restore failed:\n{e}")

    def archive_file(self):
        file_id = self.file_combo.currentData()
        if not file_id:
            QMessageBox.warning(self, "Warning", "Please select a file to archive.")
            return
        reply = QMessageBox.question(
            self, "Archive File",
            "Are you sure you want to archive this file?",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
        )
        if reply != QMessageBox.StandardButton.Yes:
            return
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            cur = conn.cursor()
            cur.execute("SELECT file_path, uploaded_by FROM uploaded_files WHERE id = ?", (file_id,))
            file_path, uploaded_by = cur.fetchone()
            cur.execute("UPDATE uploaded_files SET is_archived = 1 WHERE id = ?", (file_id,))
            message = f"File '{file_path}' was archived by {self.app.user_name}."
            if uploaded_by:
                cur.execute("INSERT INTO notifications (user_id, message, type, related_entity_id) VALUES (?, ?, ?, ?)",
                            (uploaded_by, message, "system", file_id))
            cur.execute("SELECT id FROM users WHERE role = 'lab_manager'")
            for (manager_id,) in cur.fetchall():
                cur.execute("INSERT INTO notifications (user_id, message, type, related_entity_id) VALUES (?, ?, ?, ?)",
                            (manager_id, message, "system", file_id))
            conn.commit()
            conn.close()
            QMessageBox.information(self, "Success", "File archived!")
            self.load_files_combo()
            self.load_archived_files()
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Archive failed:\n{e}")

    # ────────────────────────────────────────
    # 2. Archived Files
    # ────────────────────────────────────────
    def create_archive_tab(self):
        widget = QWidget()
        layout = QVBoxLayout(widget)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(10)

        btn_refresh = QPushButton("Refresh Archived Files")
        btn_refresh.clicked.connect(self.load_archived_files)
        layout.addWidget(btn_refresh)

        self.archive_table = QTableView()
        layout.addWidget(self.archive_table)

        btn_restore = QPushButton("Restore Selected File")
        btn_restore.clicked.connect(self.restore_archived_file)
        layout.addWidget(btn_restore)

        return widget

    def load_archived_files(self):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            query = """
                SELECT uf.id, uf.file_path, uf.created_at AS upload_date, COALESCE(u.full_name, u.username) AS uploaded_by
                FROM uploaded_files uf
                LEFT JOIN users u ON uf.uploaded_by = u.id
                WHERE uf.is_archived = 1
                ORDER BY uf.created_at DESC
            """
            df = pd.read_sql_query(query, conn)
            conn.close()

            model = QStandardItemModel()
            model.setHorizontalHeaderLabels(["ID", "File Path", "Upload Date", "Uploaded By"])
            for _, row in df.iterrows():
                items = [
                    QStandardItem(str(row['id'])),
                    QStandardItem(row['file_path']),
                    QStandardItem(row['upload_date'][:10] if row['upload_date'] else ''),
                    QStandardItem(row['uploaded_by'])
                ]
                model.appendRow(items)
            self.archive_table.setModel(model)
            self.archive_table.resizeColumnsToContents()
        except Exception as e:
            logger.error(f"Archive load error: {e}")
            QMessageBox.critical(self, "Error", f"Failed to load archived files:\n{e}")

    def restore_archived_file(self):
        index = self.archive_table.currentIndex()
        if not index.isValid():
            QMessageBox.warning(self, "Warning", "Please select a file to restore.")
            return
        file_id = self.archive_table.model().index(index.row(), 0).data()
        reply = QMessageBox.question(self, "Restore", "Restore this file to active?", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
        if reply == QMessageBox.StandardButton.Yes:
            try:
                conn = sqlite3.connect(self.db_path, timeout=30)
                cur = conn.cursor()
                cur.execute("SELECT file_path, uploaded_by FROM uploaded_files WHERE id = ?", (file_id,))
                file_path, uploaded_by = cur.fetchone()
                cur.execute("UPDATE uploaded_files SET is_archived = 0 WHERE id = ?", (file_id,))
                message = f"File '{file_path}' was restored by {self.app.user_name}."
                if uploaded_by:
                    cur.execute("INSERT INTO notifications (user_id, message, type, related_entity_id) VALUES (?, ?, ?, ?)",
                                (uploaded_by, message, "system", file_id))
                cur.execute("SELECT id FROM users WHERE role = 'lab_manager'")
                for (manager_id,) in cur.fetchall():
                    cur.execute("INSERT INTO notifications (user_id, message, type, related_entity_id) VALUES (?, ?, ?, ?)",
                                (manager_id, message, "system", file_id))
                conn.commit()
                conn.close()
                QMessageBox.information(self, "Success", "File restored!")
                self.load_archived_files()
                self.load_files_combo()
            except Exception as e:
                QMessageBox.critical(self, "Error", str(e))

    # ────────────────────────────────────────
    # 3. Notifications
    # ────────────────────────────────────────
    def create_notifications_tab(self):
        widget = QWidget()
        layout = QVBoxLayout(widget)
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

        return widget

    def load_notifications(self):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            query = """
                SELECT n.id, n.message, n.type, n.created_at, n.is_read,
                    CASE
                        WHEN n.type IN ('change_approved', 'change_rejected') THEN COALESCE(u_approve.full_name, u_approve.username)
                        WHEN n.type = 'approval_needed' THEN COALESCE(u_change.full_name, u_change.username)
                        ELSE 'System'
                    END as sender,
                    COALESCE(u_to.full_name, u_to.username) as recipient
                FROM notifications n
                LEFT JOIN changes_log cl ON n.related_entity_id = cl.id
                LEFT JOIN approvals a ON cl.id = a.change_id
                LEFT JOIN users u_approve ON a.approved_by = u_approve.id
                LEFT JOIN users u_change ON cl.user_id = u_change.id
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
                    QStandardItem(row['sender'] or "System"),
                    QStandardItem(row['recipient'] or "Unknown")
                ]
                if not row['is_read']:
                    for item in items:
                        item.setForeground(QColor("#dc2626"))
                        item.setFont(QFont("Segoe UI", 9, QFont.Weight.Bold))
                model.appendRow(items)

            self.notif_table.setModel(model)
            self.notif_table.resizeColumnsToContents()
            self.tabs.setTabText(self.tabs.indexOf(self.notifications_widget), f"Notifications ({unread_count})")
        except Exception as e:
            logger.error(f"Notifications load error: {e}")
            QMessageBox.critical(self, "Error", f"Failed to load notifications:\n{e}")

    def mark_selected_as_read(self):
        index = self.notif_table.currentIndex()
        if not index.isValid():
            QMessageBox.warning(self, "Warning", "Please select a notification.")
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
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed: {e}")

    # ────────────────────────────────────────
    # 4. File Workflow
    # ────────────────────────────────────────
    def create_workflow_tab(self):
        widget = QWidget()
        layout = QVBoxLayout(widget)
        layout.setContentsMargins(10, 10, 10, 10)
        layout.setSpacing(10)

        header = QHBoxLayout()
        refresh_btn = QPushButton("Refresh")
        refresh_btn.clicked.connect(self.load_workflow)
        header.addWidget(refresh_btn)
        header.addStretch()
        layout.addLayout(header)

        self.workflow_table = QTableView()
        self.workflow_table.setSelectionBehavior(QTableView.SelectionBehavior.SelectRows)
        self.workflow_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        self.workflow_table.clicked.connect(self.on_workflow_clicked)
        layout.addWidget(self.workflow_table)

        return widget

    def load_workflow(self):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            query = """
                SELECT
                    uf.id AS file_id,
                    uf.file_path AS file_path,
                    'Uploaded' AS action,
                    COALESCE(u_upload.full_name, u_upload.username) AS user_name,
                    uf.created_at AS timestamp
                FROM uploaded_files uf
                LEFT JOIN users u_upload ON uf.uploaded_by = u_upload.id
                UNION ALL
                SELECT
                    uf.id AS file_id,
                    cl.file_path,
                    'Changed' AS action,
                    COALESCE(u_change.full_name, u_change.username) AS user_name,
                    MAX(cl.timestamp) AS timestamp
                FROM changes_log cl
                LEFT JOIN users u_change ON cl.user_id = u_change.id
                LEFT JOIN uploaded_files uf ON cl.file_path = uf.file_path
                WHERE cl.action IN ('correction', 'update', 'restore')
                GROUP BY cl.file_path
                ORDER BY timestamp DESC
            """
            df = pd.read_sql_query(query, conn)
            conn.close()

            model = QStandardItemModel()
            model.setHorizontalHeaderLabels(["Event", "Time"])
            for _, row in df.iterrows():
                action_text = row['action']
                if action_text == 'Uploaded':
                    message = f"File {os.path.basename(row['file_path'])} uploaded by {row['user_name']}"
                else:
                    message = f"File {os.path.basename(row['file_path'])} changed by {row['user_name']}"
                item_event = QStandardItem(message)
                item_event.setData(row['file_path'], Qt.ItemDataRole.UserRole + 1)
                item_event.setData(action_text, Qt.ItemDataRole.UserRole + 2)
                item_event.setData(row['file_id'], Qt.ItemDataRole.UserRole + 3)
                items = [
                    item_event,
                    QStandardItem(row['timestamp'][:16].replace('T', ' '))
                ]
                model.appendRow(items)
            self.workflow_table.setModel(model)
            self.workflow_table.resizeColumnsToContents()
        except Exception as e:
            logger.error(f"Workflow load error: {e}")
            QMessageBox.critical(self, "Error", f"Failed to load workflow:\n{e}")

    def on_workflow_clicked(self, index):
        row = index.row()
        model = self.workflow_table.model()
        file_path = model.index(row, 0).data(Qt.ItemDataRole.UserRole + 1)
        action = model.index(row, 0).data(Qt.ItemDataRole.UserRole + 2)
        if action == 'Uploaded':
            self.show_upload_details(file_path)
        elif action == 'Changed':
            self.show_change_details(file_path)

    def show_upload_details(self, file_path):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            cur = conn.cursor()
            cur.execute("SELECT contracts FROM uploaded_files WHERE file_path = ?", (file_path,))
            result = cur.fetchone()
            contracts = result[0] if result else "No contracts found"
            conn.close()
            dialog = QDialog(self)
            dialog.setWindowTitle("Upload Details")
            layout = QVBoxLayout()
            label = QLabel("Contracts:")
            layout.addWidget(label)
            text_edit = QTextEdit(contracts)
            text_edit.setReadOnly(True)
            layout.addWidget(text_edit)
            dialog.setLayout(layout)
            dialog.resize(400, 300)
            dialog.exec()
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to load upload details: {e}")

    def show_change_details(self, file_path):
        dialog = QDialog(self)
        dialog.setWindowTitle("Pending Approvals for " + os.path.basename(file_path))
        layout = QVBoxLayout(dialog)
        layout.setContentsMargins(15, 15, 15, 15)
        layout.setSpacing(12)

        header_layout = QHBoxLayout()
        header_layout.addWidget(QLabel("<b>File:</b>"))
        header_layout.addWidget(QLabel(file_path), 1)
        element_combo = QComboBox()
        element_combo.setMinimumWidth(200)
        element_combo.addItem("All Elements")
        header_layout.addWidget(QLabel("Element:"))
        header_layout.addWidget(element_combo)

        approve_btn = QPushButton("Approve All")
        approve_btn.setObjectName("approve")
        approve_btn.setFixedHeight(32)
        approve_btn.clicked.connect(lambda: self.approve_element(file_path, None))
        header_layout.addWidget(approve_btn)

        reject_btn = QPushButton("Reject All")
        reject_btn.setObjectName("reject")
        reject_btn.setFixedHeight(32)
        reject_btn.clicked.connect(lambda: self.reject_element(file_path, None))
        header_layout.addWidget(reject_btn)
        header_layout.addStretch()
        layout.addLayout(header_layout)

        plot_buttons_layout = QHBoxLayout()
        plot_old_new_btn = QPushButton("Plot Old vs New")
        plot_old_new_btn.setFixedHeight(32)
        plot_old_new_btn.clicked.connect(lambda: self.plot_old_new(file_path, element_combo.currentText() if element_combo.currentText() != "All Elements" else None))
        plot_buttons_layout.addWidget(plot_old_new_btn)

        plot_calib_btn = QPushButton("Plot Calibration")
        plot_calib_btn.setFixedHeight(32)
        plot_calib_btn.clicked.connect(lambda: self.plot_calib(file_path, element_combo.currentText() if element_combo.currentText() != "All Elements" else None))
        plot_buttons_layout.addWidget(plot_calib_btn)

        plot_drift_btn = QPushButton("Plot Drift")
        plot_drift_btn.setFixedHeight(32)
        plot_drift_btn.clicked.connect(lambda: self.plot_drift(file_path, element_combo, approvals_table))
        plot_buttons_layout.addWidget(plot_drift_btn)

        plot_buttons_layout.addStretch()
        layout.addLayout(plot_buttons_layout)

        approvals_label = QLabel("Pending Changes")
        approvals_label.setStyleSheet("font-weight: bold; font-size: 14px; margin-top: 8px;")
        layout.addWidget(approvals_label)

        approvals_table = QTableView()
        approvals_table.setSelectionBehavior(QTableView.SelectionBehavior.SelectRows)
        approvals_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        layout.addWidget(approvals_table)

        dialog.resize(1000, 650)

        def refresh_all():
            self.load_elements_for_file(file_path, element_combo, approvals_table)
        element_combo.currentTextChanged.connect(lambda elem: self.load_pending_approvals_for_file(file_path, approvals_table, elem if elem != "All Elements" else None))
        refresh_all()
        dialog.exec()

    def load_elements_for_file(self, file_path, combo, table):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            query = """
                SELECT DISTINCT column_name FROM changes_log cl
                LEFT JOIN approvals a ON cl.id = a.change_id
                WHERE a.id IS NULL
                AND cl.action IN ('update', 'correction')
                AND cl.file_path = ?
                ORDER BY column_name
            """
            df = pd.read_sql_query(query, conn, params=(file_path,))
            conn.close()
            current = combo.currentText()
            combo.blockSignals(True)
            combo.clear()
            combo.addItem("All Elements")
            for elem in df['column_name']:
                if elem:
                    combo.addItem(elem)
            combo.blockSignals(False)
            if current in [combo.itemText(i) for i in range(combo.count())]:
                combo.setCurrentText(current)
            else:
                combo.setCurrentIndex(0)
            self.load_pending_approvals_for_file(file_path, table, combo.currentText() if combo.currentText() != "All Elements" else None)
        except Exception as e:
            logger.error(f"Load elements error: {e}")

    def load_pending_approvals_for_file(self, file_path, table, element=None):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            query = """
                SELECT
                    cl.pivot_index,
                    cl.timestamp,
                    COALESCE(u.full_name, u.username) as user_name,
                    cl.solution_label,
                    cl.original_value,
                    cl.new_value,
                    cl.details
                FROM changes_log cl
                LEFT JOIN users u ON cl.user_id = u.id
                LEFT JOIN approvals a ON cl.id = a.change_id
                WHERE a.id IS NULL
                AND cl.action IN ('update', 'correction')
                AND cl.file_path = ?
            """
            params = (file_path,)
            if element:
                query += " AND cl.column_name = ?"
                params += (element,)
            query += " ORDER BY CAST(cl.pivot_index AS INTEGER) ASC"
            df = pd.read_sql_query(query, conn, params=params)
            conn.close()

            model = QStandardItemModel()
            headers = ["ID", "Time", "User", "Sample", "Old", "New", "Details"]
            model.setHorizontalHeaderLabels(headers)
            for _, row in df.iterrows():
                items = [
                    QStandardItem(str(row['pivot_index'])),
                    QStandardItem(row['timestamp'][:16].replace('T', ' ')),
                    QStandardItem(row['user_name']),
                    QStandardItem(row['solution_label']),
                    QStandardItem(str(row['original_value'])),
                    QStandardItem(str(row['new_value'])),
                    QStandardItem((row['details'] or '')[:50] + ("..." if len(row['details'] or '') > 50 else ""))
                ]
                model.appendRow(items)
            table.setModel(model)
            table.resizeColumnsToContents()
        except Exception as e:
            logger.error(f"Approvals load error: {e}")
            QMessageBox.critical(self, "Error", f"Failed to load changes:\n{e}")

    def approve_element(self, file_path, element):
        reply = QMessageBox.question(self, "Approve All", "Approve ALL pending changes for this file?", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
        if reply != QMessageBox.StandardButton.Yes:
            return
        self._handle_element_approval(file_path, element, "approved", "Approved by lab manager")

    def reject_element(self, file_path, element):
        reply = QMessageBox.question(self, "Reject All", "Reject ALL pending changes for this file?", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
        if reply != QMessageBox.StandardButton.Yes:
            return
        reason, ok = QInputDialog.getText(self, "Reject Reason", f"Why reject all changes?", QLineEdit.EchoMode.Normal)
        if not ok:
            return
        self._handle_element_approval(file_path, element, "rejected", reason)

    def _handle_element_approval(self, file_path, element, status, comment):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            cur = conn.cursor()

            filename = os.path.basename(file_path)
            cur.execute("""
                SELECT id FROM uploaded_files 
                WHERE original_filename = ? OR file_path LIKE ? OR file_path LIKE ?
            """, (filename, f"%{filename}", f"%/{filename}"))
            result = cur.fetchone()
            if not result:
                QMessageBox.critical(self, "Error", f"File not found: {filename}")
                conn.close()
                return
            file_id = result[0]

            query = """
                SELECT cl.id, cl.user_id, cl.solution_label
                FROM changes_log cl
                LEFT JOIN approvals a ON cl.id = a.change_id
                WHERE a.id IS NULL
                AND cl.action IN ('update', 'correction')
                AND cl.file_path = ?
            """
            params = (file_path,)
            if element:
                query += " AND cl.column_name = ?"
                params += (element,)

            cur.execute(query, params)
            changes = cur.fetchall()

            if not changes:
                QMessageBox.information(self, "Info", "No pending changes found.")
                conn.close()
                return

            for change_id, user_id, solution_label in changes:
                cur.execute("""
                    INSERT INTO approvals (change_id, approved_by, status, comments)
                    VALUES (?, ?, ?, ?)
                """, (change_id, self.user_id, status, comment))

            user_ids = {user_id for _, user_id, _ in changes}
            for user_id in user_ids:
                message = f"Your changes on file '{filename}' were {status}."
                if status == "rejected" and comment:
                    message += f" Reason: {comment}"
                cur.execute("""
                    INSERT INTO notifications (user_id, message, type, related_entity_id)
                    VALUES (?, ?, ?, ?)
                """, (user_id, message, f"change_{status}", file_id))

            conn.commit()
            conn.close()
            QMessageBox.information(self, "Success", f"{status.capitalize()} applied to {len(changes)} change(s).")
            self.load_workflow()
            self.load_notifications()

        except Exception as e:
            if 'conn' in locals():
                conn.close()
            QMessageBox.critical(self, "Error", f"Operation failed:\n{e}")

    def plot_old_new(self, file_path, element=None):
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            query = """
                SELECT cl.solution_label, cl.original_value, cl.new_value
                FROM changes_log cl
                LEFT JOIN approvals a ON cl.id = a.change_id
                WHERE a.id IS NULL AND cl.action IN ('update', 'correction') AND cl.file_path = ?
            """
            params = (file_path,)
            if element:
                query += " AND cl.column_name = ?"
                params += (element,)
            df = pd.read_sql_query(query, conn, params=params)
            conn.close()
            df['original_value'] = pd.to_numeric(df['original_value'], errors='coerce')
            df['new_value'] = pd.to_numeric(df['new_value'], errors='coerce')
            df = df.dropna()
            if df.empty:
                QMessageBox.information(self, "Info", "No valid data to plot.")
                return

            plot_dialog = QDialog(self)
            plot_dialog.setWindowTitle('Old vs New' + (f' for {element}' if element else ''))
            plot_layout = QVBoxLayout(plot_dialog)
            plot_widget = pg.PlotWidget()
            plot_widget.setBackground('w')
            x = np.arange(len(df))
            plot_widget.addLegend(offset=(10, 10))
            plot_widget.plot(x, df['original_value'], pen=None, symbol='o', symbolBrush='b', name='Old')
            plot_widget.plot(x, df['new_value'], pen=None, symbol='o', symbolBrush='r', name='New')
            plot_widget.setLabel('bottom', 'Sample Index')
            plot_widget.setLabel('left', 'Value')
            plot_widget.showGrid(x=True, y=True)
            plot_layout.addWidget(plot_widget)
            plot_dialog.resize(600, 400)
            plot_dialog.exec()
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to plot: {e}")

    def plot_calib(self, file_path, element=None):
        print("omid ", element)
        try:
            conn = sqlite3.connect(self.db_path, timeout=30)
            cur = conn.cursor()

            # --- پیدا کردن file_id ---
            cur.execute("SELECT id FROM uploaded_files WHERE file_path = ?", (file_path,))
            result = cur.fetchone()
            if not result:
                basename = os.path.basename(file_path)
                cur.execute("SELECT id FROM uploaded_files WHERE original_filename = ? OR clean_filename = ?", (basename, basename))
                result = cur.fetchone()
            file_id = result[0] if result else None
            if not file_id:
                QMessageBox.warning(self, "Warning", f"File ID not found:\n{file_path}")
                conn.close()
                return

            # --- تعیین عنصر ---
            base_element = element.split()[0] if element and ' ' in element else element
            if not base_element:
                cur.execute("SELECT DISTINCT column_name FROM changes_log WHERE file_path = ? LIMIT 1", (file_path,))
                col = cur.fetchone()
                base_element = col[0].split()[0] if col and col[0] else "Unknown"

            crm_column = f"{base_element}"

            # --- دریافت CRM انتخاب‌شده ---
            query_crm = "SELECT solution_label, selected_crm_key FROM crm_selections WHERE file_id = ?"
            crm_df = pd.read_sql_query(query_crm, conn, params=(file_id,))
            if crm_df.empty:
                QMessageBox.information(self, "Info", "No CRM selected.")
                conn.close()
                return

            # --- دریافت مقادیر اندازه‌گیری شده ---
            query_measured = """
                SELECT m.sample_id AS solution_label, m.current_value AS measured
                FROM measurements m WHERE m.file_id = ? AND m.element = ?
            """
            measured_df = pd.read_sql_query(query_measured, conn, params=(file_id, element or ''))
            if measured_df.empty:
                QMessageBox.information(self, "Info", f"No data for '{element or 'element'}'.")
                conn.close()
                return

            measured_df['measured'] = pd.to_numeric(measured_df['measured'], errors='coerce')
            measured_df = measured_df.dropna()

            # --- ادغام ---
            data = pd.merge(measured_df, crm_df, on='solution_label', how='inner')
            if data.empty:
                QMessageBox.information(self, "Info", "No overlapping samples with CRM.")
                conn.close()
                return

            # --- استخراج CRM ID و روش ---
            data['crm_id'] = ''
            data['method'] = ''
            data['is_verification'] = False

            for idx, row in data.iterrows():
                key = row['selected_crm_key']
                if '(' not in key:
                    continue
                try:
                    crm_id, method = key.split(' (', 1)
                    method = method.rstrip(')')
                    data.at[idx, 'crm_id'] = crm_id.strip()
                    data.at[idx, 'method'] = method.strip()
                    data.at[idx, 'is_verification'] = any(x in method.upper() for x in ['B', 'R', 'Y', 'VER', 'CHK', 'VERIFICATION'])
                except:
                    continue

            # --- دریافت مقادیر گواهی‌شده ---
            certified_values = {}
            for crm_id in data['crm_id'].unique():
                if not crm_id:
                    continue
                main_row = data[(data['crm_id'] == crm_id) & (~data['is_verification'])]
                if main_row.empty:
                    continue
                main_method = main_row.iloc[0]['method']
                query = f'SELECT "{crm_column}" FROM pivot_crm WHERE [CRM ID] = ? AND [Analysis Method] = ?'
                cert_df = pd.read_sql_query(query, conn, params=(crm_id, main_method))
                if cert_df.empty or pd.isna(cert_df.iloc[0, 0]):
                    continue
                certified_values[crm_id] = float(cert_df.iloc[0, 0])

            if not certified_values:
                QMessageBox.information(self, "Info", f"No certified value for '{base_element}'.")
                conn.close()
                return

            # --- آماده‌سازی داده ---
            plot_data = []
            colors = ['#1f77b4', '#ff7f0e', '#2ca02c', '#d62728', '#9467bd', '#8c564b', '#e377c2']
            crm_color_map = {}
            color_idx = 0

            crm_ids_sorted = sorted(certified_values.keys(), key=lambda x: str(x))
            crm_to_index = {crm_id: i for i, crm_id in enumerate(crm_ids_sorted)}

            for crm_id in crm_ids_sorted:
                if crm_id not in certified_values:
                    continue
                color = colors[color_idx % len(colors)]
                crm_color_map[crm_id] = color
                color_idx += 1

                subset = data[data['crm_id'] == crm_id]
                cert_val = certified_values[crm_id]
                x_index = crm_to_index[crm_id]

                for _, row in subset.iterrows():
                    y_val = row['measured']
                    symbol = 's' if row['is_verification'] else 'o'
                    brush = pg.mkBrush(color) if not row['is_verification'] else pg.mkBrush(150, 150, 150, 200)
                    label = f"{crm_id} ({row['method']})"
                    plot_data.append({
                        'crm_id': crm_id,
                        'x': x_index,
                        'y': y_val,
                        'symbol': symbol,
                        'brush': brush,
                        'label': label,
                        'cert_val': cert_val,
                        'color': color
                    })

            if not plot_data:
                QMessageBox.information(self, "Info", "No valid data to plot.")
                conn.close()
                return

            df_plot = pd.DataFrame(plot_data)

            # --- رسم ---
            plot_dialog = QDialog(self)
            plot_dialog.setWindowTitle(f'Calibration: {element or base_element}')
            layout = QVBoxLayout(plot_dialog)
            plot_widget = pg.PlotWidget()
            plot_widget.setBackground('w')
            layout.addWidget(plot_widget)

            legend = plot_widget.addLegend(offset=(10, 10))
            seen_labels = set()

            # رنگ و شکل برای Certified Value
            cert_color = '#FF0000'
            cert_brush = pg.mkBrush(cert_color)
            cert_symbol = 'x'

            # tooltip dictionary
            tooltips = {}

            # رسم نقاط و رنج مجاز
            for crm_id in crm_ids_sorted:
                sub = df_plot[df_plot['crm_id'] == crm_id]
                cert_val = sub['cert_val'].iloc[0]
                x_index = sub['x'].iloc[0]

                # --- نقاط اصلی (Main) ---
                main = sub[~sub['label'].str.contains(r'\b(B|R|Y|VER|CHK|VERIFICATION)\b', regex=True, case=False)]
                main_labels = main['label'].tolist()
                if not main.empty:
                    label_main = f"{crm_id} (Main)"
                    if label_main not in seen_labels:
                        scatter_main = pg.ScatterPlotItem(
                            x=[x_index] * len(main), y=main['y'].tolist(),
                            symbol='o', size=12, brush=pg.mkBrush(crm_color_map[crm_id]),
                            name=label_main, hoverable=True
                        )
                        plot_widget.addItem(scatter_main)
                        seen_labels.add(label_main)

                        for i, lbl in enumerate(main_labels):
                            tooltips[id(scatter_main) + i] = lbl

                # --- نقاط Verification ---
                verif = sub[sub['label'].str.contains(r'\b(B|R|Y|VER|CHK|VERIFICATION)\b', regex=True, case=False)]
                verif_labels = verif['label'].tolist()
                if not verif.empty:
                    label_verif = f"{crm_id} (Verification)"
                    if label_verif not in seen_labels:
                        scatter_verif = pg.ScatterPlotItem(
                            x=[x_index] * len(verif), y=verif['y'].tolist(),
                            symbol='s', size=10, brush=pg.mkBrush(150,150,150,200),
                            name=label_verif, hoverable=True
                        )
                        plot_widget.addItem(scatter_verif)
                        seen_labels.add(label_verif)

                        for i, lbl in enumerate(verif_labels):
                            tooltips[id(scatter_verif) + i] = lbl

                # --- نقطه گواهی‌شده (Certified) ---
                cert_label = f"{crm_id} (Certified)"
                cert_scatter = pg.ScatterPlotItem(
                    x=[x_index], y=[cert_val],
                    symbol=cert_symbol, size=14, brush=cert_brush,
                    pen=pg.mkPen('k', width=1.5), name="Certified Value" if cert_label not in seen_labels else None,
                    hoverable=True
                )
                plot_widget.addItem(cert_scatter)
                if "Certified Value" not in seen_labels:
                    seen_labels.add("Certified Value")
                tooltips[id(cert_scatter) + 0] = cert_label

                # --- خطوط رنج مجاز (±10%) ---
                tolerance = 0.10
                lower = cert_val * (1 - tolerance)
                upper = cert_val * (1 + tolerance)

                # خط کوتاه افقی در بالا و پایین
                line_length = 0.3  # طول خط (در واحد X)
                x_left = x_index - line_length / 2
                x_right = x_index + line_length / 2

                # خط پایین
                lower_line = pg.PlotCurveItem(
                    [x_left, x_right], [lower, lower],
                    pen=pg.mkPen(cert_color, width=2, style=Qt.PenStyle.DashLine)
                )
                plot_widget.addItem(lower_line)

                # خط بالا
                upper_line = pg.PlotCurveItem(
                    [x_left, x_right], [upper, upper],
                    pen=pg.mkPen(cert_color, width=2, style=Qt.PenStyle.DashLine)
                )
                plot_widget.addItem(upper_line)

                # خط عمودی کوچک برای اتصال (اختیاری)
                # plot_widget.addItem(pg.PlotCurveItem([x_index, x_index], [lower, upper], pen=pg.mkPen(cert_color, width=1)))

            # --- Tooltip function ---
            def show_tooltip(item, points):
                for pt in points:
                    index = pt.index()
                    key = id(item) + index
                    if key in tooltips:
                        QToolTip.showText(QCursor.pos(), tooltips[key])

            for item in plot_widget.plotItem.items:
                if isinstance(item, pg.ScatterPlotItem):
                    item.sigHovered.connect(lambda item=item, points=None: show_tooltip(item, points))

            # --- محور X ---
            plot_widget.getAxis('bottom').setTicks([
                [(i, crm_id) for i, crm_id in enumerate(crm_ids_sorted)]
            ])

            # --- محدوده Y ---
            all_y = df_plot['y'].tolist() + list(certified_values.values())
            if all_y:
                y_min, y_max = min(all_y), max(all_y)
                y_margin = (y_max - y_min) * 0.15
                plot_widget.setYRange(y_min - y_margin, y_max + y_margin)

            plot_widget.setLabel('bottom', 'CRM ID')
            plot_widget.setLabel('left', 'Measured Value')
            plot_widget.setTitle(f'{base_element} - Calibration Plot')
            plot_widget.showGrid(x=True, y=True)

            # --- دکمه ذخیره ---
            save_btn = QPushButton("Save as PNG")
            save_btn.clicked.connect(lambda: self.save_plot(
                plot_widget,
                f"calib_{base_element}_{os.path.splitext(os.path.basename(file_path))[0]}.png"
            ))
            layout.addWidget(save_btn)

            plot_dialog.resize(1000, 650)
            plot_dialog.exec()
            conn.close()

        except Exception as e:
            QMessageBox.critical(self, "Error", f"Plot failed:\n{str(e)}")
            if 'conn' in locals():
                conn.close()

    def plot_drift(self, file_path, element_combo, approvals_table):
        element = element_combo.currentText()
        if element == "All Elements" or not element:
            QMessageBox.warning(self, "Warning", "Please select a specific element to plot drift.")
            return

        try:
            # --- دریافت داده‌ها از جدول approvals_table ---
            model = approvals_table.model()
            if not model or model.rowCount() == 0:
                QMessageBox.information(self, "Info", "No data available in the table.")
                return

            # استخراج داده‌ها از جدول
            data = []
            for row in range(model.rowCount()):
                solution_label = model.index(row, 3).data()  # Sample
                old_val = model.index(row, 4).data()  # Old
                new_val = model.index(row, 5).data()  # New
                pivot_idx = model.index(row, 0).data()  # ID (pivot_index)

                try:
                    pivot_idx = int(pivot_idx)
                except:
                    continue

                try:
                    old_val = float(old_val) if old_val not in (None, '', 'None') else None
                    new_val = float(new_val) if new_val not in (None, '', 'None') else None
                except:
                    old_val = new_val = None

                data.append({
                    'solution_label': solution_label or '',
                    'pivot_index': pivot_idx,
                    'old_value': old_val,
                    'new_value': new_val
                })

            if not data:
                QMessageBox.information(self, "Info", "No valid numeric data in the table.")
                return

            df = pd.DataFrame(data)

            # --- استخراج RM با regex دقیق ---
            # فقط: RM, RM1, RM 1, RM1 check, RM check
            # CRM, CRM1, CRM 1 و غیره نباید مطابقت داشته باشند
            def extract_rm(label):
                if not label:
                    return None
                # الگو: RM یا RM1 یا RM 1 یا RM check یا RM1 check
                # \b برای مرز کلمه، و \d* برای عدد اختیاری
                match = re.search(r'\bRM\s*(\d*)\s*(check)?\b', str(label), re.IGNORECASE)
                if match:
                    num = match.group(1) or ''
                    check = ' check' if match.group(2) else ''
                    return f'RM{num}{check}'.strip()
                return None

            df['rm_id'] = df['solution_label'].apply(extract_rm)
            df_rm = df[df['rm_id'].notnull()].copy()

            if df_rm.empty:
                QMessageBox.information(self, "Info", "No RM samples found in the current data.")
                return

            # --- تعیین مقدار old و new ---
            df_rm['old_y'] = df_rm['old_value']
            df_rm['new_y'] = df_rm['new_value'].fillna(df_rm['old_value'])

            # حذف مواردی که هر دو None هستند
            df_rm = df_rm.dropna(subset=['old_y', 'new_y'], how='all')

            if df_rm.empty:
                QMessageBox.information(self, "Info", "No valid values to plot.")
                return

            # --- رسم ---
            plot_dialog = QDialog(self)
            plot_dialog.setWindowTitle(f'Drift Plot - {element}')
            layout = QVBoxLayout(plot_dialog)
            plot_widget = pg.PlotWidget()
            plot_widget.setBackground('w')
            layout.addWidget(plot_widget)

            plot_widget.addLegend(offset=(10, 10))
            plot_widget.setLabel('bottom', 'Position in File (Pivot Index)')
            plot_widget.setLabel('left', 'Value')
            plot_widget.setTitle(f'{element} - Drift (Old vs New)')
            plot_widget.showGrid(x=True, y=True)

            colors = ['#1f77b4', '#ff7f0e', '#2ca02c', '#d62728', '#9467bd', '#8c564b', '#e377c2']
            rm_ids = sorted(df_rm['rm_id'].unique(), key=lambda x: (''.join(filter(str.isdigit, x)), x))
            color_map = {rm_id: colors[i % len(colors)] for i, rm_id in enumerate(rm_ids)}

            for rm_id in rm_ids:
                sub = df_rm[df_rm['rm_id'] == rm_id].sort_values('pivot_index')
                x = sub['pivot_index'].values

                # فقط اگر old_y وجود داشته باشد
                if sub['old_y'].notna().any():
                    old_y = sub['old_y'].values
                    plot_widget.plot(
                        x, old_y,
                        pen=pg.mkPen(color_map[rm_id], width=2, style=Qt.PenStyle.DashLine),
                        symbol='o', symbolBrush=color_map[rm_id], symbolSize=8,
                        name=f'{rm_id} (Old)'
                    )

                # new_y همیشه باید وجود داشته باشد
                new_y = sub['new_y'].values
                plot_widget.plot(
                    x, new_y,
                    pen=pg.mkPen(color_map[rm_id], width=2),
                    symbol='o', symbolBrush=color_map[rm_id], symbolSize=8,
                    name=f'{rm_id} (New)'
                )

            # تنظیم محدوده Y
            all_y = pd.concat([df_rm['old_y'], df_rm['new_y']]).dropna()
            if not all_y.empty:
                y_min, y_max = all_y.min(), all_y.max()
                margin = (y_max - y_min) * 0.1
                plot_widget.setYRange(y_min - margin, y_max + margin)

            # دکمه ذخیره
            save_btn = QPushButton("Save as PNG")
            save_btn.clicked.connect(lambda: self.save_plot(
                plot_widget,
                f"drift_{element}_{os.path.splitext(os.path.basename(file_path))[0]}.png"
            ))
            layout.addWidget(save_btn)

            plot_dialog.resize(1000, 650)
            plot_dialog.exec()

        except Exception as e:
            QMessageBox.critical(self, "Error", f"Plot failed:\n{str(e)}")

    def save_plot(self, plot_widget, default_name):
        path, _ = QFileDialog.getSaveFileName(self, "Save Plot", default_name, "PNG Files (*.png);;PDF Files (*.pdf)")
        if path:
            exporter = pg.exporters.ImageExporter(plot_widget.plotItem)
            exporter.export(path)
            QMessageBox.information(self, "Saved", f"Plot saved:\n{path}")

# --- کلاس محور رشته‌ای ---
class StringAxis(pg.AxisItem):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)

    def tickStrings(self, values, scale, spacing):
        return [str(v) for v in values]