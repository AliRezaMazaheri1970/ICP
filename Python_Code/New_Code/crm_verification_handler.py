# screens/process/verification/crm_verification_handler.py
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QCheckBox, QLabel, QLineEdit, QPushButton,
    QMessageBox, QTreeView, QGroupBox, QGridLayout, QSlider, QDialog, QComboBox
)
from PyQt6.QtCore import Qt, pyqtSignal
from PyQt6.QtGui import QStandardItemModel, QStandardItem
import pyqtgraph as pg
import re
import pandas as pd
import logging
from datetime import datetime

logging.basicConfig(level=logging.DEBUG, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

class CRMVerificationHandler:
    def __init__(self, window):
        self.w = window
        # *** UNDO STACK برای CRM ***
        self.crm_undo_stack = []
        self.crm_backup_columns = {}  # ذخیره backup ستون‌ها
    def run_calibration(self):
        # اول: file_ranges رو از اپلیکیشن بگیر (ممکنه تازه ساخته شده باشه)
        file_ranges = getattr(self.w.app, 'file_ranges', [])
        
        # اگر هنوز file_ranges وجود نداره، یه پیام بده
        if not file_ranges:
            QMessageBox.warning(self.w, "No Files", "No file ranges detected. Please load data first.")
            return

        # حالا ComboBox رو پر کن
        self.w.file_selector.clear()
        self.w.file_selector.setEnabled(True)
        self.w.file_selector.setToolTip("Switch between files")
        self.w.file_selector.addItem("All Files")  # اولین گزینه

        for i, fr in enumerate(file_ranges):
            clean_name = fr.get('clean_name', f'File {i+1}')
            start_row = fr.get('start_pivot_row', 0) + 1
            end_row = fr.get('end_pivot_row', 0)
            row_count = fr.get('pivot_row_count', end_row - start_row + 2)

            display_text = f"{clean_name}  |  Rows {start_row}–{end_row}  ({row_count} rows)"
            self.w.file_selector.addItem(display_text)

        # اتصال سیگنال (فقط یکبار!)
        try:
            self.w.file_selector.currentIndexChanged.disconnect()
        except:
            pass
        self.w.file_selector.currentIndexChanged.connect(self.w.rm_handler.on_file_changed)

        # اولین بار همه فایل‌ها رو نشون بده
        self.w.file_selector.setCurrentIndex(0)
        self.w.rm_handler.on_file_changed(0)
        self.w.rm_handler.start_check_rm_thread()

    def update_pivot_plot(self):
        """Update the plot based on current settings."""
        if not self.w.selected_element or self.w.selected_element not in self.w.pivot_df.columns:
            self.w.logger.warning(f"Element '{self.w.selected_element}' not found in pivot data!")
            QMessageBox.warning(self.w, "Warning", f"Element '{self.w.selected_element}' not found!")
            return
        try:
            self.w.verification_plot.clear()
            self.w.annotations = []
            def extract_crm_id(label):
                m = re.search(r'(?i)(?:\bCRM\b|\bOREAS\b)?[\s-]*(\d+[a-zA-Z]?)[\s-]*(?:\bpar\b)?', str(label))
                return m.group(1) if m else str(label)
            concentration_column = self.get_concentration_column(self.w.original_df) if self.w.original_df is not None else None
            if self.w.original_df is not None and not self.w.original_df.empty and concentration_column:
                sample_data = self.w.original_df[
                    (self.w.original_df['Type'].isin(['Samp', 'Sample'])) &
                    (self.w.original_df['Element'] == self.w.selected_element)
                ][concentration_column]
                sample_data_numeric = [float(x) for x in sample_data if self.is_numeric(x)]
                if not sample_data_numeric:
                    soln_conc_min = '---'
                    soln_conc_max = '---'
                    soln_conc_range = '---'
                    in_calibration_range_soln = False
                else:
                    soln_conc_min = min(sample_data_numeric)
                    soln_conc_max = max(sample_data_numeric)
                    soln_conc_range = f"[{self.format_number(soln_conc_min)} to {self.format_number(soln_conc_max)}]"
                    in_calibration_range_soln = (
                        float(self.w.calibration_range.split(' to ')[0][1:]) <= soln_conc_min <= float(self.w.calibration_range.split(' to ')[1][:-1]) and
                        float(self.w.calibration_range.split(' to ')[0][1:]) <= soln_conc_max <= float(self.w.calibration_range.split(' to ')[1][:-1])
                    ) if self.w.calibration_range != "[0 to 0]" else False
            else:
                soln_conc_min = '---'
                soln_conc_max = '---'
                soln_conc_range = '---'
                in_calibration_range_soln = False
            blank_rows = self.w.pivot_df[
                self.w.pivot_df['Solution Label'].str.contains(r'(?:CRM\s*)?(?:BLANK|BLNK)(?:\s+.*)?', case=False, na=False, regex=True)
            ]
            blank_val = 0
            blank_correction_status = "Not Applied"
            selected_blank_label = "None"
            self.w.blank_labels = []
            if not blank_rows.empty:
                best_blank_val = 0
                best_blank_label = "None"
                min_distance = float('inf')
                in_range_found = False
                for _, row in blank_rows.iterrows():
                    candidate_blank = row[self.w.selected_element] if pd.notna(row[self.w.selected_element]) else 0
                    candidate_label = row['Solution Label']
                    if not self.is_numeric(candidate_blank):
                        continue
                    candidate_blank = float(candidate_blank)
                    self.w.blank_labels.append(f"{candidate_label}: {self.format_number(candidate_blank)}")
                    in_range = False
                    for sol_label in self.w.app.crm_check._inline_crm_rows_display.keys():
                        if sol_label in blank_rows['Solution Label'].values:
                            continue
                        pivot_row = self.w.pivot_df[self.w.pivot_df['Solution Label'] == sol_label]
                        if pivot_row.empty:
                            continue
                        pivot_val = pivot_row.iloc[0][self.w.selected_element]
                        if not self.is_numeric(pivot_val):
                            continue
                        pivot_val_float = float(pivot_val)
                        for row_data, _ in self.w.app.crm_check._inline_crm_rows_display[sol_label]:
                            if isinstance(row_data, list) and row_data and row_data[0].endswith("CRM"):
                                val = row_data[self.w.pivot_df.columns.get_loc(self.w.selected_element)] if self.w.selected_element in self.w.pivot_df.columns else ""
                                if self.is_numeric(val):
                                    crm_val = float(val)
                                    range_val = self.calculate_dynamic_range(crm_val)
                                    lower, upper = crm_val - range_val, crm_val + range_val
                                    corrected_pivot = pivot_val_float - candidate_blank
                                    if lower <= corrected_pivot <= upper:
                                        in_range = True
                                        break
                        if in_range:
                            break
                    if in_range:
                        best_blank_val = candidate_blank
                        best_blank_label = candidate_label
                        in_range_found = True
                        break
                if not in_range_found:
                    for sol_label in self.w.app.crm_check._inline_crm_rows_display.keys():
                        if sol_label in blank_rows['Solution Label'].values:
                            continue
                        pivot_row = self.w.pivot_df[self.w.pivot_df['Solution Label'] == sol_label]
                        if pivot_row.empty:
                            continue
                        pivot_val = pivot_row.iloc[0][self.w.selected_element]
                        if not self.is_numeric(pivot_val):
                            continue
                        pivot_val_float = float(pivot_val)
                        for row_data, _ in self.w.app.crm_check._inline_crm_rows_display[sol_label]:
                            if isinstance(row_data, list) and row_data and row_data[0].endswith("CRM"):
                                val = row_data[self.w.pivot_df.columns.get_loc(self.w.selected_element)] if self.w.selected_element in self.w.pivot_df.columns else ""
                                if not self.is_numeric(val):
                                    continue
                                crm_val = float(val)
                                corrected_pivot = pivot_val_float - candidate_blank
                                distance = abs(corrected_pivot - crm_val)
                                if distance < min_distance:
                                    min_distance = distance
                                    best_blank_val = candidate_blank
                                    best_blank_label = candidate_label
                blank_val = best_blank_val
                selected_blank_label = best_blank_label
                blank_correction_status = "Applied" if blank_val != 0 else "Not Applied"
            self.w.blank_display.setText("Blanks:\n" + "\n".join(self.w.blank_labels) if self.w.blank_labels else "Blanks: None")
            crm_labels = [
                label for label in self.w.app.crm_check._inline_crm_rows_display.keys()
                if label not in blank_rows['Solution Label'].values
                and label in self.w.app.crm_check.included_crms and self.w.app.crm_check.included_crms[label].isChecked()
            ]
            crm_id_to_labels = {}
            for sol_label in crm_labels:
                crm_id = extract_crm_id(sol_label)
                if crm_id not in crm_id_to_labels:
                    crm_id_to_labels[crm_id] = []
                crm_id_to_labels[crm_id].append(sol_label)
            unique_crm_ids = sorted(crm_id_to_labels.keys())
            x_pos_map = {crm_id: i for i, crm_id in enumerate(unique_crm_ids)}
            certificate_values = {}
            sample_values = {}
            outlier_values = {}
            lower_bounds = {}
            upper_bounds = {}
            soln_concs = {}
            int_values = {}
            element_name = self.w.selected_element.split()[0]
            wavelength = ' '.join(self.w.selected_element.split()[1:]) if len(self.w.selected_element.split()) > 1 else ""
            analysis_date = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            for crm_id in unique_crm_ids:
                certificate_values[crm_id] = []
                sample_values[crm_id] = []
                outlier_values[crm_id] = []
                lower_bounds[crm_id] = []
                upper_bounds[crm_id] = []
                soln_concs[crm_id] = []
                int_values[crm_id] = []
                for sol_label in crm_id_to_labels[crm_id]:
                    pivot_row = self.w.pivot_df[self.w.pivot_df['Solution Label'] == sol_label]
                    if pivot_row.empty:
                        continue
                    pivot_val = pivot_row.iloc[0][self.w.selected_element]
                    if pd.isna(pivot_val) or not self.is_numeric(pivot_val):
                        pivot_val = 0
                    else:
                        pivot_val = float(pivot_val)
                    if self.w.original_df is not None and not self.w.original_df.empty and concentration_column:
                        sample_rows = self.w.original_df[
                            (self.w.original_df['Solution Label'] == sol_label) &
                            (self.w.original_df['Element'].str.startswith(element_name)) &
                            (self.w.original_df['Type'].isin(['Samp', 'Sample']))
                        ]
                        soln_conc = sample_rows[concentration_column].iloc[0] if not sample_rows.empty else '---'
                        int_val = sample_rows['Int'].iloc[0] if not sample_rows.empty else '---'
                    else:
                        soln_conc = '---'
                        int_val = '---'
                    for row_data, _ in self.w.app.crm_check._inline_crm_rows_display[sol_label]:
                        if isinstance(row_data, list) and row_data and row_data[0].endswith("CRM"):
                            val = row_data[self.w.pivot_df.columns.get_loc(self.w.selected_element)] if self.w.selected_element in self.w.pivot_df.columns else ""
                            if not val or not self.is_numeric(val):
                                if sol_label not in self.w.excluded_outliers.get(self.w.selected_element, set()):
                                    annotation = f"Verification ID: {crm_id} (Label: {sol_label})\n - Certificate Value: {val or 'N/A'}\n - Sample Value: {self.format_number(pivot_val)}\n - Acceptable Range: [N/A]\n - Status: Out of range (non-numeric data).\n - Blank Value: {self.format_number(blank_val)}\n - Blank Label: {selected_blank_label}\n - Blank Correction Status: {blank_correction_status}\n - Sample Value - Blank: {self.format_number(pivot_val)}\n - Corrected Range: [N/A]\n - Status after Blank Subtraction: Out of range (non-numeric data).\n - Soln Conc: {soln_conc if isinstance(soln_conc, str) else self.format_number(soln_conc)} {'in_range' if in_calibration_range_soln else 'out_range'}\n - Int: {int_val if isinstance(int_val, str) else self.format_number(int_val)}\n - Calibration Range: {self.w.calibration_range} {'in_range' if in_calibration_range_soln else 'out_range'}\n - CRM Source: NIST\n - Sample Matrix: Soil\n - Element Wavelength: {wavelength}\n - Analysis Date: {analysis_date}"
                                    self.w.annotations.append(annotation)
                                continue
                            crm_val = float(val)
                            pivot_val_float = float(pivot_val)
                            corrected_val = pivot_val_float
                            if (sol_label not in self.w.excluded_from_correct and
                                self.is_numeric(pivot_val) and
                                (self.w.scale_range_min is None or self.w.scale_range_max is None or
                                 self.w.scale_range_min <= float(pivot_val) <= self.w.scale_range_max) and
                                (not self.w.scale_above_50_cb.isChecked() or float(pivot_val) > 50)):
                                corrected_val = (pivot_val_float - self.w.preview_blank) * self.w.preview_scale
                            range_val = self.calculate_dynamic_range(crm_val)
                            lower = crm_val - range_val
                            upper = crm_val + range_val
                            in_range = lower <= corrected_val <= upper
                            if sol_label not in self.w.excluded_outliers.get(self.w.selected_element, set()):
                                annotation = f"Verification ID: {crm_id} (Label: {sol_label})\n - Certificate Value: {self.format_number(crm_val)}\n - Sample Value: {self.format_number(pivot_val_float)}\n - Acceptable Range: [{self.format_number(lower)} to {self.format_number(upper)}]"
                                if in_range:
                                    annotation += f"\n - Status: In range (no adjustment needed)."
                                    annotation += f"\n - Blank Value: {self.format_number(blank_val)}\n - Blank Label: {selected_blank_label}\n - Blank Correction Status: Not Applied (in range)\n - Sample Value - Blank: {self.format_number(corrected_val)}\n - Corrected Range: [{self.format_number(lower)} to {self.format_number(upper)}]\n - Status after Blank Subtraction: In range."
                                else:
                                    annotation += f"\n - Status: Out of range without adjustment."
                                    annotation += f"\n - Blank Value: {self.format_number(blank_val)}\n - Blank Label: {selected_blank_label}\n - Blank Correction Status: {blank_correction_status}\n - Sample Value - Blank: {self.format_number(corrected_val)}\n - Corrected Range: [{self.format_number(lower)} to {self.format_number(upper)}]"
                                    corrected_in_range = lower <= corrected_val <= upper
                                    if corrected_in_range:
                                        annotation += f"\n - Status after Blank Subtraction: In range."
                                    else:
                                        annotation += f"\n - Status after Blank Subtraction: Out of range."
                                        if corrected_val != 0:
                                            if corrected_val < lower:
                                                scale_factor = lower / corrected_val
                                                direction = "increase"
                                            elif corrected_val > upper:
                                                scale_factor = upper / corrected_val
                                                direction = "decrease"
                                            else:
                                                scale_factor = 1.0
                                                direction = ""
                                            scale_percent = abs((scale_factor - 1) * 100)
                                            annotation += f"\n - Required Scaling: {scale_percent:.2f}% {direction} to fit within range."
                                            if scale_percent > 32:
                                                annotation += f"\n - Warning: Scaling exceeds 32% ({scale_percent:.2f}%)."
                                        else:
                                            annotation += f"\n - Scaling not applicable (corrected sample value is zero)."
                                annotation += f"\n - Soln Conc: {soln_conc if isinstance(soln_conc, str) else self.format_number(soln_conc)} {'in_range' if in_calibration_range_soln else 'out_range'}\n - Int: {int_val if isinstance(int_val, str) else self.format_number(int_val)}\n - Calibration Range: {self.w.calibration_range} {'in_range' if in_calibration_range_soln else 'out_range'}\n - CRM Source: NIST\n - Sample Matrix: Soil\n - Element Wavelength: {wavelength}\n - Analysis Date: {analysis_date}"
                                self.w.annotations.append(annotation)
                      
                            certificate_values[crm_id].append(crm_val)
                            if sol_label in self.w.excluded_outliers.get(self.w.selected_element, set()):
                                outlier_values[crm_id].append(corrected_val)
                            else:
                                sample_values[crm_id].append(corrected_val)
                            lower_bounds[crm_id].append(lower)
                            upper_bounds[crm_id].append(upper)
                            soln_concs[crm_id].append(soln_conc)
                            int_values[crm_id].append(int_val)
            if not unique_crm_ids:
                self.w.verification_plot.clear()
                self.w.logger.warning(f"No valid Verification data for {self.w.selected_element}")
                QMessageBox.warning(self.w, "Warning", f"No valid Verification data for {self.w.selected_element}")
                return
            self.w.verification_plot.setLabel('bottom', 'Verification ID')
            self.w.verification_plot.setLabel('left', f'{self.w.selected_element} Value')
            self.w.verification_plot.setTitle(f'Verification Values for {self.w.selected_element}')
            self.w.verification_plot.getAxis('bottom').setTicks([[(i, f'V {id}') for i, id in enumerate(unique_crm_ids)]])
            all_y_values = []
            for crm_id in unique_crm_ids:
                all_y_values.extend(certificate_values.get(crm_id, []))
                all_y_values.extend(sample_values.get(crm_id, []))
                all_y_values.extend(outlier_values.get(crm_id, []))
                all_y_values.extend(lower_bounds.get(crm_id, []))
                all_y_values.extend(upper_bounds.get(crm_id, []))
            if all_y_values:
                y_min, y_max = min(all_y_values), max(all_y_values)
                margin = (y_max - y_min) * 0.1
                self.w.verification_plot.setXRange(-0.5, len(unique_crm_ids) - 0.5)
                self.w.verification_plot.setYRange(y_min - margin, y_max + margin)
            if self.w.show_crm_cb.isChecked():
                for crm_id in unique_crm_ids:
                    x_pos = x_pos_map[crm_id]
                    cert_vals = certificate_values.get(crm_id, [])
                    if cert_vals:
                        x_vals = [x_pos] * len(cert_vals)
                        scatter = pg.PlotDataItem(
                            x=x_vals, y=cert_vals, pen=None, symbol='o', symbolSize=8,
                            symbolPen='g', symbolBrush='g', name='Certificate Value'
                        )
                        self.w.verification_plot.addItem(scatter)
            if self.w.show_cert_cb.isChecked():
                for crm_id in unique_crm_ids:
                    x_pos = x_pos_map[crm_id]
                    for idx, sol_label in enumerate(crm_id_to_labels[crm_id]):
                        samp_vals = sample_values.get(crm_id, [])
                        if idx < len(samp_vals):
                            scatter = pg.PlotDataItem(
                                x=[x_pos], y=[samp_vals[idx]], pen=None, symbol='t', symbolSize=8,
                                symbolPen='b', symbolBrush='b', name=sol_label
                            )
                            self.w.verification_plot.addItem(scatter)
                        outlier_vals = outlier_values.get(crm_id, [])
                        if idx < len(outlier_vals):
                            scatter = pg.PlotDataItem(
                                x=[x_pos], y=[outlier_vals[idx]], pen=None, symbol='t', symbolSize=8,
                                symbolPen='#FFA500', symbolBrush='#FFA500', name=f"{sol_label} (Outlier)"
                            )
                            self.w.verification_plot.addItem(scatter)
            if self.w.show_range_cb.isChecked():
                for crm_id in unique_crm_ids:
                    x_pos = x_pos_map[crm_id]
                    low_bounds = lower_bounds.get(crm_id, [])
                    up_bounds = upper_bounds.get(crm_id, [])
                    if low_bounds and up_bounds:
                        for low, up in zip(low_bounds, up_bounds):
                            line_lower = pg.PlotDataItem(
                                x=[x_pos - 0.2, x_pos + 0.2], y=[low, low],
                                pen=pg.mkPen('r', width=2)
                            )
                            line_upper = pg.PlotDataItem(
                                x=[x_pos - 0.2, x_pos + 0.2], y=[up, up],
                                pen=pg.mkPen('r', width=2)
                            )
                            self.w.verification_plot.addItem(line_lower)
                            self.w.verification_plot.addItem(line_upper)
            self.w.verification_plot.showGrid(x=True, y=True, alpha=0.3)
        
            # Secondary plot - فقط اینجا فیلتر اعمال می‌شود
            filter_text = self.w.filter_solution_edit.text().strip().lower()
            if 'pivot_index' not in self.w.pivot_df.columns:
                self.w.pivot_df['pivot_index'] = self.w.pivot_df.index
            filtered_data = self.w.pivot_df.copy()
            if filter_text:
                filtered_data = filtered_data[filtered_data['Solution Label'].str.lower().str.contains(filter_text, na=False)]
            x_sec = filtered_data['pivot_index'].values
            y_sec = pd.to_numeric(filtered_data[self.w.selected_element], errors='coerce').fillna(0).values
        except Exception as e:
            self.w.verification_plot.clear()
            self.w.logger.error(f"Failed to update plot: {str(e)}")
            QMessageBox.warning(self.w, "Error", f"Failed to update plot: {str(e)}")

    def calculate_dynamic_range(self, value):
        """Calculate the dynamic range for a given value."""
        try:
            value = float(value)
            abs_value = abs(value)
            if abs_value < 10:
                return self.w.range_low
            elif 10 <= abs_value < 100:
                return abs_value * (self.w.range_mid / 100)
            elif 100 <= abs_value < 1000:
                return abs_value * (self.w.range_high1 / 100)
            elif 1000 <= abs_value < 10000:
                return abs_value * (self.w.range_high2 / 100)
            elif 10000 <= abs_value < 100000:
                return abs_value * (self.w.range_high3 / 100)
            else:
                return abs_value * (self.w.range_high4 / 100)
        except (ValueError, TypeError):
            return 0

    def is_numeric(self, value):
        """Check if a value is numeric."""
        try:
            float(value)
            return True
        except (ValueError, TypeError):
            return False

    def format_number(self, value):
        """Format a number for display."""
        if not self.is_numeric(value):
            return str(value)
        num = float(value)
        if num == 0:
            return "0"
        return f"{num:.4f}".rstrip('0').rstrip('.')

    def update_calibration_range(self):
        # اگر هنوز UI ساخته نشده، هیچ کاری نکن
        if not hasattr(self.w, 'calibration_display'):
            return
        # بقیه کد مثل قبل...
        if self.w.original_df is not None and not self.w.original_df.empty:
            concentration_column = self.get_concentration_column(self.w.original_df)
            if concentration_column:
                element_name = self.w.selected_element[:-2] if len(self.w.selected_element) >= 2 and self.w.selected_element[-2] == '_' else self.w.selected_element
                std_data = self.w.original_df[
                    (self.w.original_df['Type'] == 'Std') &
                    (self.w.original_df['Element'] == element_name)
                ][concentration_column]
                std_data_numeric = [float(x) for x in std_data if self.is_numeric(x)]
                if std_data_numeric:
                    calibration_min = min(std_data_numeric)
                    calibration_max = max(std_data_numeric)
                    self.w.calibration_range = f"[{self.format_number(calibration_min)} to {self.format_number(calibration_max)}]"
                else:
                    self.w.calibration_range = "[0 to 0]"
            else:
                self.w.calibration_range = "[0 to 0]"
        else:
            self.w.calibration_range = "[0 to 0]"
        # حالا که مطمئنیم ویجت وجود داره
        self.w.calib_range_label.setText(f"Calibration: {self.w.calibration_range}")

    def run_pivot_plot(self):
        if self.w.element_combo:
            self.update_calibration_range()
        self.w.update_navigation_buttons()
        self.update_pivot_plot()

    def get_concentration_column(self, df):
        # تابع برای پیدا کردن ستون concentration
        # فرض کنید ستونی به نام 'Conc' یا مشابه
        return 'Conc' if 'Conc' in df.columns else None
    
    def update_preview_params(self):
        try:
            self.w.preview_blank = float(self.w.blank_edit.text())
        except ValueError:
            self.w.preview_blank = 0.0
        self.w.preview_scale = self.w.scale_slider.value() / 100.0
        self.w.scale_label.setText(f"Scale: {self.w.preview_scale:.2f}")
        # آپدیت هر دو نمودار با پیش‌نمایش blank/scale
        self.w.rm_handler.update_rm_plot()
        self.update_pivot_plot()

    def reset_blank_and_scale(self):
        """Reset blank and scale to default values."""
        self.w.preview_blank = 0.0
        self.w.blank_edit.setText("0.0")
        self.w.preview_scale = 1.0
        self.w.scale_slider.setValue(100)
        self.w.scale_label.setText(f"Scale: {self.w.preview_scale:.2f}")
        self.update_pivot_plot()

    def open_range_dialog(self):
        """Open dialog to set acceptable ranges."""
        dialog = QDialog(self.w)
        dialog.setWindowFlags(Qt.WindowType.Dialog | Qt.WindowType.WindowCloseButtonHint)
        # dialog.setStyleSheet(global_style)
        dialog.setWindowTitle("تنظیم بازه‌های مجاز")
        dialog.setGeometry(200, 200, 400, 300)
        layout = QGridLayout(dialog)
        layout.setSpacing(5)
      
        layout.addWidget(QLabel("|x| <10 (±):"), 0, 0)
        self.w.range_low_edit = QLineEdit(str(self.w.range_low))
        self.w.range_low_edit.setFixedWidth(40)
        layout.addWidget(self.w.range_low_edit, 0, 1)
      
        layout.addWidget(QLabel("10<=|x|<100 (%):"), 0, 2)
        self.w.range_mid_edit = QLineEdit(str(self.w.range_mid))
        self.w.range_mid_edit.setFixedWidth(40)
        layout.addWidget(self.w.range_mid_edit, 0, 3)
      
        layout.addWidget(QLabel("100<=|x|<1000 (%):"), 1, 0)
        self.w.range_high1_edit = QLineEdit(str(self.w.range_high1))
        self.w.range_high1_edit.setFixedWidth(40)
        layout.addWidget(self.w.range_high1_edit, 1, 1)
      
        layout.addWidget(QLabel("1000<=|x|<10000 (%):"), 1, 2)
        self.w.range_high2_edit = QLineEdit(str(self.w.range_high2))
        self.w.range_high2_edit.setFixedWidth(40)
        layout.addWidget(self.w.range_high2_edit, 1, 3)
      
        layout.addWidget(QLabel("10000<=|x|<100000 (%):"), 2, 0)
        self.w.range_high3_edit = QLineEdit(str(self.w.range_high3))
        self.w.range_high3_edit.setFixedWidth(40)
        layout.addWidget(self.w.range_high3_edit, 2, 1)
      
        layout.addWidget(QLabel("|x|>=100000 (%):"), 2, 2)
        self.w.range_high4_edit = QLineEdit(str(self.w.range_high4))
        self.w.range_high4_edit.setFixedWidth(40)
        layout.addWidget(self.w.range_high4_edit, 2, 3)
      
        button_layout = QHBoxLayout()
        ok_btn = QPushButton("OK")
        ok_btn.clicked.connect(lambda: self.apply_ranges(dialog))
        button_layout.addWidget(ok_btn)
      
        cancel_btn = QPushButton("Cancel")
        cancel_btn.clicked.connect(dialog.reject)
        button_layout.addWidget(cancel_btn)
      
        layout.addLayout(button_layout, 3, 0, 1, 4)
      
        dialog.exec()

    def apply_ranges(self, dialog):
        """Apply the ranges from the dialog."""
        try:
            self.w.range_low = float(self.w.range_low_edit.text())
            self.w.range_mid = float(self.w.range_mid_edit.text())
            self.w.range_high1 = float(self.w.range_high1_edit.text())
            self.w.range_high2 = float(self.w.range_high2_edit.text())
            self.w.range_high3 = float(self.w.range_high3_edit.text())
            self.w.range_high4 = float(self.w.range_high4_edit.text())
            dialog.accept()
            self.update_pivot_plot()
        except ValueError:
            QMessageBox.warning(self, "Error", "Invalid range values. Please enter numbers.")
    
    def open_exclude_window(self):
        """Open window to exclude Solution Labels from correction."""
        w = QDialog(self.w)
        w.setWindowFlags(Qt.WindowType.Dialog | Qt.WindowType.WindowCloseButtonHint)
        # w.setStyleSheet(global_style)
        w.setWindowTitle("Exclude from Correct")
        w.setGeometry(200, 200, 400, 400)
        layout = QVBoxLayout(w)
        tree_view = QTreeView()
        model = QStandardItemModel()
        model.setHorizontalHeaderLabels(["Solution Label", "Value", "Exclude"])
        tree_view.setModel(model)
        tree_view.setRootIsDecorated(False)
        tree_view.header().resizeSection(0, 160)
        tree_view.header().resizeSection(1, 80)
        tree_view.header().resizeSection(2, 80)
        for label in sorted(self.w.pivot_df['Solution Label']):
            match = self.w.pivot_df[self.w.pivot_df['Solution Label'] == label]
            value = match[self.w.selected_element].iloc[0] if not match.empty else 'N/A'
            label_item = QStandardItem(label)
            value_item = QStandardItem(str(value))
            check_item = QStandardItem()
            check_item.setCheckable(True)
            check_item.setCheckState(Qt.CheckState.Checked if label in self.w.excluded_from_correct else Qt.CheckState.Unchecked)
            model.appendRow([label_item, value_item, check_item])
        tree_view.clicked.connect(lambda index: self.toggle_exclude_check(index, model))
        layout.addWidget(tree_view)
        close_btn = QPushButton("Close")
        close_btn.clicked.connect(w.accept)
        layout.addWidget(close_btn)
        w.exec()

    def toggle_exclude_check(self, index, model):
        """Toggle exclusion of a solution label."""
        if index.column() != 2:
            return
        label = model.item(index.row(), 0).text()
        if model.item(index.row(), 2).checkState() == Qt.CheckState.Checked:
            self.w.excluded_from_correct.add(label)
        else:
            self.w.excluded_from_correct.discard(label)
        self.update_pivot_plot()


    def open_select_crms_window(self):
        """Open window to select verifications to include."""
        w = QDialog(self.w)
        w.setWindowFlags(Qt.WindowType.Dialog | Qt.WindowType.WindowCloseButtonHint)
        # w.setStyleSheet(global_style)
        w.setWindowTitle("Select Verifications to Include")
        w.setGeometry(200, 200, 300, 400)
        w.setModal(True)
        layout = QVBoxLayout(w)
        tree_view = QTreeView()
        model = QStandardItemModel()
        model.setHorizontalHeaderLabels(["Label", "Include"])
        tree_view.setModel(model)
        tree_view.setRootIsDecorated(False)
        tree_view.header().resizeSection(0, 160)
        tree_view.header().resizeSection(1, 80)
        for label in sorted(self.w.app.crm_check.included_crms.keys()):
            value_item = QStandardItem(label)
            check_item = QStandardItem()
            check_item.setCheckable(True)
            check_item.setCheckState(Qt.CheckState.Checked if self.w.app.crm_check.included_crms[label].isChecked() else Qt.CheckState.Unchecked)
            model.appendRow([value_item, check_item])
        tree_view.clicked.connect(lambda index: self.toggle_crm_check(index, model))
        layout.addWidget(tree_view)
        button_layout = QHBoxLayout()
        select_all_btn = QPushButton("Select All")
        select_all_btn.setFocusPolicy(Qt.FocusPolicy.NoFocus)
        select_all_btn.clicked.connect(lambda: self.set_all_crms(True, model))
        button_layout.addWidget(select_all_btn)
      
        deselect_all_btn = QPushButton("Deselect All")
        deselect_all_btn.setFocusPolicy(Qt.FocusPolicy.NoFocus)
        deselect_all_btn.clicked.connect(lambda: self.set_all_crms(False, model))
        button_layout.addWidget(deselect_all_btn)
      
        close_btn = QPushButton("Close")
        close_btn.setFocusPolicy(Qt.FocusPolicy.NoFocus)
        close_btn.clicked.connect(w.accept)
        button_layout.addWidget(close_btn)
      
        layout.addLayout(button_layout)
        w.exec()

    def update_scale_range(self):
        """Update the scale application range based on user input."""
        try:
            min_val = self.w.crm_min_edit.text().strip()
            max_val = self.w.crm_max_edit.text().strip()
            self.w.scale_range_min = float(min_val) if min_val else None
            self.w.scale_range_max = float(max_val) if max_val else None
            if self.w.scale_range_min is not None and self.w.scale_range_max is not None and self.w.scale_range_min > self.w.scale_range_max:
                self.w.scale_range_min, self.w.scale_range_max = self.w.scale_range_max, self.w.scale_range_min
                self.w.scale_range_min.setText(str(self.w.scale_range_min))
                self.w.scale_range_max.setText(str(self.w.scale_range_max))
            self.update_pivot_plot()
        except ValueError:
            self.w.scale_range_min = None
            self.w.scale_range_max = None
            self.update_pivot_plot()

    def toggle_crm_check(self, index, model):
        """Toggle CRM inclusion."""
        if index.column() != 1:
            return
        label = model.item(index.row(), 0).text()
        if label in self.w.app.crm_check.included_crms:
            self.w.app.crm_check.included_crms[label].setChecked(not self.w.app.crm_check.included_crms[label].isChecked())
            model.item(index.row(), 1).setCheckState(
                Qt.CheckState.Checked if self.w.app.crm_check.included_crms[label].isChecked() else Qt.CheckState.Unchecked)
            self.update_pivot_plot()

    def set_all_crms(self, value, model):
        """Set all CRMs to included or excluded."""
        for label, checkbox in self.w.app.crm_check.included_crms.items():
            checkbox.setChecked(value)
        model.clear()
        model.setHorizontalHeaderLabels(["Label", "Include"])
        for label in sorted(self.w.app.crm_check.included_crms.keys()):
            value_item = QStandardItem(label)
            check_item = QStandardItem()
            check_item.setCheckable(True)
            check_item.setCheckState(Qt.CheckState.Checked if self.w.app.crm_check.included_crms[label].isChecked() else Qt.CheckState.Unchecked)
            model.appendRow([value_item, check_item])
        self.update_pivot_plot()

    def undo_crm_correction(self):
        """حذف ضرایب CRM + برگرداندن مقادیر اصلی"""
        try:
            if not hasattr(self.w.app.results, 'report_change'):
                QMessageBox.warning(self.w, "⚠️ No Data", "No CRM corrections!")
                return
            
            report_change = self.w.app.results.report_change
            element = self.w.selected_element
            
            # پیدا کردن CRM entries
            crm_mask = (
                (report_change['Element'] == element) & 
                (report_change['Scale'].notna() | report_change['Blank'].notna())
            )
            
            if not crm_mask.any():
                QMessageBox.warning(self.w, "⚠️ No Data", f"No CRM corrections for {element}!")
                return
            
            # *** 1. برگرداندن مقادیر اصلی از report_change ***
            for _, row in report_change[crm_mask].iterrows():
                sl = row['Solution Label']
                original_val = row['Original Value']
                
                # Update all_pivot_df
                if hasattr(self.w, 'all_pivot_df') and not self.w.all_pivot_df.empty:
                    mask = self.w.all_pivot_df['Solution Label'] == sl
                    if mask.any():
                        self.w.all_pivot_df.loc[mask, element] = original_val
                
                # Update pivot_df
                if not self.w.pivot_df.empty:
                    mask = self.w.pivot_df['Solution Label'] == sl
                    if mask.any():
                        self.w.pivot_df.loc[mask, element] = original_val
            
            # *** 2. حذف ضرایب از report_change ***
            report_change = report_change[~crm_mask].reset_index(drop=True)
            self.w.app.results.report_change = report_change
            column = self.w.selected_element
            self.w.app.crm_check.restore_column(column)
            # *** 3. آپدیت Results tab ***
            if hasattr(self.w.app.results, 'show_processed_data'):
                self.w.app.results.last_filtered_data = self.w.all_pivot_df.copy() if hasattr(self.w, 'all_pivot_df') else self.w.pivot_df.copy()
                self.w.app.results.show_processed_data()
            # Success message
            removed_count = crm_mask.sum()
            QMessageBox.information(
                self.w, "✅ CRM Undo Successful",
                f"✅ Removed {removed_count} CRM coefficients\n"
                f"🔄 Element: {element}\n"
                f"📊 Values restored to original!\n\n"
                f"💾 Results tab updated!"
            )
            
            self.update_pivot_plot()
            logger.info(f"✅ CRM undo: removed {removed_count} coefficients for {element}")
            
        except Exception as e:
            logger.error(f"❌ CRM Undo failed: {str(e)}")
            QMessageBox.critical(self.w, "❌ Undo Error", f"Failed to undo:\n{str(e)}")

    def correct_crm_callback(self):
        """Apply CRM correction + ذخیره ضرایب + آپدیت REAL DATA"""
        try:
            if self.w.pivot_df is None or self.w.pivot_df.empty:
                QMessageBox.warning(self, "Error", "No data available to correct!")
                return
            
            column_to_correct = self.w.selected_element
            if column_to_correct not in self.w.pivot_df.columns:
                QMessageBox.warning(self, "Error", f"Column {column_to_correct} not found!")
                return

            corrected_count = 0
            correction_data = []
            
            # *** 1. BACKUP و اعمال روی pivot_df ***
            pivot_backup = self.w.pivot_df[column_to_correct].copy()  # برای undo
            
            for index, row in self.w.pivot_df.iterrows():
                solution_label = row['Solution Label']
                current_val = row[column_to_correct]
                if pd.notna(current_val) and self.is_numeric(current_val):
                    val = float(current_val)
                    if (solution_label not in self.w.excluded_from_correct and
                        (self.w.scale_range_min is None or self.w.scale_range_max is None or
                        self.w.scale_range_min <= val <= self.w.scale_range_max) and
                        (not self.w.scale_above_50_cb.isChecked() or val > 50)):
                        
                        new_val = (val - self.w.preview_blank) * self.w.preview_scale
                        self.w.pivot_df.at[index, column_to_correct] = new_val
                        corrected_count += 1
                        
                        correction_data.append({
                            'Solution Label': solution_label,
                            'Element': column_to_correct,
                            'Scale': self.w.preview_scale,
                            'Blank': self.w.preview_blank,
                            'Original Value': val,
                            'New Value': new_val
                        })

            # *** 2. آپدیت GLOBAL DATA (مهم‌ترین بخش!) ***
            self.update_global_data_crm(column_to_correct, correction_data)
            
            # *** 3. ذخیره ضرایب در report_change ***
            self.save_crm_to_report_change(correction_data, column_to_correct)
            
            # *** 4. Reset controls ***
            # self.reset_preview_controls()
            
            # Success message
            range_text = (f"[{self.format_number(self.w.scale_range_min)} to {self.format_number(self.w.scale_range_max)}]"
                        if self.w.scale_range_min is not None and self.w.scale_range_max is not None else "All values")
            QMessageBox.information(
                self.w, "✅ Success",
                f"✅ CRM Correction applied!\n\n"
                f"📊 Corrected: {corrected_count} samples\n"
                f"🧪 Blank: {self.format_number(self.w.preview_blank)}\n"
                f"⚖️  Scale: {self.w.preview_scale:.4f}\n"
                f"📏 Range: {range_text}\n\n"
                f"💾 Values UPDATED in Results tab!\n"
                f"📋 Coefficients saved to Report Changes!"
            )
            
            self.update_pivot_plot()
            
        except Exception as e:
            logger.error(f"❌ CRM Correction failed: {str(e)}")
            QMessageBox.critical(self.w, "❌ Error", f"Failed to apply CRM correction:\n{str(e)}")

    def update_global_data_crm(self, column, correction_data):
        """🔥 آپدیت REAL DATA در همه جا"""
        try:
            # *** A. آپدیت all_pivot_df (اصلی‌ترین داده Results tab) ***
            if hasattr(self.w, 'all_pivot_df') and not self.w.all_pivot_df.empty:
                for sl in [item['Solution Label'] for item in correction_data]:
                    mask = self.w.all_pivot_df['Solution Label'] == sl
                    if mask.any():
                        old_val = self.w.all_pivot_df.loc[mask, column].iloc[0]
                        new_val = self.w.pivot_df.loc[self.w.pivot_df['Solution Label'] == sl, column].iloc[0]
                        self.w.all_pivot_df.loc[mask, column] = new_val
                        logger.debug(f"Updated all_pivot_df: {sl} | {old_val} → {new_val}")
            
            # *** B. آپدیت last_filtered_data در ResultsFrame ***
            if hasattr(self.w.app, 'results') and self.w.app.results:
                # کپی از all_pivot_df یا pivot_df
                results_data = self.w.all_pivot_df.copy() if hasattr(self.w, 'all_pivot_df') else self.w.pivot_df.copy()
                self.w.app.results.last_filtered_data = results_data
                logger.debug(f"Updated results.last_filtered_data for {column}")
            
            # *** C. آپدیت original_df (long format) ***
            if hasattr(self.w, 'original_df') and self.w.original_df is not None:
                conc_col = self.get_concentration_column(self.w.original_df)
                if conc_col:
                    for item in correction_data:
                        sl = item['Solution Label']
                        mask = (
                            (self.w.original_df['Solution Label'] == sl) & 
                            (self.w.original_df['Element'] == column) &
                            (self.w.original_df['Type'].isin(['Samp', 'Sample']))
                        )
                        if mask.any():
                            self.w.original_df.loc[mask, conc_col] = item['New Value']
                    
                    # Sync to all_original_df
                    if hasattr(self.w, 'all_original_df'):
                        self.w.all_original_df.loc[self.w.original_df.index, :] = self.w.original_df
            
            # *** D. فراخوانی show_processed_data برای REFRESH فوری ***
            if hasattr(self.w.app.results, 'show_processed_data'):
                self.w.app.results.show_processed_data()
                logger.info(f"✅ Results tab refreshed for {column}")
            
            # *** E. Notify app برای sync کامل ***
            if hasattr(self.w.app, 'notify_data_changed'):
                self.w.app.notify_data_changed()
            
        except Exception as e:
            logger.error(f"❌ Error updating global CRM data: {str(e)}")

    def save_crm_to_report_change(self, correction_data, element):
        """ذخیره ضرایب CRM در report_change"""
        try:
            # Initialize report_change if needed
            if not hasattr(self.w.app.results, 'report_change'):
                self.w.app.results.report_change = pd.DataFrame(
                    columns=['Solution Label', 'Element', 'Scale', 'Blank', 'Original Value', 'New Value']
                )
            
            report_change = self.w.app.results.report_change
            
            # Create CRM DataFrame
            if correction_data:
                crm_df = pd.DataFrame(correction_data)
                
                # Remove existing CRM entries for this element
                if 'Element' in report_change.columns:
                    existing_mask = report_change['Element'] == element
                    report_change = report_change[~existing_mask]
                
                # Add new CRM entries
                report_change = pd.concat([report_change, crm_df], ignore_index=True)
                self.w.app.results.report_change = report_change
                
                logger.info(f"✅ Saved {len(crm_df)} CRM coefficients to report_change for {element}")
            
        except Exception as e:
            logger.error(f"❌ Error saving CRM to report_change: {str(e)}")

    def apply_model(self):
        """Apply the model corrections."""
        try:
            from ..report_dialog import ReportDialog
            dialog = ReportDialog(self.w, self.w.annotations)
            recommended_blank, recommended_scale = dialog.get_correction_parameters()
            self.w.blank_edit.setText(f"{recommended_blank:.3f}")
            self.w.scale_slider.setValue(int(recommended_scale * 100))
            self.update_preview_params()
        except Exception as e:
            logger.error(f"Error applying model: {str(e)}")
            QMessageBox.warning(self.w, "Error", f"Failed to apply model: {str(e)}")

    def show_report(self):
        """Show the report dialog."""
        try:
            from ..report_dialog import ReportDialog
            logger.debug(f"Opening report with {len(self.w.annotations)} annotations")
            dialog = ReportDialog(self.w, self.w.annotations)
            result = dialog.exec()
            if result == QDialog.DialogCode.Accepted:
                logger.debug("Report dialog accepted")
            else:
                logger.debug("Report dialog closed without applying corrections")
        except Exception as e:
            logger.error(f"Error opening ReportDialog: {str(e)}")
            QMessageBox.warning(self.w, "Error", f"Failed to open report: {str(e)}")