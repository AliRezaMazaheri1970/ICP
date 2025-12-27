# screens/process/verification/master_verification.py
import logging
import pandas as pd
import numpy as np
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QCheckBox, QLabel, QLineEdit, QPushButton,
    QGroupBox, QSlider, QComboBox, QTableView, QSplitter, QDoubleSpinBox, QFrame,
    QProgressDialog, QMessageBox, QHeaderView, QSizePolicy, QMenu
)
from PyQt6.QtCore import Qt, QThread, pyqtSignal
from PyQt6.QtGui import QColor, QStandardItemModel, QStandardItem, QFont
import pyqtgraph as pg
from typing import Any, Dict, List, Optional, Tuple

from .rm_drift_handler import RMDriftHandler
from .crm_verification_handler import CRMVerificationHandler

logging.basicConfig(level=logging.DEBUG, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

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

        # Handlers
        # Initial variables
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
        self.empty_rows_from_check = pd.DataFrame()
        self.empty_pivot_set = set()
        self.ignored_pivots = set()
        self.selected_row = 0
        self.selected_point_pivot = None
        self.corrected_drift = {}
        self.current_rm_num = None
        self.undo_stack = []
        self.elements = []
        self.current_element_index = -1
        if getattr(self.app.results, 'last_filtered_data', None) is not None:
            df = self.app.results.last_filtered_data
            self.elements = [col for col in df.columns if col != 'Solution Label']
            if self.elements:
                self.current_element_index = 0
                self.selected_element = self.elements[self.current_element_index]
        empty_outliers = {el: set() for el in self.elements} if self.elements else {}
        self.original_df = None

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

        self.rm_handler = RMDriftHandler(self)
        self.crm_handler = CRMVerificationHandler(self)
        self.setup_ui()
    
        self.rm_handler.setup_plot_items()
        self.connect_signals()
        
    def setup_ui(self):
        main_layout = QVBoxLayout(self)

        # ====================== Shared Settings ======================
        shared_gb = QGroupBox("Shared Settings")
        shared_gb.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Fixed)
        shared_l = QHBoxLayout(shared_gb)
        shared_l.setContentsMargins(10, 10, 10, 10)
        shared_l.setSpacing(12)
        shared_gb.setObjectName("sharedSettings")

        fh = QHBoxLayout()
        fh.addWidget(QLabel("File:"))
        self.file_selector = QComboBox()
        self.file_selector.setMinimumWidth(420)
        self.file_selector.setEnabled(False)
        self.file_selector.setToolTip("Click 'Calibration' to load files")
        self.file_selector.addItem("Click 'Calibration' to load files...")
        fh.addWidget(self.file_selector)
        shared_l.addLayout(fh)

        eh = QHBoxLayout()
        eh.addWidget(QLabel("Element:"))
        self.element_combo = QComboBox()
        self.element_combo.setMinimumWidth(180)
        eh.addWidget(self.element_combo)
        eh.addWidget(QLabel("Current RM:"))
        self.current_rm_label = QLabel("None")
        self.current_rm_label.setObjectName("currentRmLabel")
        self.current_rm_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        eh.addWidget(self.current_rm_label)
        self.prev_rm_btn = QPushButton("Previous RM")
        self.next_rm_btn = QPushButton("Next RM")
        eh.addWidget(self.prev_rm_btn)
        eh.addWidget(self.next_rm_btn)

        eh.addWidget(QLabel("Filter Solution:"))
        self.filter_solution_edit = QLineEdit()
        self.filter_solution_edit.setPlaceholderText("e.g. CRM001, Check, Sample...")
        eh.addWidget(self.filter_solution_edit)
        self.reset_all_btn = QPushButton("Reset All")
        self.reset_all_btn.setStyleSheet("background-color: #D32F2F; color: white; font-weight: bold;")
        eh.addWidget(self.reset_all_btn)
        self.reset_all_btn.clicked.connect(self.rm_handler.reset_all)

        self.plot_calibration_btn = QPushButton("Calibration")
        eh.addWidget(self.plot_calibration_btn)
        shared_l.addLayout(eh)
        main_layout.addWidget(shared_gb)

        # ====================== Main Splitter ======================
        main_splitter = QSplitter(Qt.Orientation.Horizontal)
        main_layout.addWidget(main_splitter)

        # ====================== Controls with ScrollArea ======================
        from PyQt6.QtWidgets import QScrollArea

        # محتوای اصلی کنترل‌ها
        controls_content = QWidget()
        controls_layout = QVBoxLayout(controls_content)
        controls_layout.setSpacing(12)
        controls_layout.setContentsMargins(10, 10, 10, 10)

        # CRM Verification
        crm_gb = QGroupBox("CRM Verification")
        crm_l = QVBoxLayout(crm_gb)
        show_l = QHBoxLayout()
        self.show_cert_cb = QCheckBox("Certificate"); self.show_cert_cb.setChecked(True); show_l.addWidget(self.show_cert_cb)
        self.show_crm_cb = QCheckBox("CRM"); self.show_crm_cb.setChecked(True); show_l.addWidget(self.show_crm_cb)
        self.show_range_cb = QCheckBox("Acceptable Range"); self.show_range_cb.setChecked(True); show_l.addWidget(self.show_range_cb)
        crm_l.addLayout(show_l)
        self.scale_above_50_cb = QCheckBox("Scale >50% Only")
        self.scale_above_50_cb.toggled.connect(self.crm_handler.update_pivot_plot)
        crm_l.addWidget(self.scale_above_50_cb)
        minmax_l = QHBoxLayout()
        minmax_l.addWidget(QLabel("Min:")); self.crm_min_edit = QLineEdit("0.0"); self.crm_min_edit.setFixedWidth(80); minmax_l.addWidget(self.crm_min_edit);self.crm_min_edit.textChanged.connect(self.crm_handler.update_scale_range)
        minmax_l.addWidget(QLabel("Max:")); self.crm_max_edit = QLineEdit("1000.0"); self.crm_max_edit.setFixedWidth(80); minmax_l.addWidget(self.crm_max_edit);self.crm_max_edit.textChanged.connect(self.crm_handler.update_scale_range)
        minmax_l.addStretch()
        crm_l.addLayout(minmax_l)
        blank_l = QHBoxLayout()
        blank_l.addWidget(QLabel("Blank:")); self.blank_edit = QLineEdit("0.0"); blank_l.addWidget(self.blank_edit)
        reset_bs_btn = QPushButton("Reset B&S"); blank_l.addWidget(reset_bs_btn)
        reset_bs_btn.clicked.connect(self.crm_handler.reset_blank_and_scale)
        crm_l.addLayout(blank_l)
        crm_l.addWidget(QLabel("Scale:"))
        self.scale_slider = QSlider(Qt.Orientation.Horizontal); self.scale_slider.setRange(0, 200); self.scale_slider.setValue(100)
        crm_l.addWidget(self.scale_slider)
        self.scale_label = QLabel("Scale: 1.00")
        crm_l.addWidget(self.scale_label)
        btns1 = QHBoxLayout()
        self.range_btn = QPushButton("Ranges"); btns1.addWidget(self.range_btn)
        self.range_btn.clicked.connect(self.crm_handler.open_range_dialog)
        self.exclude_btn = QPushButton("Exclude"); btns1.addWidget(self.exclude_btn)
        self.exclude_btn.clicked.connect(self.crm_handler.open_exclude_window)
        self.select_crms_btn = QPushButton("Select CRMs"); btns1.addWidget(self.select_crms_btn)
        self.select_crms_btn.clicked.connect(self.crm_handler.open_select_crms_window)
        crm_l.addLayout(btns1)
        btns2 = QHBoxLayout()
        self.undo_crm_btn = QPushButton("Undo CRM"); btns2.addWidget(self.undo_crm_btn)
        self.undo_crm_btn.clicked.connect(self.crm_handler.undo_crm_correction)
        self.correct_crm_btn = QPushButton("Correct CRM"); btns2.addWidget(self.correct_crm_btn)
        self.correct_crm_btn.clicked.connect(self.crm_handler.correct_crm_callback)
        crm_l.addLayout(btns2)
        model_btns = QHBoxLayout()
        self.apply_model_btn = QPushButton("Apply Our Model")
        self.apply_model_btn.clicked.connect(self.crm_handler.apply_model)
        self.report_btn = QPushButton("Report")
        self.report_btn.clicked.connect(self.crm_handler.show_report)
        model_btns.addWidget(self.apply_model_btn)
        model_btns.addWidget(self.report_btn)
        crm_l.addLayout(model_btns)
        controls_layout.addWidget(crm_gb)

        # RM Drift Correction
        rm_gb = QGroupBox("RM Drift Correction")
        rm_l = QVBoxLayout(rm_gb)
        top_h = QHBoxLayout()
        top_h.addWidget(QLabel("Keyword:"))
        self.keyword_entry2 = QLineEdit("RM"); self.keyword_entry2.setFixedWidth(80); top_h.addWidget(self.keyword_entry2)
        self.run_rm_btn = QPushButton("Check RM");self.run_rm_btn.setEnabled(False); top_h.addWidget(self.run_rm_btn)
        self.reset_original_btn = QPushButton("Reset to Original"); top_h.addWidget(self.reset_original_btn)
        self.reset_original_btn.clicked.connect(self.rm_handler.reset_to_original)
        top_h.addStretch()
        rm_l.addLayout(top_h)
        rm_cheks_layout = QHBoxLayout()
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
        self.undo_rm_btn = QPushButton("Undo RM"); bottom_h.addWidget(self.undo_rm_btn)
        rm_l.addLayout(bottom_h)
        controls_layout.addWidget(rm_gb)

        # Calibration Frame
        calib_frame = QFrame(); calib_frame.setObjectName("calibFrame")
        calib_l = QHBoxLayout(calib_frame)
        calib_l.addWidget(QLabel("Calibration Range:"))
        self.calib_range_label = QLabel("[Not Set]")
        calib_l.addWidget(self.calib_range_label); calib_l.addStretch()
        controls_layout.addWidget(calib_frame)

        self.blank_display = QLabel("")
        controls_layout.addWidget(self.blank_display)
        controls_layout.addStretch()  # مهم: باعث میشه محتوا بالا جمع بشه

        # ایجاد اسکرول
        controls_scroll = QScrollArea()
        controls_scroll.setWidget(controls_content)
        controls_scroll.setWidgetResizable(True)
        controls_scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        controls_scroll.setVerticalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAsNeeded)
        controls_scroll.setFrameShape(QFrame.Shape.NoFrame)
        controls_scroll.setStyleSheet("""
            QScrollArea { background: transparent; border: none; }
            QScrollBar:vertical {
                border: none;
                background: #E8F5E9;
                width: 12px;
                margin: 4px 2px;
                border-radius: 6px;
            }
            QScrollBar::handle:vertical {
                background: #81C784;
                min-height: 30px;
                border-radius: 6px;
            }
            QScrollBar::handle:vertical:hover { background: #66BB6A; }
            QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical { height: 0px; }
        """)

        main_splitter.addWidget(controls_scroll)
        main_splitter.setStretchFactor(0, 2)

        # ====================== Tables with Vertical Splitter ======================
        tables_splitter = QSplitter(Qt.Orientation.Vertical)  # جداکننده عمودی → بالا و پایین

        # --- جدول بالایی: RM Points ---
        upper_table_widget = QWidget()
        upper_table_layout = QVBoxLayout(upper_table_widget)
        upper_table_layout.setContentsMargins(0, 0, 0, 0)
        upper_table_layout.setSpacing(8)

        self.rm_table = QTableView()
        self.rm_table.setContextMenuPolicy(Qt.ContextMenuPolicy.CustomContextMenu)
        self.rm_table.setSelectionMode(QTableView.SelectionMode.SingleSelection)
        self.rm_table.setSelectionBehavior(QTableView.SelectionBehavior.SelectRows)
        self.rm_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        self.rm_table.verticalHeader().setVisible(False)
        self.rm_table.customContextMenuRequested.connect(self.rm_handler.show_rm_context_menu)

        rm_table_gb = QGroupBox("RM Points — Current RM Only")
        rm_table_gb.setStyleSheet("QGroupBox { font-weight: bold; color: #1A3C34; }")
        rm_table_l = QVBoxLayout(rm_table_gb)
        rm_table_l.addWidget(self.rm_table)
        upper_table_layout.addWidget(rm_table_gb)

        tables_splitter.addWidget(upper_table_widget)

        # --- جدول پایینی: All Acquisition Data ---
        lower_table_widget = QWidget()
        lower_table_layout = QVBoxLayout(lower_table_widget)
        lower_table_layout.setContentsMargins(0, 0, 0, 0)
        lower_table_layout.setSpacing(8)

        self.detail_table = QTableView()
        self.detail_table.horizontalHeader().setSectionResizeMode(QHeaderView.ResizeMode.Stretch)
        self.detail_table.verticalHeader().setVisible(False)

        detail_gb = QGroupBox("All Acquisition Data — Original + New Value")
        detail_gb.setStyleSheet("QGroupBox { font-weight: bold; color: #1A3C34; }")
        detail_l = QVBoxLayout(detail_gb)
        detail_l.addWidget(self.detail_table)
        lower_table_layout.addWidget(detail_gb)

        tables_splitter.addWidget(lower_table_widget)

        # تنظیمات اولیه تقسیم ارتفاع (مثلاً 40% بالا، 60% پایین)
        tables_splitter.setStretchFactor(0, 4)
        tables_splitter.setStretchFactor(1, 6)
        tables_splitter.setSizes([300, 500])  # مقدار اولیه پیکسلی

        # اضافه کردن splitter جدول‌ها به main_splitter
        main_splitter.addWidget(tables_splitter)
        main_splitter.setStretchFactor(1, 3)

        # ====================== Plots with Horizontal Splitter (Vertical Split) ======================
        plots_splitter = QSplitter(Qt.Orientation.Vertical)  # این splitter افقی عمل می‌کند → دو پلات بالا و پایین

        # --- پلات بالایی: Full Drift Plot ---
        upper_plot_widget = QWidget()
        upper_layout = QVBoxLayout(upper_plot_widget)
        upper_layout.setContentsMargins(0, 0, 0, 0)

        self.rm_plot = pg.PlotWidget()
        self.rm_plot.scene().sigMouseMoved.connect(lambda evt: None)
        self.rm_plot.setBackground('w')
        self.rm_plot.showGrid(x=True, y=True, alpha=0.7)
        self.rm_plot.setLabel('left', 'Intensity')
        self.rm_plot.setLabel('bottom', 'Acquisition Order')
        self.rm_plot.addLegend()
        self.highlight_point = pg.ScatterPlotItem(size=20, pen=pg.mkPen('yellow', width=4), brush=None, symbol='o')
        self.rm_plot.addItem(self.highlight_point)

        plot_gb = QGroupBox("Full Drift Plot — All Samples + All RMs")
        plot_l = QVBoxLayout(plot_gb)
        plot_l.addWidget(self.rm_plot)
        upper_layout.addWidget(plot_gb)

        plots_splitter.addWidget(upper_plot_widget)

        # --- پلات پایینی: Verification Plot ---
        lower_plot_widget = QWidget()
        lower_layout = QVBoxLayout(lower_plot_widget)
        lower_layout.setContentsMargins(0, 0, 0, 0)

        self.verification_plot = pg.PlotWidget()
        self.verification_plot.setBackground('w')
        self.verification_plot.showGrid(x=True, y=True, alpha=0.7)
        self.verification_plot.setLabel('left', 'Concentration (ppm)')
        self.verification_plot.setLabel('bottom', 'Acquisition Order')
        self.verification_plot.addLegend()

        verification_gb = QGroupBox("Verification Plot — CRM & Check Standards")
        verification_gb_layout = QVBoxLayout(verification_gb)
        verification_gb_layout.addWidget(self.verification_plot)
        lower_layout.addWidget(verification_gb)

        plots_splitter.addWidget(lower_plot_widget)

        # تنظیم اولیه نسبت تقسیم (مثلاً 60% بالا، 40% پایین)
        plots_splitter.setStretchFactor(0, 6)
        plots_splitter.setStretchFactor(1, 4)
        plots_splitter.setSizes([400, 300])  # مقادیر اولیه پیکسلی

        # اضافه کردن splitter پلات‌ها به main_splitter
        main_splitter.addWidget(plots_splitter)
        main_splitter.setStretchFactor(2, 5)

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
            self.update_labels(); self.rm_handler.update_displays()
            self.auto_flat_btn.setEnabled(True); self.auto_zero_slope_btn.setEnabled(True)
        else:
            self.current_nav_index = -1

        self.selected_row = -1
        self.selected_point_pivot = None
        self.selected_point_y = None
        self.rm_handler.update_navigation_buttons()
    def update_labels(self):
        self.current_rm_label.setText(f"Current RM: {self.current_rm_num if self.current_rm_num is not None else 'None'}")
        if self.element_combo.count() > 0:
            self.element_combo.blockSignals(True); self.element_combo.setCurrentText(self.selected_element or ''); self.element_combo.blockSignals(False)
    def _create_segments(self, positions_df: pd.DataFrame) -> List[Dict[str, Any]]:
        segments = []
        for seg_id in positions_df['segment_id'].unique():
            seg_df = positions_df[positions_df['segment_id'] == seg_id].copy()
            if seg_df.empty:
                continue
            ref_num = seg_df['ref_rm_num'].iloc[0]
            segments.append({
                'segment_id': seg_id,
                'ref_rm_num': ref_num,
                'positions': seg_df
            })
        return segments    
    def connect_signals(self):
        self.element_combo.currentTextChanged.connect(self.rm_handler.on_element_changed)
        self.prev_rm_btn.clicked.connect(self.rm_handler.prev)
        self.next_rm_btn.clicked.connect(self.rm_handler.next)
        self.blank_edit.textChanged.connect(self.crm_handler.update_preview_params)
        self.scale_slider.valueChanged.connect(self.crm_handler.update_preview_params)
        self.auto_flat_btn.clicked.connect(self.rm_handler.auto_optimize_to_flat)
        self.auto_zero_slope_btn.clicked.connect(self.rm_handler.auto_optimize_slope_to_zero)
        self.run_rm_btn.clicked.connect(self.rm_handler.start_check_rm_thread)
        self.rm_table.clicked.connect(self.rm_handler.on_table_row_clicked)
        self.rm_table.customContextMenuRequested.connect(self.rm_handler.show_rm_context_menu)
        self.detail_table.clicked.connect(self.rm_handler.on_detail_table_clicked)
        self.plot_calibration_btn.clicked.connect(self.crm_handler.run_calibration)
        self.show_cert_cb.toggled.connect(self.crm_handler.update_pivot_plot)
        self.show_crm_cb.toggled.connect(self.crm_handler.update_pivot_plot)
        self.show_range_cb.toggled.connect(self.crm_handler.update_pivot_plot)
        self.filter_solution_edit.textChanged.connect(self.rm_handler.on_filter_changed)
        self.apply_slope_btn.clicked.connect(self.rm_handler.apply_slope_from_spin)
        self.undo_rm_btn.clicked.connect(self.rm_handler.undo_changes)
        self.file_selector.currentIndexChanged.connect(self.rm_handler.on_file_changed)
        


    def update_current_rm_after_file_change(self):
        """
        بعد از تغییر فایل، اولین RM موجود در داده‌های جدید رو انتخاب کنه
        """
        if not hasattr(self, 'rm_df') or self.rm_df.empty:
            return

        available_rm_nums = sorted(self.rm_df['rm_num'].dropna().unique().astype(int).tolist())
        
        if not available_rm_nums:
            self.current_rm_num = None
            self.current_rm_label.setText("None")
            return

        # اگر RM فعلی هنوز در این فایل هست، همون رو نگه دار
        if self.current_rm_num in available_rm_nums:
            new_rm_num = self.current_rm_num
        else:
            # در غیر این صورت اولین RM موجود در فایل جدید
            new_rm_num = available_rm_nums[0]

        self.current_rm_num = new_rm_num
        self.current_rm_label.setText(f"Current RM: {new_rm_num}")
        
        # ریست انتخاب ردیف در جدول
        self.selected_row = -1