# screens/file/file_tab.py
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QPushButton, QLabel, QComboBox,
    QLineEdit, QFileDialog, QMessageBox, QFormLayout, QDialog,
    QDialogButtonBox, QProgressDialog, QTextEdit, QTableWidget,
    QTableWidgetItem, QHeaderView, QHBoxLayout, QDateEdit, QGroupBox,
    QGridLayout, QTabWidget, QScrollArea,QFrame, QAbstractItemView
)
from PyQt6.QtCore import Qt, QThread, QDate, QRegularExpression
from PyQt6.QtGui import QRegularExpressionValidator
import pandas as pd
import sqlite3
import os
import logging
import re
from collections import defaultdict
from utils.load_file import FileLoaderThread
from screens.pivot.pivot_creator import PivotCreator
import jdatetime
logger = logging.getLogger(__name__)


# --- UploadDialog (با اضافه کردن CRUD حرفه‌ای) ---
class UploadDialog(QDialog):
    def __init__(self, parent=None, devices=None, user_id=None):
        super().__init__(parent)
        self.setWindowTitle("File Management (Upload & CRUD)")
        self.setFixedSize(1200, 800)  # اندازه بزرگ‌تر برای CRUD
        self.devices = devices or []
        self.user_id = user_id  # برای فیلتر فایل‌های کاربر
        self.selected_file = None
        self.contracts_text = ""
        self.crms_text = ""
        self.blanks_text = ""
        self.temp_df = None
        self.valid_crm_labels = set()
        self.valid_blank_labels = set()
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout(self)

        # تب ویجت برای جداسازی Upload و Manage
        self.tab_widget = QTabWidget()
        self.tab_widget.setStyleSheet("QTabWidget::pane { border: 1px solid #bdc3c7; } QTabBar::tab { padding: 10px; }")

        # تب Upload
        self.upload_tab = QWidget()
        self.init_upload_tab()
        self.tab_widget.addTab(self.upload_tab, "Upload New File")

        # تب Manage (CRUD)
        self.manage_tab = QWidget()
        self.init_manage_tab()
        self.tab_widget.addTab(self.manage_tab, "Manage Existing Files")

        main_layout.addWidget(self.tab_widget)

        # دکمه‌های پایین (برای کل دیالوگ)
        buttons = QDialogButtonBox(
            QDialogButtonBox.StandardButton.Close
        )
        buttons.rejected.connect(self.reject)
        main_layout.addWidget(buttons)

    def init_upload_tab(self):
        layout = QFormLayout(self.upload_tab)

        self.file_label = QLabel("No file selected")
        self.file_btn = QPushButton("Select File")
        self.file_btn.clicked.connect(self.select_file)
        file_layout = QVBoxLayout()
        file_layout.addWidget(self.file_label)
        file_layout.addWidget(self.file_btn)
        layout.addRow("Raw Data File:", file_layout)

        self.contracts_display = QTextEdit()
        self.contracts_display.setReadOnly(True)
        self.contracts_display.setPlaceholderText("Contracts will be displayed here after file selection...")
        layout.addRow("Extracted Contracts:", self.contracts_display)

        self.crms_display = QTextEdit()
        self.crms_display.setReadOnly(True)
        self.crms_display.setPlaceholderText("CRMs will be displayed here after file selection...")
        layout.addRow("Extracted CRMs:", self.crms_display)

        self.blanks_display = QTextEdit()
        self.blanks_display.setReadOnly(True)
        self.blanks_display.setPlaceholderText("Blanks will be displayed here after file selection...")
        layout.addRow("Extracted Blanks:", self.blanks_display)

        self.device_combo = QComboBox()
        self.device_combo.addItems([d[1] for d in self.devices])
        layout.addRow("Device:", self.device_combo)

        self.file_type = QComboBox()
        self.file_type.addItems(["oes 4ac", "oes fire", "mass"])
        layout.addRow("File Type:", self.file_type)

        self.description = QLineEdit()
        self.description.setPlaceholderText("Optional notes...")
        layout.addRow("Description:", self.description)

        upload_btn = QPushButton("Upload")
        upload_btn.clicked.connect(self.upload_file)
        layout.addRow(upload_btn)

    def init_manage_tab(self):
        layout = QVBoxLayout(self.manage_tab)

        # گروه فیلترها
        filter_group = QGroupBox("Search Filters")
        filter_grid = QGridLayout()

        row = 0

        # قرارداد
        filter_grid.addWidget(QLabel("Contract:"), row, 0)
        self.contract_search = QLineEdit()
        self.contract_search.setPlaceholderText("Enter contract number...")
        filter_grid.addWidget(self.contract_search, row, 1)

        # تاریخ آپلود
        filter_grid.addWidget(QLabel("Upload From:"), row, 2)
        self.from_date = QDateEdit()
        self.from_date.setCalendarPopup(True)
        self.from_date.setDate(QDate.currentDate().addMonths(-1))
        filter_grid.addWidget(self.from_date, row, 3)

        filter_grid.addWidget(QLabel("Upload To:"), row, 4)
        self.to_date = QDateEdit()
        self.to_date.setCalendarPopup(True)
        self.to_date.setDate(QDate.currentDate())
        filter_grid.addWidget(self.to_date, row, 5)

        row += 1

        # تاریخ جلالی
        filter_grid.addWidget(QLabel("Jalali From:"), row, 0)
        self.jalali_from_edit = QLineEdit()
        self.jalali_from_edit.setPlaceholderText("1404-01-01")
        filter_grid.addWidget(self.jalali_from_edit, row, 1)

        filter_grid.addWidget(QLabel("Jalali To:"), row, 2)
        self.jalali_to_edit = QLineEdit()
        self.jalali_to_edit.setPlaceholderText("1404-12-29")
        filter_grid.addWidget(self.jalali_to_edit, row, 3)

        # وضعیت
        filter_grid.addWidget(QLabel("Status:"), row, 4)
        self.status_combo = QComboBox()
        self.status_combo.addItems(["All", "Active", "Archived"])
        filter_grid.addWidget(self.status_combo, row, 5)

        row += 1

        # دکمه جستجو
        search_btn = QPushButton("Search")
        search_btn.clicked.connect(self.load_filtered_files)
        filter_grid.addWidget(search_btn, row, 0, 1, 6)

        filter_group.setLayout(filter_grid)
        layout.addWidget(filter_group)

        # اعتبارسنجی تاریخ جلالی
        jalali_validator = QRegularExpressionValidator(QRegularExpression(r"\d{4}-\d{2}-\d{2}"))
        self.jalali_from_edit.setValidator(jalali_validator)
        self.jalali_to_edit.setValidator(jalali_validator)

        # جدول نتایج
        self.search_table = QTableWidget()
        self.search_table.setColumnCount(8)
        self.search_table.setHorizontalHeaderLabels([
            "ID", "Jalali Date", "File Name", "Uploaded By", "Device", "Upload Date", "Status", "Contracts"
        ])
        self.search_table.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self.search_table.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOn)
        self.search_table.setHorizontalScrollMode(QTableWidget.ScrollMode.ScrollPerPixel)
        self.search_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Interactive)
        self.search_table.horizontalHeader().setStretchLastSection(False)
        self.search_table.setWordWrap(False)
        self.search_table.setColumnHidden(0, True)  # پنهان کردن ID

        scroll_area = QScrollArea()
        scroll_area.setWidget(self.search_table)
        scroll_area.setWidgetResizable(True)
        layout.addWidget(scroll_area)

        # دکمه‌های CRUD
        btn_layout = QHBoxLayout()
        edit_btn = QPushButton("Edit")
        edit_btn.clicked.connect(self.edit_selected_file)
        delete_btn = QPushButton("Delete")
        delete_btn.clicked.connect(self.delete_selected_file)
        refresh_btn = QPushButton("Refresh")
        refresh_btn.clicked.connect(self.load_filtered_files)
        btn_layout.addStretch()
        btn_layout.addWidget(edit_btn)
        btn_layout.addWidget(delete_btn)
        btn_layout.addWidget(refresh_btn)
        layout.addLayout(btn_layout)

        # بارگذاری اولیه
        self.load_filtered_files()

    def select_file(self):
        file_path, _ = QFileDialog.getOpenFileName(
            self, "Select Data File", "", "Data Files (*.xlsx *.xls *.csv *.rep)"
        )
        if file_path:
            self.selected_file = file_path
            self.file_label.setText(os.path.basename(file_path))
            self.load_and_display_contracts(file_path)

    def load_and_display_contracts(self, file_path):
        try:
            self.progress_dialog = QProgressDialog("Analyzing file...", "Cancel", 0, 100, self)
            self.progress_dialog.setWindowTitle("Processing")
            self.progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)
            self.progress_dialog.setMinimumDuration(0)
            self.progress_dialog.show()

            self.temp_worker = FileLoaderThread(file_path, self)
            self.temp_worker.progress.connect(self.update_temp_progress)
            self.temp_worker.finished.connect(lambda df, _: self.on_temp_file_loaded(df, file_path))
            self.temp_worker.error.connect(self.on_temp_load_error)
            self.progress_dialog.canceled.connect(self.temp_worker.cancel)
            self.temp_worker.start()

        except Exception as e:
            logger.error(f"Error: {e}")
            self.contracts_display.setText("Error loading file.")
            self.contracts_display.setStyleSheet("color: red;")

    def update_temp_progress(self, value, message):
        self.progress_dialog.setValue(value)
        self.progress_dialog.setLabelText(message)

    def on_temp_file_loaded(self, df, file_path):
        try:
            self.progress_dialog.close()

            if df is None or df.empty:
                self.contracts_display.setText("No data in file.")
                self.contracts_display.setStyleSheet("color: red;")
                return

            self.temp_df = df

            solution_col = None
            for col in df.columns:
                if 'solution' in str(col).lower() and 'label' in str(col).lower():
                    solution_col = col
                    break

            if solution_col is None:
                self.contracts_display.setText("Column 'Solution Label' not found.")
                self.contracts_display.setStyleSheet("color: orange;")
                return

            labels = df[solution_col].dropna().astype(str).unique()

            # استخراج contracts (همان کد قبلی)
            contracts = defaultdict(list)

            # الگوهای انعطاف‌پذیر با re.search (نه match)
            range_pattern = re.compile(r'(\d{4})\s*\(\s*(\d+)\s*-\s*(\d+)\s*\)', re.IGNORECASE)
            simple_pattern = re.compile(r'(\d{4})\s*-\s*(\d+)', re.IGNORECASE)
            contract_only_pattern = re.compile(r'^(\d{4})$')

            for raw_label in labels:
                label = str(raw_label).strip()
                if not label:
                    continue

                # 1. تاریخ‌ها را رد کن (مثل 2025-01-01)
                if re.match(r'^\d{4}-\d{2}-\d{2}$', label):
                    continue

                # 2. الگوی دامنه: 1666(01-05) — حتی با فاصله یا متن اطراف
                range_match = range_pattern.search(label)
                if range_match:
                    contract_num = range_match.group(1)
                    start = int(range_match.group(2))
                    end = int(range_match.group(3))
                    for i in range(start, end + 1):
                        contracts[contract_num].append(f"{contract_num}-{i:02d}")
                    continue  # این label پردازش شد

                # 3. الگوی ساده: 1666-242 — حتی با متن قبل/بعد
                simple_match = simple_pattern.search(label)
                if simple_match:
                    contract_num = simple_match.group(1)
                    variety = simple_match.group(2).zfill(2)  # اطمینان از دو رقمی بودن
                    contracts[contract_num].append(f"{contract_num}-{variety}")
                    continue

                # 4. فقط شماره قرارداد: 1666
                if contract_only_pattern.match(label):
                    contract_num = label
                    contracts[contract_num] = []  # بدون واریته
                    continue

                # اگر هیچ‌کدام نبود، نادیده بگیر

            # نمایش contracts
            display_lines = []
            for contract, varieties in sorted(contracts.items(), key=lambda x: x[0]):
                if varieties:
                    varieties = sorted(set(varieties))  # حذف تکراری + مرتب‌سازی
                    varieties_str = ', '.join(varieties)
                    display_lines.append(f"{contract} with varieties: {varieties_str}")
                else:
                    display_lines.append(f"{contract}")

            if not display_lines:
                self.contracts_text = "No valid contracts found in 'Solution Label'."
                self.contracts_display.setText(self.contracts_text)
                self.contracts_display.setStyleSheet("color: orange;")
            else:
                self.contracts_text = "\n".join(display_lines)
                self.contracts_display.setText(self.contracts_text)
                self.contracts_display.setStyleSheet("color: green; font-family: Consolas; font-size: 10pt;")

            # استخراج CRMs
            crm_ids = ['258', '252', '906', '506', '233', '255', '263', '260']
            crm_pattern = re.compile(rf'(?i)(?:(?:^|(?<=\s))(?:CRM|OREAS)?\s*({"|".join(crm_ids)})(?:[a-zA-Z0-9]{{0,2}})?\b)')
            crms = set()

            for raw_label in labels:
                label = str(raw_label).strip()
                if not label:
                    continue
                match = crm_pattern.search(label)
                if match:
                    crms.add(match.group(0).strip())  # کل مطابقت را اضافه کن (با پیشوند اگر باشد)

            self.valid_crm_labels = crms

            # نمایش CRMs
            if not crms:
                self.crms_text = "No valid CRMs found in 'Solution Label'."
                self.crms_display.setText(self.crms_text)
                self.crms_display.setStyleSheet("color: orange;")
            else:
                self.crms_text = "\n".join(sorted(crms))
                self.crms_display.setText(self.crms_text)
                self.crms_display.setStyleSheet("color: green; font-family: Consolas; font-size: 10pt;")

            # استخراج Blanks
            blank_pattern = re.compile(r'(?:CRM\s*)?(?:BLANK|BLNK)(?:\s+.*)?', re.IGNORECASE)
            blanks = set()

            for raw_label in labels:
                label = str(raw_label).strip()
                if not label:
                    continue
                if blank_pattern.search(label):
                    blanks.add(label.strip())  # کل label را اضافه کن

            self.valid_blank_labels = blanks

            # نمایش Blanks
            if not blanks:
                self.blanks_text = "No valid Blanks found in 'Solution Label'."
                self.blanks_display.setText(self.blanks_text)
                self.blanks_display.setStyleSheet("color: orange;")
            else:
                self.blanks_text = "\n".join(sorted(blanks))
                self.blanks_display.setText(self.blanks_text)
                self.blanks_display.setStyleSheet("color: green; font-family: Consolas; font-size: 10pt;")

        except Exception as e:
            logger.error(f"Error extracting contracts/CRMs/Blanks: {e}")
            self.contracts_display.setText(f"Error: {str(e)}")
            self.contracts_display.setStyleSheet("color: red;")

    def on_temp_load_error(self, message):
        self.progress_dialog.close()
        logger.error(f"Temp file load failed: {message}")
        self.contracts_display.setText(f"Error: {message}")
        self.contracts_display.setStyleSheet("color: red;")

    def upload_file(self):
        if not self.selected_file:
            QMessageBox.warning(self, "No File", "Please select a file first.")
            return
        data = self.get_data()
        if data:
            self.parent().process_upload(data)
            self.load_filtered_files()  # به‌روزرسانی لیست پس از آپلود

    def get_data(self):
        if not self.selected_file:
            return None
        device_id = self.devices[self.device_combo.currentIndex()][0] if self.devices else None
        device_name = self.device_combo.currentText()
        return {
            "file_path": self.selected_file,
            "device_id": device_id,
            "device_name": device_name,
            "file_type": self.file_type.currentText(),
            "description": self.description.text().strip(),
            "contracts": self.contracts_text,
            "df": self.temp_df,
            "crm_labels": self.valid_crm_labels,
            "blank_labels": self.valid_blank_labels
        }

    def load_filtered_files(self):
        try:
            db_path = self.parent().main_window.resource_path("crm_data.db")
            conn = sqlite3.connect(db_path)
            cur = conn.cursor()

            query = """
                SELECT uf.id, uf.original_filename, u.full_name, d.name, uf.created_at, uf.is_archived, uf.contracts
                FROM uploaded_files uf
                JOIN users u ON uf.uploaded_by = u.id
                JOIN devices d ON uf.device_id = d.id
                WHERE uf.uploaded_by = ?
            """
            params = [self.user_id]

            # وضعیت
            status = self.status_combo.currentText()
            if status == "Active":
                query += " AND uf.is_archived = 0"
            elif status == "Archived":
                query += " AND uf.is_archived = 1"

            # قرارداد
            contract_query = self.contract_search.text().strip()
            if contract_query:
                query += " AND uf.contracts LIKE ?"
                params.append(f"%{contract_query}%")

            # تاریخ آپلود
            from_date_str = self.from_date.date().toString("yyyy-MM-dd")
            to_date_str = self.to_date.date().toString("yyyy-MM-dd")
            query += " AND DATE(uf.created_at) BETWEEN ? AND ?"
            params.extend([from_date_str, to_date_str])

            query += " ORDER BY uf.created_at DESC"

            cur.execute(query, params)
            all_files = cur.fetchall()
            conn.close()

            # فیلتر تاریخ جلالی
            jalali_from = self.jalali_from_edit.text().strip()
            jalali_to = self.jalali_to_edit.text().strip()
            has_jalali_filter = bool(jalali_from or jalali_to)

            filtered_files = []
            for row in all_files:
                jalali_date, _ = self.parent().parse_filename(row[1])
                include = True

                if has_jalali_filter and jalali_date:
                    if jalali_from and jalali_date < jalali_from:
                        include = False
                    if jalali_to and jalali_date > jalali_to:
                        include = False
                elif has_jalali_filter and not jalali_date:
                    include = False

                if include:
                    filtered_files.append(row)

            # نمایش
            self.search_table.setRowCount(len(filtered_files))
            header = self.search_table.horizontalHeader()
            header.setSectionResizeMode(1, QHeaderView.ResizeMode.ResizeToContents)
            header.setSectionResizeMode(2, QHeaderView.ResizeMode.Interactive)
            header.setSectionResizeMode(7, QHeaderView.ResizeMode.ResizeToContents)
            header.resizeSection(7, 350)

            for i, row in enumerate(filtered_files):
                jalali_date, clean_name = self.parent().parse_filename(row[1])
                archive_status = "Archived" if row[5] else "Active"

                self.search_table.setItem(i, 0, QTableWidgetItem(str(row[0])))
                self.search_table.setItem(i, 1, QTableWidgetItem(jalali_date or "N/A"))
                self.search_table.setItem(i, 2, QTableWidgetItem(clean_name))
                self.search_table.setItem(i, 3, QTableWidgetItem(row[2]))
                self.search_table.setItem(i, 4, QTableWidgetItem(row[3]))
                self.search_table.setItem(i, 5, QTableWidgetItem(row[4][:19]))
                self.search_table.setItem(i, 6, QTableWidgetItem(archive_status))
                self.search_table.setItem(i, 7, QTableWidgetItem(row[6] or ""))

            logger.info(f"Loaded {len(filtered_files)} files after filtering")

        except Exception as e:
            logger.error(f"Failed to load filtered files: {e}")
            QMessageBox.critical(self, "Error", f"Failed to load files:\n{e}")

    def edit_selected_file(self):
        selected_row = self.search_table.currentRow()
        if selected_row < 0:
            QMessageBox.warning(self, "No Selection", "Please select a file to edit.")
            return

        file_id = int(self.search_table.item(selected_row, 0).text())
        self.open_edit_dialog(file_id)

    def open_edit_dialog(self, file_id):
        try:
            db_path = self.parent().main_window.resource_path("crm_data.db")
            conn = sqlite3.connect(db_path)
            cur = conn.cursor()
            cur.execute("""
                SELECT uf.description, uf.file_type, d.id, d.name
                FROM uploaded_files uf
                JOIN devices d ON uf.device_id = d.id
                WHERE uf.id = ?
            """, (file_id,))
            result = cur.fetchone()
            conn.close()

            if not result:
                QMessageBox.warning(self, "Error", "File not found.")
                return

            description, file_type, device_id, device_name = result

            edit_dialog = QDialog(self)
            edit_dialog.setWindowTitle("Edit File Metadata")
            edit_layout = QFormLayout(edit_dialog)

            desc_edit = QLineEdit(description or "")
            edit_layout.addRow("Description:", desc_edit)

            type_combo = QComboBox()
            type_combo.addItems(["oes 4ac", "oes fire", "mass"])
            type_combo.setCurrentText(file_type)
            edit_layout.addRow("File Type:", type_combo)

            device_combo = QComboBox()
            device_combo.addItems([d[1] for d in self.devices])
            device_combo.setCurrentText(device_name)
            edit_layout.addRow("Device:", device_combo)

            buttons = QDialogButtonBox(QDialogButtonBox.StandardButton.Save | QDialogButtonBox.StandardButton.Cancel)
            buttons.accepted.connect(lambda: self.save_edit(file_id, desc_edit.text(), type_combo.currentText(), self.devices[device_combo.currentIndex()][0], edit_dialog))
            buttons.rejected.connect(edit_dialog.reject)
            edit_layout.addRow(buttons)

            edit_dialog.exec()

        except Exception as e:
            logger.error(f"Error opening edit dialog: {e}")
            QMessageBox.warning(self, "Error", f"Failed to open edit dialog: {e}")

    def save_edit(self, file_id, new_desc, new_type, new_device_id, dialog):
        try:
            db_path = self.parent().main_window.resource_path("crm_data.db")
            conn = sqlite3.connect(db_path)
            cur = conn.cursor()
            cur.execute("""
                UPDATE uploaded_files
                SET description = ?, file_type = ?, device_id = ?
                WHERE id = ?
            """, (new_desc, new_type, new_device_id, file_id))
            conn.commit()
            conn.close()
            QMessageBox.information(self, "Success", "File metadata updated.")
            self.load_filtered_files()
            dialog.accept()
        except Exception as e:
            logger.error(f"Failed to update file: {e}")
            QMessageBox.critical(self, "Error", f"Failed to update: {e}")

    def delete_selected_file(self):
        selected_row = self.search_table.currentRow()
        if selected_row < 0:
            QMessageBox.warning(self, "No Selection", "Please select a file to delete.")
            return

        file_id = int(self.search_table.item(selected_row, 0).text())
        file_name = self.search_table.item(selected_row, 2).text()

        confirm = QMessageBox.question(self, "Confirm Delete", f"Are you sure you want to delete '{file_name}'?\nThis will also remove related CRM data.", QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No)
        if confirm == QMessageBox.StandardButton.Yes:
            self.perform_delete(file_id, file_name)

    def perform_delete(self, file_id, file_name):
        try:
            db_path = self.parent().main_window.resource_path("crm_data.db")
            elements_db_path = self.parent().main_window.resource_path("excels_elements.db")
            conn = sqlite3.connect(db_path)
            conn_elements = sqlite3.connect(elements_db_path)
            cur = conn.cursor()
            cur_elements = conn_elements.cursor()

            # حذف از crm_data
            cur.execute("DELETE FROM crm_data WHERE file_name = ?", (file_name,))

            # حذف از elements_data
            cur_elements.execute("DELETE FROM elements_data WHERE file_name = ?", (file_name,))

            # حذف از uploaded_files
            cur.execute("DELETE FROM uploaded_files WHERE id = ?", (file_id,))

            conn.commit()
            conn_elements.commit()
            conn.close()
            conn_elements.close()

            QMessageBox.information(self, "Success", "File and related data deleted.")
            self.load_filtered_files()

        except Exception as e:
            logger.error(f"Failed to delete file: {e}")
            QMessageBox.critical(self, "Error", f"Failed to delete: {e}")


# --- FileTab با به‌روزرسانی Result تب ---
class FileTab(QWidget):
    def __init__(self, main_window):
        super().__init__()
        self.main_window = main_window
        self.init_ui()

    def parse_filename(self, filename):
        """استخراج تاریخ جلالی و نام تمیز از نام فایل"""
        filename = filename.strip()
        match = re.match(r'(\d{4}-\d{2}-\d{2})\s*(.+)', filename, re.IGNORECASE)
        if match:
            return match.group(1), match.group(2).strip()
        match_no_space = re.match(r'(\d{4}-\d{2}-\d{2})(.+)', filename, re.IGNORECASE)
        if match_no_space:
            return match_no_space.group(1), match_no_space.group(2).strip()
        return None, filename
    def miladi_to_jalali(self, miladi_date):
        """تبدیل تاریخ میلادی (YYYY-MM-DD) به جلالی"""
        try:
            from datetime import datetime
            miladi = datetime.strptime(miladi_date[:10], '%Y-%m-%d')
            jalali = jdatetime.date.fromgregorian(date=miladi)
            return jalali.strftime('%Y-%m-%d')
        except Exception as e:
            logger.error(f"Error converting date {miladi_date} to Jalali: {e}")
            return "N/A"
    def init_ui(self):
        layout = QVBoxLayout(self)
        layout.setAlignment(Qt.AlignmentFlag.AlignCenter)

        title = QLabel("File Management")
        title.setStyleSheet("font: bold 24px; color: #2c3e50;")
        layout.addWidget(title, alignment=Qt.AlignmentFlag.AlignCenter)

        self.file_btn = QPushButton("Upload Raw Data File")
        self.file_btn.setFixedSize(300, 50)
        self.file_btn.setStyleSheet("""
            QPushButton {
                background: #3498db; color: white; font: bold 16px;
                border-radius: 10px; padding: 10px;
            }
            QPushButton:hover { background: #2980b9; }
        """)
        self.file_btn.clicked.connect(self.on_file_button_clicked)

        # جدول فایل‌ها
        self.files_table = QTableWidget()
        self.files_table.setColumnCount(7)
        self.files_table.setHorizontalHeaderLabels([
            "ID", "Jalali Date", "File Name", "Uploaded By", "Device", "Upload Date", "Status"
        ])
        self.files_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        self.files_table.hide()

        self.refresh_btn = QPushButton("Refresh List")
        self.refresh_btn.setFixedSize(300, 40)
        self.refresh_btn.clicked.connect(self.load_uploaded_files_list)
        self.refresh_btn.hide()

        layout.addWidget(self.file_btn, alignment=Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(self.refresh_btn, alignment=Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(self.files_table)
        layout.addStretch()

        self.apply_role_restrictions()

    def apply_role_restrictions(self):
        role = self.main_window.user_role
        if role == "report_manager":
            self.file_btn.setText("Select Raw Data File")
            self.refresh_btn.show()
            self.files_table.show()
            self.load_uploaded_files_list()
        elif role == "device_operator":
            self.file_btn.setText("Manage Files (Upload & CRUD)")
            self.refresh_btn.hide()
            self.files_table.hide()
        else:
            self.file_btn.hide()
            self.refresh_btn.hide()
            self.files_table.hide()

    def on_file_button_clicked(self):
        role = self.main_window.user_role
        if role == "device_operator":
            self.show_upload_dialog()
        elif role == "report_manager":
            self.open_existing_file()

    def show_upload_dialog(self):
        logger.debug("[FileTab] show_upload_dialog called")

        try:
            db_path = self.main_window.resource_path("crm_data.db")
            conn = sqlite3.connect(db_path)
            cur = conn.cursor()
            cur.execute("SELECT id, name FROM devices ORDER BY name")
            devices = cur.fetchall()
            conn.close()

            if not devices:
                QMessageBox.warning(self, "No Devices", "No devices are registered. Contact admin.")
                return

        except Exception as e:
            logger.error(f"[FileTab] DB Error in show_upload_dialog: {e}")
            QMessageBox.critical(self, "Database Error", f"Could not load devices:\n{e}")
            return

        try:
            dialog = UploadDialog(self, devices, self.main_window.user_id_from_username())
            dialog.exec()
        except Exception as e:
            logger.error(f"[FileTab] UploadDialog error: {e}", exc_info=True)
            QMessageBox.critical(self, "Upload Error", f"Failed to open upload dialog:\n{e}")

    def normalize_crm_id(self,crm_id):
        """Extract numeric part from CRM ID (e.g., 'CRM 258b' → '258', '258 b' → '258')."""
        if not isinstance(crm_id, str):
            return None
        crm_pattern = re.compile(r'^(?:\s*CRM\s*)?(\d{3})(?:\s*[a-zA-Z])?$', re.IGNORECASE)
        match = crm_pattern.match(crm_id.strip())
        if match:
            # logger.debug(f"Normalized CRM ID: {crm_id} → {match.group(1)}")
            return match.group(1)
        # logger.debug(f"Invalid CRM ID format: {crm_id}")
        return None

    def process_upload(self, upload_data):
        file_path = upload_data["file_path"]
        df = upload_data.get("df")
        crm_labels = upload_data.get("crm_labels", set())
        blank_labels = upload_data.get("blank_labels", set())
        file_type = upload_data["file_type"]

        if df is None or df.empty:
            QMessageBox.critical(self, "Empty File", "The selected file is empty or could not be read.")
            return

        try:
            # 1. ذخیره متادیتا فایل در جدول uploaded_files
            self.save_to_db(upload_data, file_path)

            # 2. مسیر دیتابیس
            db_path = self.main_window.resource_path("crm_data.db")

            # 3. ستون Solution Label
            solution_col = 'Solution Label'
            if solution_col not in df.columns:
                raise ValueError(f"Column '{solution_col}' not found in file.")

            # 4. فیلتر ردیف‌های حاوی CRM یا Blank با regex
            crm_numbers = {'258', '252', '906', '506', '233', '255', '263', '260'}
            patterns = []

            # --- CRM: استخراج اعداد معتبر از labelهای شناسایی‌شده ---
            for label in crm_labels:
                m = re.search(r'\b(\d{3})\b', str(label))
                if m and m.group(1) in crm_numbers:
                    patterns.append(rf'\b{m.group(1)}\b')

            # --- Blank: الگوی ساده ---
            if blank_labels:
                patterns.append(r'(?i)\b(BLANK|BLNK)\b')

            df_crm_raw = pd.DataFrame()
            if patterns:
                combined_regex = re.compile('|'.join(patterns))
                mask = df[solution_col].astype(str).str.contains(combined_regex, na=False, regex=True)
                df_crm_raw = df[mask].copy()
                logger.debug(f"CRM/Blank regex: {combined_regex.pattern}")
                logger.debug(f"Matched {len(df_crm_raw)} rows with CRM/Blank patterns.")
            else:
                logger.info("No CRM or Blank patterns to match. Skipping crm_data import.")

            if df_crm_raw.empty:
                logger.info("No CRM/Blank rows found in data. Skipping import.")
            else:
                # 5. چک ستون‌های ضروری
                required_cols = ['Element', 'Corr Con']
                missing = [col for col in required_cols if col not in df_crm_raw.columns]
                if missing:
                    raise ValueError(f"Missing required columns in CRM/Blank data: {missing}")

                # 6. استخراج داده‌ها
                df_crm = df_crm_raw[[solution_col, 'Element', 'Corr Con']].copy()
                df_crm = df_crm.rename(columns={
                    solution_col: 'solution_label',
                    'Element': 'element',
                    'Corr Con': 'value'
                })

                # تبدیل مقدار به عدد
                df_crm['value'] = pd.to_numeric(df_crm['value'], errors='coerce')
                df_crm = df_crm.dropna(subset=['value', 'element'])
                df_crm = df_crm[df_crm['element'].str.strip() != '']

                if df_crm.empty:
                    logger.warning("No valid numeric values in 'Corr Con' for CRM/Blank rows.")
                else:
                    # 7. تعیین crm_id با تابع قوی
                    def get_crm_id(label):
                        label_str = str(label).strip().upper()
                        if re.search(r'\b(BLANK|BLNK)\b', label_str):
                            return "BLANK"
                        match = re.search(r'\b(\d{3})\b', label_str)
                        if match:
                            num = match.group(1)
                            if num in crm_numbers:
                                return num
                        return None

                    df_crm['crm_id'] = df_crm['solution_label'].apply(get_crm_id)
                    df_crm = df_crm.dropna(subset=['crm_id'])

                    if df_crm.empty:
                        logger.warning("No valid CRM IDs after normalization. All rows dropped.")
                    else:
                        # 8. اضافه کردن ستون‌های باقی‌مانده
                        jalali_date, _ = self.parse_filename(os.path.basename(file_path))
                        df_crm['file_name'] = os.path.basename(file_path)
                        df_crm['folder_name'] = file_type
                        df_crm['date'] = jalali_date or ""

                        # 9. ترتیب نهایی ستون‌ها
                        df_crm_final = df_crm[[
                            'crm_id', 'solution_label', 'element', 'value',
                            'file_name', 'folder_name', 'date'
                        ]]

                        # 10. ذخیره در دیتابیس
                        self.safe_import_crm_data(df_crm_final, db_path)
                        logger.info(f"Successfully imported {len(df_crm_final)} CRM/Blank records into crm_data.")

            # --- اضافه کردن: ذخیره تمام عناصر در excels_elements.db ---
            self.save_all_elements_to_db(df, file_path)

            # 11. بارگذاری داده در UI
            self.main_window.reset_app_state()
            self.main_window.data = df.copy()
            self.main_window.init_data = df.copy()
            self.main_window.file_path = file_path

            _, clean_name = self.parse_filename(os.path.basename(file_path))
            self.main_window.file_path_label.setText(f"File: {clean_name}")
            self.main_window.setWindowTitle(f"RASF Data Processor - {clean_name}")

            self.main_window.notify_data_changed()

            # به‌روزرسانی تب‌ها
            if hasattr(self.main_window, 'elements_tab') and self.main_window.elements_tab:
                self.main_window.elements_tab.process_blk_elements()

            if hasattr(self.main_window, 'pivot_tab') and self.main_window.pivot_tab:
                try:
                    PivotCreator(self.main_window.pivot_tab).create_pivot()
                except Exception as e:
                    logger.warning(f"Could not update pivot: {e}")

            if hasattr(self.main_window, 'results') and hasattr(self.main_window.results, 'show_processed_data'):
                try:
                    self.main_window.results.show_processed_data()
                except Exception as e:
                    logger.warning(f"Could not update Result tab: {e}")

            QMessageBox.information(self, "Success", "File uploaded and CRM/Blank data imported successfully.")

        except Exception as e:
            logger.error(f"Upload failed: {e}", exc_info=True)
            QMessageBox.critical(self, "Error", f"Failed to process file:\n{str(e)}")

    def save_all_elements_to_db(self, df, file_path):
        """ذخیره تمام solution_label و عناصر در excels_elements.db با ساختار pivot شده (wide format)"""
        try:
            elements_db_path = self.main_window.resource_path("excels_elements.db")
            conn = sqlite3.connect(elements_db_path)
            cursor = conn.cursor()

            # چک ستون‌های ضروری
            solution_col = 'Solution Label'
            if solution_col not in df.columns or 'Element' not in df.columns or 'Corr Con' not in df.columns:
                raise ValueError("Missing required columns for elements data.")

            # استخراج داده‌ها
            df_elements = df[[solution_col, 'Element', 'Corr Con']].copy()
            df_elements = df_elements.rename(columns={
                solution_col: 'sample_id',
                'Element': 'element',
                'Corr Con': 'value'
            })
            df_elements['value'] = pd.to_numeric(df_elements['value'], errors='coerce')
            df_elements = df_elements.dropna(subset=['value', 'element', 'sample_id'])

            # pivot به wide format با مدیریت تکراری‌ها (میانگین)
            df_pivoted = df_elements.pivot_table(index='sample_id', columns='element', values='value', aggfunc='mean').reset_index()
            df_pivoted['file_name'] = os.path.basename(file_path)

            # چک تکراری بودن فایل
            cursor.execute("SELECT COUNT(*) FROM elements_data WHERE file_name = ?", (df_pivoted['file_name'].iloc[0],))
            if cursor.fetchone()[0] > 0:
                # raise ValueError(f"File '{df_pivoted['file_name'].iloc[0]}' already exists in elements_data table.")
                pass

            # اضافه کردن ستون‌های جدید برای عناصر (اگر وجود نداشته باشند)
            for element in df_pivoted.columns:
                if element not in ['sample_id', 'file_name']:
                    try:
                        cursor.execute(f'ALTER TABLE elements_data ADD COLUMN "{element}" REAL')
                    except sqlite3.OperationalError:
                        pass  # ستون از قبل وجود دارد

            # ذخیره ردیف‌ها
            columns = df_pivoted.columns
            quoted_columns = ', '.join([f'"{col}"' for col in columns])
            placeholders = ', '.join(['?' for _ in columns])

            for _, row in df_pivoted.iterrows():
                values = tuple(row)
                cursor.execute(f'INSERT INTO elements_data ({quoted_columns}) VALUES ({placeholders})', values)

            conn.commit()

            # WAL Checkpoint و VACUUM
            cursor.execute("PRAGMA wal_checkpoint(FULL);")
            conn.commit()
            cursor.execute("VACUUM;")
            conn.commit()

            logger.info(f"Successfully imported {len(df_pivoted)} pivoted records into elements_data.")

        except Exception as e:
            if conn:
                conn.rollback()
            logger.error(f"Elements import failed: {e}")
            raise e
        finally:
            if conn:
                conn.close()

    def safe_import_crm_data(self, df_crm_final, db_path):
        """ذخیره امن CRM data با checkpoint و vacuum"""
        conn = None
        try:
            conn = sqlite3.connect(db_path)
            cursor = conn.cursor()

            # چک تکراری بودن فایل
            file_name = df_crm_final['file_name'].iloc[0]
            cursor.execute("SELECT COUNT(*) FROM crm_data WHERE file_name = ?", (file_name,))
            if cursor.fetchone()[0] > 0:
                # raise ValueError(f"File '{file_name}' already exists in crm_data table.")
                pass

            # ذخیره داده
            df_crm_final.to_sql('crm_data', conn, if_exists='append', index=False)
            conn.commit()

            # WAL Checkpoint
            cursor.execute("PRAGMA wal_checkpoint(FULL);")
            conn.commit()

            # VACUUM برای بهینه‌سازی و به‌روزرسانی حجم
            cursor.execute("VACUUM;")
            conn.commit()

            logger.info(f"CRM data imported: {len(df_crm_final)} rows. WAL checkpointed and DB vacuumed.")

        except Exception as e:
            if conn:
                conn.rollback()
            logger.error(f"CRM import failed: {e}")
            raise e
        finally:
            if conn:
                conn.close()

    def save_to_db(self, upload_data, file_path):
        try:
            db_path = self.main_window.resource_path("crm_data.db")
            conn = sqlite3.connect(db_path)
            cur = conn.cursor()

            clean_name = os.path.basename(file_path).replace(" ", "_")
            cur.execute("""
                INSERT INTO uploaded_files 
                (original_filename, clean_filename, file_path, device_id, file_type, description, contracts, uploaded_by, created_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, datetime('now'))
            """, (
                os.path.basename(file_path),
                clean_name,
                file_path,
                upload_data["device_id"],
                upload_data["file_type"],
                upload_data["description"],
                upload_data["contracts"],
                self.main_window.user_id_from_username()
            ))
            conn.commit()
            conn.close()
        except Exception as e:
            logger.error(f"Failed to save file metadata: {e}")
            QMessageBox.critical(self, "DB Error", f"Failed to save file:\n{e}")

    def open_existing_file(self):
        try:
            dialog = QDialog(self)
            dialog.setWindowTitle("Select Existing File")
            dialog.setFixedSize(1200, 700)  # اندازه قبلی
            layout = QVBoxLayout(dialog)

            # --- گروه فیلترها (دقیقاً مثل قبل، فقط بدون تاریخ آپلود میلادی) ---
            filter_group = QGroupBox("Search Filters")
            filter_grid = QGridLayout()
            row = 0

            # قرارداد
            filter_grid.addWidget(QLabel("Contract:"), row, 0)
            self.contract_search = QLineEdit()
            self.contract_search.setPlaceholderText("Enter contract number...")
            filter_grid.addWidget(self.contract_search, row, 1)

            # تاریخ جلالی
            filter_grid.addWidget(QLabel("From:"), row, 2)
            self.jalali_from_edit = QLineEdit()
            self.jalali_from_edit.setPlaceholderText("1404-01-01")
            filter_grid.addWidget(self.jalali_from_edit, row, 3)

            filter_grid.addWidget(QLabel("To:"), row, 4)
            self.jalali_to_edit = QLineEdit()
            self.jalali_to_edit.setPlaceholderText("1404-12-29")
            filter_grid.addWidget(self.jalali_to_edit, row, 5)

            # وضعیت
            filter_grid.addWidget(QLabel("Status:"), row, 6)
            self.status_combo = QComboBox()
            self.status_combo.addItems(["All", "Active", "Archived"])
            filter_grid.addWidget(self.status_combo, row, 7)


            filter_grid.addWidget(QLabel("File Type:"), row, 8)
            self.file_type_combo = QComboBox()
            self.file_type_combo.addItems(["All", "oes 4ac", "oes fire", "mass"])
            filter_grid.addWidget(self.file_type_combo, row, 9)
            # دکمه جستجو (همون جای قبلی)
            search_btn = QPushButton("Search")
            search_btn.clicked.connect(lambda: self.load_filtered_files(dialog))
            filter_grid.addWidget(search_btn, row, 10)  # همون جای قبلی

            filter_group.setLayout(filter_grid)
            layout.addWidget(filter_group)

            # --- جدول نتایج (اضافه کردن ستون Upload Date (Jalali)) ---
            self.search_table = QTableWidget()
            self.search_table.setColumnCount(9)  # اضافه کردن یک ستون برای تاریخ آپلود جلالی
            self.search_table.setHorizontalHeaderLabels([
                "ID", "Date", "File Name", "Uploaded By","Upload Date", "Device", "File Type", "Status", "Contracts"
            ])
            self.search_table.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
            self.search_table.setSelectionMode(QAbstractItemView.SelectionMode.ExtendedSelection)
            self.search_table.setColumnHidden(0, True)
            self.search_table.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOn)
            self.search_table.setHorizontalScrollMode(QTableWidget.ScrollMode.ScrollPerPixel)
            self.search_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Interactive)
            self.search_table.horizontalHeader().setStretchLastSection(False)
            self.search_table.setWordWrap(False)
            layout.addWidget(self.search_table)

            # --- دکمه‌های پایین (بدون تغییر) ---
            btn_layout = QHBoxLayout()
            btn_layout.addStretch()

            pivoted_btn = QPushButton("Open Pivoted File")
            pivoted_btn.clicked.connect(lambda: self.load_pivoted_file())
            btn_layout.addWidget(pivoted_btn)

            select_btn = QPushButton("Select")
            select_btn.clicked.connect(lambda: self.select_and_load_file(dialog))
            btn_layout.addWidget(select_btn)

            cancel_btn = QPushButton("Cancel")
            cancel_btn.clicked.connect(dialog.reject)
            btn_layout.addWidget(cancel_btn)

            layout.addLayout(btn_layout)

            # بارگذاری اولیه
            self.load_filtered_files(dialog)
            dialog.exec()

        except Exception as e:
            logger.error(f"Error opening existing files dialog: {e}")
            QMessageBox.warning(self, "Error", f"Failed to open dialog: {e}")

    def load_pivoted_file(self):
        file_path, _ = QFileDialog.getOpenFileName(
            self, "Open Pivoted File", "", "Data Files (*.xlsx *.xls *.csv)"
        )
        if not file_path:
            return

        try:
            progress = QProgressDialog("Loading pivoted file...", "Cancel", 0, 100, self)
            progress.setWindowTitle("Loading...")
            progress.setWindowModality(Qt.WindowModality.WindowModal)
            progress.setMinimumDuration(0)
            progress.show()

            # مهم: is_pivoted=True
            worker = FileLoaderThread(file_path, self, is_pivoted=True)

            def on_progress(v, msg):
                progress.setValue(v)
                progress.setLabelText(msg)
                if progress.wasCanceled():
                    worker.cancel()

            def on_finished(df, path):
                try:
                    if df is None or df.empty:
                        QMessageBox.warning(self, "Invalid File", "Pivoted file is empty or invalid.")
                        return

                    # --- ۱. آپدیت داده اصلی از main_window ---
                    self.main_window.data = df.copy()                     # این خط درست
                    self.main_window.file_path = path                     # برای عنوان

                    # --- ۲. تنظیم data_type در ResultsFrame ---
                    if hasattr(self.main_window, 'results') and self.main_window.results:
                        self.main_window.results.data_type = 'wide'       # این خط حیاتی
                        self.main_window.results.last_filtered_data = df.copy()
                        self.main_window.results.show_processed_data()    # نمایش مستقیم

                    # --- ۳. آپدیت PivotTab ---
                    if hasattr(self.main_window, 'pivot_tab') and self.main_window.pivot_tab:
                        self.main_window.pivot_tab.pivot_data = df.copy()
                        self.main_window.pivot_tab.update_pivot_display()

                    # --- ۴. آپدیت عنوان ---
                    _, clean_name = self.parse_filename(os.path.basename(path))
                    self.main_window.file_path_label.setText(f"Pivoted: {clean_name}")
                    self.main_window.setWindowTitle(f"RASF Data Processor - Pivoted: {clean_name}")

                    progress.setValue(100)
                    QMessageBox.information(self, "Success", f"Pivoted file loaded:\n{clean_name}")

                except Exception as e:
                    logger.error(f"Pivoted file load failed: {e}")
                    QMessageBox.critical(self, "Error", f"Failed to load pivoted file:\n{e}")
                finally:
                    progress.close()

            def on_error(msg):
                progress.close()
                QMessageBox.critical(self, "Error", f"Failed to read file:\n{msg}")

            worker.progress.connect(on_progress)
            worker.finished.connect(on_finished)
            worker.error.connect(on_error)
            worker.start()

        except Exception as e:
            logger.error(f"Unexpected error in load_pivoted_file: {e}")
            QMessageBox.critical(self, "Error", f"Unexpected error:\n{e}")

    def load_filtered_files(self, dialog):
        try:
            conn = sqlite3.connect(self.main_window.resource_path("crm_data.db"))
            cur = conn.cursor()
            query = """
                SELECT uf.id, uf.original_filename, u.full_name, d.name, uf.file_type, 
                       uf.is_archived, uf.contracts, uf.created_at
                FROM uploaded_files uf
                JOIN users u ON uf.uploaded_by = u.id
                JOIN devices d ON uf.device_id = d.id
                WHERE u.role = 'device_operator'
            """
            params = []

            # فیلتر نوع فایل
            if self.file_type_combo.currentText() != "All":
                query += " AND uf.file_type = ?"
                params.append(self.file_type_combo.currentText())

            # وضعیت آرشیو
            status = self.status_combo.currentText()
            if status == "Active":
                query += " AND uf.is_archived = 0"
            elif status == "Archived":
                query += " AND uf.is_archived = 1"

            # قرارداد
            contract = self.contract_search.text().strip()
            if contract:
                query += " AND uf.contracts LIKE ?"
                params.append(f"%{contract}%")

            # مرتب‌سازی بر اساس تاریخ جلالی (از نام فایل)
            query += " ORDER BY uf.original_filename DESC"

            cur.execute(query, params)
            all_files = cur.fetchall()
            conn.close()

            # فیلتر تاریخ جلالی
            jalali_from = self.jalali_from_edit.text().strip()
            jalali_to = self.jalali_to_edit.text().strip()
            filtered_files = []
            for row in all_files:
                jalali_date, _ = self.parse_filename(row[1])
                if not jalali_date:
                    jalali_date = "0000-00-00"  # فایل‌های بدون تاریخ همیشه نمایش داده بشن

                include = True
                if jalali_from and jalali_date < jalali_from:
                    include = False
                if jalali_to and jalali_date > jalali_to:
                    include = False

                if include:
                    filtered_files.append(row)

            # نمایش در جدول
            self.search_table.setRowCount(len(filtered_files))
            header = self.search_table.horizontalHeader()
            header.setSectionResizeMode(1, QHeaderView.ResizeMode.ResizeToContents)
            header.setSectionResizeMode(2, QHeaderView.ResizeMode.Interactive)
            header.setSectionResizeMode(7, QHeaderView.ResizeMode.ResizeToContents)
            header.setSectionResizeMode(8, QHeaderView.ResizeMode.ResizeToContents)  # ستون جدید
            header.resizeSection(7, 350)
            header.resizeSection(8, 120)  # عرض مناسب برای تاریخ جلالی

            for i, row in enumerate(filtered_files):
                jalali_date, clean_name = self.parse_filename(row[1])
                status_text = "Archived" if row[5] else "Active"
                upload_date_jalali = self.miladi_to_jalali(row[7])  # تبدیل تاریخ آپلود به جلالی
                self.search_table.setItem(i, 0, QTableWidgetItem(str(row[0])))
                self.search_table.setItem(i, 1, QTableWidgetItem(jalali_date or "N/A"))
                self.search_table.setItem(i, 2, QTableWidgetItem(clean_name))
                self.search_table.setItem(i, 3, QTableWidgetItem(row[2]))
                self.search_table.setItem(i, 4, QTableWidgetItem(upload_date_jalali))  # ستون جدید
                self.search_table.setItem(i, 5, QTableWidgetItem(row[3]))
                self.search_table.setItem(i, 6, QTableWidgetItem(row[4]))  # File Type
                self.search_table.setItem(i, 7, QTableWidgetItem(status_text))
                self.search_table.setItem(i, 8, QTableWidgetItem(row[6] or ""))

            logger.info(f"Loaded {len(filtered_files)} files with Jalali upload date")
        except Exception as e:
            logger.error(f"load_filtered_files error: {e}")
            QMessageBox.critical(dialog, "خطا", f"بارگذاری فایل‌ها ناموفق:\n{e}")

    def select_and_load_file(self, dialog):
        selected_indexes = self.search_table.selectionModel().selectedRows()
        if not selected_indexes:
            QMessageBox.warning(dialog, "No Selection", "Please select at least one file.")
            return

        file_paths = []
        clean_names = []
        for index in selected_indexes:
            row = index.row()
            file_id = int(self.search_table.item(row, 0).text())
            clean_name = self.search_table.item(row, 2).text()
            try:
                conn = sqlite3.connect(self.main_window.resource_path("crm_data.db"))
                cur = conn.cursor()
                cur.execute("SELECT file_path FROM uploaded_files WHERE id = ?", (file_id,))
                result = cur.fetchone()
                conn.close()
                if result and os.path.exists(result[0]):
                    file_paths.append(result[0])
                    clean_names.append(clean_name)
                else:
                    QMessageBox.warning(self, "File Error", f"File for {clean_name} not found or inaccessible. Skipping.")
            except Exception as e:
                logger.error(f"Error fetching file path for ID {file_id}: {e}")
                QMessageBox.warning(self, "Error", f"Failed to fetch file path for {clean_name}: {e}. Skipping.")

        if not file_paths:
            return

        dialog.accept()  # بستن دیالوگ انتخاب
        self.chain_load(file_paths, clean_names)

    def chain_load(self, file_paths, clean_names, index=0):
        if index >= len(file_paths):
            # به‌روزرسانی نهایی UI پس از لود همه فایل‌ها
            try:
                self.main_window.notify_data_changed()

                if hasattr(self.main_window, 'elements_tab') and self.main_window.elements_tab:
                    self.main_window.elements_tab.process_blk_elements()

                if hasattr(self.main_window, 'pivot_tab') and self.main_window.pivot_tab:
                    PivotCreator(self.main_window.pivot_tab).create_pivot()  # پیوت نهایی

                if hasattr(self.main_window, 'results') and hasattr(self.main_window.results, 'show_processed_data'):
                    self.main_window.results.show_processed_data()

                combined_names = ' + '.join(clean_names)
                self.main_window.file_path_label.setText(f"Files: {combined_names}")
                self.main_window.setWindowTitle(f"RASF Data Processor - {combined_names}")

                QMessageBox.information(self, "Success", "All selected files loaded and concatenated successfully.")

            except Exception as e:
                logger.error(f"Final UI update failed after loading multiple files: {e}")
                QMessageBox.warning(self, "Error", f"Failed to finalize UI update: {e}")
            return

        file_path = file_paths[index]
        is_first = (index == 0)

        progress_dialog = QProgressDialog(f"Loading file {index + 1}/{len(file_paths)}: {os.path.basename(file_path)}...", "Cancel", 0, 100, self)
        progress_dialog.setWindowTitle("Processing")
        progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)
        progress_dialog.setMinimumDuration(0)
        progress_dialog.show()

        worker = FileLoaderThread(file_path, self)

        def on_progress(value, message):
            progress_dialog.setValue(value)
            progress_dialog.setLabelText(f"Loading file {index + 1}/{len(file_paths)}: {os.path.basename(file_path)}..."+"\n"+message )
            if progress_dialog.wasCanceled():
                worker.cancel()

        def on_finished(df, loaded_file_path):
            try:
                if df is None or not isinstance(df, pd.DataFrame):
                    raise ValueError("Loaded DataFrame is None or invalid")
                if df.empty:
                    raise ValueError("Loaded DataFrame is empty")


                _, clean_name = self.parse_filename(os.path.basename(loaded_file_path))

                # --- مرحله ۱: محاسبه تعداد سطرهای پیوت این فایل تنها ---
                original_data = self.main_window.data  # ذخیره وضعیت فعلی
                original_init_data = self.main_window.init_data

                # فقط داده این فایل رو موقتاً ست کن
                self.main_window.data = df.copy()
                self.main_window.init_data = df.copy()

                # پیوت این فایل تنها رو بساز
                pivot_creator = PivotCreator(self.main_window.pivot_tab)
                pivot_creator.create_pivot()  # هیچ چیزی return نمی‌کنه، فقط pivot_data رو پر می‌کنه

                # حالا تعداد سطرهای پیوت رو از pivot_data بگیر
                current_pivot_df = self.main_window.pivot_tab.pivot_data
                pivot_row_count = len(current_pivot_df) if current_pivot_df is not None and not current_pivot_df.empty else 0

                # fallback: اگر به هر دلیلی pivot_data ساخته نشد
                if pivot_row_count == 0 and 'Solution Label' in df.columns:
                    pivot_row_count = df[df['Type'].isin(['Samp', 'Sample'])]['Solution Label'].nunique()
                print(clean_name,pivot_row_count)
                # --- مرحله ۲: محاسبه start_pivot_row ---
                if not hasattr(self.main_window, 'file_ranges'):
                    self.main_window.file_ranges = []

                current_start = sum(fr.get('pivot_row_count', 0) for fr in self.main_window.file_ranges)

                # === CRM Detection - Your Lab's Exact Regex ===
                crm_ids = '258|252|906|506|233|255|263|260'
                crm_pattern = re.compile(rf'(?i)(?:^|\s)(?:CRM|OREAS)?\s*({crm_ids})(?:[a-zA-Z]{{0,2}})?\b')
                labels = df['Solution Label'].dropna().astype(str)
                has_crm = labels.str.contains(crm_pattern, regex=True).any()

                if len(self.main_window.file_ranges) >=1 and not has_crm :
                    prev_name=self.main_window.file_ranges[-1]["clean_name"]
                    reply = QMessageBox.question(
                        self,
                        "No CRM Detected",
                        f"<b>No CRM found</b> in:\n\n<b>{clean_name}</b>\n\n"
                        f"Append to previous file?\n→ <b>{prev_name}</b>",
                        QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
                        QMessageBox.StandardButton.Yes
                    )
                    append_to_previous = (reply == QMessageBox.StandardButton.Yes)
                    if append_to_previous :
                        self.main_window.file_ranges[-1]["file_path"]+=" + "+ loaded_file_path
                        self.main_window.file_ranges[-1]["clean_name"]+=" + "+ clean_name
                        self.main_window.file_ranges[-1]["end_pivot_row"]+=pivot_row_count 
                        self.main_window.file_ranges[-1]["pivot_row_count"]+=pivot_row_count
                else:
                    # --- مرحله ۳: ذخیره در file_ranges ---
                    self.main_window.file_ranges.append({
                        "file_path": loaded_file_path,
                        "clean_name": clean_name,
                        "start_pivot_row": current_start,
                        "end_pivot_row": current_start + pivot_row_count - 1 if pivot_row_count > 0 else current_start,
                        "pivot_row_count": pivot_row_count,
                    })

                logger.debug(f"[Pivot Range] {clean_name}: {pivot_row_count} rows → "
                            f"rows {current_start}–{current_start + pivot_row_count - 1}")


                print(self.main_window.file_ranges)
                # --- مرحله ۴: برگرداندن داده اصلی + concat ---
                self.main_window.data = original_data
                self.main_window.init_data = original_init_data

                if is_first:
                    self.main_window.reset_app_state()
                    self.main_window.data = df.copy()
                    self.main_window.init_data = df.copy()
                    self.main_window.file_path = loaded_file_path
                    self.main_window.file_ranges.append({
                    "file_path": loaded_file_path,
                    "clean_name": clean_name,
                    "start_pivot_row": current_start,
                    "end_pivot_row": current_start + pivot_row_count - 1 if pivot_row_count > 0 else current_start,
                    "pivot_row_count": pivot_row_count,
                })
                else:
                    if self.main_window.data is None:
                        self.main_window.data = df.copy()
                        self.main_window.init_data = df.copy()
                    else:
                        self.main_window.data = pd.concat([self.main_window.data, df], ignore_index=True)
                        self.main_window.init_data = self.main_window.data.copy()

                progress_dialog.close()
                self.chain_load(file_paths, clean_names, index + 1)

            except Exception as e:
                progress_dialog.close()
                logger.error(f"Error processing file {os.path.basename(loaded_file_path)}: {e}")
                QMessageBox.critical(self, "خطا", f"فایل پردازش نشد:\n{e}")

        def on_error(error_message):
            progress_dialog.close()
            logger.error(f"Failed to load file {os.path.basename(file_path)}: {error_message}")
            QMessageBox.critical(self, "Error", f"Failed to load file {os.path.basename(file_path)}:\n{error_message}")

        worker.progress.connect(on_progress)
        worker.finished.connect(on_finished)
        worker.error.connect(on_error)
        worker.start()

    def load_uploaded_files_list(self):
        try:
            db_path = self.main_window.resource_path("crm_data.db")
            conn = sqlite3.connect(db_path)
            query = """
                SELECT uf.id, uf.original_filename, u.full_name, d.name, uf.created_at, uf.is_archived
                FROM uploaded_files uf
                JOIN users u ON uf.uploaded_by = u.id
                JOIN devices d ON uf.device_id = d.id
                WHERE u.role = 'device_operator'
                ORDER BY uf.created_at DESC
            """
            df = pd.read_sql_query(query, conn)
            conn.close()

            self.files_table.setRowCount(len(df))
            header = self.files_table.horizontalHeader()
            header.setSectionResizeMode(1, QHeaderView.ResizeMode.ResizeToContents)
            header.setSectionResizeMode(2, QHeaderView.ResizeMode.Interactive)

            for i, row in df.iterrows():
                jalali_date, clean_name = self.parse_filename(row['original_filename'])
                archive_status = "Archived" if row['is_archived'] else "Active"

                self.files_table.setItem(i, 0, QTableWidgetItem(str(row['id'])))
                self.files_table.setItem(i, 1, QTableWidgetItem(jalali_date or "N/A"))
                self.files_table.setItem(i, 2, QTableWidgetItem(clean_name))
                self.files_table.setItem(i, 3, QTableWidgetItem(row['full_name']))
                self.files_table.setItem(i, 4, QTableWidgetItem(row['name']))
                self.files_table.setItem(i, 5, QTableWidgetItem(row['created_at'][:19]))
                self.files_table.setItem(i, 6, QTableWidgetItem(archive_status))

            logger.info(f"Loaded {len(df)} uploaded files")

        except Exception as e:
            logger.error(f"Failed to load files: {e}")
            QMessageBox.critical(self, "Error", f"Failed to load files:\n{e}")