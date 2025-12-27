# utils/load_file.py
import pandas as pd
import csv
import os
import logging
import re
import sys
from PyQt6.QtWidgets import (
    QFileDialog, QMessageBox, QProgressDialog
)
from PyQt6.QtCore import QThread, pyqtSignal, Qt
from screens.pivot.pivot_creator import PivotCreator

# Setup logging
logger = logging.getLogger(__name__)


def split_element_name(element):
    """Split element name like 'Ce140' into 'Ce 140'."""
    if not isinstance(element, str):
        return element
    match = re.match(r'^([A-Za-z]+)(\d+\.?\d*)$', element.strip())
    if match:
        symbol, number = match.groups()
        return f"{symbol} {number}"
    return element


def resource_path(self, relative_path):
    try:
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)


class FileLoaderThread(QThread):
    """Worker thread to load and parse Excel/CSV files with progress updates."""
    progress = pyqtSignal(int, str)  # Signal for progress (value, message)
    finished = pyqtSignal(object, str)  # Signal for completion with DataFrame and file path
    error = pyqtSignal(str)  # Signal for errors

    def __init__(self, file_path, parent=None, is_pivoted=False):
        super().__init__(parent)
        self.file_path = file_path
        self.is_canceled = False
        self.is_pivoted = is_pivoted  # جدید: تشخیص فایل پیوت شده

    def cancel(self):
        """Mark the thread as canceled."""
        self.is_canceled = True

    def run(self):
        try:
            if self.is_pivoted:
                self.load_pivoted_directly()
            else:
                self.load_and_parse_normal()
        except Exception as e:
            logger.error(f"Unexpected error in thread: {str(e)}")
            self.error.emit(f"Unexpected error: {str(e)}")

    def load_pivoted_directly(self):
        """لود مستقیم فایل پیوت شده (wide format) بدون هیچ پردازشی"""
        try:
            self.progress.emit(0, "Loading pivoted file...")

            # خواندن فایل
            if self.file_path.lower().endswith('.csv'):
                df = pd.read_csv(self.file_path, on_bad_lines='skip')
            else:
                engine = 'openpyxl' if self.file_path.lower().endswith('.xlsx') else 'xlrd'
                df = pd.read_excel(self.file_path, engine=engine)

            if df.empty:
                self.error.emit("File is empty")
                return

            self.progress.emit(50, "Cleaning data...")

            # پاکسازی ساده
            df = df.copy()
            if 'Solution Label' in df.columns:
                df['Solution Label'] = df['Solution Label'].astype(str).str.strip()
            else:
                # اگر ستون اول احتمالاً Solution Label است
                df.iloc[:, 0] = df.iloc[:, 0].astype(str).str.strip()

            # حذف ردیف‌های کاملاً خالی
            df = df.dropna(how='all').reset_index(drop=True)

            # تبدیل ستون‌های عددی
            for col in df.columns:
                if col != 'Solution Label' and df[col].dtype == 'object':
                    df[col] = pd.to_numeric(df[col], errors='coerce')

            self.progress.emit(100, "Pivoted file loaded successfully")
            self.finished.emit(df, self.file_path)

        except Exception as e:
            logger.error(f"Failed to load pivoted file: {str(e)}")
            self.error.emit(f"Failed to load pivoted file: {str(e)}")

    def load_and_parse_normal(self):
        """پردازش فایل‌های خام (long format) — کد قبلی شما"""
        try:
            logger.debug(f"Starting file loading in thread for: {self.file_path}")
            self.progress.emit(0, "Analyzing file format...")

            is_new_format = False
            preview_steps = 10

            # تشخیص فرمت
            if self.file_path.lower().endswith('.csv'):
                try:
                    with open(self.file_path, 'r', encoding='utf-8') as f:
                        first_lines = [f.readline().strip() for _ in range(15)]
                    if any("Sample ID:" in line for line in first_lines) or any("Net Intensity" in line for line in first_lines):
                        is_new_format = True
                    elif any(all(col in line for col in ["Solution Label", "Element", "Int", "Corr Con"]) for line in first_lines):
                        is_new_format = False
                    else:
                        is_new_format = True
                except:
                    is_new_format = True
            else:
                try:
                    engine = 'openpyxl' if self.file_path.lower().endswith('.xlsx') else 'xlrd'
                    preview = pd.read_excel(self.file_path, header=None, nrows=15, engine=engine)
                    if preview.empty:
                        self.error.emit("File is empty")
                        return
                    first_col = preview.iloc[:, 0].astype(str)
                    if any("Sample ID:" in str(x) for x in first_col) or any("Net Intensity" in str(x) for x in first_col):
                        is_new_format = True
                    elif preview.shape[1] >= 4 and all(col in preview.columns for col in ["Solution Label", "Element", "Int", "Corr Con"]):
                        is_new_format = False
                    else:
                        is_new_format = True
                except Exception as e:
                    logger.warning(f"Preview failed: {e}, assuming new format")
                    is_new_format = True

            logger.debug(f"Detected format: {'NEW (Sample ID-based)' if is_new_format else 'OLD (tabular)'}")
            self.progress.emit(preview_steps, "Format detected, parsing data...")

            if self.is_canceled:
                self.error.emit("File loading canceled by user")
                return

            data_rows = []
            current_sample = None
            parse_steps = 70

            if is_new_format:
                logger.debug("Detected new file format (Sample ID-based)")
                if self.file_path.lower().endswith('.csv') or self.file_path.lower().endswith('.rep'):
                    try:
                        with open(self.file_path, 'r', encoding='utf-8') as f:
                            reader = list(csv.reader(f, delimiter=',', quotechar='"'))
                            total_rows = len(reader)
                            rows_per_step = max(1, total_rows // parse_steps) if total_rows > 0 else 1
                            for idx, row in enumerate(reader):
                                if self.is_canceled:
                                    self.error.emit("File loading canceled by user")
                                    return
                                if idx == total_rows - 1:
                                    logger.debug("Skipping last row of CSV")
                                    continue
                                if not row or all(cell.strip() == "" for cell in row):
                                    continue
                                if len(row) > 0 and row[0].startswith("Sample ID:"):
                                    current_sample = row[1].strip()
                                    logger.debug(f"Found Sample ID: {current_sample}")
                                    continue
                                if len(row) > 0 and (row[0].startswith("Method File:") or row[0].startswith("Calibration File:")):
                                    continue
                                if current_sample is None:
                                    current_sample = "Unknown_Sample"
                                element = split_element_name(row[0].strip())
                                try:
                                    intensity = float(row[1]) if len(row) > 1 and row[1].strip() else None
                                    concentration = float(row[5]) if len(row) > 5 and row[5].strip() else None
                                    if intensity is not None or concentration is not None:
                                        data_rows.append({
                                            "Solution Label": current_sample,
                                            "Element": element,
                                            "Int": intensity,
                                            "Corr Con": concentration,
                                            "Type": 'Sample'
                                        })
                                except Exception as e:
                                    logger.warning(f"Invalid data for element {element} in sample {current_sample}: {str(e)}")
                                    continue
                                if idx % rows_per_step == 0:
                                    progress = preview_steps + (idx // rows_per_step)
                                    self.progress.emit(min(progress, preview_steps + parse_steps), f"Parsing row {idx}/{total_rows}")
                    except Exception as e:
                        logger.error(f"Failed to parse CSV: {str(e)}")
                        self.error.emit(f"Failed to parse CSV: {str(e)}")
                        return
                else:
                    try:
                        engine = 'openpyxl' if self.file_path.lower().endswith('.xlsx') else 'xlrd'
                        raw_data = pd.read_excel(self.file_path, header=None, engine=engine)
                        total_rows = raw_data.shape[0]
                        rows_per_step = max(1, total_rows // parse_steps) if total_rows > 0 else 1
                        for index, row in raw_data.iterrows():
                            if self.is_canceled:
                                self.error.emit("File loading canceled by user")
                                return
                            if index == total_rows - 1:
                                logger.debug("Skipping last row of Excel")
                                continue
                            row_list = row.tolist()
                            if any("No valid data found in the file" in str(cell) for cell in row_list):
                                continue
                            if isinstance(row[0], str) and row[0].startswith("Sample ID:"):
                                current_sample = row[0].split("Sample ID:")[1].strip()
                                logger.debug(f"Found Sample ID: {current_sample}")
                                continue
                            if isinstance(row[0], str) and (row[0].startswith("Method File:") or row[0].startswith("Calibration File:")):
                                continue
                            if current_sample and pd.notna(row[0]):
                                element = split_element_name(str(row[0]).strip())
                                try:
                                    intensity = float(row[1]) if pd.notna(row[1]) else None
                                    concentration = float(row[5]) if pd.notna(row[5]) else None
                                    if intensity is not None or concentration is not None:
                                        type_value = "Blk" if "BLANK" in current_sample.upper() else "Sample"
                                        data_rows.append({
                                            "Solution Label": current_sample,
                                            "Element": element,
                                            "Int": intensity,
                                            "Corr Con": concentration,
                                            "Type": type_value
                                        })
                                except Exception as e:
                                    logger.warning(f"Invalid data for element {element} in sample {current_sample}: {str(e)}")
                                    continue
                            if index % rows_per_step == 0:
                                progress = preview_steps + (index // rows_per_step)
                                self.progress.emit(min(progress, preview_steps + parse_steps), f"Parsing row {index}/{total_rows}")
                    except Exception as e:
                        logger.error(f"Failed to parse Excel: {str(e)}")
                        self.error.emit(f"Failed to parse Excel: {str(e)}")
                        return
            else:
                logger.debug("Detected previous file format (tabular)")
                if self.file_path.lower().endswith('.csv') or self.file_path.lower().endswith('.rep')  :
                    try:
                        temp_df = pd.read_csv(self.file_path, header=None, nrows=1, on_bad_lines='skip')
                        if temp_df.iloc[0].notna().sum() == 1:
                            df = pd.read_csv(self.file_path, header=1, on_bad_lines='skip')
                        else:
                            df = pd.read_csv(self.file_path, header=0, on_bad_lines='skip')
                    except Exception as e:
                        logger.error(f"Failed to read CSV as tabular: {str(e)}")
                        self.error.emit(f"Could not parse CSV as tabular format: {str(e)}")
                        return
                else:
                    try:
                        engine = 'openpyxl' if self.file_path.lower().endswith('.xlsx') else 'xlrd'
                        temp_df = pd.read_excel(self.file_path, header=None, nrows=1, engine=engine)
                        if temp_df.iloc[0].notna().sum() == 1:
                            df = pd.read_excel(self.file_path, header=1, engine=engine)
                        else:
                            df = pd.read_excel(self.file_path, header=0, engine=engine)
                    except Exception as e:
                        logger.error(f"Failed to read Excel as tabular: {str(e)}")
                        self.error.emit(f"Could not parse Excel as tabular format: {str(e)}")
                        return

                self.progress.emit(preview_steps + parse_steps // 2, "Reading tabular data...")
                if self.is_canceled:
                    self.error.emit("File loading canceled by user")
                    return

                df = df.iloc[:-1]
                expected_columns = ["Solution Label", "Element", "Int", "Corr Con"]
                column_mapping = {"Sample ID": "Solution Label"}
                df.rename(columns=column_mapping, inplace=True)

                if not all(col in df.columns for col in expected_columns):
                    missing = set(expected_columns) - set(df.columns)
                    logger.error(f"Missing columns in tabular format: {missing}")
                    self.error.emit(f"Required columns missing: {', '.join(missing)}")
                    return

                total_rows = df.shape[0]
                rows_per_step = max(1, total_rows // (parse_steps // 2)) if total_rows > 0 else 1
                df['Element'] = df['Element'].apply(split_element_name)
                for idx in range(total_rows):
                    if self.is_canceled:
                        self.error.emit("File loading canceled by user")
                        return
                    if idx % rows_per_step == 0:
                        progress = preview_steps + parse_steps // 2 + (idx // rows_per_step)
                        self.progress.emit(min(progress, preview_steps + parse_steps), f"Processing row {idx}/{total_rows}")
                if 'Type' not in df.columns:
                    df['Type'] = df['Solution Label'].apply(lambda x: "Blk" if "BLANK" in str(x).upper() else "Sample")
                self.finished.emit(df, self.file_path)
                return

            if not data_rows and is_new_format:
                logger.error(" No valid data rows were parsed")
                self.error.emit("No valid data found in the file")
                return

            df = pd.DataFrame(data_rows, columns=["Solution Label", "Element", "Int", "Corr Con", "Type"])
            total_rows = df.shape[0]
            rows_per_step = max(1, total_rows // (parse_steps // 2)) if total_rows > 0 else 1
            for idx in range(total_rows):
                if self.is_canceled:
                    self.error.emit("File loading canceled by user")
                    return
                df.loc[idx, 'Element'] = split_element_name(df.loc[idx, 'Element'])
                if idx % rows_per_step == 0:
                    progress = preview_steps + parse_steps // 2 + (idx // rows_per_step)
                    self.progress.emit(min(progress, preview_steps + parse_steps), f"Processing row {idx}/{total_rows}")
            self.finished.emit(df, self.file_path)

        except Exception as e:
            logger.error(f"Unexpected error in thread: {str(e)}")
            self.error.emit(f"Unexpected error: {str(e)}")


def load_additional(app, file_path=None):
    """Load additional CSV and append to existing data, reset PivotTab filters."""
    logger.debug("Starting load_additional")

    if app.data is None:
        QMessageBox.warning(app, "Warning", "Please open a file first before importing additional data.")
        return None

    if hasattr(app, 'pivot_tab') and app.pivot_tab:
        logger.debug("Resetting PivotTab filters and cache on additional file load")
        app.pivot_tab.reset_cache()

    if file_path is None:
        file_path, _ = QFileDialog.getOpenFileName(
            app,
            "Import Additional CSV",
            "",
            "CSV files (*.csv)"
        )
        if not file_path:
            logger.debug("No additional file selected")
            return None

    app.file_path_label.setText(f"File Path: {app.file_path} + Additional: {os.path.basename(file_path)}")

    progress_dialog = QProgressDialog("Loading additional file and updating UI...", "Cancel", 0, 100, app)
    progress_dialog.setWindowTitle("Processing Additional")
    progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)
    progress_dialog.setMinimumDuration(0)
    progress_dialog.setValue(0)
    progress_dialog.show()

    worker = FileLoaderThread(file_path, app, is_pivoted=False)  # همیشه false برای additional

    def on_progress(value, message):
        progress_dialog.setValue(value)
        progress_dialog.setLabelText(message)
        if progress_dialog.wasCanceled():
            worker.cancel()

    def on_finished_additional(df, additional_file_path):
        """Called when a file is loaded via 'Load Additional'."""
        try:
            # Basic validation
            if df is None or not isinstance(df, pd.DataFrame):
                QMessageBox.warning(app, "Error", "The additional file is invalid or failed to load.")
                progress_dialog.close()
                return

            if df.empty:
                QMessageBox.warning(app, "Error", "The additional file is empty.")
                progress_dialog.close()
                return

            # Get clean file name (e.g. "1403-11-20 MASS")
            _, clean_name = app.file_tab.parse_filename(os.path.basename(additional_file_path))

            # Save current data state so we can restore it later
            original_data = app.data
            original_init_data = app.init_data

            # Temporarily set ONLY this new file as the app data → pivot will be built from it alone
            app.data = df.copy()
            app.init_data = df.copy()

            # Build pivot for this single file
            pivot_creator = PivotCreator(app.pivot_tab)
            pivot_creator.create_pivot()                     # returns nothing, just fills pivot_tab.pivot_data

            # Get the real number of rows this file contributes to the final pivot table
            current_pivot_df = app.pivot_tab.pivot_data
            if current_pivot_df is not None and not current_pivot_df.empty:
                pivot_row_count = len(current_pivot_df)
            else:
                # Fallback if pivot failed for some reason
                samp_df = df[df['Type'].isin(['Samp', 'Sample'])]
                pivot_row_count = samp_df['Solution Label'].nunique() if 'Solution Label' in samp_df.columns else 0

            # Calculate where this file starts in the final pivot table
            if not hasattr(app, 'file_ranges'):
                app.file_ranges = []

            start_row_in_pivot = sum(fr.get('pivot_row_count', 0) for fr in app.file_ranges)

            # Store range info (based on actual pivot rows, not raw rows!)
            app.file_ranges.append({
                "file_path": additional_file_path,
                "clean_name": clean_name,
                "start_pivot_row": start_row_in_pivot,
                "end_pivot_row": start_row_in_pivot + pivot_row_count - 1 if pivot_row_count > 0 else start_row_in_pivot,
                "pivot_row_count": pivot_row_count,
            })

            logger.debug(f"[Load Additional] {clean_name} → {pivot_row_count} pivot rows "
                        f"(rows {start_row_in_pivot}–{start_row_in_pivot + pivot_row_count - 1})")

            # Restore original full dataset and append the new file
            app.data = original_data
            app.init_data = original_init_data

            if app.data is None:
                app.data = df.copy()
                app.init_data = df.copy()
            else:
                app.data = pd.concat([app.data, df], ignore_index=True)
                app.init_data = app.data.copy()

            # Final UI update – rebuild full pivot with all files
            app.notify_data_changed()
            if hasattr(app, 'elements_tab') and app.elements_tab:
                app.elements_tab.process_blk_elements()

            PivotCreator(app.pivot_tab).create_pivot()   # final combined pivot

            if hasattr(app, 'results') and hasattr(app.results, 'show_processed_data'):
                app.results.show_processed_data()

            # Update file label
            current_label = app.file_path_label.text()
            new_label = f"Files: {clean_name}" if "Files:" not in current_label else f"{current_label.split(':')[0]}: {current_label.split(':')[1].strip()} + {clean_name}"
            app.file_path_label.setText(new_label)
            app.setWindowTitle(f"RASF Data Processor - {new_label.split(':')[-1].strip()}")

            progress_dialog.close()
            QMessageBox.information(app, "Success",
                                    f"Additional file loaded successfully:\n{clean_name}\n\n"
                                    f"Pivot rows added: {pivot_row_count}")

        except Exception as e:
            progress_dialog.close()
            logger.error(f"Error loading additional file {additional_file_path}: {e}")
            QMessageBox.critical(app, "Error", f"Failed to load additional file:\n{e}")
        finally:
            if 'progress_dialog' in locals():
                progress_dialog.close()

    def on_error(error_message):
        progress_dialog.close()
        logger.error(f"Failed to load additional file: {error_message}")
        QMessageBox.warning(app, "Error", f"Failed to load additional file:\n{error_message}")

    worker.progress.connect(on_progress)
    worker.finished.connect(on_finished_additional)
    worker.error.connect(on_error)
    worker.start()

    return None