# screens/process/verification/master_verification.py
import logging
import pandas as pd
import numpy as np
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QCheckBox, QLabel, QLineEdit, QPushButton,
    QGroupBox, QSlider, QComboBox, QTableView, QSplitter, QDoubleSpinBox, QFrame,
    QProgressDialog, QMessageBox, QHeaderView,QSizePolicy,QMenu
)
from PyQt6.QtCore import Qt, QThread, pyqtSignal
from PyQt6.QtGui import QColor, QStandardItemModel, QStandardItem,QFont
import pyqtgraph as pg
from .find_rm import CheckRMThread
from .rm_ratio import ApplySingleRM
from datetime import datetime
import re
logging.basicConfig(level=logging.DEBUG, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)
from functools import partial
global_style = """
    QWidget { background-color: #F5F7FA; font-family: 'Segoe UI', sans-serif; font-size: 13px; }
    QGroupBox { 
        font-weight: bold; color: #1A3C34; border: 1px solid #D0D7DE; border-radius: 8px;
        margin-top: 12px; padding: 10px; 
    }
    QGroupBox::title { 
        subcontrol-origin: margin; subcontrol-position: top left; padding: 0 8px; left: 10px; background: #F5F7FA; 
    }
    QPushButton { 
        background-color: #2E7D32; color: white; border: none; padding: 9px 16px;
        border-radius: 6px; font-weight: 600; min-width: 100px; 
    }
    QPushButton:hover { background-color: #1B5E20; }
    QLineEdit, QComboBox, QDoubleSpinBox { 
        padding: 6px; border: 1px solid #D0D7DE; border-radius: 6px; background: white; 
    }
    QLineEdit:focus, QComboBox:focus, QDoubleSpinBox:focus { border: 2px solid #2E7D32; }
    QCheckBox { padding: 4px; }
    QFrame#calibFrame { background: #E8F5E9; border-radius: 6px; padding: 10px; margin: 8px; }
    QLabel#currentRmLabel { 
        font-weight: bold; color: #1565C0; font-size: 16px; 
        background: #E3F2FD; padding: 10px; border-radius: 8px; 
    }
    QGroupBox#sharedSettings {
    font-weight: bold;
    color: #1A3C34;
    background: #E8F5E9;
    border: 1px solid #81C784;
    border-radius: 8px;
    padding: 8px;
    margin-top: 5px;
}
"""

class MasterVerificationWindow(QWidget):
    data_changed = pyqtSignal()
    results_update_requested = pyqtSignal(pd.DataFrame)
    def __init__(self, parent, annotations=None):
        super().__init__(parent)
        self.setWindowFlags(Qt.WindowType.Window)
        self.setStyleSheet(global_style)
        self.app = parent
        self.setWindowTitle("Master Verification — RM Drift + CRM Correction")
        self.setGeometry(100, 100, 1920, 1000)

        self.analysis_data = None
        self.selected_element = None
        self.rm_numbers_list = []
        self.current_rm_index = -1
        self.element_list = []
        self.current_element_index = -1
        self.current_file_index = -1
        self.current_nav_index = -1
        self.navigation_list = []
        self.logger = logging.getLogger(__name__)
        self.file_ranges = getattr(self.app, 'file_ranges', [])
        self.manual_corrections = {}
        ## RM variable
        self.empty_rows_from_check = pd.DataFrame()
        self.ignored_pivots = set()
        self.selected_row=0
        self.selected_point_pivot = None
        self.corrected_drift = {}
        self.current_rm_num = None
        self.undo_stack = []


        #### pivot plot variable
        self.elements = []
        self.current_element_index = -1
        if getattr(self.app.results, 'last_filtered_data', None) is not None:
            df = self.app.results.last_filtered_data
            self.elements = [col for col in df.columns if col != 'Solution Label']
            if self.elements:
                self.current_element_index = 0
                self.selected_element = self.elements[self.current_element_index]
        empty_outliers = {el: set() for el in self.elements} if self.elements else {}
        self.original_df=None
        
        self.params = {}
        for i in range(-1, len(self.file_ranges)):
            self.params[i] = {
                'range_low': 2.0, 'range_mid': 20.0,
                'range_high1': 10.0, 'range_high2': 8.0,
                'range_high3': 5.0, 'range_high4': 3.0,
                'preview_blank': 0.0, 'preview_scale': 1.0,
                'excluded_outliers': empty_outliers.copy(),
                'excluded_from_correct': set(),
                'scale_above_50': False,
                'scale_range_min': None, 'scale_range_max': None,
            }
        # =================================================================
        # 3. مقداردهی اولیه ویژگی‌ها (قبل از UI!)
        # =================================================================
        self.range_low = 2.0
        self.range_mid = 20.0
        self.range_high1 = 10.0
        self.range_high2 = 8.0
        self.range_high3 = 5.0
        self.range_high4 = 3.0
        self.preview_blank = 0.0
        self.preview_scale = 1.0
        self.excluded_outliers = empty_outliers.copy()
        self.excluded_from_correct = set()
        self.scale_range_min = None
        self.scale_range_max = None
        self.calibration_range = "[0 to 0]"
        self.blank_labels = []
        self.setup_ui()

    def setup_ui(self):
        main_layout = QVBoxLayout(self)  # Changed to QVBoxLayout for vertical stacking

        # ====================== Shared Settings (moved to top, full width) ======================
        shared_gb = QGroupBox("Shared Settings")
        shared_gb.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Fixed)  # مهم: ارتفاع ثابت
        shared_l = QHBoxLayout(shared_gb)  # از QHBoxLayout استفاده کن (نه QVBoxLayout)
        shared_l.setContentsMargins(10, 10, 10, 10)   # حاشیه داخلی
        shared_l.setSpacing(12)                       # فاصله بین ویجت‌ها
        shared_gb.setObjectName("sharedSettings")

        if getattr(self.app, 'file_ranges', None):
            fh = QHBoxLayout()
            fh.addWidget(QLabel("File:"))
            self.file_selector = QComboBox()
            self.file_selector.setMinimumWidth(200)
            fh.addWidget(self.file_selector)
            shared_l.addLayout(fh)

        eh = QHBoxLayout()
        eh.addWidget(QLabel("Element:"))
        self.element_combo = QComboBox()
        self.element_combo.setMinimumWidth(180)
        self.element_combo.currentTextChanged.connect(self.on_element_changed)
        eh.addWidget(self.element_combo)
        eh.addWidget(QLabel("Current RM:"))
        self.current_rm_label = QLabel("None")
        self.current_rm_label.setObjectName("currentRmLabel")
        self.current_rm_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        eh.addWidget(self.current_rm_label)

        self.prev_rm_btn = QPushButton("Previous RM")
        self.next_rm_btn = QPushButton("Next RM")
        self.prev_rm_btn.clicked.connect(self.prev)
        self.next_rm_btn.clicked.connect(self.next)
        eh.addWidget(self.prev_rm_btn)
        eh.addWidget(self.next_rm_btn)
        # فیلتر سولوشن
        filter_h = QHBoxLayout()
        eh.addWidget(QLabel("Filter Solution:"))
        self.filter_solution_edit = QLineEdit()
        self.filter_solution_edit.setPlaceholderText("e.g. CRM001, Check, Sample...")
        eh.addWidget(self.filter_solution_edit)
        self.filter_solution_btn = QPushButton("Reset")
        self.filter_solution_btn.clicked.connect(self.apply_solution_filter)
        eh.addWidget(self.filter_solution_btn)
        self.plot_calibration_btn = QPushButton("Calibration")
        self.plot_calibration_btn.clicked.connect(self.run_calibration)
        eh.addWidget(self.plot_calibration_btn)
        shared_l.addLayout(eh)

        main_layout.addWidget(shared_gb)  # Add Shared Settings at the top, full width

        # ====================== Main Splitter (below Shared Settings) ======================
        main_splitter = QSplitter(Qt.Orientation.Horizontal)
        main_layout.addWidget(main_splitter)

        # ====================== کنترل‌ها (without Shared Settings) ======================
        controls = QGroupBox("Controls")
        controls_layout = QVBoxLayout(controls)

        # ====================== CRM Verification ======================
        crm_gb = QGroupBox("CRM Verification")
        crm_l = QVBoxLayout(crm_gb)

        show_l = QHBoxLayout()
        self.show_cert_cb = QCheckBox("Certificate"); self.show_cert_cb.setChecked(True); show_l.addWidget(self.show_cert_cb)
        self.show_crm_cb = QCheckBox("CRM"); self.show_crm_cb.setChecked(True); show_l.addWidget(self.show_crm_cb)
        self.show_range_cb = QCheckBox("Acceptable Range"); self.show_range_cb.setChecked(True); show_l.addWidget(self.show_range_cb)
        crm_l.addLayout(show_l)

        self.scale_above_50_cb = QCheckBox("Scale >50% Only")
        crm_l.addWidget(self.scale_above_50_cb)

        minmax_l = QHBoxLayout()
        minmax_l.addWidget(QLabel("Min:")); self.crm_min_edit = QLineEdit("0.0"); self.crm_min_edit.setFixedWidth(80); minmax_l.addWidget(self.crm_min_edit)
        minmax_l.addWidget(QLabel("Max:")); self.crm_max_edit = QLineEdit("1000.0"); self.crm_max_edit.setFixedWidth(80); minmax_l.addWidget(self.crm_max_edit)
        minmax_l.addStretch()
        crm_l.addLayout(minmax_l)

        blank_l = QHBoxLayout()
        blank_l.addWidget(QLabel("Blank:")); self.blank_edit = QLineEdit("0.0"); blank_l.addWidget(self.blank_edit)
        self.blank_edit.textChanged.connect(self.update_preview_params)
        reset_bs_btn = QPushButton("Reset B&S"); blank_l.addWidget(reset_bs_btn)
        reset_bs_btn.clicked.connect(self.reset_blank_and_scale)
        crm_l.addLayout(blank_l)

        crm_l.addWidget(QLabel("Scale:"))
        self.scale_slider = QSlider(Qt.Orientation.Horizontal); self.scale_slider.setRange(0, 200); self.scale_slider.setValue(100)
        self.scale_slider.valueChanged.connect(self.update_preview_params)
    
        crm_l.addWidget(self.scale_slider)
        self.scale_label = QLabel("Scale: 1.00")
        crm_l.addWidget(self.scale_label)

        btns1 = QHBoxLayout()
        self.range_btn = QPushButton("Ranges"); btns1.addWidget(self.range_btn)
        self.exclude_btn = QPushButton("Exclude"); btns1.addWidget(self.exclude_btn)
        self.select_crms_btn = QPushButton("Select CRMs"); btns1.addWidget(self.select_crms_btn)
        crm_l.addLayout(btns1)

        btns2 = QHBoxLayout()
        self.undo_crm_btn = QPushButton("Undo CRM"); btns2.addWidget(self.undo_crm_btn)
        self.correct_crm_btn = QPushButton("Correct CRM"); btns2.addWidget(self.correct_crm_btn)
        crm_l.addLayout(btns2)

        # دکمه‌های جدید — رنگ معمولی
        model_btns = QHBoxLayout()
        self.apply_model_btn = QPushButton("Apply Our Model")
        self.report_btn = QPushButton("Report")
        model_btns.addWidget(self.apply_model_btn)
        model_btns.addWidget(self.report_btn)
        crm_l.addLayout(model_btns)

        controls_layout.addWidget(crm_gb)

        # ====================== RM Drift Correction ======================
        rm_gb = QGroupBox("RM Drift Correction")
        rm_l = QVBoxLayout(rm_gb)

        top_h = QHBoxLayout()
        top_h.addWidget(QLabel("Keyword:"))
        self.keyword_entry2 = QLineEdit("RM"); self.keyword_entry2.setFixedWidth(80); top_h.addWidget(self.keyword_entry2)
        self.run_rm_btn = QPushButton("Check RM"); top_h.addWidget(self.run_rm_btn)
        self.reset_original_btn = QPushButton("Reset to Original"); top_h.addWidget(self.reset_original_btn)
        top_h.addStretch()
        rm_l.addLayout(top_h)

        rm_cheks_layout=QHBoxLayout()
        self.per_file_cb = QCheckBox("Per File RM Reference"); self.per_file_cb.setChecked(True); rm_cheks_layout.addWidget(self.per_file_cb)
        self.global_optimize_cb = QCheckBox("Global Optimize (Ignore Checks)"); rm_cheks_layout.addWidget(self.global_optimize_cb)
        rm_l.addLayout(rm_cheks_layout)
        self.stepwise_cb = QCheckBox("Stepwise Changes"); rm_l.addWidget(self.stepwise_cb)

        slope_h = QHBoxLayout()
        slope_h.addWidget(QLabel("Manual Slope:"))
        self.slope_spin = QDoubleSpinBox(); self.slope_spin.setRange(-9999, 9999); self.slope_spin.setDecimals(8); self.slope_spin.setValue(0.0)
        slope_h.addWidget(self.slope_spin)
        self.apply_slope_btn = QPushButton("Apply Slope"); slope_h.addWidget(self.apply_slope_btn)
        rm_l.addLayout(slope_h)

        self.slope_display = QLabel("Current Slope: 0.00000000")
        self.slope_display.setStyleSheet("font-weight: bold; color: #1A3C34; background: #E8F5E9; padding: 8px; border-radius: 6px;")
        rm_l.addWidget(self.slope_display)

        bottom_h = QHBoxLayout()
        self.auto_flat_btn = QPushButton("Auto Flat"); bottom_h.addWidget(self.auto_flat_btn)
        self.auto_zero_slope_btn = QPushButton("Auto Zero Slope"); bottom_h.addWidget(self.auto_zero_slope_btn)
        self.auto_flat_btn.clicked.connect(self.auto_optimize_to_flat)
        self.auto_zero_slope_btn.clicked.connect(self.auto_optimize_slope_to_zero)
        self.undo_rm_btn = QPushButton("Undo RM"); bottom_h.addWidget(self.undo_rm_btn)
        rm_l.addLayout(bottom_h)

        controls_layout.addWidget(rm_gb)

        calib_frame = QFrame(); calib_frame.setObjectName("calibFrame")
        calib_l = QHBoxLayout(calib_frame)
        calib_l.addWidget(QLabel("Calibration Range:"))
        self.calib_range_label = QLabel("[Not Set]")
        calib_l.addWidget(self.calib_range_label); calib_l.addStretch()
        controls_layout.addWidget(calib_frame)

        controls_layout.addStretch()
        main_splitter.addWidget(controls)
        main_splitter.setStretchFactor(0, 2)

        # ====================== جدول‌ها و نمودار ======================
        tables_widget = QWidget()
        tables_l = QVBoxLayout(tables_widget)

        self.rm_table = QTableView()
        self.rm_table.setSelectionMode(QTableView.SelectionMode.SingleSelection)
        self.rm_table.setSelectionBehavior(QTableView.SelectionBehavior.SelectRows)
        self.rm_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        self.rm_table.verticalHeader().setVisible(False)
        self.rm_table.clicked.connect(self.on_table_row_clicked)
        self.rm_table.setContextMenuPolicy(Qt.ContextMenuPolicy.CustomContextMenu)
        self.rm_table.customContextMenuRequested.connect(self.show_rm_context_menu)  # راست‌کلیک
        rm_table_gb = QGroupBox("RM Points — Current RM Only")
        rm_table_l = QVBoxLayout(rm_table_gb); rm_table_l.addWidget(self.rm_table)
        tables_l.addWidget(rm_table_gb)

        self.detail_table = QTableView()
        self.detail_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        self.detail_table.verticalHeader().setVisible(False)
        self.detail_table.clicked.connect(self.on_detail_table_clicked)
        detail_gb = QGroupBox("All Acquisition Data — Original + New Value")
        detail_l = QVBoxLayout(detail_gb); detail_l.addWidget(self.detail_table)
        tables_l.addWidget(detail_gb)

        main_splitter.addWidget(tables_widget)
        main_splitter.setStretchFactor(1, 3)

        plots_widget = QWidget()
        plots_l = QVBoxLayout(plots_widget)
        self.rm_plot = pg.PlotWidget()
        self.rm_plot.setBackground('w')
        self.rm_plot.showGrid(x=True, y=True, alpha=0.7)
        self.rm_plot.setLabel('left', 'Intensity')
        self.rm_plot.setLabel('bottom', 'Acquisition Order')
        self.rm_plot.addLegend()
        self.highlight_point = pg.ScatterPlotItem(size=20, pen=pg.mkPen('yellow', width=4), brush=None, symbol='o')
        self.rm_plot.addItem(self.highlight_point)

        plot_gb = QGroupBox("Full Drift Plot — All Samples + All RMs")
        plot_l = QVBoxLayout(plot_gb); plot_l.addWidget(self.rm_plot)
        plots_l.addWidget(plot_gb)

        # ====================== Verification Plot ======================
        verification_widget = QWidget()
        verification_l = QVBoxLayout(verification_widget)

        self.verification_plot = pg.PlotWidget()
        self.verification_plot.setBackground('w')
        self.verification_plot.showGrid(x=True, y=True, alpha=0.7)
        self.verification_plot.setLabel('left', 'Concentration (ppm)')
        self.verification_plot.setLabel('bottom', 'Acquisition Order')
        self.verification_plot.addLegend()

        verification_gb = QGroupBox("Verification Plot — CRM & Check Standards")
        verification_gb_layout = QVBoxLayout(verification_gb)
        verification_gb_layout.addWidget(self.verification_plot)
        verification_l.addWidget(verification_gb)

        # اضافه کردن به plots_widget (زیر نمودار اصلی)
        plots_l.addWidget(verification_widget)  # وزن 2 (کوچکتر از Drift Plot)
        
        main_splitter.addWidget(plots_widget)
        main_splitter.setStretchFactor(2, 5)
        main_splitter.setSizes([440, 520, 950])



    def has_changes(self):
        if len(self.original_rm_values) == 0 or len(self.display_rm_values) == 0:
            return False
        return not np.allclose(self.original_rm_values, self.display_rm_values, rtol=1e-5, atol=1e-8, equal_nan=True)

    def prompt_apply_changes(self):
        if self.has_changes():
            reply = QMessageBox.question(self, 'Apply Changes', 'Do you want to apply the changes to this RM?', QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No, QMessageBox.StandardButton.No)
            if reply == QMessageBox.StandardButton.Yes:
                self.apply_to_single_rm()

    def apply_to_single_rm(self):
        if not self.selected_element or self.current_rm_num is None:
            QMessageBox.critical(self, "Error", "No element or RM number selected.")
            return

        # ذخیره undo
        self.undo_stack.append((
            self.app.results.last_filtered_data.copy(),
            self.rm_df.copy(),
            self.corrected_drift.copy(),
            self.manual_corrections.copy()  # اضافه شد
        ))
        self.undo_rm_btn.setEnabled(True)

        self.progress_dialog = QProgressDialog("Applying corrections...", "Cancel", 0, 100, self)
        self.progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)

        applier = ApplySingleRM(
            self.app,
            self.keyword,
            self.selected_element,
            self.current_rm_num,
            self.rm_df,
            self.initial_rm_df,
            self.segments,
            self.stepwise_cb.isChecked(),
            self.progress_dialog
        )
        results = applier.run()
        self.progress_dialog.close()

        if 'error' in results:
            QMessageBox.critical(self, "Error", results['error'])
            return

        new_df = results['df']
        self.rm_df = results['rm_df']
        self.sync_rm_to_all()
        self.corrected_drift.update(results['corrected_drift'])

        # مهم: اعمال manual corrections روی df نهایی
        for orig_index, manual_val in self.manual_corrections.items():
            # پیدا کردن ردیف با original_index
            mask = new_df['original_index'] == orig_index
            if mask.any():
                old_val = new_df.loc[mask, self.selected_element].iloc[0]
                new_df.loc[mask, self.selected_element] = manual_val

                # ذخیره در corrected_drift با کلید (Solution Label, Element)
                sl = new_df.loc[mask, 'Solution Label'].iloc[0]
                key = (sl, self.selected_element)
                ratio = manual_val / old_val if old_val != 0 else 1.0
                self.corrected_drift[key] = ratio

        # به‌روزرسانی نهایی
        self.app.results.last_filtered_data = new_df
        self.save_corrected_drift()  # حالا شامل دستی‌ها هم هست
        self.results_update_requested.emit(new_df)

        # پاک کردن manual corrections بعد از اعمال (اختیاری — من پاک می‌کنم تا دوباره شروع شود)
        # self.manual_corrections.clear()

        self.update_displays()
        QMessageBox.information(self, "Success", "All corrections (Drift + Manual) applied and saved!")


    def show_rm_context_menu(self, pos):
        index = self.rm_table.indexAt(pos)
        if not index.isValid():
            return
        row = index.row()
        if row < 0 or row >= len(self.current_valid_pivot_indices):
            return

        pivot = self.current_valid_pivot_indices[row]
        # نقاط واقعاً empty را نمی‌گذاریم دستی ignore کنیم
        if pivot in self.empty_pivot_set:
            return

        menu = QMenu(self)
        if pivot in self.ignored_pivots:
            action = menu.addAction("Unignore this point)")
        else:
            action = menu.addAction("Ignore this point")

        if menu.exec(self.rm_table.mapToGlobal(pos)) == action:
            if pivot in self.ignored_pivots:
                self.ignored_pivots.remove(pivot)
            else:
                self.ignored_pivots.add(pivot)
            self.update_displays()  # همه چیز دوباره محاسبه می‌شود
    # جدید: کلیک روی جدول detail → highlight نقطه
    def on_detail_table_clicked(self, index):
        row = index.row()
        if row < 0 or self.selected_row < 0: return

        data = self.get_data_between_rm()
        if data.empty or row >= len(data): return

        pivot = data.iloc[row]['pivot_index']
        orig_y = data.iloc[row][self.selected_element]

        self.selected_point_pivot = pivot
        self.selected_point_y = orig_y
        self.highlight_point.setData([pivot], [orig_y])

    # جدید: کلیک روی نقطه در نمودار → انتخاب سطر مربوطه در جدول
    def handle_point_click(self, table_type, scatter, points, ev):
        if not points: return
        pt = points[0]
        label = pt.data()
        pivot = pt.pos().x()
        y = pt.pos().y()

        self.selected_point_pivot = pivot
        self.selected_point_y = y
        self.highlight_point.setData([pivot], [y])

        if table_type == 'rm' or self.keyword.lower() in str(label).lower():
            model = self.rm_table.model()
            for r in range(model.rowCount()):
                if model.item(r, 0).text().startswith(label):
                    self.rm_table.selectRow(r)
                    self.selected_row = r
                    self.update_detail_table()
                    break
        else:
            model = self.detail_table.model()
            for r in range(model.rowCount()):
                if model.item(r, 0).text() == label:
                    self.detail_table.selectRow(r)
                    break
    def update_detail_table(self):
        model = QStandardItemModel()
        model.setHorizontalHeaderLabels(["Solution Label", "Original Value", "Corrected Value"])
        
        data = self.get_data_between_rm()
        if data.empty:
            self.detail_table.setModel(model)
            return

        orig = data[self.selected_element].values
        ratio = (self.display_rm_values[self.selected_row + 1] / self.original_rm_values[self.selected_row + 1] 
                if self.original_rm_values[self.selected_row + 1] != 0 else 1.0)
        
        # محاسبه corrected پایه (با preview blank/scale)
        adjusted = orig - self.preview_blank
        scaled = adjusted * self.preview_scale
        base_corr = self.calculate_corrected_values(scaled, ratio)   # array

        for i in range(len(data)):
            sl_item = QStandardItem(data.iloc[i]['Solution Label'])
            o_item = QStandardItem(f"{orig[i]:.3f}")
            o_item.setEditable(False)

            orig_index = int(data.iloc[i]['original_index'])
            manual_val = self.manual_corrections.get(orig_index)
            display_val = manual_val if manual_val is not None else base_corr[i]

            c_item = QStandardItem(f"{display_val:.3f}")
            c_item.setEditable(True)
            c_item.setData(orig_index, Qt.ItemDataRole.UserRole)   # برای شناسایی ردیف

            model.appendRow([sl_item, o_item, c_item])

        self.detail_table.setModel(model)
        
        # اتصال فقط یکبار (جلوگیری از چندبار اتصال)
        try:
            model.itemChanged.disconnect()
        except:
            pass
        model.itemChanged.connect(self.on_detail_value_changed)

    def on_detail_value_changed(self, item):
        if item.column() != 2:   # فقط ستون Corrected Value
            return
        
        try:
            new_val = float(item.text())
            orig_index = item.data(Qt.ItemDataRole.UserRole)
            if orig_index is None:
                return

            self.manual_corrections[orig_index] = new_val

            # آپدیت فوری plot و slope (slope روی RM است، تغییری نمی‌کند ولی plot آپدیت می‌شود)
            self.update_rm_plot()
            self.update_slope_from_data()    # اگر لازم باشد

            # جدول خودش آپدیت نمی‌شود مگر دوباره فراخوانی کنیم — اما چون فقط یک سلول تغییر کرد، ok است
            # اگر بخواهیم دقیق، می‌توانیم دوباره update_detail_table() هم بزنیم، ولی هزینه دارد — فعلاً فقط plot

        except ValueError:
            QMessageBox.warning(self, "Invalid Value", "Please enter a valid number.")
    def update_navigation_buttons(self):
        self.prev_rm_btn.setEnabled(self.current_nav_index > 0)
        self.next_rm_btn.setEnabled(self.current_nav_index < len(self.navigation_list) - 1)
        enabled = bool(self.current_rm_num is not None and self.selected_element)
        self.slope_spin.setEnabled(enabled); self.auto_flat_btn.setEnabled(enabled); self.auto_zero_slope_btn.setEnabled(enabled)

    def get_data_between_rm(self):
        if self.selected_row < 0 or self.selected_row >= len(self.current_valid_pivot_indices) - 1: return pd.DataFrame()

        pivot_prev = self.current_valid_pivot_indices[self.selected_row]
        pivot_curr = self.current_valid_pivot_indices[self.selected_row + 1]

        min_row = self.positions_df[self.positions_df['pivot_index'] == pivot_prev]
        max_row = self.positions_df[self.positions_df['pivot_index'] == pivot_curr]

        if min_row.empty or max_row.empty: return pd.DataFrame()

        min_pos = min_row['min'].values[0]
        max_pos = max_row['max'].values[0]

        cond = (self.pivot_df['original_index'] > min_pos) & (self.pivot_df['original_index'] < max_pos) & self.pivot_df[self.selected_element].notna()
        data = self.pivot_df[cond].copy().sort_values('original_index')

        filter_text = self.filter_solution_edit.text().strip().lower()
        if filter_text:
            filter_mask = data['Solution Label'].str.lower().str.contains(filter_text)
            data = data[filter_mask]
        return data

    def update_rm_data(self):
        if len(self.display_rm_values) == 0: return

        valid_mask = ~np.isnan(self.display_rm_values)
        valid_pivot_indices = np.array(self.current_valid_pivot_indices)[valid_mask]
        valid_display_values = self.display_rm_values[valid_mask]

        label_df = self.rm_df[(self.rm_df['rm_num'] == self.current_rm_num) & (self.rm_df['pivot_index'].isin(valid_pivot_indices))].sort_values('pivot_index').reset_index(drop=True)

        if len(label_df) != len(valid_display_values): return

        for i, row in label_df.iterrows():
            self.rm_df.loc[self.rm_df['pivot_index'] == row['pivot_index'], self.selected_element] = valid_display_values[i]
            self.all_rm_df.loc[self.all_rm_df['pivot_index'] == row['pivot_index'], self.selected_element] = valid_display_values[i]

            df = self.app.results.last_filtered_data
            if 'original_index' not in df.columns:
                if 'pivot_index' in df.columns:
                    df['original_index'] = df['pivot_index']
                else:
                    df['original_index'] = df.index

            cond = (df['original_index'] == row['original_index'])
            if not df[cond].empty:
                df.loc[cond, self.selected_element] = valid_display_values[i]

    def update_rm_table_ratios(self):
        model = self.rm_table.model()
        for i in range(model.rowCount()):
            if i < len(self.original_rm_values):
                ratio = self.display_rm_values[i] / self.original_rm_values[i] if self.original_rm_values[i] != 0 else np.nan
                model.item(i, 5).setText(f"{ratio:.3f}" if pd.notna(ratio) else "N/A")

      
    def on_rm_value_changed(self, item):
        row = item.row()
        model = self.rm_table.model()
        try:
            if item.column() == 4:  # Current Value
                val = float(item.text())
                self.display_rm_values[row] = val
                ratio = val / self.original_rm_values[row] if self.original_rm_values[row] != 0 else np.nan
                model.item(row, 5).setText(f"{ratio:.3f}" if pd.notna(ratio) else "N/A")
            elif item.column() == 5:  # Ratio
                ratio = float(item.text())
                val = self.original_rm_values[row] * ratio
                self.display_rm_values[row] = val
                model.item(row, 4).setText(f"{val:.3f}")

            self.selected_row = row
            self.update_rm_data()
            self.update_rm_plot(); self.update_slope_from_data()
            self.update_detail_table()
        except ValueError as e:
            QMessageBox.warning(self, "Invalid Value", str(e))
            # برگرداندن مقدار قبلی
    def handle_point_click(self, table_type, scatter, points, ev):
        if not points: return
        pt = points[0]
        label = pt.data()
        pivot = pt.pos().x()
        y = pt.pos().y()

        self.selected_point_pivot = pivot
        self.selected_point_y = y
        self.highlight_point.setData([pivot], [y])

        if table_type == 'rm' or self.keyword.lower() in str(label).lower():
            model = self.rm_table.model()
            for r in range(model.rowCount()):
                if model.item(r, 0).text().startswith(label):
                    self.rm_table.selectRow(r)
                    self.selected_row = r
                    self.update_detail_table()
                    break
        else:
            model = self.detail_table.model()
            for r in range(model.rowCount()):
                if model.item(r, 0).text() == label:
                    self.detail_table.selectRow(r)
                    break

    def calculate_corrected_values(self, original_values, current_ratio):
        n = len(original_values)
        if n == 0: return np.array([])
        delta = current_ratio - 1.0
        step_delta = delta / n if n > 0 else 0.0
        stepwise = self.stepwise_cb.isChecked()
        return original_values * np.array([1.0 + step_delta * (j + 1) if stepwise else current_ratio for j in range(n)])

    def update_rm_plot(self):
        self.rm_plot.clear()
        self.rm_plot.addLegend(offset=(10, 10))
        self.highlight_point.setData([], [])
        self.rm_plot.addItem(self.highlight_point)

        if len(self.display_rm_values) == 0:
            return

        # --- داده‌های RM ---
        valid_mask = ~np.isnan(self.display_rm_values)
        x_valid = self.current_valid_pivot_indices[valid_mask]
        y_valid = self.display_rm_values[valid_mask]
        types_valid = self.rm_types[valid_mask]
        labels_valid = self.solution_labels_for_group[valid_mask]

        is_empty = np.array([p in self.empty_pivot_set for p in x_valid])
        is_ignored = np.array([p in self.ignored_pivots for p in x_valid])
        normal_mask = ~(is_empty | is_ignored)

        # اعمال پیش‌نمایش Blank و Scale روی RMها
        y_rm_preview = (y_valid - self.preview_blank) * self.preview_scale

        # --- جمع‌آوری نقاطی که در ترند شرکت می‌کنند ---
        trend_x = []
        trend_y = []

        # 1. همه RMهای معتبر (Base/Check/Cone)
        for i in range(len(x_valid)):
            if normal_mask[i]:
                trend_x.append(x_valid[i])
                trend_y.append(y_rm_preview[i])

        # 2. نقاط دستی مهم که کاربر تغییر داده (CRM, Check, STD, Ref, Cal, ...)
        important_keywords = ['CRM', 'CHECK', 'STD', 'REF', 'CAL', 'QC', 'BLK']  # تنظیم کن به دلخواه
        manual_trend_x = []
        manual_trend_y = []

        for orig_index, manual_val in self.manual_corrections.items():
            row = self.pivot_df[self.pivot_df['original_index'] == orig_index]
            if row.empty:
                continue

            label = row['Solution Label'].iloc[0].upper()
            pivot_val = row['pivot_index'].iloc[0]

            # اعمال preview blank/scale روی مقدار دستی
            final_val = (manual_val - self.preview_blank) * self.preview_scale

            # فقط اگر برچسب شامل یکی از کلمات مهم باشد → در ترند شرکت کنه
            # if any(k in label for k in important_keywords):
            manual_trend_x.append(pivot_val)
            manual_trend_y.append(final_val)

        # اضافه کردن به لیست ترند
        trend_x.extend(manual_trend_x)
        trend_y.extend(manual_trend_y)

        # --- رسم خطوط ترند بر اساس نقاط جدید (RM + دستی مهم) ---
        if len(trend_x) >= 2:
            tx = np.array(trend_x)
            ty = np.array(trend_y)
            order = np.argsort(tx)
            tx = tx[order]
            ty = ty[order]

            # Global Trend Line
            p_global = np.poly1d(np.polyfit(tx, ty, 1))
            self.rm_plot.plot(tx, p_global(tx),
                            pen=pg.mkPen('black', width=3, style=Qt.PenStyle.DashLine),
                            name="Global Trend (incl. Manual Refs)")

            # Segment Trend Lines
            colors = ['#43A047', '#FF6B00', '#7B1FA2', '#1A3C34']
            for seg_idx, seg in enumerate(self.segments):
                seg_pivots = seg['positions']['pivot_index'].values
                seg_mask = np.isin(tx, seg_pivots)
                if np.sum(seg_mask) >= 2:
                    xs = tx[seg_mask]
                    ys = ty[seg_mask]
                    color = colors[seg_idx % len(colors)]
                    self.rm_plot.plot(xs, ys, pen=pg.mkPen(color, width=2.5), name="Segment Trend")
                    p_seg = np.poly1d(np.polyfit(xs, ys, 1))
                    self.rm_plot.plot(xs, p_seg(xs), pen=pg.mkPen(color, width=2, style=Qt.PenStyle.DashLine), name="Segment Trend")

            # نمایش نقاط دستی مهم روی خط ترند با ستاره صورتی
            if manual_trend_x:
                self.rm_plot.addItem(pg.ScatterPlotItem(
                    manual_trend_x, manual_trend_y,
                    symbol='star', size=15, brush='#E91E63', pen=pg.mkPen('white', width=2),
                    name="Manual Ref Points (in Trend)"
                ))

        # --- رسم نقاط RM ---
        brush_colors = []
        for i in range(len(x_valid)):
            if is_ignored[i]:
                brush_colors.append('#FF9800')
            elif is_empty[i]:
                brush_colors.append('#B0BEC5')
            else:
                brush_colors.append('#2E7D32')

        self.rm_scatter = pg.ScatterPlotItem(
            x_valid, y_rm_preview,
            symbol=[{'Base': 'o', 'Check': 't', 'Cone': 's'}.get(t, 'o') for t in types_valid],
            size=11, brush=brush_colors, pen='w', hoverable=True,
            tip=lambda x,y, data: f"Label: {data}\nValue: {y:.4f}",
            data=labels_valid,
            name="RM Reference Points"
        )
        self.rm_plot.addItem(self.rm_scatter)
        self.rm_scatter.sigClicked.connect(partial(self.handle_point_click, 'rm'))

        # --- رسم نمونه‌ها (Original + Corrected) ---
        all_orig_x, all_orig_y = [], []
        all_corr_x, all_corr_y = [], []
        all_labels = []

        filter_text = self.filter_solution_edit.text().strip().lower()

        for i in range(len(x_valid) - 1):
            if not normal_mask[i] or not normal_mask[i + 1]:
                continue

            pivot_prev = x_valid[i]
            pivot_curr = x_valid[i + 1]

            min_row = self.positions_df[self.positions_df['pivot_index'] == pivot_prev]
            max_row = self.positions_df[self.positions_df['pivot_index'] == pivot_curr]
            if min_row.empty or max_row.empty:
                continue

            min_pos = min_row['min'].values[0]
            max_pos = max_row['max'].values[0]

            cond = (self.pivot_df['original_index'] > min_pos) & \
                (self.pivot_df['original_index'] < max_pos) & \
                self.pivot_df[self.selected_element].notna()

            segment_data = self.pivot_df[cond].copy()
            if filter_text:
                segment_data = segment_data[segment_data['Solution Label'].str.lower().str.contains(filter_text, na=False)]
            if segment_data.empty:
                continue

            seg_x = segment_data['pivot_index'].values
            seg_y_orig = segment_data[self.selected_element].values
            seg_labels = segment_data['Solution Label'].values

            # Original points
            all_orig_x.extend(seg_x)
            all_orig_y.extend(seg_y_orig)

            # Corrected points (با drift + preview + دستی)
            ratio = y_valid[i + 1] / self.original_rm_values[i + 1] if self.original_rm_values[i + 1] != 0 else 1.0
            adjusted = seg_y_orig - self.preview_blank
            scaled = adjusted * self.preview_scale
            base_corr = self.calculate_corrected_values(scaled, ratio)

            corr_vals = []
            for j, row in enumerate(segment_data.itertuples()):
                idx = getattr(row, 'original_index')
                manual = self.manual_corrections.get(idx)
                corr_vals.append(manual if manual is not None else base_corr[j])

            all_corr_x.extend(seg_x)
            all_corr_y.extend(corr_vals)
            all_labels.extend(seg_labels)

        # Original Samples
        if all_orig_x:
            self.rm_plot.addItem(pg.ScatterPlotItem(
                all_orig_x, all_orig_y, symbol='o', size=7, brush='#2196F3', pen='#1976D2',
                hoverable=True, tip=lambda x,y,data: f"{data}\nOrig: {y:.4f}",
                data=all_labels, name="Original Samples"
            ))

        # Corrected Samples
        if all_corr_x:
            self.rm_plot.addItem(pg.ScatterPlotItem(
                all_corr_x, all_corr_y, symbol='x', size=9, brush='#F44336', pen='#D32F2F',
                hoverable=True, tip=lambda x,y,data: f"{data}\nCorr: {y:.4f}",
                data=all_labels, name="Corrected Samples (Drift + Manual)"
            ))

        # Highlight خط بین دو RM انتخاب شده
        if 0 <= self.selected_row < len(x_valid) - 1 and normal_mask[self.selected_row:self.selected_row + 2].all():
            self.rm_plot.plot([x_valid[self.selected_row], x_valid[self.selected_row + 1]],
                            [y_rm_preview[self.selected_row], y_rm_preview[self.selected_row + 1]],
                            pen=pg.mkPen('#FFD700', width=5))

        # خطوط عمودی فایل‌ها
        if self.current_file_index == 0 and len(self.file_ranges) > 1:
            for fr in self.file_ranges:
                self.rm_plot.addItem(pg.InfiniteLine(fr['start_pivot_row'], angle=90, pen=pg.mkPen('gray', style=Qt.PenStyle.DashLine)))
                self.rm_plot.addItem(pg.InfiniteLine(fr['end_pivot_row'], angle=90, pen=pg.mkPen('gray', style=Qt.PenStyle.DashLine)))

        valid_mask = ~np.isnan(self.display_rm_values)
        x_vals = self.current_valid_pivot_indices[valid_mask]
        y_vals = (self.display_rm_values[valid_mask] - self.preview_blank) * self.preview_scale
        
        normal = np.array([p not in self.empty_pivot_set and p not in self.ignored_pivots for p in x_vals])
        x_clean = x_vals[normal]
        y_clean = y_vals[normal]
        
        if len(x_clean) >= 2:
            slope = np.polyfit(x_clean, y_clean, 1)[0]
            color = "#2E7D32" if slope >= 0 else "#D32F2F"
            sign = "+" if slope >= 0 else ""
            self.slope_display.setText(f"Current Slope: <span style='color:{color};font-weight:bold'>{sign}{slope:.6f}</span>")
        else:
            self.slope_display.setText(" Current Slope: —")

        self.rm_plot.autoRange()

    def update_slope_from_data(self):
        """محاسبه شیب فعلی — با در نظر گرفتن تمام نقاط اصلاح‌شده (RM + دستی مهم + حتی نمونه‌های دستی اگر بخوای)"""
        
        # جمع‌آوری نقاطی که برای محاسبه شیب استفاده می‌کنیم
        trend_x = []
        trend_y = []

        # 1. نقاط RM (با اعمال blank و scale)
        valid_mask = ~np.isnan(self.display_rm_values)
        if np.any(valid_mask):
            x_rm = self.current_valid_pivot_indices[valid_mask]
            y_rm_raw = self.display_rm_values[valid_mask]
            y_rm = (y_rm_raw - self.preview_blank) * self.preview_scale

            # فقط نقاطی که خالی یا ignored نیستند
            for px, py in zip(x_rm, y_rm):
                if px not in self.empty_pivot_set and px not in self.ignored_pivots:
                    trend_x.append(px)
                    trend_y.append(py)

        # 2. اضافه کردن نقاط دستی مهم (CRM, Check, STD, QC, REF و ...) — حتماً در شیب شرکت کنن
        important_keywords = ['CRM', 'CHECK', 'STD', 'QC', 'REF', 'CAL', 'BLK', 'BRK']
        
        for orig_index, manual_val in self.manual_corrections.items():
            row = self.pivot_df[self.pivot_df['original_index'] == orig_index]
            if row.empty:
                continue
            label = row['Solution Label'].iloc[0].upper()
            pivot_val = row['pivot_index'].iloc[0]

            # اعمال blank و scale روی مقدار دستی
            final_val = (manual_val - self.preview_blank) * self.preview_scale

            trend_x.append(pivot_val)
            trend_y.append(final_val)

        # محاسبه شیب
        if len(trend_x) >= 2:
            tx = np.array(trend_x)
            ty = np.array(trend_y)
            order = np.argsort(tx)
            tx = tx[order]
            ty = ty[order]
            self.current_slope = np.polyfit(tx, ty, 1)[0]
        else:
            self.current_slope = 0.0

        # به‌روزرسانی QDoubleSpinBox
        self.slope_spin.blockSignals(True)
        self.slope_spin.setValue(self.current_slope)
        self.slope_spin.blockSignals(False)

        color = "#2E7D32" if self.current_slope >= 0 else "#D32F2F"
        sign = "+" if self.current_slope >= 0 else ""
        self.slope_display.setText(f"Current Slope: <span style='color:{color};font-weight:bold'>{sign}{self.current_slope:.7f}</span>")

    def auto_optimize_to_flat(self):
        if len(self.display_rm_values) == 0:
            return
        if self.current_file_index == 0 and self.per_file_checkbox.isChecked():
            self.auto_optimize_to_flat_per_file()
            return
        empty_set = set(self.empty_rows_from_check['original_index'].dropna().astype(int).tolist()) if not self.empty_rows_from_check.empty else set()
        rm_mask = (self.rm_df['rm_num'] == self.current_rm_num)
        if not rm_mask.any():
            return
        y = self.rm_df.loc[rm_mask, self.selected_element].astype(float).values
        pivot = self.rm_df.loc[rm_mask, 'pivot_index'].values
        is_empty = np.array([p in empty_set for p in pivot])
        normal_mask = ~is_empty & ~np.isnan(y)
        if normal_mask.sum() == 0:
            return
        seg_dict = dict(zip(self.positions_df['pivot_index'], self.positions_df['segment_id']))
        unique_segs = np.unique([seg_dict.get(p, -1) for p in pivot[normal_mask]])
        if self.global_optimize_cb.isChecked():
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
        self.rm_df.loc[rm_mask, self.selected_element] = y
        self.sync_rm_to_all()
        self.update_displays()
        self.update_slope_from_data()
        QMessageBox.information(self, "Info", "Selected RM optimized to flat relative to the first valid point in each segment (or globally if checked).")

    def sync_rm_to_all(self):
        for pivot, val in zip(self.rm_df['pivot_index'], self.rm_df[self.selected_element]):
            self.all_rm_df.loc[self.all_rm_df['pivot_index'] == pivot, self.selected_element] = val
    def auto_optimize_to_flat_per_file(self):
        for file_idx, fr in enumerate(self.file_ranges):
            start = fr['start_pivot_row']
            end = fr['end_pivot_row']
            file_rm_mask = (self.all_rm_df['pivot_index'].between(start, end)) & (self.all_rm_df['rm_num'] == self.current_rm_num)
            if not file_rm_mask.any():
                continue
            y_file = self.all_rm_df.loc[file_rm_mask, self.selected_element].astype(float).values
            pivot_file = self.all_rm_df.loc[file_rm_mask, 'pivot_index'].values
            empty_set = set(self.empty_rows_from_check['original_index'].dropna().astype(int).tolist()) if not self.empty_rows_from_check.empty else set()
            is_empty_file = np.array([p in empty_set for p in pivot_file])
            normal_mask_file = ~is_empty_file & ~np.isnan(y_file)
            if normal_mask_file.sum() == 0:
                continue
            # Find reference RM in this file, assume first valid point
            first_idx = np.where(normal_mask_file)[0][0]
            first_val = y_file[first_idx]
            y_file[normal_mask_file] = first_val
            self.all_rm_df.loc[file_rm_mask, self.selected_element] = y_file
        if self.current_file_index == 0:
            self.rm_df[self.selected_element] = self.all_rm_df[self.selected_element]
        self.sync_rm_to_all()
        self.update_displays()
        self.update_slope_from_data()
        QMessageBox.information(self, "Info", "Optimized to flat per file based on reference RM in each file.")

    def auto_optimize_slope_to_zero(self):
        if len(self.display_rm_values) < 2:
            return
        if self.current_file_index == 0 and self.per_file_checkbox.isChecked():
            self.auto_optimize_slope_to_zero_per_file()
            return
        empty_set = set(self.empty_rows_from_check['original_index'].dropna().astype(int).tolist()) if not self.empty_rows_from_check.empty else set()
        rm_mask = (self.rm_df['rm_num'] == self.current_rm_num)
        if not rm_mask.any():
            return
        y = self.rm_df.loc[rm_mask, self.selected_element].astype(float).values
        pivot = self.rm_df.loc[rm_mask, 'pivot_index'].values
        is_empty = np.array([p in empty_set for p in pivot])
        normal_mask = ~is_empty & ~np.isnan(y)
        if normal_mask.sum() < 2:
            return
        seg_dict = dict(zip(self.positions_df['pivot_index'], self.positions_df['segment_id']))
        x = pivot
        if self.global_optimize_checkbox.isChecked():
            x_n = x[normal_mask]
            y_n = y[normal_mask].copy()
            if len(x_n) >= 2:
                slope, intercept = np.polyfit(x_n, y_n, 1)
                first_x = x_n[0]
                y_n -= slope * (x_n - first_x)
            y[normal_mask] = y_n
        else:
            unique_segs = np.unique([seg_dict.get(p, -1) for p in pivot[normal_mask]])
            for seg_id in unique_segs:
                if seg_id == -1:
                    continue
                seg_mask = np.array([seg_dict.get(p, -1) == seg_id for p in pivot])
                seg_normal_mask = seg_mask & normal_mask
                if seg_normal_mask.sum() < 2:
                    continue
                x_n = x[seg_normal_mask]
                y_n = y[seg_normal_mask].copy()
                if len(x_n) >= 2:
                    slope, intercept = np.polyfit(x_n, y_n, 1)
                    first_x = x_n[0]
                    y_n -= slope * (x_n - first_x)
                y[seg_normal_mask] = y_n
        self.rm_df.loc[rm_mask, self.selected_element] = y
        self.sync_rm_to_all()
        self.update_displays()
        self.update_slope_from_data()
        QMessageBox.information(self, "Info", "Slope optimized to zero for the selected RM in each segment (or globally if checked), preserving the starting point.")

    def auto_optimize_slope_to_zero_per_file(self):
        for file_idx, fr in enumerate(self.file_ranges):
            start = fr['start_pivot_row']
            end = fr['end_pivot_row']
            file_rm_mask = (self.all_rm_df['pivot_index'].between(start, end)) & (self.all_rm_df['rm_num'] == self.current_rm_num)
            if not file_rm_mask.any():
                continue
            y_file = self.all_rm_df.loc[file_rm_mask, self.selected_element].astype(float).values
            pivot_file = self.all_rm_df.loc[file_rm_mask, 'pivot_index'].values
            empty_set = set(self.empty_rows_from_check['original_index'].dropna().astype(int).tolist()) if not self.empty_rows_from_check.empty else set()
            is_empty_file = np.array([p in empty_set for p in pivot_file])
            normal_mask_file = ~is_empty_file & ~np.isnan(y_file)
            if normal_mask_file.sum() < 2:
                continue
            x_file = pivot_file
            x_n = x_file[normal_mask_file]
            y_n = y_file[normal_mask_file].copy()
            if len(x_n) >= 2:
                slope, intercept = np.polyfit(x_n, y_n, 1)
                first_x = x_n[0]
                y_n -= slope * (x_n - first_x)
            y_file[normal_mask_file] = y_n
            self.all_rm_df.loc[file_rm_mask, self.selected_element] = y_file
        if self.current_file_index == 0:
            self.rm_df[self.selected_element] = self.all_rm_df[self.selected_element]
        self.sync_rm_to_all()
        self.update_displays()
        self.update_slope_from_data()
        QMessageBox.information(self, "Info", "Slope optimized to zero per file based on reference RM in each file.")


    # ====================== توابع اصلی ======================
    def run_calibration(self):
        self.start_check_rm_thread()

    def start_check_rm_thread(self):
        keyword = self.keyword_entry2.text().strip()
        if not keyword:
            QMessageBox.critical(self, "Error", "Please enter a valid keyword.")
            return
        self.keyword = keyword
        self.progress_dialog = QProgressDialog("Processing RM Changes...", "Cancel", 0, 100, self)
        self.progress_dialog.setWindowModality(Qt.WindowModality.WindowModal)
        self.thread = CheckRMThread(self.app, keyword)
        self.thread.progress.connect(self.progress_dialog.setValue)
        self.thread.finished.connect(self.on_check_rm_finished)
        self.thread.error.connect(self.on_check_rm_error)
        self.thread.start()

    def on_check_rm_finished(self, results):
        self.progress_dialog.close()
        self.all_initial_rm_df = results['rm_df'].copy(deep=True)
        self.all_rm_df = results['rm_df'].copy(deep=True)
        self.all_positions_df = results['positions_df']
        self.all_segments = results['segments']
        self.all_pivot_df = results['pivot_df'].copy(deep=True)
        self.elements = results['elements']

        self.file_ranges = self.app.file_ranges if hasattr(self.app, 'file_ranges') else []

        if len(self.file_ranges) > 1:
            if self.file_selector.count() == 0:
                self.file_selector.addItem("All")
                self.file_selector.addItems([fr['clean_name'] for fr in self.file_ranges])
                # self.left_layout.insertLayout(1, self.file_selector_layout)
            self.filter_by_file(-1)
            self.current_file_index = 0
            self.file_selector.setCurrentIndex(0)
        else:
            self.initial_rm_df = self.all_initial_rm_df
            self.rm_df = self.all_rm_df
            self.positions_df = self.all_positions_df
            self.segments = self.all_segments
            self.pivot_df = self.all_pivot_df
            self.unique_rm_nums = sorted(self.rm_df['rm_num'].unique())

            if self.unique_rm_nums and self.elements:
                self.navigation_list = [(el, num) for el in self.elements for num in self.unique_rm_nums]
                self.current_nav_index = 0
                self.selected_element, self.current_rm_num = self.navigation_list[0]
                self.element_combo.addItems(self.elements)
                self.update_labels(); self.update_displays()
                self.auto_flat_btn.setEnabled(True); self.auto_zero_slope_btn.setEnabled(True)

        self.save_corrected_drift()
        self.run_pivot_plot()
        self.data_changed.emit()
        self.update_navigation_buttons()
        
    def on_check_rm_error(self, message):
        self.progress_dialog.close()
        QMessageBox.critical(self, "Error", message)

    def update_displays(self):
        if self.current_rm_num is not None and self.selected_element:
            self.display_rm_table()
            self.update_rm_plot()
            self.update_detail_table()
    def on_table_row_clicked(self, index):
        self.selected_row = index.row()

        if 0 <= self.selected_row < len(self.current_valid_pivot_indices):
            pivot = self.current_valid_pivot_indices[self.selected_row]
            y = self.display_rm_values[self.selected_row]
            self.selected_point_pivot = pivot
            self.selected_point_y = y
            self.highlight_point.setData([pivot], [y])

        self.update_rm_plot()
        self.update_detail_table()
    def display_rm_table(self):
        model = QStandardItemModel()
        model.setHorizontalHeaderLabels(["RM Label", "Next RM", "Type", "Original Value", "Current Value", "Ratio"])

        label_df = self.rm_df[self.rm_df['rm_num'] == self.current_rm_num].sort_values('pivot_index')
        initial_label_df = self.initial_rm_df[self.initial_rm_df['rm_num'] == self.current_rm_num].sort_values('pivot_index')

        # همسان‌سازی طول
        if len(label_df) != len(initial_label_df):
            common_pivot = np.intersect1d(label_df['pivot_index'], initial_label_df['pivot_index'])
            label_df = label_df[label_df['pivot_index'].isin(common_pivot)].sort_values('pivot_index')
            initial_label_df = initial_label_df[initial_label_df['pivot_index'].isin(common_pivot)].sort_values('pivot_index')

        pivot_indices = label_df['pivot_index'].values
        original_values = pd.to_numeric(initial_label_df[self.selected_element], errors='coerce').values
        display_values = pd.to_numeric(label_df[self.selected_element], errors='coerce').values

        valid_mask = ~np.isnan(original_values) & ~np.isnan(display_values)
        self.current_valid_pivot_indices = pivot_indices[valid_mask]
        self.original_rm_values = original_values[valid_mask]
        self.display_rm_values = display_values[valid_mask]
        self.rm_types = label_df.loc[label_df.index[valid_mask], 'rm_type'].values
        self.solution_labels_for_group = label_df.loc[label_df.index[valid_mask], 'Solution Label'].values

        # تشخیص نقاط واقعاً خالی و دستی ignore شده
        self.empty_pivot_set = set(self.empty_rows_from_check['original_index'].dropna().astype(int).tolist()) \
            if not self.empty_rows_from_check.empty and 'original_index' in self.empty_rows_from_check.columns else set()

        is_really_empty = np.array([p in self.empty_pivot_set for p in self.current_valid_pivot_indices], dtype=bool)
        is_manually_ignored = np.array([p in self.ignored_pivots for p in self.current_valid_pivot_indices], dtype=bool)
        effective_empty = is_really_empty | is_manually_ignored

        blue_pivot_indices = self.current_valid_pivot_indices[~effective_empty]
        blue_index_to_pos = {idx: i for i, idx in enumerate(blue_pivot_indices)}

        if len(self.display_rm_values) == 0:
            model.appendRow([QStandardItem("No Data") for _ in range(6)])
        else:
            for i in range(len(self.display_rm_values)):
                current_rm_label = f"{self.solution_labels_for_group[i]}-{self.current_valid_pivot_indices[i]}"
                next_rm_label = "N/A"
                if not effective_empty[i]:
                    pos = blue_index_to_pos.get(self.current_valid_pivot_indices[i])
                    if pos is not None and pos < len(blue_pivot_indices) - 1:
                        next_rm_label = f"{self.solution_labels_for_group[pos + 1]}-{blue_pivot_indices[pos + 1]}"

                orig_val = self.original_rm_values[i]
                curr_val = self.display_rm_values[i]
                ratio = curr_val / orig_val if orig_val != 0 else np.nan
                rm_type = self.rm_types[i]

                row_items = [
                    QStandardItem(current_rm_label),
                    QStandardItem(next_rm_label),
                    QStandardItem(rm_type),
                    QStandardItem(f"{orig_val:.3f}"),
                    QStandardItem(f"{curr_val:.3f}"),
                    QStandardItem(f"{ratio:.3f}" if pd.notna(ratio) else "N/A")
                ]

                # رنگ‌بندی
                if is_really_empty[i]:
                    bg = QColor('red')
                    fg = QColor('white')
                elif is_manually_ignored[i]:
                    bg = QColor('#FF9800')  # نارنجی
                    fg = QColor('white')
                else:
                    bg = None
                    fg = None

                if bg:
                    for item in row_items:
                        item.setBackground(bg)
                        item.setForeground(fg)
                        item.setEditable(False)
                else:
                    for j in [0, 1, 3]: row_items[j].setEditable(False)
                    row_items[4].setEditable(True)
                    row_items[5].setEditable(True)
                    color_map = {'Base': '#2E7D32', 'Check': '#FF6B00', 'Cone': '#7B1FA2'}
                    color = QColor(color_map.get(rm_type, '#000000'))
                    row_items[2].setForeground(color)
                    row_items[2].setFont(QFont("Segoe UI", 9, QFont.Weight.Bold))

                model.appendRow(row_items)

        self.rm_table.setModel(model)
        try: model.itemChanged.disconnect()
        except: pass
        model.itemChanged.connect(self.on_rm_value_changed)

        self.update_slope_from_data()

        if 0 <= self.selected_row < len(self.current_valid_pivot_indices):
            self.rm_table.selectRow(self.selected_row)

    def update_labels(self):
        self.current_rm_label.setText(f"Current RM: {self.current_rm_num if self.current_rm_num is not None else 'None'}")
        if self.element_combo.count() > 0:
            self.element_combo.blockSignals(True); self.element_combo.setCurrentText(self.selected_element or ''); self.element_combo.blockSignals(False)

    def filter_by_file(self, index):
        if index < 0:  # All selected
            self.pivot_df = self.all_pivot_df.copy()
            self.rm_df = self.all_rm_df.copy()
            self.initial_rm_df = self.all_initial_rm_df.copy()
            self.positions_df = self.all_positions_df.copy()
            self.segments = self._create_segments(self.positions_df)
            self.unique_rm_nums = sorted(self.rm_df['rm_num'].unique())
        else:
            fr = self.file_ranges[index]
            start = fr['start_pivot_row']
            end = fr['end_pivot_row']

            if 'pivot_index' in self.all_pivot_df.columns:
                self.pivot_df = self.all_pivot_df[self.all_pivot_df['pivot_index'].between(start, end)].copy()
                self.rm_df = self.all_rm_df[self.all_rm_df['pivot_index'].between(start, end)].copy()
                self.initial_rm_df = self.all_initial_rm_df[self.all_initial_rm_df['pivot_index'].between(start, end)].copy()
                self.positions_df = self.all_positions_df[self.all_positions_df['pivot_index'].between(start, end)].copy()
            else:
                self.pivot_df = self.all_pivot_df.iloc[start:end+1].copy()
                self.rm_df = self.all_rm_df.iloc[start:end+1].copy()
                self.initial_rm_df = self.all_initial_rm_df.iloc[start:end+1].copy()
                self.positions_df = self.all_positions_df.iloc[start:end+1].copy()

            self.segments = self._create_segments(self.positions_df)
            self.unique_rm_nums = sorted(self.rm_df['rm_num'].unique())

        if self.unique_rm_nums and self.elements:
            self.navigation_list = [(el, num) for el in self.elements for num in self.unique_rm_nums]
            self.current_nav_index = 0
            self.selected_element, self.current_rm_num = self.navigation_list[0]
            self.element_combo.clear()
            self.element_combo.addItems(self.elements)
            self.update_labels(); self.update_displays()
            self.auto_optimize_flat_button.setEnabled(True); self.auto_optimize_zero_button.setEnabled(True)
        else:
            self.current_nav_index = -1

        self.selected_row = -1
        self.selected_point_pivot = None
        self.selected_point_y = None
        self.update_navigation_buttons()


    def save_corrected_drift(self):
        try:
            if not hasattr(self.app.results, 'corrected_drift'):
                self.app.results.corrected_drift = {}
            
            # ترکیب drift خودکار + دستی
            self.app.results.corrected_drift.update(self.corrected_drift)

            drift_data = [
                {'Solution Label': k[0], 'Element': k[1], 'Ratio': v}
                for k, v in self.corrected_drift.items()
            ]
            drift_df = pd.DataFrame(drift_data)

            if not hasattr(self.app.results, 'report_change'):
                self.app.results.report_change = pd.DataFrame(columns=['Solution Label', 'Element', 'Ratio'])

            if not drift_df.empty:
                # حذف قبلی‌های همین المنت و اضافه کردن جدید
                self.app.results.report_change = self.app.results.report_change[
                    ~self.app.results.report_change['Element'].isin(drift_df['Element'])
                ]
                self.app.results.report_change = pd.concat([self.app.results.report_change, drift_df], ignore_index=True)

        except Exception as e:
            logger.error(f"Error saving corrected_drift: {str(e)}")

    def update_rm_list_and_go_first(self):
        if not self.analysis_data:
            return
        rm_df = self.analysis_data['rm_df']
        self.rm_numbers_list = sorted(rm_df['rm_num'].dropna().unique().astype(int).tolist())
        self.current_rm_index = 0 if self.rm_numbers_list else -1
        if self.rm_numbers_list:
            self.selected_rm_num = self.rm_numbers_list[0]
            self.current_rm_label.setText(f"RM-{int(self.selected_rm_num)}")
        self.update_all_displays()

    def on_element_changed(self, element):
        self.selected_element = element
        self.current_element_index = self.element_list.index(element) if element in self.element_list else 0
        self.update_rm_list_and_go_first()

    
    def prev(self):
        if self.current_nav_index > 0:
            self.prompt_apply_changes()
            self.current_nav_index -= 1
            self.selected_element, self.current_rm_num = self.navigation_list[self.current_nav_index]
            self.selected_row = -1
            self.selected_point_pivot = None
            self.selected_point_y = None
            self.update_labels()
            self.update_displays()
            self.update_navigation_buttons()
            self.update_pivot_plot()

    def next(self):
        if self.current_nav_index < len(self.navigation_list) - 1:
            self.prompt_apply_changes()
            self.current_nav_index += 1
            self.selected_element, self.current_rm_num = self.navigation_list[self.current_nav_index]
            self.selected_row = -1
            self.selected_point_pivot = None
            self.selected_point_y = None
            self.update_labels()
            self.update_displays()
            self.update_navigation_buttons()
            self.update_pivot_plot()

    def update_tables_and_plot(self):
        if not self.analysis_data or not self.selected_element:
            return

        rm_df = self.analysis_data['rm_df']
        full_df = self.analysis_data['full_df']
        element = self.selected_element

        if element not in full_df.columns:
            return

        # جدول بالایی: فقط RM فعلی
        model_rm = QStandardItemModel()
        model_rm.setHorizontalHeaderLabels(["Label", "Original", "Current", "Ratio"])
        if self.selected_rm_num is not None:
            selected_rm_data = rm_df[rm_df['rm_num'] == self.selected_rm_num]
            for _, row in selected_rm_data.iterrows():
                val = row[element]
                if pd.notna(val):
                    model_rm.appendRow([
                        QStandardItem(row['Solution Label']),
                        QStandardItem(f"{val:.5f}"),
                        QStandardItem(f"{val:.5f}"),
                        QStandardItem("1.000")
                    ])
        self.rm_table.setModel(model_rm)

        # جدول پایینی: همه داده‌ها
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
        self.detail_table.setModel(model_all)

        # نمودار
        self.rm_plot.clear()
        self.rm_plot.addLegend()

        # نمونه‌ها
        sample_mask = ~full_df['Solution Label'].str.contains("RM", case=False, na=False)
        samples = full_df[sample_mask & pd.notna(full_df[element])]
        if not samples.empty:
            self.rm_plot.addItem(pg.ScatterPlotItem(
                x=samples['pivot_index'].values,
                y=pd.to_numeric(samples[element], errors='coerce').values,
                size=7, brush='#B0BEC5', pen=None, name="Samples"
            ))

        # همه RMها
        rm_valid = rm_df[pd.notna(rm_df[element]) & rm_df['rm_num'].notna()].sort_values('pivot_index')
        if not rm_valid.empty:
            x_rm = rm_valid['pivot_index'].values
            y_rm = pd.to_numeric(rm_valid[element], errors='coerce').values

            symbol_map = {'Base': 'o', 'Check': 't', 'Cone': 's'}
            color_map = {'Base': '#2E7D32', 'Check': '#FF6B00', 'Cone': '#7B1FA2'}

            # Scatter با قابلیت کلیک
            scatter = pg.ScatterPlotItem(
                x=x_rm, y=y_rm,
                symbol=[symbol_map.get(t, 'o') for t in rm_valid['rm_type']],
                size=18,  # کمی بزرگتر برای کلیک راحت‌تر
                brush=[color_map.get(t, '#2E7D32') for t in rm_valid['rm_type']],
                pen=pg.mkPen('white', width=2),
                name="All RMs"
            )

            # تابع کلیک روی نقطه
            def on_rm_click(plot_item, points):
                if not points:
                    return
                point = points[0]
                idx = point.index()
                clicked_rm_num = rm_valid.iloc[idx]['rm_num']
                if clicked_rm_num != self.selected_rm_num:
                    self.selected_rm_num = clicked_rm_num
                    self.current_rm_index = self.rm_numbers_list.index(int(clicked_rm_num))
                    self.current_rm_label.setText(f"RM-{int(clicked_rm_num)}")
                    self.update_tables_and_plot()  # فقط جدول بالایی آپدیت میشه

            scatter.sigClicked.connect(on_rm_click)
            self.rm_plot.addItem(scatter)

            # خط وصل RMها
            self.rm_plot.plot(
                x_rm, y_rm,
                pen=pg.mkPen('#43A047', width=3),
                name="RM Trend Line"
            )

        self.rm_plot.setLabel('left', f'{element} Intensity')
        self.rm_plot.setTitle(f"Drift Plot — {element} — Click on RM to select")
        self.rm_plot.autoRange()

    def apply_solution_filter(self):
        text = self.filter_solution_edit.text().strip()
        if not text:
            self.update_tables_and_plot()
            return
        # بعداً فیلتر واقعی اضافه میشه
        QMessageBox.information(self, "Filter", f"Filter applied: {text}")



    # pivot plot fuctions 

    def run_pivot_plot(self):
        if self.element_combo:
            self.update_calibration_range()
        self.update_navigation_buttons()
        self.update_pivot_plot()

    def update_calibration_range(self):
        # اگر هنوز UI ساخته نشده، هیچ کاری نکن
        if not hasattr(self, 'calibration_display'):
            return
        # بقیه کد مثل قبل...
        if self.original_df is not None and not self.original_df.empty:
            concentration_column = self.get_concentration_column(self.original_df)
            if concentration_column:
                element_name = self.selected_element[:-2] if len(self.selected_element) >= 2 and self.selected_element[-2] == '_' else self.selected_element
                std_data = self.original_df[
                    (self.original_df['Type'] == 'Std') &
                    (self.original_df['Element'] == element_name)
                ][concentration_column]
                std_data_numeric = [float(x) for x in std_data if self.is_numeric(x)]
                if std_data_numeric:
                    calibration_min = min(std_data_numeric)
                    calibration_max = max(std_data_numeric)
                    self.calibration_range = f"[{self.format_number(calibration_min)} to {self.format_number(calibration_max)}]"
                else:
                    self.calibration_range = "[0 to 0]"
            else:
                self.calibration_range = "[0 to 0]"
        else:
            self.calibration_range = "[0 to 0]"
        # حالا که مطمئنیم ویجت وجود داره
        self.calib_range_label.setText(f"Calibration: {self.calibration_range}")

    def update_pivot_plot(self):
        """Update the plot based on current settings."""
        if not self.selected_element or self.selected_element not in self.pivot_df.columns:
            self.logger.warning(f"Element '{self.selected_element}' not found in pivot data!")
            QMessageBox.warning(self, "Warning", f"Element '{self.selected_element}' not found!")
            return
        try:
            self.verification_plot.clear()
            self.annotations = []
            def extract_crm_id(label):
                m = re.search(r'(?i)(?:\bCRM\b|\bOREAS\b)?[\s-]*(\d+[a-zA-Z]?)[\s-]*(?:\bpar\b)?', str(label))
                return m.group(1) if m else str(label)
            concentration_column = self.get_concentration_column(self.original_df) if self.original_df is not None else None
            if self.original_df is not None and not self.original_df.empty and concentration_column:
                sample_data = self.original_df[
                    (self.original_df['Type'].isin(['Samp', 'Sample'])) &
                    (self.original_df['Element'] == self.selected_element)
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
                        float(self.calibration_range.split(' to ')[0][1:]) <= soln_conc_min <= float(self.calibration_range.split(' to ')[1][:-1]) and
                        float(self.calibration_range.split(' to ')[0][1:]) <= soln_conc_max <= float(self.calibration_range.split(' to ')[1][:-1])
                    ) if self.calibration_range != "[0 to 0]" else False
            else:
                soln_conc_min = '---'
                soln_conc_max = '---'
                soln_conc_range = '---'
                in_calibration_range_soln = False
            blank_rows = self.pivot_df[
                self.pivot_df['Solution Label'].str.contains(r'(?:CRM\s*)?(?:BLANK|BLNK)(?:\s+.*)?', case=False, na=False, regex=True)
            ]
            blank_val = 0
            blank_correction_status = "Not Applied"
            selected_blank_label = "None"
            self.blank_labels = []
            if not blank_rows.empty:
                best_blank_val = 0
                best_blank_label = "None"
                min_distance = float('inf')
                in_range_found = False
                for _, row in blank_rows.iterrows():
                    candidate_blank = row[self.selected_element] if pd.notna(row[self.selected_element]) else 0
                    candidate_label = row['Solution Label']
                    if not self.is_numeric(candidate_blank):
                        continue
                    candidate_blank = float(candidate_blank)
                    self.blank_labels.append(f"{candidate_label}: {self.format_number(candidate_blank)}")
                    in_range = False
                    for sol_label in self.app.crm_check._inline_crm_rows_display.keys():
                        if sol_label in blank_rows['Solution Label'].values:
                            continue
                        pivot_row = self.pivot_df[self.pivot_df['Solution Label'] == sol_label]
                        if pivot_row.empty:
                            continue
                        pivot_val = pivot_row.iloc[0][self.selected_element]
                        if not self.is_numeric(pivot_val):
                            continue
                        pivot_val_float = float(pivot_val)
                        for row_data, _ in self.app.crm_check._inline_crm_rows_display[sol_label]:
                            if isinstance(row_data, list) and row_data and row_data[0].endswith("CRM"):
                                val = row_data[self.pivot_df.columns.get_loc(self.selected_element)] if self.selected_element in self.pivot_df.columns else ""
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
                    for sol_label in self.app.crm_check._inline_crm_rows_display.keys():
                        if sol_label in blank_rows['Solution Label'].values:
                            continue
                        pivot_row = self.pivot_df[self.pivot_df['Solution Label'] == sol_label]
                        if pivot_row.empty:
                            continue
                        pivot_val = pivot_row.iloc[0][self.selected_element]
                        if not self.is_numeric(pivot_val):
                            continue
                        pivot_val_float = float(pivot_val)
                        for row_data, _ in self.app.crm_check._inline_crm_rows_display[sol_label]:
                            if isinstance(row_data, list) and row_data and row_data[0].endswith("CRM"):
                                val = row_data[self.pivot_df.columns.get_loc(self.selected_element)] if self.selected_element in self.pivot_df.columns else ""
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
            # self.blank_display.setText("Blanks:\n" + "\n".join(self.blank_labels) if self.blank_labels else "Blanks: None")
            crm_labels = [
                label for label in self.app.crm_check._inline_crm_rows_display.keys()
                if label not in blank_rows['Solution Label'].values
                and label in self.app.crm_check.included_crms and self.app.crm_check.included_crms[label].isChecked()
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
            element_name = self.selected_element.split()[0]
            wavelength = ' '.join(self.selected_element.split()[1:]) if len(self.selected_element.split()) > 1 else ""
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
                    pivot_row = self.pivot_df[self.pivot_df['Solution Label'] == sol_label]
                    if pivot_row.empty:
                        continue
                    pivot_val = pivot_row.iloc[0][self.selected_element]
                    if pd.isna(pivot_val) or not self.is_numeric(pivot_val):
                        pivot_val = 0
                    else:
                        pivot_val = float(pivot_val)
                    if self.original_df is not None and not self.original_df.empty and concentration_column:
                        sample_rows = self.original_df[
                            (self.original_df['Solution Label'] == sol_label) &
                            (self.original_df['Element'].str.startswith(element_name)) &
                            (self.original_df['Type'].isin(['Samp', 'Sample']))
                        ]
                        soln_conc = sample_rows[concentration_column].iloc[0] if not sample_rows.empty else '---'
                        int_val = sample_rows['Int'].iloc[0] if not sample_rows.empty else '---'
                    else:
                        soln_conc = '---'
                        int_val = '---'
                    for row_data, _ in self.app.crm_check._inline_crm_rows_display[sol_label]:
                        if isinstance(row_data, list) and row_data and row_data[0].endswith("CRM"):
                            val = row_data[self.pivot_df.columns.get_loc(self.selected_element)] if self.selected_element in self.pivot_df.columns else ""
                            if not val or not self.is_numeric(val):
                                if sol_label not in self.excluded_outliers.get(self.selected_element, set()):
                                    annotation = f"Verification ID: {crm_id} (Label: {sol_label})\n - Certificate Value: {val or 'N/A'}\n - Sample Value: {self.format_number(pivot_val)}\n - Acceptable Range: [N/A]\n - Status: Out of range (non-numeric data).\n - Blank Value: {self.format_number(blank_val)}\n - Blank Label: {selected_blank_label}\n - Blank Correction Status: {blank_correction_status}\n - Sample Value - Blank: {self.format_number(pivot_val)}\n - Corrected Range: [N/A]\n - Status after Blank Subtraction: Out of range (non-numeric data).\n - Soln Conc: {soln_conc if isinstance(soln_conc, str) else self.format_number(soln_conc)} {'in_range' if in_calibration_range_soln else 'out_range'}\n - Int: {int_val if isinstance(int_val, str) else self.format_number(int_val)}\n - Calibration Range: {self.calibration_range} {'in_range' if in_calibration_range_soln else 'out_range'}\n - CRM Source: NIST\n - Sample Matrix: Soil\n - Element Wavelength: {wavelength}\n - Analysis Date: {analysis_date}"
                                    self.annotations.append(annotation)
                                continue
                            crm_val = float(val)
                            pivot_val_float = float(pivot_val)
                            corrected_val = pivot_val_float
                            if (sol_label not in self.excluded_from_correct and
                                self.is_numeric(pivot_val) and
                                (self.scale_range_min is None or self.scale_range_max is None or
                                 self.scale_range_min <= float(pivot_val) <= self.scale_range_max) and
                                (not self.scale_above_50_cb.isChecked() or float(pivot_val) > 50)):
                                corrected_val = (pivot_val_float - self.preview_blank) * self.preview_scale
                            range_val = self.calculate_dynamic_range(crm_val)
                            lower = crm_val - range_val
                            upper = crm_val + range_val
                            in_range = lower <= corrected_val <= upper
                            if sol_label not in self.excluded_outliers.get(self.selected_element, set()):
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
                                annotation += f"\n - Soln Conc: {soln_conc if isinstance(soln_conc, str) else self.format_number(soln_conc)} {'in_range' if in_calibration_range_soln else 'out_range'}\n - Int: {int_val if isinstance(int_val, str) else self.format_number(int_val)}\n - Calibration Range: {self.calibration_range} {'in_range' if in_calibration_range_soln else 'out_range'}\n - CRM Source: NIST\n - Sample Matrix: Soil\n - Element Wavelength: {wavelength}\n - Analysis Date: {analysis_date}"
                                self.annotations.append(annotation)
                          
                            certificate_values[crm_id].append(crm_val)
                            if sol_label in self.excluded_outliers.get(self.selected_element, set()):
                                outlier_values[crm_id].append(corrected_val)
                            else:
                                sample_values[crm_id].append(corrected_val)
                            lower_bounds[crm_id].append(lower)
                            upper_bounds[crm_id].append(upper)
                            soln_concs[crm_id].append(soln_conc)
                            int_values[crm_id].append(int_val)
            if not unique_crm_ids:
                self.verification_plot.clear()
                self.logger.warning(f"No valid Verification data for {self.selected_element}")
                QMessageBox.warning(self, "Warning", f"No valid Verification data for {self.selected_element}")
                return
            self.verification_plot.setLabel('bottom', 'Verification ID')
            self.verification_plot.setLabel('left', f'{self.selected_element} Value')
            self.verification_plot.setTitle(f'Verification Values for {self.selected_element}')
            self.verification_plot.getAxis('bottom').setTicks([[(i, f'V {id}') for i, id in enumerate(unique_crm_ids)]])
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
                self.verification_plot.setXRange(-0.5, len(unique_crm_ids) - 0.5)
                self.verification_plot.setYRange(y_min - margin, y_max + margin)
            if self.show_crm_cb.isChecked():
                for crm_id in unique_crm_ids:
                    x_pos = x_pos_map[crm_id]
                    cert_vals = certificate_values.get(crm_id, [])
                    if cert_vals:
                        x_vals = [x_pos] * len(cert_vals)
                        scatter = pg.PlotDataItem(
                            x=x_vals, y=cert_vals, pen=None, symbol='o', symbolSize=8,
                            symbolPen='g', symbolBrush='g', name='Certificate Value'
                        )
                        self.verification_plot.addItem(scatter)
            if self.show_cert_cb.isChecked():
                for crm_id in unique_crm_ids:
                    x_pos = x_pos_map[crm_id]
                    for idx, sol_label in enumerate(crm_id_to_labels[crm_id]):
                        samp_vals = sample_values.get(crm_id, [])
                        if idx < len(samp_vals):
                            scatter = pg.PlotDataItem(
                                x=[x_pos], y=[samp_vals[idx]], pen=None, symbol='t', symbolSize=8,
                                symbolPen='b', symbolBrush='b', name=sol_label
                            )
                            self.verification_plot.addItem(scatter)
                        outlier_vals = outlier_values.get(crm_id, [])
                        if idx < len(outlier_vals):
                            scatter = pg.PlotDataItem(
                                x=[x_pos], y=[outlier_vals[idx]], pen=None, symbol='t', symbolSize=8,
                                symbolPen='#FFA500', symbolBrush='#FFA500', name=f"{sol_label} (Outlier)"
                            )
                            self.verification_plot.addItem(scatter)
            if self.show_range_cb.isChecked():
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
                            self.verification_plot.addItem(line_lower)
                            self.verification_plot.addItem(line_upper)
            self.verification_plot.showGrid(x=True, y=True, alpha=0.3)
            
            # Secondary plot - فقط اینجا فیلتر اعمال می‌شود
            filter_text = self.filter_solution_edit.text().strip().lower()
            if 'pivot_index' not in self.pivot_df.columns:
                self.pivot_df['pivot_index'] = self.pivot_df.index
            filtered_data = self.pivot_df.copy()
            if filter_text:
                filtered_data = filtered_data[filtered_data['Solution Label'].str.lower().str.contains(filter_text, na=False)]
            x_sec = filtered_data['pivot_index'].values
            y_sec = pd.to_numeric(filtered_data[self.selected_element], errors='coerce').fillna(0).values
        except Exception as e:
            self.verification_plot.clear()
            self.logger.error(f"Failed to update plot: {str(e)}")
            QMessageBox.warning(self, "Error", f"Failed to update plot: {str(e)}")

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

    def calculate_dynamic_range(self, value):
        """Calculate the dynamic range for a given value."""
        try:
            value = float(value)
            abs_value = abs(value)
            if abs_value < 10:
                return self.range_low
            elif 10 <= abs_value < 100:
                return abs_value * (self.range_mid / 100)
            elif 100 <= abs_value < 1000:
                return abs_value * (self.range_high1 / 100)
            elif 1000 <= abs_value < 10000:
                return abs_value * (self.range_high2 / 100)
            elif 10000 <= abs_value < 100000:
                return abs_value * (self.range_high3 / 100)
            else:
                return abs_value * (self.range_high4 / 100)
        except (ValueError, TypeError):
            return 0
        
    def update_preview_params(self):
        try:
            self.preview_blank = float(self.blank_edit.text())
        except ValueError:
            self.preview_blank = 0.0

        self.preview_scale = self.scale_slider.value() / 100.0
        self.scale_label.setText(f"Scale: {self.preview_scale:.2f}")

        # آپدیت هر دو نمودار با پیش‌نمایش blank/scale
        self.update_rm_plot()
        self.update_pivot_plot()

    def reset_blank_and_scale(self):
        """Reset blank and scale to default values."""
        self.preview_blank = 0.0
        self.blank_edit.setText("0.0")
        self.preview_scale = 1.0
        self.scale_slider.setValue(100)
        self.scale_label.setText(f"Scale: {self.preview_scale:.2f}")
        self.update_pivot_plot()

    def on_filter_changed(self):
        # فیلتر متن تغییر کرد → آپدیت هر دو نمودار
        self.update_rm_plot()
        self.update_pivot_plot()