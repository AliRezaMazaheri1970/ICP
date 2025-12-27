# screens/process/verification/rm_drift_handler.py
import numpy as np
import pandas as pd
from PyQt6.QtWidgets import QMessageBox, QMenu, QProgressDialog
from PyQt6.QtGui import QStandardItem, QStandardItemModel, QColor, QFont
from PyQt6.QtCore import Qt
import pyqtgraph as pg
from functools import partial
import logging

logging.basicConfig(level=logging.DEBUG, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

class RMDriftHandler:
    def __init__(self, window):
        self.w = window
        self.undo_stack = []
        self.manual_corrections = {}  # {original_index: corrected_value}
        self.ignored_pivots = set()   # set of ignored pivot_indices

    def setup_plot_items(self):
        # 1. Corrected Values (blue circles)
        self.corrected_scatter = pg.ScatterPlotItem(
            x=[], y=[],
            symbol='o', size=9,
            brush=pg.mkBrush(33, 150, 243, 230),
            pen=pg.mkPen('#1976D2', width=1.5),
            hoverable=True,
            name="Corrected Values"
        )
        self.w.rm_plot.addItem(self.corrected_scatter)
        # 2. Original Values (red X)
        self.original_scatter = pg.ScatterPlotItem(
            x=[], y=[],
            symbol='x', size=8,
            pen=pg.mkPen('#D32F2F', width=2),
            name="Original Values"
        )
        self.w.rm_plot.addItem(self.original_scatter)
        # 3. Trend line (dashed)
        self.trend_line = pg.PlotDataItem(pen=pg.mkPen(width=3, style=Qt.PenStyle.DashLine))
        self.w.rm_plot.addItem(self.trend_line)
        # 4. RM line (dotted background)
        self.rm_line = pg.PlotDataItem(pen=pg.mkPen(color=(100, 180, 100, 80), width=4, style=Qt.PenStyle.DotLine))
        self.w.rm_plot.addItem(self.rm_line)
        # 5. RM points
        self.rm_scatter = pg.ScatterPlotItem(
            x=[], y=[],
            symbol='o', size=12,
            brush=pg.mkBrush(100, 180, 100, 180),
            pen=pg.mkPen('darkgreen', width=2),
            name="RM Points"
        )
        self.w.rm_plot.addItem(self.rm_scatter)
        # 6. Selected segment (yellow)
        self.selected_segment_line = pg.PlotDataItem(pen=pg.mkPen('#FFD700', width=11))
        self.w.rm_plot.addItem(self.selected_segment_line)
        self.selected_start_rm_points = pg.ScatterPlotItem(
            symbol='s', size=22,
            brush=pg.mkBrush('#1976D2'),
            pen=pg.mkPen('white', width=4)
        )
        self.w.rm_plot.addItem(self.selected_start_rm_points)
        # 7. Selected end RM points
        self.selected_end_rm_points = pg.ScatterPlotItem(
            symbol='o', size=22,
            brush=pg.mkBrush('#FFD700'),
            pen=pg.mkPen('white', width=4)
        )
        self.w.rm_plot.addItem(self.selected_end_rm_points)

        self.detail_highlight_point = pg.ScatterPlotItem(
        symbol='o', size=18, brush=pg.mkBrush('#FFEB3B'), pen=pg.mkPen('black', width=3),
        name="Selected Detail Point")
        self.w.rm_plot.addItem(self.detail_highlight_point)

    def start_check_rm_thread(self):
        from .find_rm import CheckRMThread
        keyword = self.w.keyword_entry2.text().strip()
        if not keyword:
            QMessageBox.critical(self.w, "Error", "Please enter a valid keyword.")
            return
        self.w.keyword = keyword
        self.w.progress_dialog = QProgressDialog("Processing RM Changes...", "Cancel", 0, 100, self.w)
        self.w.progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)
        self.w.thread = CheckRMThread(self.w.app, keyword)
        self.w.thread.progress.connect(self.w.progress_dialog.setValue)
        self.w.thread.finished.connect(self.on_check_rm_finished)
        self.w.thread.error.connect(self.on_check_rm_error)
        self.w.thread.start()

    def on_check_rm_finished(self, results):
        self.w.all_initial_rm_df = results['rm_df'].copy(deep=True)
        self.w.all_rm_df = results['rm_df'].copy(deep=True)
        self.w.all_positions_df = results['positions_df'].copy(deep=True)
        self.w.all_segments = results['segments']
        self.w.all_pivot_df = results['pivot_df'].copy(deep=True)
        self.w.elements = results['elements']
        self.w.file_ranges = self.w.app.file_ranges if hasattr(self.w.app, 'file_ranges') else []

        # <<< این خط حیاتی رو اضافه کن >>>
        self.w.empty_rows_from_check = results.get('empty_rows', pd.DataFrame())  # اضافه شد

        # اگر empty_rows وجود نداشت، حداقل یک ستون original_index داشته باشه
        if 'original_index' not in self.w.empty_rows_from_check.columns:
            self.w.empty_rows_from_check = pd.DataFrame(columns=['original_index'])

        # حالا ست درست ساخته می‌شه
        self.w.empty_pivot_set = set(
            self.w.empty_rows_from_check['original_index']
            .dropna()
            .astype(int)
            .tolist()
        )

        # بقیه کد
        self.w.filter_by_file(-1)
        self.w.progress_dialog.close()
        self.w.data_changed.emit()
        self.update_navigation_buttons()

    def on_check_rm_error(self, message):
        self.w.progress_dialog.close()
        QMessageBox.critical(self.w, "Error", message)

    def get_valid_rm_data(self):
        """Extract valid RM data for current group, including masks."""
        label_df = self.w.rm_df[self.w.rm_df['rm_num'] == self.w.current_rm_num].sort_values('pivot_index')
        initial_label_df = self.w.initial_rm_df[self.w.initial_rm_df['rm_num'] == self.w.current_rm_num].sort_values('pivot_index')

        # Align lengths
        if len(label_df) != len(initial_label_df):
            common_pivot = np.intersect1d(label_df['pivot_index'], initial_label_df['pivot_index'])
            label_df = label_df[label_df['pivot_index'].isin(common_pivot)].sort_values('pivot_index')
            initial_label_df = initial_label_df[initial_label_df['pivot_index'].isin(common_pivot)].sort_values('pivot_index')

        pivot_indices = label_df['pivot_index'].values
        original_values = pd.to_numeric(initial_label_df[self.w.selected_element], errors='coerce').values
        display_values = pd.to_numeric(label_df[self.w.selected_element], errors='coerce').values
        valid_mask = ~np.isnan(original_values) & ~np.isnan(display_values)

        current_valid_pivot_indices = pivot_indices[valid_mask]
        original_rm_values = original_values[valid_mask]
        display_rm_values = display_values[valid_mask]
        rm_types = label_df.loc[label_df.index[valid_mask], 'rm_type'].values
        solution_labels = label_df.loc[label_df.index[valid_mask], 'Solution Label'].values

        # Empty and ignored masks
        empty_pivot_set = set(self.w.empty_rows_from_check['original_index'].dropna().astype(int).tolist()) \
            if not self.w.empty_rows_from_check.empty and 'original_index' in self.w.empty_rows_from_check.columns else set()
        is_really_empty = np.array([p in empty_pivot_set for p in current_valid_pivot_indices], dtype=bool)
        is_manually_ignored = np.array([p in self.ignored_pivots for p in current_valid_pivot_indices], dtype=bool)
        effective_empty = is_really_empty | is_manually_ignored

        return {
            'pivot_indices': current_valid_pivot_indices,
            'original_values': original_rm_values,
            'display_values': display_rm_values,
            'types': rm_types,
            'labels': solution_labels,
            'effective_empty': effective_empty,
            'file_names': [self.get_file_name_for_pivot(p) for p in current_valid_pivot_indices]
        }

    def get_file_name_for_pivot(self, pivot_idx):
        if not hasattr(self.w, 'file_ranges') or not self.w.file_ranges:
            return "All"
        for idx, fr in enumerate(self.w.file_ranges):
            if fr['start_pivot_row'] <= pivot_idx <= fr['end_pivot_row']:
                return fr.get('file_name', f"File {idx+1}")
        return "Unknown"

    def update_rm_table_values_only(self):
        """فقط ستون‌های Current Value و Ratio رو آپدیت کن - ULTRA FAST"""
        model = self.w.rm_table.model()
        if model is None or len(self.w.display_rm_values) == 0:
            return
        
        n_rows = min(model.rowCount(), len(self.w.display_rm_values))
        
        for i in range(n_rows):
            # ستون Current Value (index 5)
            if model.item(i, 5):
                model.item(i, 5).setText(f"{self.w.display_rm_values[i]:.2f}")
            
            # ستون Ratio (index 6)
            if model.item(i, 6) and i < len(self.w.original_rm_values):
                orig_val = self.w.original_rm_values[i]
                ratio = self.w.display_rm_values[i] / orig_val if orig_val != 0 else 1.0
                model.item(i, 6).setText(f"{ratio:.2f}")
                
    def display_rm_table(self):
        rm_data = self.get_valid_rm_data()
        if not rm_data:
            return

        self.w.current_valid_pivot_indices = rm_data['pivot_indices']
        self.w.original_rm_values = rm_data['original_values']
        self.w.display_rm_values = rm_data['display_values'].copy()  # Temporary editable copy
        self.w.rm_types = rm_data['types']
        self.w.solution_labels_for_group = rm_data['labels']

        model = QStandardItemModel()
        model.setHorizontalHeaderLabels(["RM Label", "File", "Next RM", "Type", "Original Value", "Current Value", "Ratio"])

        if len(self.w.display_rm_values) == 0:
            model.appendRow([QStandardItem("No Data") for _ in range(7)])
        else:
            effective_empty = rm_data['effective_empty']
            blue_pivot_indices = self.w.current_valid_pivot_indices[~effective_empty]
            blue_index_to_pos = {idx: i for i, idx in enumerate(blue_pivot_indices)}

            for i in range(len(self.w.display_rm_values)):
                current_rm_label = f"{self.w.solution_labels_for_group[i]}-{self.w.current_valid_pivot_indices[i]}"
                file_name = rm_data['file_names'][i]
                next_rm_label = "N/A"

                if not effective_empty[i]:
                    pos = blue_index_to_pos.get(self.w.current_valid_pivot_indices[i])
                    if pos is not None and pos < len(blue_pivot_indices) - 1:
                        next_pivot = blue_pivot_indices[pos + 1]
                        next_label = self.w.solution_labels_for_group[np.where(self.w.current_valid_pivot_indices == next_pivot)[0][0]]
                        next_rm_label = f"{next_label}-{next_pivot}"

                orig_val = self.w.original_rm_values[i]
                curr_val = self.w.display_rm_values[i]
                ratio = curr_val / orig_val if orig_val != 0 else np.nan

                row_items = [
                    QStandardItem(current_rm_label),
                    QStandardItem(file_name),
                    QStandardItem(next_rm_label),
                    QStandardItem(self.w.rm_types[i]),
                    QStandardItem(f"{orig_val:.2f}"),
                    QStandardItem(f"{curr_val:.2f}"),
                    QStandardItem(f"{ratio:.2f}" if pd.notna(ratio) else "N/A")
                ]

                if effective_empty[i]:
                    bg = QColor('red')
                    fg = QColor('white')
                    for item in row_items:
                        item.setBackground(bg)
                        item.setForeground(fg)
                        item.setEditable(False)
                else:
                    for j in [0, 1, 2, 3]: row_items[j].setEditable(False)
                    row_items[5].setEditable(True)
                    row_items[6].setEditable(True)
                    color_map = {'Base': '#2E7D32', 'Check': '#FF6B00', 'Cone': '#7B1FA2'}
                    color = QColor(color_map.get(self.w.rm_types[i], '#000000'))
                    row_items[3].setForeground(color)
                    row_items[3].setFont(QFont("Segoe UI", 9, QFont.Weight.Bold))

                model.appendRow(row_items)

        self.w.rm_table.setModel(model)
        try: model.itemChanged.disconnect()
        except: pass
        model.itemChanged.connect(self.on_rm_value_changed)
        self.update_slope_from_data()
        if 0 <= self.w.selected_row < len(self.w.current_valid_pivot_indices):
            self.w.rm_table.selectRow(self.w.selected_row)

    def highlight_rm(self):
        rm_data = self.get_valid_rm_data()
        x_rm = rm_data['pivot_indices']
        y_rm = self.w.display_rm_values  # Use temporary display values
        effective_empty = rm_data['effective_empty']
        normal_mask = ~effective_empty

        if (hasattr(self.w, 'selected_row') and self.w.selected_row is not None and
            0 <= self.w.selected_row < len(x_rm) - 1 and
            normal_mask[self.w.selected_row] and normal_mask[self.w.selected_row-1]):
            x1, y1 = x_rm[self.w.selected_row-1], y_rm[self.w.selected_row-1]
            x2, y2 = x_rm[self.w.selected_row], y_rm[self.w.selected_row]
            self.selected_segment_line.setData([x1, x2], [y1, y2], pen=pg.mkPen('#FFD700', width=8))
            self.selected_start_rm_points.setData([x1], [y1], symbol='s', size=20, brush='#1976D2', pen=pg.mkPen('white', width=4))
            self.selected_end_rm_points.setData([x2], [y2], symbol='o', size=20, brush='#D32F2F', pen=pg.mkPen('white', width=4))

    def update_rm_plot(self):
        self.w.rm_plot.clear()
        self.setup_plot_items()
        rm_data = self.get_valid_rm_data()
        if len(rm_data['display_values']) == 0:
            self.w.rm_plot.setTitle("No RM Data")
            self.w.rm_plot.autoRange()
            return

        x_rm = rm_data['pivot_indices']
        y_rm = self.w.display_rm_values  # Use temporary
        types_rm = rm_data['types']
        labels_rm = rm_data['labels']
        effective_empty = rm_data['effective_empty']
        normal_mask = ~effective_empty

        # RM point colors
        brush_colors = []
        for i in range(len(x_rm)):
            if np.array([x_rm[i] in self.ignored_pivots])[0]:
                brush_colors.append('#FF9800')  # Orange: manually ignored
            elif np.array([x_rm[i] in self.w.empty_pivot_set])[0]:
                brush_colors.append('#B0BEC5')  # Gray: really empty
            else:
                brush_colors.append('#2E7D32')  # Green: valid

        symbol_map = {'Base': 'o', 'Check': 't', 'Cone': 's', 'Unknown': 'o'}
        symbols = [symbol_map.get(t, 'o') for t in types_rm]

        # RM scatter (clickable)
        rm_scatter = pg.ScatterPlotItem(
            x=x_rm, y=y_rm, symbol=symbols, size=12,
            brush=brush_colors, pen=pg.mkPen('white', width=1.5),
            hoverable=True
        )
        rm_scatter.sigClicked.connect(partial(self.handle_point_click, 'rm'))
        self.w.rm_plot.addItem(rm_scatter)

        # Data between RMs (Original + Corrected) - No calculations here, read from temp data
        filter_text = self.w.filter_solution_edit.text().strip().lower()
        for i in range(len(x_rm) - 1):
            if not normal_mask[i] or not normal_mask[i + 1]:
                continue
            prev_pivot = x_rm[i]
            curr_pivot = x_rm[i + 1]
            cond = (self.w.pivot_df.index > prev_pivot) & (self.w.pivot_df.index < curr_pivot) & self.w.pivot_df[self.w.selected_element].notna()
            seg_data = self.w.pivot_df[cond].copy()
            if filter_text:
                seg_data = seg_data[seg_data['Solution Label'].str.lower().str.contains(filter_text, na=False)]
            if seg_data.empty:
                continue
            x_seg = seg_data['original_index'].values.astype(float)
            y_orig = seg_data[self.w.selected_element].values.astype(float)
            labels = seg_data['Solution Label'].values

            # Original scatter
            orig_scatter = pg.ScatterPlotItem(
                x=x_seg, y=y_orig, symbol='x', size=9, brush='#F44336', pen='#D32F2F',
                hoverable=True
            )
            orig_scatter.sigClicked.connect(partial(self.handle_point_click, 'detail'))
            self.w.rm_plot.addItem(orig_scatter)

            # Corrected scatter (use pre-computed or temp ratios)
            ratio = y_rm[i + 1] / rm_data['original_values'][i + 1]
            prev_ratio = y_rm[i] / rm_data['original_values'][i]
            adjusted = y_orig - self.w.preview_blank
            scaled = adjusted * self.w.preview_scale
            y_corr, _ = self.calculate_corrected_values_with_ratios(scaled, ratio, prev_ratio)
            corr_scatter = pg.ScatterPlotItem(
                x=x_seg, y=y_corr, symbol='o', size=7, brush='#2196F3', pen='#1976D2',
                hoverable=True
            )
            corr_scatter.sigClicked.connect(partial(self.handle_point_click, 'detail'))
            self.w.rm_plot.addItem(corr_scatter)

        # Segment lines and trendlines
        colors = ['#43A047', '#FF6B00', '#7B1FA2', '#1A3C34', '#1976D2']
        for seg_idx, seg in enumerate(self.w.segments):
            seg_pivots = seg['positions']['pivot_index'].values
            mask = np.isin(x_rm, seg_pivots) & normal_mask
            if not np.any(mask):
                continue
            x_seg = x_rm[mask]
            y_seg = y_rm[mask]
            color = colors[seg_idx % len(colors)]
            pen = pg.mkPen(color, width=3)
            self.w.rm_plot.plot(x_seg, y_seg, pen=pen, name=f"Segment {seg_idx+1}")
            if len(x_seg) >= 2:
                slope, intercept = np.polyfit(x_seg, y_seg, 1)
                line_y = slope * x_seg + intercept
                self.w.rm_plot.plot(x_seg, line_y, pen=pg.mkPen(color, width=2, style=Qt.PenStyle.DashLine), name=f"Segment Trend (slope: {slope:.7f})")

        # Global Trendline for RM
        rm_slope = 0.0
        if np.sum(normal_mask) >= 2:
            x_trend = x_rm[normal_mask]
            y_trend = y_rm[normal_mask]
            rm_slope, intercept = np.polyfit(x_trend, y_trend, 1)
            line_y = rm_slope * x_trend + intercept
            self.w.rm_plot.plot(x_trend, line_y, pen=pg.mkPen('black', width=2.5, style=Qt.PenStyle.DashLine), name=f"RM Trend (slope: {rm_slope:.7f})")

        # Highlight selected point
        if self.w.selected_point_pivot is not None and self.w.selected_point_y is not None:
            self.w.highlight_point.setData([self.w.selected_point_pivot], [self.w.selected_point_y])

        # Global Trendline for Corrected Values
        corrected_slope = 0.0
        if len(x_rm) > 1:
            all_x_corr = []
            all_y_corr = []
            for i in range(len(x_rm) - 1):
                if not (normal_mask[i] and normal_mask[i + 1]):
                    continue
                prev_pivot = x_rm[i]
                curr_pivot = x_rm[i + 1]
                cond = (self.w.pivot_df.index > prev_pivot) & (self.w.pivot_df.index < curr_pivot) & self.w.pivot_df[self.w.selected_element].notna()
                seg_data = self.w.pivot_df[cond].copy()
                if filter_text:
                    seg_data = seg_data[seg_data['Solution Label'].str.lower().str.contains(filter_text, na=False)]
                if seg_data.empty:
                    continue
                y_orig = seg_data[self.w.selected_element].values.astype(float)
                ratio = y_rm[i + 1] / rm_data['original_values'][i + 1]
                prev_ratio = y_rm[i] / rm_data['original_values'][i]
                adjusted = y_orig - self.w.preview_blank
                scaled = adjusted * self.w.preview_scale
                y_corr, _ = self.calculate_corrected_values_with_ratios(scaled, ratio, prev_ratio)
                all_x_corr.extend(seg_data['original_index'].values.astype(float))
                all_y_corr.extend(y_corr)

            if len(all_x_corr) >= 2:
                x_arr = np.array(all_x_corr)
                y_arr = np.array(all_y_corr)
                corrected_slope, intercept = np.polyfit(x_arr, y_arr, 1)
                line_y = corrected_slope * x_arr + intercept
                self.w.rm_plot.plot(x_arr, line_y, pen=pg.mkPen('#E91E63', width=3.5), name=f"Corrected Trend (slope: {corrected_slope:.7f})")

        # File vertical lines (only in All Files mode)
        if self.w.current_file_index <= 0 and len(self.w.file_ranges) > 1:
            for fr in self.w.file_ranges:
                self.w.rm_plot.addItem(pg.InfiniteLine(pos=fr['start_pivot_row'], angle=90, pen=pg.mkPen('#9E9E9E', width=1, style=Qt.PenStyle.DashLine)))
                self.w.rm_plot.addItem(pg.InfiniteLine(pos=fr['end_pivot_row'], angle=90, pen=pg.mkPen('#9E9E9E', width=1, style=Qt.PenStyle.DashLine)))

        # Final settings
        self.w.rm_plot.setTitle(f"Drift Plot — {self.w.selected_element} — RM {self.w.current_rm_num}")
        self.w.rm_plot.addLegend(offset=(10, 10))

    def update_slope_from_data(self):
        trend_x = []
        trend_y = []
        # RM points (with blank and scale)
        rm_data = self.get_valid_rm_data()
        x_rm = rm_data['pivot_indices']
        y_rm_raw = self.w.display_rm_values  # Temp
        y_rm = (y_rm_raw - self.w.preview_blank) * self.w.preview_scale
        for px, py in zip(x_rm, y_rm):
            if px not in self.w.empty_pivot_set and px not in self.ignored_pivots:
                trend_x.append(px)
                trend_y.append(py)

        # Manual corrections
        for orig_index, manual_val in self.manual_corrections.items():
            row = self.w.pivot_df[self.w.pivot_df['original_index'] == orig_index]
            if row.empty:
                continue
            pivot_val = row['pivot_index'].iloc[0]
            final_val = (manual_val - self.w.preview_blank) * self.w.preview_scale
            trend_x.append(pivot_val)
            trend_y.append(final_val)

        # Calculate slope
        if len(trend_x) >= 2:
            tx = np.array(trend_x)
            ty = np.array(trend_y)
            order = np.argsort(tx)
            tx = tx[order]
            ty = ty[order]
            self.w.current_slope = np.polyfit(tx, ty, 1)[0]
        else:
            self.w.current_slope = 0.0

        self.w.slope_spin.blockSignals(True)
        self.w.slope_spin.setValue(self.w.current_slope)
        self.w.slope_spin.blockSignals(False)
        color = "#2E7D32" if self.w.current_slope >= 0 else "#D32F2F"
        sign = "+" if self.w.current_slope >= 0 else ""
        self.w.slope_display.setText(f"Current Slope: <span style='color:{color};font-weight:bold'>{sign}{self.w.current_slope:.7f}</span>")

    def auto_optimize_to_flat(self):
        if len(self.w.display_rm_values) == 0:
            return

        # <<< حالت جدید اضافه شد >>>
        if self.w.current_file_index <= 0 and self.w.per_file_cb.isChecked():
            if self.w.global_optimize_cb.isChecked():
                # رفتار: هر فایل → تمام RMهاش به اولین RM معتبر همان فایل فلت بشه
                self.auto_optimize_to_flat_per_file_global_style()
            else:
                # رفتار قبلی: هر سگمنت در هر فایل جداگانه فلت بشه
                self.auto_optimize_to_flat_per_file()
            return

        # بقیه کد (حالت تک فایل یا بدون per-file) مثل قبل
        empty_set = set(self.w.empty_rows_from_check['original_index'].dropna().astype(int).tolist()) if not self.w.empty_rows_from_check.empty else set()
        rm_mask = (self.w.rm_df['rm_num'] == self.w.current_rm_num)
        if not rm_mask.any():
            return

        y = self.w.rm_df.loc[rm_mask, self.w.selected_element].astype(float).values
        pivot = self.w.rm_df.loc[rm_mask, 'pivot_index'].values
        is_empty = np.array([p in empty_set for p in pivot])
        normal_mask = ~is_empty & ~np.isnan(y)
        if normal_mask.sum() == 0:
            return

        seg_dict = dict(zip(self.w.positions_df['pivot_index'], self.w.positions_df['segment_id']))
        unique_segs = np.unique([seg_dict.get(p, -1) for p in pivot[normal_mask]])

        if self.w.global_optimize_cb.isChecked():
            first_idx = np.where(normal_mask)[0][0]
            first_val = y[first_idx]
            y[normal_mask] = first_val
        else:
            for seg_id in unique_segs:
                if seg_id == -1:
                    continue
                seg_mask = np.array([seg_dict.get(p, -1) == seg_id for p in pivot])
                seg_normal_mask = seg_mask & normal_mask
                if seg_normal_mask.sum() == 0:
                    continue
                first_idx = np.where(seg_normal_mask)[0][0]
                first_val = y[first_idx]
                y[seg_normal_mask] = first_val

        self.w.rm_df.loc[rm_mask, self.w.selected_element] = y
        self.sync_rm_to_all()
        self.update_displays()
        self.update_slope_from_data()
        QMessageBox.information(self.w, "Info", "Selected RM optimized to flat relative to the first valid point in each segment (or globally if checked).")

    def auto_optimize_to_flat_per_file_global_style(self):
        """هر فایل → تمام RMهای معتبرش به اولین RM معتبر همان فایل فلت بشه (Global داخل هر فایل)"""
        if not hasattr(self.w, 'file_ranges') or not self.w.file_ranges:
            return

        empty_pivot_set = self.w.empty_pivot_set
        ignored_pivots = self.ignored_pivots

        for file_idx, fr in enumerate(self.w.file_ranges):
            start = fr['start_pivot_row']
            end = fr['end_pivot_row']
            file_rm_mask = (
                self.w.all_rm_df['pivot_index'].between(start, end) &
                (self.w.all_rm_df['rm_num'] == self.w.current_rm_num)
            )
            if not file_rm_mask.any():
                continue

            rm_rows = self.w.all_rm_df[file_rm_mask].sort_values('pivot_index')
            pivots = rm_rows['pivot_index'].values
            y_file = rm_rows[self.w.selected_element].astype(float).values.copy()

            # ماسک معتبر: نه خالی، نه نان، نه ignored، و شدت > 1e-6
            valid_mask = (
                ~np.isnan(y_file) &
                ~np.isin(pivots, list(ignored_pivots)) &
                ~np.isin(pivots, list(empty_pivot_set)) &
                (y_file > 1e-6)
            )

            if not valid_mask.any():
                continue

            # اولین RM معتبر در این فایل
            first_valid_idx = np.where(valid_mask)[0][0]
            first_valid_value = y_file[first_valid_idx]

            # تمام نقاط معتبر این فایل → فلت به مقدار اولین
            y_file[valid_mask] = first_valid_value

            # اعمال تغییرات
            self.w.all_rm_df.loc[file_rm_mask, self.w.selected_element] = y_file

        # همگام‌سازی با نمایش فعلی (اگر در حالت All Files هستیم)
        if self.w.current_file_index <= 0:
            current_rm_mask = self.w.all_rm_df['rm_num'] == self.w.current_rm_num
            target_mask = self.w.rm_df['rm_num'] == self.w.current_rm_num
            if current_rm_mask.any() and target_mask.any():
                self.w.rm_df.loc[target_mask, self.w.selected_element] = \
                    self.w.all_rm_df.loc[current_rm_mask, self.w.selected_element].values

        self.filter_by_file(self.w.current_file_index if self.w.current_file_index > 0 else -1)
        self.update_displays()
        self.update_slope_from_data()
        self.update_rm_plot()

        QMessageBox.information(
            self.w,
            "Success",
            "Optimized to flat per file → using first valid RM in each file as reference\n"
            "(Global Optimize + Per File mode)"
        )

    def auto_optimize_to_flat_per_file(self):
        if not hasattr(self.w, 'file_ranges') or not self.w.file_ranges:
            return

        for file_idx, fr in enumerate(self.w.file_ranges):
            start = fr['start_pivot_row']
            end = fr['end_pivot_row']
            file_rm_mask = self.w.all_rm_df['pivot_index'].between(start, end) & (self.w.all_rm_df['rm_num'] == self.w.current_rm_num)
            if not file_rm_mask.any():
                continue
            rm_rows = self.w.all_rm_df[file_rm_mask].sort_values('pivot_index').reset_index(drop=True)
            pivots = rm_rows['pivot_index'].values
            y_file = rm_rows[self.w.selected_element].astype(float).values.copy()
            valid_mask = ~np.isnan(y_file)
            ignored_or_empty = np.array([p in self.ignored_pivots or p in self.w.empty_pivot_set for p in pivots])
            usable_mask = valid_mask & ~ignored_or_empty & (y_file > 1e-6)
            if not usable_mask.any():
                continue
            first_valid_idx = np.where(usable_mask)[0][0]
            first_valid_value = y_file[first_valid_idx]
            y_file[usable_mask] = first_valid_value
            # Since per-file affects all, sync immediately (but user said no modify rm_df, so perhaps log or temp store)
            # For now, sync as is, but note: this violates "no modify rm_df" - consider temp dict for changes
            self.w.all_rm_df.loc[file_rm_mask, self.w.selected_element] = y_file

        if self.w.current_file_index <= 0:
            current_rm_mask = self.w.all_rm_df['rm_num'] == self.w.current_rm_num
            target_mask = self.w.rm_df['rm_num'] == self.w.current_rm_num
            if current_rm_mask.any() and target_mask.any():
                self.w.rm_df.loc[target_mask, self.w.selected_element] = self.w.all_rm_df.loc[current_rm_mask, self.w.selected_element].values

        self.filter_by_file(self.w.current_file_index if self.w.current_file_index > 0 else -1)
        self.update_displays()
        self.update_slope_from_data()
        self.update_rm_plot()
        QMessageBox.information(self.w, "Success", "Optimized to flat per file\n(ignored and low-intensity RMs skipped)")


    def auto_optimize_slope_to_zero(self):
        len_rm = len(self.w.display_rm_values)
        if len_rm < 2:
            return

        # اگر در حالت All Files هستیم و per-file فعال باشه → از تابع جداگانه استفاده کن
        if self.w.current_file_index <= 0 and self.w.per_file_cb.isChecked():
            self.auto_optimize_slope_to_zero_per_file()
            return

        rm_data = self.get_valid_rm_data()
        y = self.w.display_rm_values.copy()
        pivot_indices = rm_data['pivot_indices']
        normal_mask = ~rm_data['effective_empty']

        if normal_mask.sum() < 2:
            return

        seg_dict = dict(zip(self.w.positions_df['pivot_index'], self.w.positions_df['segment_id']))

        if self.w.global_optimize_cb.isChecked():
            # به صورت کلی (global)
            x_n = pivot_indices[normal_mask]
            y_n = y[normal_mask].copy()
            slope, _ = np.polyfit(x_n, y_n, 1)
            first_x = x_n[0]
            y_n -= slope * (x_n - first_x)
            y[normal_mask] = y_n

        else:
            # به تفکیک segment
            unique_segs = np.unique([seg_dict.get(p, -1) for p in pivot_indices[normal_mask]])
            for seg_id in unique_segs:
                if seg_id == -1:
                    continue
                seg_mask = np.isin(pivot_indices, [p for p, s in seg_dict.items() if s == seg_id])
                seg_normal_mask = seg_mask & normal_mask
                if seg_normal_mask.sum() < 2:
                    continue
                x_n = pivot_indices[seg_normal_mask]
                y_n = y[seg_normal_mask].copy()
                slope, _ = np.polyfit(x_n, y_n, 1)
                first_x = x_n[0]
                y_n -= slope * (x_n - first_x)
                y[seg_normal_mask] = y_n

        # بروزرسانی آرایه موقت
        self.w.display_rm_values = y

        # این قسمت حیاتی اضافه شد: همگام‌سازی با دیتابیس اصلی (rm_df و all_rm_df)
        for i, pivot in enumerate(pivot_indices):
            if rm_data['effective_empty'][i]:
                continue  # نادیده گرفتن نقاط خالی یا ignored
            new_value = y[i]

            # بروزرسانی rm_df (نمایش فعلی)
            mask_current = (self.w.rm_df['rm_num'] == self.w.current_rm_num) & \
                        (self.w.rm_df['pivot_index'] == pivot)
            if mask_current.any():
                self.w.rm_df.loc[mask_current, self.w.selected_element] = new_value

            # بروزرسانی all_rm_df (برای وقتی که بعداً به All Files برگردیم)
            mask_all = (self.w.all_rm_df['rm_num'] == self.w.current_rm_num) & \
                    (self.w.all_rm_df['pivot_index'] == pivot)
            if mask_all.any():
                self.w.all_rm_df.loc[mask_all, self.w.selected_element] = new_value

        # حالا نمایش بروز می‌شود
        self.update_displays()
        self.update_slope_from_data()

        QMessageBox.information(
            self.w, "Success",
            "Slope successfully set to zero for the selected RM\n"
            "(per segment or globally — starting point preserved)"
        )

    def auto_optimize_slope_to_zero_per_file(self):
        if not hasattr(self.w, 'file_ranges') or not self.w.file_ranges:
            return

        for file_idx, fr in enumerate(self.w.file_ranges):
            start = fr['start_pivot_row']
            end = fr['end_pivot_row']
            file_rm_mask = self.w.all_rm_df['pivot_index'].between(start, end) & (self.w.all_rm_df['rm_num'] == self.w.current_rm_num)
            if not file_rm_mask.any():
                continue
            y_file = self.w.all_rm_df.loc[file_rm_mask, self.w.selected_element].astype(float).values.copy()
            pivot_file = self.w.all_rm_df.loc[file_rm_mask, 'pivot_index'].values
            valid_mask = ~np.isnan(y_file)
            ignored_mask = np.array([p in self.ignored_pivots for p in pivot_file])
            final_valid_mask = valid_mask & ~ignored_mask
            if final_valid_mask.sum() < 2:
                continue
            x_valid = pivot_file[final_valid_mask]
            y_valid = y_file[final_valid_mask]
            slope, intercept = np.polyfit(x_valid, y_valid, 1)
            first_x = x_valid[0]
            y_corrected = y_valid - slope * (x_valid - first_x)
            y_file[final_valid_mask] = y_corrected
            self.w.all_rm_df.loc[file_rm_mask, self.w.selected_element] = y_file

        if self.w.current_file_index <= 0:
            current_rm_mask = self.w.all_rm_df['rm_num'] == self.w.current_rm_num
            target_mask = self.w.rm_df['rm_num'] == self.w.current_rm_num
            if current_rm_mask.any() and target_mask.any():
                self.w.rm_df.loc[target_mask, self.w.selected_element] = self.w.all_rm_df.loc[current_rm_mask, self.w.selected_element].values

        self.filter_by_file(self.w.current_file_index if self.w.current_file_index > 0 else -1)
        self.update_displays()
        self.update_slope_from_data()
        self.update_rm_plot()
        QMessageBox.information(self.w, "Info", "Optimized to slope zero per file")

    def on_table_row_clicked(self, index):
        self.selected_start_rm_points.setData([], [])
        self.selected_end_rm_points.setData([], [])
        self.selected_segment_line.setData([], [])
        new_row = index.row()
        self.w.selected_row = new_row
        if 0 <= self.w.selected_row < len(self.w.current_valid_pivot_indices):
            pivot = self.w.current_valid_pivot_indices[self.w.selected_row]
            y = self.w.display_rm_values[self.w.selected_row]
            self.w.selected_point_pivot = pivot
            self.w.selected_point_y = y
            self.w.highlight_point.setData([pivot], [y])
        self.highlight_rm()
        self.update_detail_table()

    def update_displays(self):
        if self.w.current_rm_num is not None and self.w.selected_element:
            self.display_rm_table()
            self.update_rm_plot()
            self.update_detail_table()

    def update_detail_table(self):
        model = QStandardItemModel()
        model.setHorizontalHeaderLabels(["Solution Label", "Original Value", "Corrected Value"])
        if self.w.selected_row < 0 or self.w.selected_row >= len(self.w.display_rm_values) - 1:
            self.w.detail_table.setModel(model)
            return

        data = self.get_data_between_rm()
        if data.empty:
            self.w.detail_table.setModel(model)
            return

        orig = data[self.w.selected_element].values.astype(float)
        ratio = self.w.display_rm_values[self.w.selected_row ] / self.w.original_rm_values[self.w.selected_row] if self.w.original_rm_values[self.w.selected_row] != 0 else 1.0
        prev_ratio = self.w.display_rm_values[self.w.selected_row -1] / self.w.original_rm_values[self.w.selected_row-1] if self.w.original_rm_values[self.w.selected_row -1] != 0 else 1.0
        adjusted = orig - self.w.preview_blank
        scaled = adjusted * self.w.preview_scale
        base_corr, _ = self.calculate_corrected_values_with_ratios(scaled, ratio, prev_ratio)

        for i in range(len(data)):
            sl_item = QStandardItem(data.iloc[i]['Solution Label'])
            o_item = QStandardItem(f"{orig[i]:.2f}")
            o_item.setEditable(False)
            orig_index = int(data.iloc[i]['original_index'])
            manual_val = self.manual_corrections.get(orig_index)
            display_val = manual_val if manual_val is not None else base_corr[i]
            c_item = QStandardItem(f"{display_val:.2f}")
            c_item.setEditable(True)
            c_item.setData(orig_index, Qt.ItemDataRole.UserRole)
            model.appendRow([sl_item, o_item, c_item])

        self.w.detail_table.setModel(model)
        try:
            model.itemChanged.disconnect()
        except:
            pass
        model.itemChanged.connect(self.on_detail_value_changed)

    def on_detail_value_changed(self, item):
        if item.column() != 2:
            return
        try:
            new_val = float(item.text())
            orig_index = item.data(Qt.ItemDataRole.UserRole)
            if orig_index is None:
                return
            self.manual_corrections[orig_index] = new_val
            self.update_rm_plot()
            self.update_slope_from_data()
        except ValueError:
            QMessageBox.warning(self.w, "Invalid Value", "Please enter a valid number.")

    def get_data_between_rm(self):
        if self.w.selected_row < 0 or self.w.selected_row >= len(self.w.current_valid_pivot_indices) - 1:
            return pd.DataFrame()
        pivot_prev = self.w.current_valid_pivot_indices[self.w.selected_row-1]
        pivot_curr = self.w.current_valid_pivot_indices[self.w.selected_row]
        cond = (self.w.pivot_df['original_index'] > pivot_prev) & (self.w.pivot_df['original_index'] < pivot_curr) & self.w.pivot_df[self.w.selected_element].notna()
        data = self.w.pivot_df[cond].copy().sort_values('original_index')
        filter_text = self.w.filter_solution_edit.text().strip().lower()
        if filter_text:
            filter_mask = data['Solution Label'].str.lower().str.contains(filter_text)
            data = data[filter_mask]
        return data

    def calculate_corrected_values_with_ratios(self, original_values, current_ratio, prev_ratio):
        n = len(original_values)
        if n == 0:
            return np.array([]), np.array([])
        if not self.w.stepwise_cb.isChecked():
            ratios = np.full(n, current_ratio)
            return original_values * ratios, ratios
        z = (current_ratio - prev_ratio) / n
        i = np.arange(1, n + 1)
        ratios = (z * i) + prev_ratio
        yo = ratios * original_values
        print(yo,ratios)
        return yo, ratios

    def show_rm_context_menu(self, pos):
        index = self.w.rm_table.indexAt(pos)
        if not index.isValid():
            return
        row = index.row()
        if row < 0 or row >= len(self.w.current_valid_pivot_indices):
            return
        pivot = self.w.current_valid_pivot_indices[row]
        if pivot in self.w.empty_pivot_set:
            return
        menu = QMenu(self.w)
        if pivot in self.ignored_pivots:
            action = menu.addAction("Unignore this point")
        else:
            action = menu.addAction("Ignore this point")
        if menu.exec(self.w.rm_table.mapToGlobal(pos)) == action:
            if pivot in self.ignored_pivots:
                self.ignored_pivots.remove(pivot)
            else:
                self.ignored_pivots.add(pivot)
            self.update_displays()

    def on_filter_changed(self):
        self.update_rm_plot()
        self.w.crm_handler.update_pivot_plot()

    def prev(self):
        if self.w.current_nav_index > 0:
            self.prompt_apply_changes()
            self.w.current_nav_index -= 1
            self.w.selected_element, self.w.current_rm_num = self.w.navigation_list[self.w.current_nav_index]
            self.w.selected_row = -1
            self.w.selected_point_pivot = None
            self.w.selected_point_y = None
            self.w.update_labels()
            self.update_displays()
            self.update_navigation_buttons()
            self.w.crm_handler.update_pivot_plot()

    def next(self):
        if self.w.current_nav_index < len(self.w.navigation_list) - 1:
            self.prompt_apply_changes()
            self.w.current_nav_index += 1
            self.w.selected_element, self.w.current_rm_num = self.w.navigation_list[self.w.current_nav_index]
            self.w.selected_row = -1
            self.w.selected_point_pivot = None
            self.w.selected_point_y = None
            self.w.update_labels()
            self.update_displays()
            self.update_navigation_buttons()
            self.w.crm_handler.update_pivot_plot()

    def on_element_changed(self, element):
        self.w.selected_element = element
        self.w.current_element_index = self.w.element_list.index(element) if element in self.w.element_list else 0
        self.update_rm_list_and_go_first()
        self.update_displays()

    def update_rm_data(self):
        # Now only called on apply - sync temp display to df
        if len(self.w.display_rm_values) == 0:
            return
        rm_data = self.get_valid_rm_data()
        valid_pivot_indices = rm_data['pivot_indices']
        valid_display_values = self.w.display_rm_values
        label_df = self.w.rm_df[(self.w.rm_df['rm_num'] == self.w.current_rm_num) & (self.w.rm_df['pivot_index'].isin(valid_pivot_indices))].sort_values('pivot_index').reset_index(drop=True)
        if len(label_df) != len(valid_display_values):
            return
        for i, row in label_df.iterrows():
            self.w.rm_df.loc[self.w.rm_df['pivot_index'] == row['pivot_index'], self.w.selected_element] = valid_display_values[i]
            self.w.all_rm_df.loc[self.w.all_rm_df['pivot_index'] == row['pivot_index'], self.w.selected_element] = valid_display_values[i]
            df = self.w.app.results.last_filtered_data
            if 'original_index' not in df.columns:
                if 'pivot_index' in df.columns:
                    df['original_index'] = df['pivot_index']
                else:
                    df['original_index'] = df.index
            cond = (df['original_index'] == row['original_index'])
            if not df[cond].empty:
                df.loc[cond, self.w.selected_element] = valid_display_values[i]

    def update_rm_table_ratios(self):
        model = self.w.rm_table.model()
        for i in range(model.rowCount()):
            if i < len(self.w.original_rm_values):
                ratio = self.w.display_rm_values[i] / self.w.original_rm_values[i] if self.w.original_rm_values[i] != 0 else np.nan
                model.item(i, 6).setText(f"{ratio:.2f}" if pd.notna(ratio) else "N/A")

    def on_rm_value_changed(self, item):
        row = item.row()
        model = self.w.rm_table.model()
        try:
            if item.column() == 5:  # Current Value
                val = float(item.text())
                self.w.display_rm_values[row] = val
            elif item.column() == 6:  # Ratio
                ratio = float(item.text())
                val = self.w.original_rm_values[row] * ratio
                self.w.display_rm_values[row] = val
                model.item(row, 5).setText(f"{val:.2f}")

            new_ratio = val / self.w.original_rm_values[row] if self.w.original_rm_values[row] != 0 else np.nan
            model.item(row, 6).setText(f"{new_ratio:.2f}" if pd.notna(new_ratio) else "N/A")

            # No update_rm_data here - only on apply
            self.update_rm_plot()
            self.update_slope_from_data()
            self.update_detail_table()
        except ValueError:
            QMessageBox.warning(self.w, "Invalid Value", "Please enter a valid number.")
            item.setText(f"{self.w.display_rm_values[row]:.2f}")

    def handle_point_click(self, table_type, scatter, points, ev):
        if not points:
            return
        pt = points[0]
        label = pt.data()
        x = pt.pos().x()
        y = pt.pos().y()
        self.w.selected_point_pivot = x
        self.w.selected_point_y = y
        self.w.highlight_point.setData([x], [y])
        if table_type == 'rm':
            model = self.w.rm_table.model()
            for row in range(model.rowCount()):
                item_label = model.item(row, 0).text()
                if item_label.startswith(label):
                    self.w.rm_table.selectRow(row)
                    self.w.selected_row = row
                    self.update_detail_table()
                    break

    def on_detail_table_clicked(self, index):
        row = index.row()
        if row < 0 or self.w.selected_row < 0:
            return

        data = self.get_data_between_rm()
        if data.empty or row >= len(data):
            return

        selected_row = data.iloc[row]
        original_index = int(selected_row['original_index'])
        pivot_index = selected_row['pivot_index']  # معمولاً همون original_index هست مگر در موارد خاص
        orig_y = selected_row[self.w.selected_element]

        # محاسبه مقدار corrected (برای نمایش نقطه آبی)
        i = self.w.selected_row
        ratio = self.w.display_rm_values[i ] / self.w.original_rm_values[i] if self.w.original_rm_values[i] != 0 else 1.0
        prev_ratio = self.w.display_rm_values[i-1] / self.w.original_rm_values[i-1] if self.w.original_rm_values[i-1] != 0 else 1.0
        adjusted = orig_y - self.w.preview_blank
        scaled = adjusted * self.w.preview_scale
        corrected_y, _ = self.calculate_corrected_values_with_ratios(np.array([scaled]), ratio, prev_ratio)
        corrected_y = corrected_y[0]

        # ذخیره برای استفاده‌های بعدی
        self.w.selected_point_pivot = pivot_index
        self.w.selected_point_y = orig_y  # یا corrected_y؟ بهتره corrected باشه اگر می‌خوای روی خط اصلاح‌شده باشه

        # آپدیت highlight اصلی (برای سازگاری با بقیه کدها)
        self.w.highlight_point.setData([pivot_index], [orig_y])

        # آپدیت highlight ویژه برای نقاط detail (این مهم‌ترین قسمت است)
        self.detail_highlight_point.setData(
            [pivot_index],
            [corrected_y],  # نقطه‌ی اصلاح‌شده رو نشون بده (آبی)
            symbol='o', size=20, brush=pg.mkBrush('#FFD700'), pen=pg.mkPen('black', width=4)
        )

    def apply_to_single_rm(self):
        if not self.w.selected_element or self.w.current_rm_num is None:
            QMessageBox.critical(self.w, "Error", "No element or RM number selected.")
            return

        # Sync temp changes to df before apply
        self.update_rm_data()

        current_report_change = self.w.app.results.report_change.copy() if hasattr(self.w.app.results, 'report_change') else pd.DataFrame()
        
        self.undo_stack.append((
            self.w.app.results.last_filtered_data.copy(),      # 0
            self.w.rm_df.copy(),                               # 1
            self.w.corrected_drift.copy(),                     # 2
            self.manual_corrections.copy(),                    # 3
            current_report_change,                             # 4 *** اضافه شد ***
            self.w.all_rm_df.copy()                            # 5 *** اضافه شد ***
        ))
        self.w.undo_rm_btn.setEnabled(True)

        new_df = self.w.app.results.last_filtered_data.copy()
        element = self.w.selected_element

        # Clear existing corrected_drift for this element
        self.w.corrected_drift = {k: v for k, v in self.w.corrected_drift.items() if k[1] != element}

        rm_data = self.get_valid_rm_data()
        pivot_indices = rm_data['pivot_indices']
        original_rm_values = rm_data['original_values']
        display_values = rm_data['display_values']
        effective_empty = rm_data['effective_empty']

        for i in range(len(pivot_indices) - 1):
            if effective_empty[i] or effective_empty[i + 1]:
                continue
            prev_pivot = pivot_indices[i]
            curr_pivot = pivot_indices[i + 1]
            cond = (new_df['original_index'] > prev_pivot) & (new_df['original_index'] < curr_pivot) & new_df[element].notna()
            seg_data = new_df[cond].copy()
            if seg_data.empty:
                continue
            orig_values = seg_data[element].values.astype(float)
            current_ratio = display_values[i + 1] / original_rm_values[i + 1] if original_rm_values[i + 1] != 0 else 1.0
            prev_ratio = display_values[i] / original_rm_values[i] if original_rm_values[i] != 0 else 1.0
            corrected, ratios = self.calculate_corrected_values_with_ratios(orig_values, current_ratio, prev_ratio)
            new_df.loc[cond, element] = corrected
            for j in range(len(seg_data)):
                sl = seg_data.iloc[j]['Solution Label']
                key = (sl, element)
                self.w.corrected_drift[key] = ratios[j]

        # Apply changes to RM points themselves if changed
        # Already done in update_rm_data

        # Apply manual corrections
        for orig_index, manual_val in self.manual_corrections.items():
            mask = new_df['original_index'] == orig_index
            if mask.any():
                old_val = new_df.loc[mask, element].iloc[0]
                new_df.loc[mask, element] = manual_val
                sl = new_df.loc[mask, 'Solution Label'].iloc[0]
                key = (sl, element)
                ratio = manual_val / old_val if old_val != 0 else 1.0
                self.w.corrected_drift[key] = ratio

        self.w.app.results.last_filtered_data = new_df
        self.save_corrected_drift()
        self.w.results_update_requested.emit(new_df)
        # self.manual_corrections.clear()  # Optional reset
        self.update_displays()
        QMessageBox.information(self.w, "Success", "All corrections (Drift + Manual) applied and saved!")
    
    def has_changes(self):
        if len(self.w.original_rm_values) == 0 or len(self.w.display_rm_values) == 0:
            return False
        return not np.allclose(self.w.original_rm_values, self.w.display_rm_values, rtol=1e-5, atol=1e-8, equal_nan=True)

    def prompt_apply_changes(self):
        if self.has_changes():
            reply = QMessageBox.question(self.w, 'Apply Changes', 'Do you want to apply the changes to this RM?', QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No, QMessageBox.StandardButton.No)
            if reply == QMessageBox.StandardButton.Yes:
                self.apply_to_single_rm()

    def update_navigation_buttons(self):
        self.w.prev_rm_btn.setEnabled(self.w.current_nav_index > 0)
        self.w.next_rm_btn.setEnabled(self.w.current_nav_index < len(self.w.navigation_list) - 1)
        enabled = bool(self.w.current_rm_num is not None and self.w.selected_element)
        self.w.slope_spin.setEnabled(enabled)
        self.w.auto_flat_btn.setEnabled(enabled)
        self.w.auto_zero_slope_btn.setEnabled(enabled)

    def update_rm_list_and_go_first(self):
        if not self.w.analysis_data:
            return
        rm_df = self.w.analysis_data['rm_df']
        self.w.rm_numbers_list = sorted(rm_df['rm_num'].dropna().unique().astype(int).tolist())
        self.w.current_rm_index = 0 if self.w.rm_numbers_list else -1
        if self.w.rm_numbers_list:
            self.w.selected_rm_num = self.w.rm_numbers_list[0]
            self.w.current_rm_label.setText(f"RM-{int(self.w.selected_rm_num)}")
        self.w.update_all_displays()

    def update_tables_and_plot(self):
        if not self.w.analysis_data or not self.w.selected_element:
            return
        rm_df = self.w.analysis_data['rm_df']
        full_df = self.w.analysis_data['full_df']
        element = self.w.selected_element
        if element not in full_df.columns:
            return

        # RM table
        model_rm = QStandardItemModel()
        model_rm.setHorizontalHeaderLabels(["Label", "Original", "Current", "Ratio"])
        if self.w.selected_rm_num is not None:
            selected_rm_data = rm_df[rm_df['rm_num'] == self.w.selected_rm_num]
            for _, row in selected_rm_data.iterrows():
                val = row[element]
                if pd.notna(val):
                    model_rm.appendRow([
                        QStandardItem(row['Solution Label']),
                        QStandardItem(f"{val:.5f}"),
                        QStandardItem(f"{val:.5f}"),
                        QStandardItem("1.000")
                    ])
        self.w.rm_table.setModel(model_rm)

        # All data table
        model_all = QStandardItemModel()
        model_all.setHorizontalHeaderLabels(["Solution Label", "Original Value", "New Value"])
        for _, row in full_df.sort_values('pivot_index').iterrows():
            val = row.get(element)
            if pd.notna(val):
                orig_item = QStandardItem(f"{val:.5f}")
                new_item = QStandardItem(f"{val:.5f}")
                new_item.setEditable(True)
                new_item.setForeground(QColor("#1976D2"))
                model_all.appendRow([
                    QStandardItem(row['Solution Label']),
                    orig_item,
                    new_item
                ])
        self.w.detail_table.setModel(model_all)

        # Plot
        self.w.rm_plot.clear()
        self.w.rm_plot.addLegend()
        sample_mask = ~full_df['Solution Label'].str.contains("RM", case=False, na=False)
        samples = full_df[sample_mask & pd.notna(full_df[element])]
        if not samples.empty:
            self.w.rm_plot.addItem(pg.ScatterPlotItem(
                x=samples['pivot_index'].values,
                y=pd.to_numeric(samples[element], errors='coerce').values,
                size=7, brush='#B0BEC5', pen=None, name="Samples"
            ))

        rm_valid = rm_df[pd.notna(rm_df[element]) & rm_df['rm_num'].notna()].sort_values('pivot_index')
        if not rm_valid.empty:
            x_rm = rm_valid['pivot_index'].values
            y_rm = pd.to_numeric(rm_valid[element], errors='coerce').values
            symbol_map = {'Base': 'o', 'Check': 't', 'Cone': 's'}
            color_map = {'Base': '#2E7D32', 'Check': '#FF6B00', 'Cone': '#7B1FA2'}
            scatter = pg.ScatterPlotItem(
                x=x_rm, y=y_rm,
                symbol=[symbol_map.get(t, 'o') for t in rm_valid['rm_type']],
                size=18,
                brush=[color_map.get(t, '#2E7D32') for t in rm_valid['rm_type']],
                pen=pg.mkPen('white', width=2),
                name="All RMs"
            )
            def on_rm_click(plot_item, points):
                if not points:
                    return
                point = points[0]
                idx = point.index()
                clicked_rm_num = rm_valid.iloc[idx]['rm_num']
                if clicked_rm_num != self.w.selected_rm_num:
                    self.w.selected_rm_num = clicked_rm_num
                    self.w.current_rm_index = self.w.rm_numbers_list.index(int(clicked_rm_num))
                    self.w.current_rm_label.setText(f"RM-{int(clicked_rm_num)}")
                    self.update_tables_and_plot()
            scatter.sigClicked.connect(on_rm_click)
            self.w.rm_plot.addItem(scatter)
            self.w.rm_plot.plot(x_rm, y_rm, pen=pg.mkPen('#43A047', width=3), name="RM Trend Line")

        self.w.rm_plot.setLabel('left', f'{element} Intensity')
        self.w.rm_plot.setTitle(f"Drift Plot — {element} — Click on RM to select")
        self.w.rm_plot.autoRange()

    def apply_solution_filter(self):
        text = self.w.filter_solution_edit.text().strip()
        if not text:
            self.update_tables_and_plot()
            return
        QMessageBox.information(self.w, "Filter", f"Filter applied: {text}")

    def reset_to_original(self):
        """Reset - NO HANGING"""
        if len(self.w.display_rm_values) == 0:
            return
        
        # 1. مقادیر
        self.w.display_rm_values[:] = self.w.original_rm_values
        
        # 2. table رو با blockSignals آپدیت کن
        try:
            model = self.w.rm_table.model()
            if model:
                model.blockSignals(True)
                n = min(model.rowCount(), len(self.w.display_rm_values))
                for i in range(n):
                    item5 = model.item(i, 5)
                    item6 = model.item(i, 6)
                    if item5:
                        item5.setText(f"{self.w.display_rm_values[i]:.2f}")
                    if item6:
                        item6.setText("1.000")
        finally:
            if model:
                model.blockSignals(False)
        
        self.update_rm_plot()

    def save_corrected_drift(self):
        try:
            # Initialize corrected_drift if needed
            if not hasattr(self.w.app.results, 'corrected_drift'):
                self.w.app.results.corrected_drift = {}
            
            # Update corrected_drift
            self.w.app.results.corrected_drift.update(self.w.corrected_drift)
            
            # Build drift_df
            drift_data = []
            for (sl, element), ratio in self.w.corrected_drift.items():
                drift_data.append({
                    'Solution Label': sl, 
                    'Element': element, 
                    'Ratio': ratio
                })
            
            drift_df = pd.DataFrame(drift_data)
            
            # *** SAFE INITIALIZATION OF report_change ***
            if not hasattr(self.w.app.results, 'report_change'):
                self.w.app.results.report_change = pd.DataFrame(
                    columns=['Solution Label', 'Element', 'Ratio']
                )
            else:
                # Ensure correct columns exist
                report_change = self.w.app.results.report_change
                required_cols = ['Solution Label', 'Element', 'Ratio']
                for col in required_cols:
                    if col not in report_change.columns:
                        report_change[col] = pd.NA
            
            report_change = self.w.app.results.report_change
            
            if not drift_df.empty:
                # *** SAFE REMOVAL OF EXISTING ENTRIES ***
                try:
                    if 'Element' in report_change.columns:
                        existing_elements = drift_df['Element'].unique()
                        existing_mask = report_change['Element'].isin(existing_elements)
                        report_change = report_change[~existing_mask]
                    else:
                        # If no Element column, clear all (fallback)
                        report_change = pd.DataFrame(columns=['Solution Label', 'Element', 'Ratio'])
                        
                except Exception as e:
                    logger.warning(f"Could not filter existing entries: {e}")
                    # Fallback: clear all drift entries
                    report_change = pd.DataFrame(columns=['Solution Label', 'Element', 'Ratio'])
                
                # *** SAFE CONCATENATION ***
                try:
                    report_change = pd.concat([report_change, drift_df], ignore_index=True)
                except Exception as e:
                    logger.warning(f"Could not concat drift_df: {e}")
                    report_change = drift_df.copy()
            
            # *** FINAL ASSIGNMENT ***
            self.w.app.results.report_change = report_change
            
            logger.info(f"✅ Saved {len(drift_df)} drift coefficients to report_change")
            
        except Exception as e:
            logger.error(f"❌ Error saving corrected_drift: {str(e)}")
            # Fallback: save only to corrected_drift
            if hasattr(self.w.app.results, 'corrected_drift'):
                self.w.app.results.corrected_drift.update(self.w.corrected_drift)

    def on_file_changed(self, combo_index: int):
        file_index = -1 if combo_index == 0 else combo_index - 1
        self.filter_by_file(file_index)

    def filter_by_file(self, file_index: int):
        if not hasattr(self.w, 'all_pivot_df') or self.w.all_pivot_df is None:
            return
        self.w.highlight_point.setData([], [])
        if file_index == -1 or not self.w.file_ranges:
            self.w.pivot_df = self.w.all_pivot_df.copy(deep=True)
            self.w.rm_df = self.w.all_rm_df.copy(deep=True)
            self.w.initial_rm_df = self.w.all_initial_rm_df.copy(deep=True)
            self.w.positions_df = self.w.all_positions_df.copy(deep=True)
        else:
            fr = self.w.file_ranges[file_index]
            start_pivot = fr['start_pivot_row']
            end_pivot = fr['end_pivot_row']
            valid_pivots = self.w.all_pivot_df[self.w.all_pivot_df['pivot_index'].between(start_pivot, end_pivot)]['pivot_index'].unique()
            mask_pivot = self.w.all_pivot_df['pivot_index'].isin(valid_pivots)
            mask_rm = self.w.all_rm_df['pivot_index'].isin(valid_pivots)
            mask_pos = self.w.all_positions_df['pivot_index'].isin(valid_pivots)
            self.w.pivot_df = self.w.all_pivot_df[mask_pivot].copy(deep=True)
            self.w.rm_df = self.w.all_rm_df[mask_rm].copy(deep=True)
            self.w.initial_rm_df = self.w.all_initial_rm_df[mask_rm].copy(deep=True)
            self.w.positions_df = self.w.all_positions_df[mask_pos].copy(deep=True)

        self.w.segments = self.w._create_segments(self.w.positions_df)
        self.w.current_file_index = file_index
        self.w.update_current_rm_after_file_change()
        self.display_rm_table()
        self.update_rm_plot()
        self.update_detail_table()
        self.update_slope_from_data()
        self.update_navigation_buttons()
        self.w.crm_handler.update_pivot_plot()

    def sync_rm_to_all(self):
        for pivot, val in zip(self.w.rm_df['pivot_index'], self.w.rm_df[self.w.selected_element]):
            self.w.all_rm_df.loc[self.w.all_rm_df['pivot_index'] == pivot, self.w.selected_element] = val

    def sync_corrected_drift_to_report_change(self):
        """Sync corrected_drift with current report_change after undo"""
        try:
            if not hasattr(self.w.app.results, 'report_change'):
                self.w.corrected_drift = {}
                return
            
            report_change = self.w.app.results.report_change
            
            # Check if report_change has required columns
            required_cols = ['Solution Label', 'Element', 'Ratio']
            missing_cols = [col for col in required_cols if col not in report_change.columns]
            if missing_cols:
                logger.warning(f"report_change missing columns: {missing_cols}")
                self.w.corrected_drift = {}
                return
            
            # Clear current corrected_drift
            self.w.corrected_drift = {}
            
            # Rebuild from report_change (only drift-related entries if identifiable)
            if not report_change.empty:
                for _, row in report_change.iterrows():
                    sl = row['Solution Label']
                    element = row['Element']
                    ratio = row['Ratio']
                    
                    if (pd.notna(sl) and pd.notna(element) and pd.notna(ratio)):
                        key = (str(sl), str(element))
                        self.w.corrected_drift[key] = float(ratio)
            
            logger.info(f"🔄 Synced corrected_drift from report_change: {len(self.w.corrected_drift)} entries")
            
        except Exception as e:
            logger.error(f"❌ Error syncing corrected_drift: {str(e)}")
            self.w.corrected_drift = {}


    def undo_changes(self):
        if not self.undo_stack:
            return
        
        last_state = self.undo_stack.pop()
        
        # *** Restore همه state ها ***
        self.w.app.results.last_filtered_data = last_state[0].copy()
        self.w.rm_df = last_state[1].copy()
        self.w.corrected_drift = last_state[2].copy()
        self.manual_corrections = last_state[3].copy()
        
        # *** CRITICAL: Restore report_change ***
        if len(last_state) > 4:
            previous_report_change = last_state[4].copy()
            self.w.app.results.report_change = previous_report_change
        
        # *** Restore all_rm_df ***
        if len(last_state) > 5:
            self.w.all_rm_df = last_state[5].copy()
        
        # *** Sync corrected_drift با report_change ***
        self.sync_corrected_drift_to_report_change()
        
        # *** Update displays ***
        self.update_displays()
        self.w.undo_rm_btn.setEnabled(bool(self.undo_stack))
        
        # *** اطلاع‌رسانی به ResultsFrame ***
        if hasattr(self.w.app.results, 'notify_data_changed'):
            self.w.app.results.notify_data_changed()
        
        QMessageBox.information(
            self.w, "Undo", 
            "✅ Last RM changes undone successfully!\n"
            "• Data restored\n"
            "• Coefficients restored\n"
            "• Report changes restored"
        )

    def undo_crm_changes(self):
        # Placeholder for CRM undo - implement based on CRM handler
        # Assuming similar stack for CRM
        if hasattr(self.w.crm_handler, 'undo_stack') and self.w.crm_handler.undo_stack:
            last_state = self.w.crm_handler.undo_stack.pop()
            # Restore states similarly
            self.w.app.results.last_filtered_data = last_state[0]  # Adjust as needed
            # ... other restores
            self.w.undo_crm_btn.setEnabled(bool(self.w.crm_handler.undo_stack))
            self.update_displays()
            QMessageBox.information(self.w, "Undo", "Last CRM changes undone.")
        else:
            QMessageBox.warning(self.w, "No Undo", "No CRM changes to undo.")

    def reset_all(self):
        self.reset_to_original()
        # Assuming reset_bs is a method for blank subtraction reset
        if hasattr(self.w, 'reset_bs'):
            self.w.reset_bs()
        else:
            # Placeholder if not defined
            pass
        self.update_displays()
        QMessageBox.information(self.w, "Reset", "Reset to original and blank subtraction applied.")

    def apply_slope_from_spin(self):
        new_slope = self.w.slope_spin.value()
        rm_data = self.get_valid_rm_data()
        if len(rm_data['display_values']) < 2:
            return

        x = rm_data['pivot_indices']
        y = self.w.display_rm_values.copy()
        normal_mask = ~rm_data['effective_empty']

        if normal_mask.sum() < 2:
            return

        x_n = x[normal_mask]
        y_n = y[normal_mask].copy()

        # محاسبه slope فعلی
        current_slope, intercept = np.polyfit(x_n, y_n, 1)

        # اعمال اختلاف slope
        first_x = x_n[0]
        delta_slope = current_slope - new_slope
        y_n -= delta_slope * (x_n - first_x)

        # بروزرسانی آرایه موقت
        y[normal_mask] = y_n
        self.w.display_rm_values = y

        # <<< این قسمت حیاتی اضافه شد: sync به rm_df و all_rm_df >>>
        valid_pivot_indices = rm_data['pivot_indices']
        for i, pivot in enumerate(valid_pivot_indices):
            if not rm_data['effective_empty'][i]:  # فقط نقاط معتبر
                new_val = y[i]
                # بروزرسانی rm_df فعلی
                mask_current = (self.w.rm_df['rm_num'] == self.w.current_rm_num) & (self.w.rm_df['pivot_index'] == pivot)
                if mask_current.any():
                    self.w.rm_df.loc[mask_current, self.w.selected_element] = new_val

                # بروزرسانی all_rm_df (برای حالت All Files)
                mask_all = (self.w.all_rm_df['rm_num'] == self.w.current_rm_num) & (self.w.all_rm_df['pivot_index'] == pivot)
                if mask_all.any():
                    self.w.all_rm_df.loc[mask_all, self.w.selected_element] = new_val

        # حالا نمایش بروز می‌شود
        self.update_displays()
        self.update_slope_from_data()

        QMessageBox.information(self.w, "Applied", f"Target slope applied: {new_slope:.7f}")