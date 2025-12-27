from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QPushButton, QTableView, QHeaderView, 
    QGroupBox, QMessageBox, QLineEdit, QLabel, QComboBox, QFileDialog,
    QDialog, QRadioButton, QCheckBox,QLineEdit
)
from PyQt6.QtCore import pyqtSignal
import pandas as pd
import re
import logging
import sqlite3
from ..Common.Freeze_column import FreezeTableWidget
from .pivot_table_model import PivotTableModel
from .verification.pivot_plot_dialog import PivotPlotWindow

# Setup logging
logging.basicConfig(level=logging.DEBUG, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

# Global stylesheet
global_style = """
    QWidget {
        background-color: #F5F7FA;
        font-family: 'Inter', 'Segoe UI', sans-serif;
        font-size: 13px;
    }
    QGroupBox {
        font-weight: bold;
        color: #1A3C34;
        margin-top: 15px;
        border: 1px solid #D0D7DE;
        border-radius: 6px;
        padding: 10px;
    }
    QGroupBox::title {
        subcontrol-origin: margin;
        subcontrol-position: top left;
        padding: 0 5px;
        left: 10px;
    }
    QPushButton {
        background-color: #2E7D32;
        color: white;
        border: none;
        padding: 8px 16px;
        font-weight: 600;
        font-size: 13px;
        border-radius: 6px;
    }
    QPushButton:hover {
        background-color: #1B5E20;
    }
    QPushButton:disabled {
        background-color: #E0E0E0;
        color: #6B7280;
    }
    QTableView {
        background-color: #FFFFFF;
        border: 1px solid #D0D7DE;
        gridline-color: #E5E7EB;
        font-size: 12px;
        selection-background-color: #DBEAFE;
        selection-color: #1A3C34;
    }
    QHeaderView::section {
        background-color: #F9FAFB;
        font-weight: 600;
        color: #1A3C34;
        border: 1px solid #D0D7DE;
        padding: 6px;
    }
    QTableView::item:selected {
        background-color: #DBEAFE;
        color: #1A3C34;
    }
    QTableView::item {
        padding: 0px;
    }
    QLineEdit {
        padding: 6px;
        border: 1px solid #D0D7DE;
        border-radius: 4px;
        font-size: 13px;
    }
    QLineEdit:focus {
        border: 1px solid #2E7D32;
    }
    QLabel {
        font-size: 13px;
        color: #1A3C34;
    }
    QComboBox {
        padding: 6px;
        border: 1px solid #D0D7DE;
        border-radius: 4px;
        font-size: 13px;
    }
    QComboBox:focus {
        border: 1px solid #2E7D32;
    }
"""

class CrmCheck(QWidget):
    data_changed = pyqtSignal()

    def __init__(self, app, results_frame, parent=None):
        super().__init__(parent)
        self.app = app
        self.results_frame = results_frame
        self.corrected_crm = {}  # Store CRM corrections: {element: {solution_label: {'scale': scale, 'blank': blank}}}
        self._inline_crm_rows = {}
        self._inline_crm_rows_display = {}
        self.included_crms = {}
        self.column_widths = {}
        self.crm_manager = CRMManager(self)
        self.crm_diff_min = QLineEdit("-12")
        self.crm_diff_max = QLineEdit("12")
        self.current_plot_window = None
        self.setup_ui()
        self.results_frame.app.notify_data_changed = self.on_data_changed
        if hasattr(self.results_frame, 'decimal_combo') and self.results_frame.decimal_combo is not None:
            self.results_frame.decimal_combo.currentTextChanged.connect(self.update_pivot_display)
        else:
            logger.error("decimal_combo is not defined in ResultsFrame")
            raise AttributeError("decimal_combo is not defined in ResultsFrame")

    def setup_ui(self):
        """Set up the UI with styling matching EmptyCheckFrame."""
        self.setStyleSheet(global_style)
        main_layout = QVBoxLayout(self)
        main_layout.setContentsMargins(15, 15, 15, 15)
        main_layout.setSpacing(15)

        # Control group
        control_group = QGroupBox("Pivot Controls")
        control_layout = QHBoxLayout(control_group)
        control_layout.setSpacing(10)

        # Check CRM button
        check_crm_btn = QPushButton("Check CRM")
        check_crm_btn.setMinimumWidth(80)
        check_crm_btn.clicked.connect(self.crm_manager.check_rm)
        control_layout.addWidget(check_crm_btn)

        # Manual CRM button
        manual_crm_btn = QPushButton("Manual CRM")
        manual_crm_btn.setMinimumWidth(80)
        manual_crm_btn.clicked.connect(self.manual_crm_selection)
        control_layout.addWidget(manual_crm_btn)

        # CRM Range input
        crm_range_label = QLabel("CRM Range (%):")
        control_layout.addWidget(crm_range_label)
        self.crm_diff_min.setFixedWidth(50)
        self.crm_diff_min.textChanged.connect(self.validate_crm_diff_range)
        control_layout.addWidget(self.crm_diff_min)
        control_layout.addWidget(QLabel("to"))
        self.crm_diff_max.setFixedWidth(50)
        self.crm_diff_max.textChanged.connect(self.validate_crm_diff_range)
        control_layout.addWidget(self.crm_diff_max)

        # Decimal places input
        decimal_label = QLabel("Decimal Places:")
        control_layout.addWidget(decimal_label)
        if hasattr(self.results_frame, 'decimal_combo') and self.results_frame.decimal_combo is not None:
            self.results_frame.decimal_combo.setFixedWidth(60)
            self.results_frame.decimal_combo.setToolTip("Set the number of decimal places for numeric values")
            control_layout.addWidget(self.results_frame.decimal_combo)
        else:
            logger.warning("decimal_combo not available, creating a local one")
            local_decimal_combo = QComboBox()
            local_decimal_combo.addItems(["0", "1", "2", "3"])
            local_decimal_combo.setCurrentText("1")
            local_decimal_combo.setFixedWidth(60)
            local_decimal_combo.setToolTip("Set the number of decimal places for numeric values")
            local_decimal_combo.currentTextChanged.connect(self.update_pivot_display)
            control_layout.addWidget(local_decimal_combo)

        # Clear CRM button
        clear_crm_btn = QPushButton("Clear CRM")
        clear_crm_btn.setMinimumWidth(80)
        clear_crm_btn.clicked.connect(self.clear_inline_crm)
        control_layout.addWidget(clear_crm_btn)

        # Calib button
        calib_btn = QPushButton("Calib")
        calib_btn.setMinimumWidth(80)
        calib_btn.clicked.connect(self.show_element_plot)
        control_layout.addWidget(calib_btn)

        # Export button
        export_btn = QPushButton("Export")
        export_btn.setMinimumWidth(80)
        export_btn.clicked.connect(self.export_table)
        control_layout.addWidget(export_btn)

        control_layout.addStretch()
        main_layout.addWidget(control_group)

        # Table group
        table_group = QGroupBox("Pivot Table")
        table_layout = QVBoxLayout(table_group)
        table_layout.setSpacing(10)

        # Table view
        self.table_view = FreezeTableWidget(PivotTableModel(self))
        self.table_view.setAlternatingRowColors(True)
        self.table_view.setSelectionBehavior(QTableView.SelectionBehavior.SelectRows)
        self.table_view.setSortingEnabled(True)
        self.table_view.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Interactive)
        table_layout.addWidget(self.table_view)

        main_layout.addWidget(table_group, stretch=1)

    def manual_crm_selection(self):
        """Open a dialog to manually select a CRM for a selected row."""
        selected_rows = self.table_view.selectionModel().selectedRows()
        if not selected_rows:
            QMessageBox.warning(self, "Warning", "Please select a row to assign a CRM!")
            logger.warning("No row selected for manual CRM assignment")
            return

        if len(selected_rows) > 1:
            QMessageBox.warning(self, "Warning", "Please select only one row!")
            logger.warning("Multiple rows selected for manual CRM assignment")
            return

        row_index = selected_rows[0].row()
        model = self.table_view.model()
        if not model:
            QMessageBox.warning(self, "Warning", "No data available in table!")
            logger.warning("No data available in table for manual CRM")
            return

        solution_label = model.data(model.index(row_index, 0))
        if not solution_label:
            QMessageBox.warning(self, "Warning", "Invalid row selected!")
            logger.warning("Invalid row selected for manual CRM")
            return

        self.crm_manager.open_manual_crm_dialog(solution_label)
        self.update_pivot_display()
        self.data_changed.emit()

    def on_data_changed(self):
        """Update pivot table when data in ResultsFrame changes."""
        logger.debug("Data changed in ResultsFrame, updating pivot display")
        self.update_pivot_display()

    def validate_crm_diff_range(self):
        """Validate CRM difference range inputs and update display."""
        try:
            min_val = float(self.crm_diff_min.text())
            max_val = float(self.crm_diff_max.text())
            if min_val > max_val:
                self.crm_diff_min.setText(str(max_val - 1))
            logger.debug(f"CRM diff range set to {min_val} to {max_val}")
            self.crm_manager.check_rm_with_diff_range(min_val, max_val)
            self.update_pivot_display()
            self.data_changed.emit()
        except ValueError:
            logger.debug("Invalid CRM diff range input, skipping update")
            pass

    def update_pivot_display(self):
        """Update the pivot table display using ResultsFrame's last_filtered_data."""
        logger.debug("Starting update_pivot_display")
        pivot_data = self.results_frame.last_filtered_data

        if pivot_data is None or pivot_data.empty:
            logger.warning("No data loaded for pivot display")
            self.table_view.setModel(None)
            self.table_view.frozenTableView.setModel(None)
            return

        logger.debug(f"Current view data shape: {pivot_data.shape}")
        self._inline_crm_rows_display = self.crm_manager._build_crm_row_lists_for_columns(list(pivot_data.columns))
        combined_rows = []
        for sol_label in pivot_data['Solution Label']:
            if sol_label in self._inline_crm_rows_display:
                combined_rows.append((sol_label, self._inline_crm_rows_display[sol_label]))

        model = PivotTableModel(self, pivot_data, combined_rows)
        self.table_view.setModel(model)
        self.table_view.frozenTableView.setModel(model)
        self.table_view.update_frozen_columns()
        self.table_view.model().layoutChanged.emit()
        self.table_view.frozenTableView.model().layoutChanged.emit()
        self.table_view.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Interactive)
        for col, width in self.column_widths.items():
            if col < len(pivot_data.columns):
                self.table_view.horizontalHeader().resizeSection(col, width)
        self.table_view.viewport().update()
        logger.debug("Completed update_pivot_display")

    def clear_inline_crm(self):
        """Clear inline CRM data."""
        logger.debug("Clearing inline CRM data")
        self._inline_crm_rows.clear()
        self._inline_crm_rows_display.clear()
        self.included_crms.clear()
        self.update_pivot_display()
        self.data_changed.emit()

    def show_element_plot(self):
        """Show element calibration plot in a resizable window."""
        logger.debug("Attempting to show element plot")
        pivot_data = self.results_frame.last_filtered_data
        if pivot_data is None or pivot_data.empty:
            logger.warning("No data to plot")
            QMessageBox.warning(self, "Warning", "No data to plot!")
            return
        logger.debug("Opening element plot window")
        if hasattr(self, 'current_plot_window') and self.current_plot_window:
            self.current_plot_window.close()
        annotations = []
        self.current_plot_window = PivotPlotWindow(self, annotations)
        self.current_plot_window.show()

    def backup_column(self, column):
        """Backup a column before applying corrections."""
        pivot_data = self.results_frame.last_filtered_data
        if column in pivot_data.columns:
            self.results_frame.column_backups[column] = pivot_data[column].copy()
            logger.debug(f"Backed up column: {column}")
            self.data_changed.emit()

    def restore_column(self, column):
        """Restore a column from backup."""
        if column in self.results_frame.column_backups:
            self.results_frame.last_filtered_data[column] = self.results_frame.column_backups[column].copy()
            self.update_pivot_display()
            logger.debug(f"Restored column: {column}")
            del self.results_frame.column_backups[column]
            self.data_changed.emit()
        else:
            logger.warning(f"No backup found for column: {column}")
            QMessageBox.warning(self, "Warning", f"No backup found for column {column}")

    def export_table(self):
        """Export the current pivot table to a CSV or Excel file."""
        logger.debug("Attempting to export table")
        pivot_data = self.results_frame.last_filtered_data
        if pivot_data is None or pivot_data.empty:
            logger.warning("No data to export")
            QMessageBox.warning(self, "Warning", "No data to export!")
            return

        try:
            file_path, selected_filter = QFileDialog.getSaveFileName(
                self,
                "Save Pivot Table",
                "",
                "CSV Files (*.csv);;Excel Files (*.xlsx);;All Files (*)"
            )
            if not file_path:
                logger.debug("Export cancelled by user")
                return

            if selected_filter.startswith("CSV"):
                pivot_data.to_csv(file_path, index=True)
            elif selected_filter.startswith("Excel"):
                pivot_data.to_excel(file_path, index=True, engine='openpyxl')
            
            logger.debug(f"Table exported successfully to {file_path}")
            QMessageBox.information(self, "Success", f"Table exported successfully to {file_path}")
        except Exception as e:
            logger.error(f"Failed to export table: {str(e)}")
            QMessageBox.warning(self, "Error", f"Failed to export table: {str(e)}")


class CRMManager:
    """Manages CRM-related operations for the PivotTab."""
    def __init__(self, pivot_tab):
        self.pivot_tab = pivot_tab
        self.logger = logger
        self.crm_selections = {}

    def check_rm(self):
        """Check Reference Materials (RM) against the CRM database and update inline CRM rows."""
        self.crm_selections = {}
        if self.pivot_tab.results_frame.last_filtered_data is None or self.pivot_tab.results_frame.last_filtered_data.empty:
            QMessageBox.warning(self.pivot_tab, "Warning", "No pivot data available!")
            self.logger.warning("No pivot data available in check_rm")
            return

        file_id = None
        try:
            file_id = self.pivot_tab.app.management_tab.file_combo.currentData()
        except AttributeError:
            logger.warning("management_tab not available, attempting to retrieve latest file_id from database.")

        user_id = self.pivot_tab.app.user_id_from_username()

        try:
            conn = self.pivot_tab.app.crm_tab.conn
            if conn is None:
                self.pivot_tab.app.crm_tab.init_db()
                conn = self.pivot_tab.app.crm_tab.conn
                if conn is None:
                    QMessageBox.warning(self.pivot_tab, "Error", "Failed to connect to CRM database!")
                    self.logger.error("Failed to connect to CRM database")
                    return

            cursor = conn.cursor()

            if file_id is None:
                cursor.execute("SELECT id FROM uploaded_files ORDER BY created_at DESC LIMIT 1")
                result = cursor.fetchone()
                if result:
                    file_id = result[0]
                    logger.info(f"Retrieved latest file_id: {file_id}")
                else:
                    logger.warning("No uploaded files found, skipping database operations for CRM selections.")
                    # Proceed without saving to db

            crm_ids = ['258', '252', '906', '506', '233', '255', '263', '260']

            def is_crm_label(label):
                label = str(label).strip().lower()
                for crm_id in crm_ids:
                    pattern = rf'(?i)(?:(?:^|(?<=\s))(?:CRM|OREAS)?\s*{crm_id}(?:[a-zA-Z0-9]{{0,2}})?\b)'
                    if re.search(pattern, label):
                        return True
                return False

            crm_rows = self.pivot_tab.results_frame.last_filtered_data[
                self.pivot_tab.results_frame.last_filtered_data['Solution Label'].apply(is_crm_label)
            ].copy()

            if crm_rows.empty:
                QMessageBox.information(self.pivot_tab, "Info", "No CRM rows found in pivot data!")
                return

            cursor.execute("PRAGMA table_info(pivot_crm)")
            cols = [x[1] for x in cursor.fetchall()]
            required = {'CRM ID', 'Analysis Method'}
            if not required.issubset(cols):
                QMessageBox.warning(self.pivot_tab, "Error", "pivot_crm table missing required columns!")
                return

            element_to_columns = {}
            for col in self.pivot_tab.results_frame.last_filtered_data.columns:
                if col == 'Solution Label':
                    continue
                element = col.split()[0].strip()
                element_to_columns.setdefault(element, []).append(col)

            try:
                dec = int(self.pivot_tab.results_frame.decimal_combo.currentText())
            except (AttributeError, ValueError):
                self.logger.warning("decimal_combo not available or invalid, using default decimal places (1)")
                dec = 1

            self.pivot_tab._inline_crm_rows.clear()
            self.pivot_tab.included_crms.clear()

            for _, row in crm_rows.iterrows():
                label = row['Solution Label']
                found_crm_id = None
                for crm_id in crm_ids:
                    pattern = rf'(?i)(?:(?:^|(?<=\s))(?:CRM|OREAS)?\s*({crm_id}(?:[a-zA-Z0-9]{{0,2}})?)\b)'
                    m = re.search(pattern, str(label))
                    if m:
                        found_crm_id = m.group(1).strip()
                        break
                if not found_crm_id:
                    continue

                crm_id_string = f"OREAS {found_crm_id}"
                cursor.execute(
                    "SELECT * FROM pivot_crm WHERE [CRM ID] LIKE ?",
                    (f"OREAS {found_crm_id}%",)
                )
                crm_data = cursor.fetchall()
                if not crm_data:
                    continue

                cursor.execute("PRAGMA table_info(pivot_crm)")
                db_columns = [x[1] for x in cursor.fetchall()]
                non_element_columns = ['CRM ID', 'Solution Label', 'Analysis Method', 'Type']

                all_crm_options = {}
                filtered_crm_options = {}
                allowed_methods = {'4-Acid Digestion', 'Aqua Regia Digestion'}

                for db_row in crm_data:
                    crm_id = db_row[db_columns.index('CRM ID')]
                    analysis_method = db_row[db_columns.index('Analysis Method')]
                    key = f"{crm_id} ({analysis_method})"
                    all_crm_options[key] = []
                    if analysis_method in allowed_methods:
                        filtered_crm_options[key] = []

                    for col in db_columns:
                        if col in non_element_columns:
                            continue
                        value = db_row[db_columns.index(col)]
                        if value not in (None, ''):
                            try:
                                symbol = col.split('_')[0].strip()
                                val = float(value)
                                all_crm_options[key].append((symbol, val))
                                if analysis_method in allowed_methods:
                                    filtered_crm_options[key].append((symbol, val))
                            except (ValueError, TypeError):
                                continue

                selected_crm_key = self.crm_selections.get(label)
                if selected_crm_key is None and file_id is not None:
                    cursor.execute(
                        "SELECT selected_crm_key FROM crm_selections WHERE file_id = ? AND solution_label = ?",
                        (file_id, label)
                    )
                    db_result = cursor.fetchone()
                    selected_crm_key = db_result[0] if db_result else None

                if selected_crm_key is None and len(filtered_crm_options) > 1:
                    dialog = QDialog(self.pivot_tab)
                    dialog.setWindowTitle(f"Select CRM for {label}")
                    layout = QVBoxLayout(dialog)
                    layout.setSpacing(5)
                    layout.setContentsMargins(10, 10, 10, 10)
                    layout.addWidget(QLabel(f"Multiple CRMs found for {label}. Please select one:"))
                    radio_container = QWidget()
                    radio_group_layout = QVBoxLayout(radio_container)
                    radio_group_layout.setSpacing(2)
                    radio_group_layout.setContentsMargins(0, 0, 0, 0)
                    layout.addWidget(radio_container)
                    more_checkbox = QCheckBox("More")
                    layout.addWidget(more_checkbox)
                    radio_buttons = []
                    radio_button_group = []

                    def update_radio_buttons(show_all=False):
                        for rb in radio_button_group:
                            rb.setParent(None)
                        radio_button_group.clear()
                        radio_buttons.clear()
                        options = all_crm_options if show_all else filtered_crm_options
                        for key in sorted(options.keys()):
                            rb = QRadioButton(key)
                            rb.setStyleSheet("margin:0px; padding:0px;")
                            radio_group_layout.addWidget(rb)
                            radio_button_group.append(rb)
                            radio_buttons.append((key, rb))
                        if radio_buttons:
                            if selected_crm_key in options:
                                for key, rb in radio_buttons:
                                    if key == selected_crm_key:
                                        rb.setChecked(True)
                                        break
                            else:
                                radio_buttons[0][1].setChecked(True)
                        radio_container.updateGeometry()
                        layout.invalidate()
                        dialog.adjustSize()

                    update_radio_buttons(show_all=False)
                    more_checkbox.toggled.connect(lambda checked: update_radio_buttons(show_all=checked))
                    button_layout = QHBoxLayout()
                    button_layout.setSpacing(10)
                    button_layout.setContentsMargins(0, 8, 0, 0)
                    confirm_btn = QPushButton("Confirm")
                    cancel_btn = QPushButton("Cancel")
                    button_layout.addWidget(confirm_btn)
                    button_layout.addWidget(cancel_btn)
                    layout.addLayout(button_layout)

                    def on_confirm():
                        nonlocal selected_crm_key
                        for key, rb in radio_buttons:
                            if rb.isChecked():
                                selected_crm_key = key
                                break
                        self.crm_selections[label] = selected_crm_key

                        if file_id is not None:
                            cursor.execute("""
                                INSERT OR REPLACE INTO crm_selections (file_id, solution_label, selected_crm_key, selected_by)
                                VALUES (?, ?, ?, ?)
                            """, (file_id, label, selected_crm_key, user_id))
                            conn.commit()

                        dialog.accept()

                    confirm_btn.clicked.connect(on_confirm)
                    cancel_btn.clicked.connect(dialog.reject)
                    if dialog.exec() == QDialog.DialogCode.Rejected:
                        return

                if selected_crm_key is None:
                    selected_crm_key = (list(filtered_crm_options.keys())[0]
                                       if filtered_crm_options else list(all_crm_options.keys())[0])
                    self.crm_selections[label] = selected_crm_key

                    if file_id is not None:
                        cursor.execute(
                            "SELECT id FROM crm_selections WHERE file_id = ? AND solution_label = ?",
                            (file_id, label)
                        )
                        if not cursor.fetchone():
                            cursor.execute("""
                                INSERT INTO crm_selections (file_id, solution_label, selected_crm_key, selected_by)
                                VALUES (?, ?, ?, ?)
                            """, (file_id, label, selected_crm_key, user_id))
                            conn.commit()

                crm_data = all_crm_options.get(selected_crm_key, [])
                crm_dict = {symbol: grade for symbol, grade in crm_data}
                crm_values = {'Solution Label': selected_crm_key}
                for element, columns in element_to_columns.items():
                    value = crm_dict.get(element)
                    if value is not None:
                        for col in columns:
                            crm_values[col] = value

                if len(crm_values) > 1:
                    self.pivot_tab._inline_crm_rows[label] = [crm_values]
                    self.pivot_tab.included_crms[label] = QCheckBox(label, checked=True)

            if not self.pivot_tab._inline_crm_rows:
                QMessageBox.information(self.pivot_tab, "Info", "No matching CRM elements found!")
                return

            self.pivot_tab._inline_crm_rows_display = self._build_crm_row_lists_for_columns(
                list(self.pivot_tab.results_frame.last_filtered_data.columns)
            )
            self.pivot_tab.update_pivot_display()
            self.pivot_tab.data_changed.emit()
            self.logger.debug("Emitted data_changed signal after check_rm")

        except Exception as e:
            self.logger.error(f"Failed to check RM: {str(e)}")
            QMessageBox.warning(self.pivot_tab, "Error", f"Failed to check RM: {str(e)}")

    def open_manual_crm_dialog(self, solution_label):
        """Open a dialog to search and select a CRM manually."""
        file_id = None
        try:
            file_id = self.pivot_tab.app.management_tab.file_combo.currentData()
        except AttributeError:
            logger.warning("management_tab not available, attempting to retrieve latest file_id from database.")

        user_id = self.pivot_tab.app.user_id_from_username()

        try:
            conn = self.pivot_tab.app.crm_tab.conn
            if conn is None:
                self.pivot_tab.app.crm_tab.init_db()
                conn = self.pivot_tab.app.crm_tab.conn
                if conn is None:
                    QMessageBox.warning(self.pivot_tab, "Error", "Failed to connect to CRM database!")
                    self.logger.error("Failed to connect to CRM database")
                    return

            cursor = conn.cursor()

            if file_id is None:
                cursor.execute("SELECT id FROM uploaded_files ORDER BY created_at DESC LIMIT 1")
                result = cursor.fetchone()
                if result:
                    file_id = result[0]
                    logger.info(f"Retrieved latest file_id: {file_id}")
                else:
                    logger.warning("No uploaded files found, skipping database operations for CRM selections.")
                    # Proceed without saving to db

            # Check for existing selection
            if file_id is not None:
                cursor.execute(
                    "SELECT selected_crm_key FROM crm_selections WHERE file_id = ? AND solution_label = ?",
                    (file_id, solution_label)
                )
                db_result = cursor.fetchone()
                if db_result:
                    selected_crm_key = db_result[0]
                    self.add_manual_crm(solution_label, selected_crm_key)
                    return  # If exists, use it without dialog

            cursor.execute("SELECT DISTINCT [CRM ID] FROM pivot_crm WHERE [CRM ID] LIKE 'OREAS%'")
            crm_ids = [row[0] for row in cursor.fetchall()]

            if not crm_ids:
                QMessageBox.warning(self.pivot_tab, "Warning", "No CRMs found in the database!")
                self.logger.warning("No CRMs found in the database")
                return

            dialog = QDialog(self.pivot_tab)
            dialog.setWindowTitle(f"Select CRM for {solution_label}")
            layout = QVBoxLayout(dialog)
            layout.setSpacing(5)
            layout.setContentsMargins(10, 10, 10, 10)

            # Search input and button
            search_layout = QHBoxLayout()
            search_label = QLabel("Search OREAS:")
            search_input = QLineEdit()
            search_input.setPlaceholderText("Enter OREAS ID (e.g., 258)")
            search_button = QPushButton("Search")
            search_layout.addWidget(search_label)
            search_layout.addWidget(search_input)
            search_layout.addWidget(search_button)
            layout.addLayout(search_layout)

            # CRM selection
            radio_container = QWidget()
            radio_group_layout = QVBoxLayout(radio_container)
            radio_group_layout.setSpacing(2)
            radio_group_layout.setContentsMargins(0, 0, 0, 0)
            layout.addWidget(radio_container)

            radio_buttons = []
            radio_button_group = []

            def update_crm_list():
                for rb in radio_button_group:
                    rb.setParent(None)
                radio_button_group.clear()
                radio_buttons.clear()

                search_text = search_input.text().strip()
                if not search_text:  # If search is empty, show no CRMs
                    radio_container.updateGeometry()
                    layout.invalidate()
                    dialog.adjustSize()
                    confirm_btn.setEnabled(False)
                    return

                filtered_crms = [crm_id for crm_id in crm_ids if search_text.lower() in crm_id.lower()]
                for crm_id in sorted(filtered_crms):
                    cursor.execute(
                        "SELECT DISTINCT [Analysis Method] FROM pivot_crm WHERE [CRM ID] = ?",
                        (crm_id,)
                    )
                    methods = [row[0] for row in cursor.fetchall()]
                    for method in sorted(methods):
                        key = f"{crm_id} ({method})"
                        rb = QRadioButton(key)
                        rb.setStyleSheet("margin:0px; padding:0px;")
                        radio_group_layout.addWidget(rb)
                        radio_button_group.append(rb)
                        radio_buttons.append((key, rb))

                if radio_buttons:
                    radio_buttons[0][1].setChecked(True)
                    confirm_btn.setEnabled(True)
                else:
                    confirm_btn.setEnabled(False)
                radio_container.updateGeometry()
                layout.invalidate()
                dialog.adjustSize()

            # Connect search button to update_crm_list
            search_button.clicked.connect(update_crm_list)

            # Buttons
            button_layout = QHBoxLayout()
            button_layout.setSpacing(10)
            button_layout.setContentsMargins(0, 8, 0, 0)
            confirm_btn = QPushButton("Confirm")
            confirm_btn.setEnabled(False)  # Disable Confirm button initially
            cancel_btn = QPushButton("Cancel")
            button_layout.addWidget(confirm_btn)
            button_layout.addWidget(cancel_btn)
            layout.addLayout(button_layout)

            def on_confirm():
                selected_crm_key = None
                for key, rb in radio_buttons:
                    if rb.isChecked():
                        selected_crm_key = key
                        break
                if selected_crm_key:
                    self.crm_selections[solution_label] = selected_crm_key

                    if file_id is not None:
                        cursor.execute("""
                            INSERT OR REPLACE INTO crm_selections (file_id, solution_label, selected_crm_key, selected_by)
                            VALUES (?, ?, ?, ?)
                        """, (file_id, solution_label, selected_crm_key, user_id))
                        conn.commit()

                    self.add_manual_crm(solution_label, selected_crm_key)
                dialog.accept()

            confirm_btn.clicked.connect(on_confirm)
            cancel_btn.clicked.connect(dialog.reject)
            dialog.exec()

        except Exception as e:
            self.logger.error(f"Failed to open manual CRM dialog: {str(e)}")
            QMessageBox.warning(self.pivot_tab, "Error", f"Failed to open manual CRM dialog: {str(e)}")

    def add_manual_crm(self, solution_label, selected_crm_key):
        """Add manually selected CRM to the pivot table."""
        try:
            conn = self.pivot_tab.app.crm_tab.conn
            cursor = conn.cursor()
            cursor.execute("PRAGMA table_info(pivot_crm)")
            db_columns = [x[1] for x in cursor.fetchall()]
            non_element_columns = ['CRM ID', 'Solution Label', 'Analysis Method', 'Type']

            crm_id = selected_crm_key.split(' (')[0]
            cursor.execute(
                "SELECT * FROM pivot_crm WHERE [CRM ID] = ? AND [Analysis Method] = ?",
                (crm_id, selected_crm_key.split(' (')[1][:-1])
            )
            crm_data = cursor.fetchall()
            if not crm_data:
                self.logger.warning(f"No data found for CRM {selected_crm_key}")
                return

            element_to_columns = {}
            for col in self.pivot_tab.results_frame.last_filtered_data.columns:
                if col == 'Solution Label':
                    continue
                element = col.split()[0].strip()
                element_to_columns.setdefault(element, []).append(col)

            crm_dict = {}
            for db_row in crm_data:
                for col in db_columns:
                    if col in non_element_columns:
                        continue
                    value = db_row[db_columns.index(col)]
                    if value not in (None, ''):
                        try:
                            symbol = col.split('_')[0].strip()
                            val = float(value)
                            crm_dict[symbol] = val
                        except (ValueError, TypeError):
                            continue

            crm_values = {'Solution Label': selected_crm_key}
            for element, columns in element_to_columns.items():
                value = crm_dict.get(element)
                if value is not None:
                    for col in columns:
                        crm_values[col] = value

            if len(crm_values) > 1:
                self.pivot_tab._inline_crm_rows[solution_label] = [crm_values]
                self.pivot_tab.included_crms[solution_label] = QCheckBox(solution_label, checked=True)
                self.pivot_tab._inline_crm_rows_display = self._build_crm_row_lists_for_columns(
                    list(self.pivot_tab.results_frame.last_filtered_data.columns)
                )
                self.pivot_tab.update_pivot_display()
                self.pivot_tab.data_changed.emit()
                self.logger.debug(f"Manually added CRM {selected_crm_key} for {solution_label}")

        except Exception as e:
            self.logger.error(f"Failed to add manual CRM: {str(e)}")
            QMessageBox.warning(self.pivot_tab, "Error", f"Failed to add manual CRM: {str(e)}")

    def check_rm_with_diff_range(self, min_diff, max_diff):
        """Update CRM rows display based on the specified difference range."""
        self.pivot_tab._inline_crm_rows_display = self._build_crm_row_lists_for_columns(
            list(self.pivot_tab.results_frame.last_filtered_data.columns)
        )
        self.pivot_tab.update_pivot_display()
        self.pivot_tab.data_changed.emit()
        self.logger.debug("Emitted data_changed signal after check_rm_with_diff_range")

    def _build_crm_row_lists_for_columns(self, columns):
        """Build CRM row lists for display."""
        crm_display = {}
        try:
            dec = int(self.pivot_tab.results_frame.decimal_combo.currentText())
        except (AttributeError, ValueError):
            self.logger.warning("decimal_combo not available or invalid, using default decimal places (1)")
            dec = 1

        try:
            min_diff = float(self.pivot_tab.crm_diff_min.text())
            max_diff = float(self.pivot_tab.crm_diff_max.text())
        except ValueError:
            min_diff, max_diff = -12, 12

        for sol_label, list_of_dicts in self.pivot_tab._inline_crm_rows.items():
            crm_display[sol_label] = []
            pivot_row = self.pivot_tab.results_frame.last_filtered_data[
                self.pivot_tab.results_frame.last_filtered_data['Solution Label'].str.strip().str.lower() == sol_label.strip().lower()
            ]
            if pivot_row.empty:
                continue
            pivot_values = pivot_row.iloc[0].to_dict()

            for d in list_of_dicts:
                crm_row_list = []
                for col in columns:
                    if col == 'Solution Label':
                        crm_row_list.append(f"{d.get('Solution Label', sol_label)} CRM")
                    else:
                        val = d.get(col, "")
                        if pd.isna(val) or val == "":
                            crm_row_list.append("")
                        else:
                            try:
                                crm_row_list.append(f"{float(val):.{dec}f}")
                            except Exception:
                                crm_row_list.append(str(val))
                crm_display[sol_label].append((crm_row_list, ["crm"] * len(columns)))

                diff_row_list = []
                diff_tags = []
                for col in columns:
                    if col == 'Solution Label':
                        diff_row_list.append(f"{sol_label} Diff (%)")
                        diff_tags.append("diff")
                    else:
                        pivot_val = pivot_values.get(col, None)
                        crm_val = d.get(col, None)
                        if pivot_val is not None and crm_val is not None:
                            try:
                                pivot_val = float(pivot_val)
                                crm_val = float(crm_val)
                                if crm_val != 0:
                                    diff = ((crm_val - pivot_val) / crm_val) * 100
                                    diff_row_list.append(f"{diff:.{dec}f}")
                                    diff_tags.append("in_range" if min_diff <= diff <= max_diff else "out_range")
                                else:
                                    diff_row_list.append("N/A")
                                    diff_tags.append("diff")
                            except Exception:
                                diff_row_list.append("")
                                diff_tags.append("diff")
                        else:
                            diff_row_list.append("")
                            diff_tags.append("diff")
                crm_display[sol_label].append((diff_row_list, diff_tags))

        return crm_display