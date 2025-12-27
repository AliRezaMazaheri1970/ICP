# main_window.py
import sys
import os
import logging
import threading
import time
import sqlite3
import pandas as pd
from datetime import datetime

from PyQt6.QtWidgets import (
    QApplication, QMainWindow, QLabel, QTabWidget, QMessageBox,
    QProgressDialog, QSystemTrayIcon, QMenu
)
from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtGui import QIcon

from screens.Common.tab import MainTabContent
from screens.calibration_tab import ElementsTab
from screens.pivot.pivot_tab import PivotTab
from screens.CRM import CRMTab
from utils.load_file import load_additional
from screens.process.result import ResultsFrame
from screens.process.verification.drift_frame import CheckRMFrame
from screens.process.weight_check import WeightCheckFrame
from screens.process.volume_check import VolumeCheckFrame
from screens.process.DF_check import DFCheckFrame
from screens.process.empty_check import EmptyCheckFrame
from screens.process.CRM_check import CrmCheck
from screens.process.verification.master_verification import MasterVerificationWindow
from screens.process.report import ReportTab
from screens.compare_tab import CompareTab
from screens.file.file_tab import FileTab
from utils.project_manager import save_project, load_project
from screens.login_window import LoginWindow
from screens.notification_tab import NotificationTab

# Import QC tabs directly
from screens.qc_tab.qc import QCTab
from screens.qc_tab.min_max_tab import MinMaxTab

logger = logging.getLogger(__name__)
logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')


class MainWindow(QMainWindow):
    open_windows = []

    def __init__(self, username="guest", user_name="Guest", user_position="Guest", user_id="0"):
        super().__init__()
        self.username = username
        self.user_name = user_name
        self.user_position = user_position
        self.user_id = int(user_id) if user_id.isdigit() else 0
        self.init_data = None
        self.data = None
        self.file_path = None
        self.file_path_label = QLabel("File Path: No file selected")
        self.file_ranges = []

        # Database setup
        from db.db_initializer import init_db_schema
        init_db_schema(self.resource_path)

        # Get user role
        self.user_role = self.get_user_role()
        logger.debug(f"User Role: {self.user_role} (ID: {self.user_id})")

        # --- Essential methods ---
        self.get_data = self._get_data
        self.set_data = self._set_data
        self.user_id_from_username = self._user_id_from_username
        self.get_excluded_samples = self._get_excluded_samples
        self.get_excluded_volumes = self._get_excluded_volumes
        self.get_excluded_dfs = self._get_excluded_dfs

        # --- Tabs (define only, no heavy object creation) ---
        self.pivot_tab = PivotTab(self, self)
        self.elements_tab = ElementsTab(self, self)
        self.crm_tab = CRMTab(self, self)
        self.results = ResultsFrame(self, self)
        self.rm_check = CheckRMFrame(self, self)
        self.weight_check = WeightCheckFrame(self, self)
        self.volume_check = VolumeCheckFrame(self, self)
        self.df_check = DFCheckFrame(self, self)
        self.compare_tab = CompareTab(self, self)
        self.empty_check = EmptyCheckFrame(self, self)
        self.crm_check = CrmCheck(self, self.results)
        self.report = ReportTab(self, self.results)
        self.file_tab = FileTab(self)
        self.master_verification = MasterVerificationWindow(self,self)

        # QC Tab — create directly
        self.qc_tab = None
        self.min_max_tab = None
        if self.user_role in ['qc', 'admin']:
            self.qc_tab = QCTab(self)
            self.min_max_tab = MinMaxTab(self)

        # --- Connections ---
        self.weight_check.data_changed.connect(self.results.data_changed)
        self.volume_check.data_changed.connect(self.results.data_changed)
        self.df_check.data_changed.connect(self.results.data_changed)
        self.rm_check.data_changed.connect(self.results.data_changed)
        self.empty_check.empty_rows_found.connect(self.rm_check.on_empty_rows_received)
        self.rm_check.results_update_requested.connect(self.results.update_table)
        # --- Tab definitions ---
        tab_info = {
            "File": {
                "Save Project": self.save_project,
                "Load Project": self.load_project,
                "New": self.new_window,
                "Close": self.close_window,
                "Logout": self.logout
            },
            "Find similarity": {"display": self.compare_tab},
            "Process": {
                "Weight Check": self.weight_check,
                "Volume Check": self.volume_check,
                "DF check": self.df_check,
                "Empty check": self.empty_check,
                "CRM Calibraton": self.crm_check,
                "Drift Calibraton": self.rm_check,
                "Calibraton Pro": self.master_verification,
                "Result": self.results,
                "Report": self.report
            },
            "Elements": {"Display": self.elements_tab},
            "Raw Data": {"Display": self.pivot_tab},
            "CRM": {"CRM": self.crm_tab}
        }

        # File access based on role
        if self.user_role in ['report_manager', 'lab_manager', 'admin', 'qc']:
            tab_info["File"]["Open"] = self.file_tab.open_existing_file
        else:
            tab_info["File"]["Upload File"] = self.file_tab.show_upload_dialog

        # Management tab
        if self.user_role in ['lab_manager', 'admin']:
            from screens.management.management_tab import ManagementTab
            self.management_tab = ManagementTab(self, self.results)
            tab_info["Management"] = {"display": self.management_tab}

        # QC Tab — only for authorized users
        if self.user_role in ['qc', 'admin']:
            tab_info["QC"] = {
                "Display": self.qc_tab,
                "Min Max": self.min_max_tab
            }

        # Notification tab
        if self.user_role in ['device_operator', 'report_manager']:
            self.notification_tab = NotificationTab(self)
            tab_info["Notifications"] = {"display": self.notification_tab}

        # --- Create main tab ---
        self.main_content = MainTabContent(tab_info)
        self.setCentralWidget(self.main_content)
        self.resize(1200, 750)

        # Apply role restrictions
        self.apply_role_restrictions()

        # System tray and notifications
        self.setup_system_tray()
        self.start_notification_checker()

        self.auto_save_timer = QTimer(self)
        self.auto_save_timer.timeout.connect(self.auto_save_project)
        self.auto_save_timer.start(2 * 60 * 1000)  # 2 دقیقه = 120000 میلی‌ثانیه
        logger.info("Auto-save timer started (every 2 minutes)")

        self.setWindowTitle(f"RASF Data Processor - {self.user_name} ({self.username})")
        MainWindow.open_windows.append(self)

    # ────────────────────────────────────────
    # Data methods
    # ────────────────────────────────────────
    def _set_data(self, df, for_results=False):
        if not isinstance(df, pd.DataFrame):
            return
        self.data = df.copy(deep=True)
        if for_results:
            self.notify_data_changed()

    def _get_data(self):
        return self.data

    def notify_data_changed(self):
        for i in range(self.main_content.tabs.count()):
            tab = self.main_content.tabs.widget(i)
            if isinstance(tab, QTabWidget):
                for j in range(tab.count()):
                    sub = tab.widget(j)
                    if hasattr(sub, 'data_changed'):
                        sub.data_changed()

    # ────────────────────────────────────────
    # Exclude methods
    # ────────────────────────────────────────
    def _get_excluded_samples(self):
        return []

    def _get_excluded_volumes(self):
        return []

    def _get_excluded_dfs(self):
        return []

    # ────────────────────────────────────────
    # Get user_id from username
    # ────────────────────────────────────────
    def _user_id_from_username(self):
        try:
            conn = sqlite3.connect(self.resource_path("crm_data.db"))
            cur = conn.cursor()
            cur.execute("SELECT id FROM users WHERE username = ? AND is_active = 1", (self.username,))
            uid = cur.fetchone()
            conn.close()
            return uid[0] if uid else None
        except Exception as e:
            logger.error(f"Failed to get user_id: {e}")
            return None

    # ────────────────────────────────────────
    # User role
    # ────────────────────────────────────────
    def get_user_role(self):
        try:
            conn = sqlite3.connect(self.resource_path("crm_data.db"))
            cur = conn.cursor()
            cur.execute("SELECT role FROM users WHERE username = ? AND is_active = 1", (self.username,))
            row = cur.fetchone()
            conn.close()
            if row and row[0]:
                return row[0].lower()
            return "viewer"
        except Exception as e:
            logger.error(f"Error getting user role: {e}")
            return "viewer"

    def apply_role_restrictions(self):
        allowed_tabs = {
            "device_operator": ["File", "Raw Data", "Elements", "CRM", "Notifications"],
            "viewer": ["Raw Data"],
            "report_manager": ["File", "Raw Data", "Elements", "CRM", "Process", "Find similarity", "Notifications"],
            "lab_manager": None,
            "admin": None,
            "guest": ["Raw Data"],
            "qc": ["File", "Elements", "CRM", "QC"]
        }

        allowed = allowed_tabs.get(self.user_role, None)
        if allowed is None:
            return

        buttons = self.main_content.tab_buttons
        contents = self.main_content.tabs

        for tab_name, btn in buttons.items():
            if tab_name not in allowed:
                btn.hide()
                if tab_name in contents:
                    contents[tab_name].hide()

        if self.main_content.current_tab not in allowed:
            first_allowed = next((t for t in allowed if t in buttons), None)
            if first_allowed:
                self.main_content.switch_tab(first_allowed)

    # ────────────────────────────────────────
    # Notifications
    # ────────────────────────────────────────
    def show_notification_popup(self, message, notif_id):
        msg = QMessageBox(self)
        msg.setIcon(QMessageBox.Icon.Information)
        msg.setWindowTitle("New Change Pending Approval")
        msg.setText(f"<b>{message}</b>")
        msg.setStandardButtons(QMessageBox.StandardButton.Ok)
        msg.buttonClicked.connect(lambda: self.mark_notification_read(notif_id))
        msg.exec()

    def mark_notification_read(self, notif_id):
        try:
            conn = sqlite3.connect(self.resource_path("crm_data.db"))
            cur = conn.cursor()
            cur.execute("UPDATE notifications SET is_read = 1 WHERE id = ?", (notif_id,))
            conn.commit()
            conn.close()

            if hasattr(self, 'management_tab') and self.management_tab:
                self.management_tab.load_notifications()
            if hasattr(self, 'notification_tab') and self.notification_tab:
                self.notification_tab.load_notifications()
        except Exception as e:
            logger.error(f"Mark read error: {e}")

    def start_notification_checker(self):
        self.last_notif_check = 0
        self.notification_thread = threading.Thread(target=self.check_notifications_loop, daemon=True)
        self.notification_thread.start()

    def check_notifications_loop(self):
        while True:
            if self.user_role == 'lab_manager':
                self.check_new_notifications()
            time.sleep(10)

    def check_new_notifications(self):
        try:
            conn = sqlite3.connect(self.resource_path("crm_data.db"))
            cur = conn.cursor()
            cur.execute("""
                SELECT id, message FROM notifications 
                WHERE user_id = ? AND is_read = 0 AND created_at > ?
                ORDER BY created_at DESC LIMIT 1
            """, (self.user_id, datetime.fromtimestamp(self.last_notif_check).isoformat()))
            row = cur.fetchone()
            conn.close()

            if row:
                notif_id, message = row
                self.last_notif_check = time.time()
                self.tray_icon.showMessage("RASF - New Change", message, QSystemTrayIcon.MessageIcon.Information, 10000)
                self.show_notification_popup(message, notif_id)
        except Exception as e:
            logger.error(f"Notification check error: {e}")

    def setup_system_tray(self):
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(QIcon(self.resource_path("icons/app_icon.png")))
        tray_menu = QMenu()
        open_action = tray_menu.addAction("Open RASF")
        open_action.triggered.connect(lambda: self.showNormal())
        exit_action = tray_menu.addAction("Exit")
        exit_action.triggered.connect(QApplication.quit)
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.activated.connect(self.on_tray_activated)
        self.tray_icon.show()

    def on_tray_activated(self, reason):
        if reason == QSystemTrayIcon.ActivationReason.Trigger:
            self.showNormal()
            self.activateWindow()

    # ────────────────────────────────────────
    # Windows
    # ────────────────────────────────────────
    def new_window(self):
        new_win = MainWindow(self.username, self.user_name, self.user_position, str(self.user_id))
        new_win.show()

    def close_window(self):
        self.close()

    def closeEvent(self, event):
        if self in MainWindow.open_windows:
            MainWindow.open_windows.remove(self)

        # Close DB connection in QC Tab
        if self.qc_tab is not None and hasattr(self.qc_tab, 'close_db_connection'):
            try:
                self.qc_tab.close_db_connection()
            except:
                pass

        # Close DB connection in Min Max Tab if needed
        if self.min_max_tab is not None:
            # Add close logic if necessary
            pass

        if not MainWindow.open_windows and not any(
            isinstance(w, LoginWindow) and w.isVisible()
            for w in QApplication.topLevelWidgets()
        ):
            QApplication.quit()

        event.accept()

    # ────────────────────────────────────────
    # Project
    # ────────────────────────────────────────
    def reset_app_state(self):
        logger.debug("Resetting application state")
        self.data = None
        self.file_path = None
        self.file_ranges = []
        self.file_path_label.setText("File Path: No file selected")
        self.setWindowTitle(f"RASF Data Processor - {self.user_name} ({self.username})")

        for attr in [
            'pivot_tab', 'elements_tab', 'crm_tab', 'results', 'rm_check',
            'weight_check', 'volume_check', 'df_check', 'compare_tab',
            'empty_check', 'crm_check', 'report'
        ]:
            obj = getattr(self, attr, None)
            if obj and hasattr(obj, 'reset_state'):
                obj.reset_state()

        # Reset QC Tab
        if self.qc_tab is not None and hasattr(self.qc_tab, 'reset_state'):
            self.qc_tab.reset_state()

        # Reset Min Max Tab
        if self.min_max_tab is not None and hasattr(self.min_max_tab, 'reset_state'):
            self.min_max_tab.reset_state()

    def handle_additional(self):
        load_additional(self)

    def save_project(self):
        save_project(self)

    def load_project(self):
        load_project(self)

    def resource_path(self, relative_path):
        # try:
        #     base_path = sys._MEIPASS
        # except Exception:
        base_path = os.path.abspath(".")
        return os.path.join(base_path, relative_path)

    # ────────────────────────────────────────
    # Logout and login
    # ────────────────────────────────────────
    def logout(self):
        reply = QMessageBox.question(
            self, "Logout",
            f"Are you sure you want to log out, {self.user_name}?",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
        )
        if reply != QMessageBox.StandardButton.Yes:
            return

        try:
            conn = sqlite3.connect(self.resource_path("crm_data.db"))
            cur = conn.cursor()
            cur.execute("UPDATE users SET remember_me = 0 WHERE username = ?", (self.username,))
            conn.commit()
            conn.close()
            logger.info(f"User {self.username} logged out.")
        except Exception as e:
            logger.error(f"Logout DB error: {e}")

        for win in list(MainWindow.open_windows):
            if win != self:
                win.close()

        self.login_window = LoginWindow()
        self.login_window.login_successful.connect(self.on_login_success)
        self.login_window.show()
        self.close()

    def on_login_success(self, username, name, pos, user_id):
        new_main = MainWindow(username, name, pos, user_id)
        new_main.show()
        self.login_window.close()


    def auto_save_project(self):
        """هر ۲ دقیقه یکبار پروژه را به صورت خودکار ذخیره می‌کند (اگر داده وجود داشته باشد)"""
        if self.data is None or self.data.empty:
            logger.debug("Auto-save skipped: no data loaded")
            return

        if not hasattr(self, 'file_path') or not self.file_path:
            logger.debug("Auto-save skipped: no file path set yet")
            return

        # استخراج مسیر فولدر از file_path (اگر فایل باشد) یا مستقیماً استفاده از فولد
        
        try :
            file_save=self.file_ranges[0]['file_path'].split('/')
        except e :
            
        try:
            logger.info(f"Auto-saving project... Folder: {'/'.join(file_save[:-1])+'/'}")
            # پاس دادن مسیر فولدر به تابع save_project
            save_project(self,save_path='/'.join(file_save[:-1])+'/')
            # اختیاری: نمایش نوتیفیکیشن کوچک در System Tray
            # self.tray_icon.showMessage(
            #     "Auto-Save",
            #     "Project saved automatically",
            #     QSystemTrayIcon.MessageIcon.Information,
            #     3000
            # )
        except Exception as e:
            logger.error(f"Auto-save failed: {e}")
            # self.tray_icon.showMessage(
            #     "Auto-Save Failed",
            #     f"Error: {str(e)}",
            #     QSystemTrayIcon.MessageIcon.Critical,
            #     5000
            # )