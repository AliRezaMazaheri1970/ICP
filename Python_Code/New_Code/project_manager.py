# project_manager.py
import joblib
import os
import logging
from datetime import datetime
import pandas as pd
import numpy as np
from PyQt6.QtWidgets import QFileDialog, QMessageBox, QCheckBox

logger = logging.getLogger(__name__)


def _is_serializable(obj):
    """Check if an object can be safely serialized (no PyQt widgets)."""
    if obj is None:
        return True
    if isinstance(obj, (str, int, float, bool, list, dict, tuple, set)):
        return True
    if isinstance(obj, (pd.DataFrame, pd.Series, np.ndarray, np.generic)):
        return True
    return False


def save_project(app,save_path):
    """Save the complete project – only logical data (no GUI widgets)."""
    if app.data is None or app.data.empty:
        QMessageBox.warning(app, "Warning", "No data to save.\nPlease open a file first.")
        return
    if save_path :
        file_path=save_path+'Auto_save'
    else:
        file_path, _ = QFileDialog.getSaveFileName(
            app, "Save Project", os.path.join(file_path, 'Auto_save'), "RASF Project Files (*.RASF)"
        )
    file_path += '.RASF'

    try:
        project_data = {
            'main_window': {
                'data': app.data,
                'file_path': app.file_path,
            },
            'timestamp': datetime.now().isoformat(),
            'version': '1.5',  # نسخه نهایی
            'tabs': {}
        }

        # === Tab-specific state keys ===
        tab_states = {
            'pivot_tab': [
                'original_pivot_data', 'pivot_data',
                'filters', 'column_widths',
                'row_filter_values', 'column_filter_values',
                '_inline_duplicates_display',
            ],
            'elements_tab': ['blk_elements', 'selected_elements'],
            'weight_check': ['excluded_samples', 'weight_data', 'corrected_weights'],
            'volume_check': ['excluded_volumes', 'volume_data', 'corrected_volumes'],
            'df_check': ['excluded_dfs', 'df_data', 'corrected_dfs'],
            'empty_check': ['empty_rows'],
            'crm_check': [
                'corrected_crm', '_inline_crm_rows', '_inline_crm_rows_display',
                'included_crms', 'column_widths', 'crm_selections',
                'range_low', 'range_mid', 'range_high1', 'range_high2', 'range_high3', 'range_high4',
                'scale_range_min', 'scale_range_max', 'scale_above_50',
                'excluded_outliers', 'excluded_from_correct',
                'preview_blank', 'preview_scale'
            ],
            'rm_check': [
                'rm_df', 'positions_df', 'original_df', 'corrected_df', 'pivot_df',
                'initial_rm_df', 'empty_rows_from_check', 'corrected_drift',
                'undo_stack', 'navigation_list', 'current_nav_index',
                'selected_element', 'current_label', 'elements', 'solution_labels',
                'selected_row', 'original_rm_values', 'display_rm_values',
                'current_valid_row_ids', 'current_slope', 'keyword',
                'stepwise_state'
            ],
            'master_verification': [
                # === داده‌های اصلی پنجره ===
                'analysis_data',
                'selected_element', 'current_rm_num', 'current_rm_index',
                'current_element_index', 'current_file_index', 'current_nav_index',
                'navigation_list', 'element_list', 'rm_numbers_list',
                'elements', 'file_ranges',
                'manual_corrections', 'empty_rows_from_check', 'empty_pivot_set',
                'ignored_pivots', 'corrected_drift', 'undo_stack',
                'params', 'preview_blank', 'preview_scale',
                'range_low', 'range_mid', 'range_high1', 'range_high2', 'range_high3', 'range_high4',
                'scale_range_min', 'scale_range_max', 'scale_above_50', 'calibration_range',
                'blank_labels', 'keyword',

                # === داده‌های داخلی (موقت) ===
                'all_pivot_df', 'all_rm_df', 'all_initial_rm_df', 'all_positions_df', 'all_segments',
                'pivot_df', 'rm_df', 'initial_rm_df', 'positions_df', 'segments',
                'unique_rm_nums',

                # === حالت‌های UI ===
                'per_file_cb', 'global_optimize_cb', 'scale_above_50_cb',
                'show_cert_cb', 'show_crm_cb', 'show_range_cb',
                'filter_solution_edit', 'keyword_entry2', 'blank_edit', 'scale_slider',
                'crm_min_edit', 'crm_max_edit',
                'file_selector', 'element_combo', 'current_rm_label',
                'slope_spin', 'slope_display','stepwise_cb'
            ],
            'results': [
                'search_var', 'filter_field', 'filter_values', 'column_filters',
                'column_widths', 'solution_label_order', 'element_order',
                'decimal_places', 'last_filtered_data', 'last_pivot_data',
                '_last_cache_key', 'data_hash'
            ],
            'report': ['report_data'],
            'compare_tab': ['comparison_results'],
            'crm_tab': ['crm_database_path'],
        }

        for tab_name, keys in tab_states.items():
            tab_obj = getattr(app, tab_name, None)
            if tab_obj:
                state = {}
                for key in keys:
                    if hasattr(tab_obj, key):
                        value = getattr(tab_obj, key)
                        if _is_serializable(value):
                            if isinstance(value, np.ndarray):
                                value = value.tolist()
                            elif isinstance(value, set):
                                value = list(value)
                            elif isinstance(value, pd.DataFrame):
                                value = value.copy(deep=True)
                            elif hasattr(value, 'isChecked'):
                                value = value.isChecked()
                            elif key == 'included_crms' and isinstance(value, dict):
                                # فقط مقدار bool ذخیره شود
                                value = {k: v.isChecked() if hasattr(v, 'isChecked') else v for k, v in value.items()}
                            state[key] = value

                # === Special: PivotTab UI States ===
                if tab_name == 'pivot_tab':
                    if hasattr(tab_obj, 'decimal_places') and tab_obj.decimal_places:
                        state['decimal_places'] = tab_obj.decimal_places.currentText()
                    if hasattr(tab_obj, 'use_int_var') and tab_obj.use_int_var:
                        state['use_int'] = tab_obj.use_int_var.isChecked()
                    if hasattr(tab_obj, 'use_oxide_var') and tab_obj.use_oxide_var:
                        state['use_oxide'] = tab_obj.use_oxide_var.isChecked()
                    if hasattr(tab_obj, 'duplicate_threshold_edit') and tab_obj.duplicate_threshold_edit:
                        try:
                            state['duplicate_threshold'] = float(tab_obj.duplicate_threshold_edit.text() or 10)
                        except:
                            state['duplicate_threshold'] = 10.0
                    if hasattr(tab_obj, 'search_var') and tab_obj.search_var:
                        state['search_text'] = tab_obj.search_var.text()

                # === Special: RM Stepwise Checkbox ===
                if tab_name == 'rm_check' and hasattr(tab_obj, 'stepwise_checkbox'):
                    state['stepwise_state'] = tab_obj.stepwise_checkbox.isChecked()

                # === Special: CRM Text Inputs ===
                if tab_name == 'crm_check':
                    if hasattr(tab_obj, 'crm_diff_min') and tab_obj.crm_diff_min:
                        state['crm_diff_min_text'] = tab_obj.crm_diff_min.text()
                    if hasattr(tab_obj, 'crm_diff_max') and tab_obj.crm_diff_max:
                        state['crm_diff_max_text'] = tab_obj.crm_diff_max.text()
                
                # === Special: Master Verification Full State ===
                if tab_name == 'master_verification':
                    tab_obj = getattr(app, tab_name, None)
                    if tab_obj:
        
                        # --- ورودی‌های متنی ---
                        for edit_name, key in [
                            ('filter_solution_edit', 'filter_solution_text'),
                            ('keyword_entry2', 'keyword'),
                            ('blank_edit', 'blank_edit_text'),
                            ('crm_min_edit', 'crm_min_text'),
                            ('crm_max_edit', 'crm_max_text'),
                        ]:
                            edit = getattr(tab_obj, edit_name, None)
                            if edit and hasattr(edit, 'text'):
                                state[key] = edit.text()
                        state['display_rm_values'] = getattr(tab_obj, 'display_rm_values', [])
                        # --- اسلایدر و لیبل‌ها ---
                        if hasattr(tab_obj, 'scale_slider') and tab_obj.scale_slider:
                            state['scale_slider_value'] = tab_obj.scale_slider.value()
                        if hasattr(tab_obj, 'scale_label') and tab_obj.scale_label:
                            state['scale_label_text'] = tab_obj.scale_label.text()
                        if hasattr(tab_obj, 'slope_spin') and tab_obj.slope_spin:
                            state['slope_spin_value'] = tab_obj.slope_spin.value()
                        if hasattr(tab_obj, 'slope_display') and tab_obj.slope_display:
                            state['slope_display_text'] = tab_obj.slope_display.text()

                        # --- کامبوباکس‌ها ---
                        if hasattr(tab_obj, 'file_selector') and tab_obj.file_selector:
                            state['file_selector_index'] = tab_obj.file_selector.currentIndex()
                        if hasattr(tab_obj, 'element_combo') and tab_obj.element_combo:
                            state['element_combo_text'] = tab_obj.element_combo.currentText()

                        # --- لیبل‌ها ---
                        if hasattr(tab_obj, 'current_rm_label') and tab_obj.current_rm_label:
                            state['current_rm_label_text'] = tab_obj.current_rm_label.text()
                        if hasattr(tab_obj, 'calib_range_label') and tab_obj.calib_range_label:
                            state['calib_range_label_text'] = tab_obj.calib_range_label.text()

                        # --- RMDriftHandler داخلی ---
                        if hasattr(tab_obj, 'rm_handler'):
                            handler = tab_obj.rm_handler
                            state['rm_drift_handler_state'] = {
                                'undo_stack': handler.undo_stack,
                                'manual_corrections': handler.manual_corrections,
                                'ignored_pivots': list(handler.ignored_pivots),
                            }

                        # --- CRM Handler (اگر نیاز باشه) ---
                        if hasattr(tab_obj, 'crm_handler'):
                            crm_handler = tab_obj.crm_handler
                            state['crm_handler_state'] = {
                                'included_crms': getattr(crm_handler, 'included_crms', {}),
                                'crm_database_path': getattr(crm_handler, 'crm_database_path', None),
                                'last_crm_correction': getattr(crm_handler, 'last_crm_correction', {}),
                            }

                project_data['tabs'][tab_name] = state

        joblib.dump(project_data, file_path, compress=6)
        logger.info(f"Project saved: {file_path}")
        QMessageBox.information(app, "Success", f"Project saved:\n{os.path.basename(file_path)}")
        app.setWindowTitle(f"RASF Data Processor - {os.path.basename(file_path)}")

    except Exception as e:
        logger.error(f"Error saving project: {str(e)}", exc_info=True)
        QMessageBox.critical(app, "Error", f"Saving failed:\n{str(e)}")


def load_project(app):
    """Load a complete project – only logical data."""
    file_path, _ = QFileDialog.getOpenFileName(
        app, "Load Project", "", "RASF Project Files (*.RASF)"
    )
    if not file_path:
        return

    try:
        project_data = joblib.load(file_path)
        logger.debug(f"Project loaded: {file_path}")

        # Full reset
        app.reset_app_state()

        # Restore main data
        main_state = project_data.get('main_window', {})
        if 'data' in main_state:
            app.data = main_state['data']
        if 'file_path' in main_state:
            app.file_path = main_state['file_path']

        project_name = os.path.basename(file_path)
        app.file_path_label.setText(f"Project: {project_name}")
        app.setWindowTitle(f"RASF Data Processor - {project_name}")

        # === UI Keys that must NOT be set with setattr ===
        ui_keys = [
            'decimal_places', 'use_int_var', 'use_oxide_var',
            'duplicate_threshold_edit', 'search_var',
            'crm_diff_min', 'crm_diff_max', 'stepwise_checkbox', 'keyword_entry',
            'included_crms'  # این را هم اضافه کردیم
        ]

        # Restore tab states
        for tab_name, state in project_data.get('tabs', {}).items():
            tab_obj = getattr(app, tab_name, None)
            if tab_obj and isinstance(state, dict):

                # === Restore logical data (safe with setattr) ===
                for key, value in state.items():
                    if key in ui_keys:
                        continue  # Skip UI widgets
                    if hasattr(tab_obj, key):
                        if key in ['original_rm_values', 'display_rm_values'] and isinstance(value, list):
                            setattr(tab_obj, key, np.array(value, dtype=float))
                        elif key in ['current_valid_row_ids'] and isinstance(value, list):
                            setattr(tab_obj, key, np.array(value, dtype=int))
                        elif key in ['excluded_outliers'] and isinstance(value, dict):
                            setattr(tab_obj, key, {k: set(v) for k, v in value.items()})
                        elif key == 'excluded_from_correct' and isinstance(value, list):
                            setattr(tab_obj, key, set(value))
                        else:
                            setattr(tab_obj, key, value)

                # === Restore PivotTab UI & Data ===
                if tab_name == 'pivot_tab':
                    if 'original_pivot_data' in state and state['original_pivot_data'] is not None:
                        tab_obj.original_pivot_data = state['original_pivot_data']
                    if 'pivot_data' in state and state['pivot_data'] is not None:
                        tab_obj.pivot_data = state['pivot_data']
                    else:
                        tab_obj.pivot_data = tab_obj.original_pivot_data.copy(deep=True) if tab_obj.original_pivot_data is not None else None

                    if 'decimal_places' in state and hasattr(tab_obj, 'decimal_places') and hasattr(tab_obj.decimal_places, 'setCurrentText'):
                        decimal_str = state['decimal_places']
                        if tab_obj.decimal_places.findText(decimal_str) != -1:
                            tab_obj.decimal_places.setCurrentText(decimal_str)
                        else:
                            tab_obj.decimal_places.setCurrentIndex(2)

                    if 'use_int' in state and hasattr(tab_obj, 'use_int_var') and hasattr(tab_obj.use_int_var, 'setChecked'):
                        tab_obj.use_int_var.setChecked(state['use_int'])

                    if 'use_oxide' in state and hasattr(tab_obj, 'use_oxide_var') and hasattr(tab_obj.use_oxide_var, 'setChecked'):
                        tab_obj.use_oxide_var.setChecked(state['use_oxide'])

                    if 'duplicate_threshold' in state and hasattr(tab_obj, 'duplicate_threshold_edit') and hasattr(tab_obj.duplicate_threshold_edit, 'setText'):
                        threshold_val = state['duplicate_threshold']
                        tab_obj.duplicate_threshold = threshold_val
                        tab_obj.duplicate_threshold_edit.setText(str(threshold_val))

                    if 'search_text' in state and hasattr(tab_obj, 'search_var') and hasattr(tab_obj.search_var, 'setText'):
                        tab_obj.search_var.setText(state['search_text'])

                    if 'filters' in state:
                        tab_obj.filters = state['filters']
                    if 'column_widths' in state:
                        tab_obj.column_widths = state['column_widths']
                    if 'row_filter_values' in state:
                        tab_obj.row_filter_values = state['row_filter_values']
                    if 'column_filter_values' in state:
                        tab_obj.column_filter_values = state['column_filter_values']
                    if '_inline_duplicates_display' in state:
                        tab_obj._inline_duplicates_display = state['_inline_duplicates_display']

                    tab_obj.update_pivot_display()

                # === Restore CRM Check UI ===
                if tab_name == 'crm_check':
                    if 'crm_diff_min_text' in state and hasattr(tab_obj, 'crm_diff_min') and hasattr(tab_obj.crm_diff_min, 'setText'):
                        tab_obj.crm_diff_min.setText(state['crm_diff_min_text'])
                    if 'crm_diff_max_text' in state and hasattr(tab_obj, 'crm_diff_max') and hasattr(tab_obj.crm_diff_max, 'setText'):
                        tab_obj.crm_diff_max.setText(state['crm_diff_max_text'])

                    # Rebuild included_crms checkboxes
                    if 'included_crms' in state and isinstance(state['included_crms'], dict):
                        tab_obj.included_crms = {}  # پاک کن
                        for label, checked in state['included_crms'].items():
                            checkbox = QCheckBox(label)
                            checkbox.setChecked(bool(checked))
                            tab_obj.included_crms[label] = checkbox
                        # اگر UI قبلاً ساخته شده، باید دوباره نمایش داده شود
                        if hasattr(tab_obj, 'update_crm_checkboxes'):
                            tab_obj.update_crm_checkboxes()

                    if hasattr(tab_obj, 'current_plot_window') and tab_obj.current_plot_window:
                        plot_win = tab_obj.current_plot_window
                        for attr in ['range_low', 'range_mid', 'range_high1', 'range_high2',
                                     'range_high3', 'range_high4', 'scale_range_min', 'scale_range_max',
                                     'preview_blank', 'preview_scale', 'excluded_outliers',
                                     'excluded_from_correct', 'scale_above_50']:
                            if attr in state:
                                val = state[attr]
                                if attr == 'excluded_outliers' and isinstance(val, dict):
                                    val = {k: set(v) for k, v in val.items()}
                                elif attr == 'excluded_from_correct' and isinstance(val, list):
                                    val = set(val)
                                setattr(plot_win, attr, val)
                        plot_win.blank_edit.setText(f"{plot_win.preview_blank:.3f}")
                        plot_win.scale_slider.setValue(int(plot_win.preview_scale * 100))
                        plot_win.scale_label.setText(f"Scale: {plot_win.preview_scale:.2f}")
                        if plot_win.scale_range_min is not None:
                            plot_win.scale_range_min_edit.setText(str(plot_win.scale_range_min))
                        if plot_win.scale_range_max is not None:
                            plot_win.scale_range_max_edit.setText(str(plot_win.scale_range_max))
                        plot_win.scale_range_display.setText(
                            f"Scale Range: [{plot_win.scale_range_min} to {plot_win.scale_range_max}]"
                            if plot_win.scale_range_min and plot_win.scale_range_max else "Scale Range: Not Set"
                        )
                        if hasattr(plot_win, 'scale_above_50'):
                            plot_win.scale_above_50.setChecked(state.get('scale_above_50', False))

                # === RM Check: Full UI Restore ===
                if tab_name == 'rm_check':
                    if 'stepwise_state' in state and hasattr(tab_obj, 'stepwise_checkbox') and hasattr(tab_obj.stepwise_checkbox, 'setChecked'):
                        tab_obj.stepwise_checkbox.setChecked(state['stepwise_state'])
                    if 'keyword' in state and hasattr(tab_obj, 'keyword_entry') and hasattr(tab_obj.keyword_entry, 'setText'):
                        tab_obj.keyword_entry.setText(state['keyword'])
                    if 'undo_stack' in state:
                        restored = []
                        for cdf, rdf, cdrift in state['undo_stack']:
                            restored.append((cdf.copy(deep=True), rdf.copy(deep=True), cdrift.copy()))
                        tab_obj.undo_stack = restored
                        tab_obj.undo_button.setEnabled(len(restored) > 0)

                    if 'elements' in state and 'solution_labels' in state:
                        tab_obj.elements = state['elements']
                        tab_obj.solution_labels = state['solution_labels']
                        tab_obj.navigation_list = [(el, lb) for el in tab_obj.elements for lb in tab_obj.solution_labels]
                        tab_obj.current_nav_index = state.get('current_nav_index', 0)
                        if tab_obj.navigation_list:
                            idx = min(tab_obj.current_nav_index, len(tab_obj.navigation_list) - 1)
                            tab_obj.selected_element, tab_obj.current_label = tab_obj.navigation_list[idx]
                        else:
                            tab_obj.selected_element = tab_obj.elements[0] if tab_obj.elements else None
                            tab_obj.current_label = tab_obj.solution_labels[0] if tab_obj.solution_labels else None

                    if hasattr(tab_obj, 'element_combo') and tab_obj.elements:
                        tab_obj.element_combo.blockSignals(True)
                        tab_obj.element_combo.clear()
                        tab_obj.element_combo.addItems(tab_obj.elements)
                        if tab_obj.selected_element:
                            tab_obj.element_combo.setCurrentText(tab_obj.selected_element)
                        tab_obj.element_combo.blockSignals(False)

                    if (hasattr(tab_obj, 'rm_df') and tab_obj.rm_df is not None and 
                        not tab_obj.rm_df.empty and 
                        hasattr(tab_obj, 'current_label') and tab_obj.current_label and
                        'Solution Label' in tab_obj.rm_df.columns and
                        tab_obj.current_label in tab_obj.rm_df['Solution Label'].values):
                        tab_obj.update_labels()
                        tab_obj.display_rm_table()
                        tab_obj.update_plot()
                        tab_obj.update_detail_plot()
                        tab_obj.update_detail_table()
                        tab_obj.update_navigation_buttons()
                        tab_obj.auto_optimize_flat_button.setEnabled(True)
                        tab_obj.auto_optimize_zero_button.setEnabled(True)
                    else:
                        tab_obj.update_labels()
                        tab_obj.update_navigation_buttons()
                        tab_obj.auto_optimize_flat_button.setEnabled(False)
                        tab_obj.auto_optimize_zero_button.setEnabled(False)
                # === Restore Master Verification Full State ===
                if tab_name == 'master_verification':
                    tab_obj = getattr(app, tab_name, None)
                    if not tab_obj or not isinstance(state, dict):
                        continue

                    # 1. فقط داده‌های غیر-UI رو مستقیم ست کن
                    for key in [
                        'analysis_data', 'selected_element', 'current_rm_num', 'current_rm_index',
                        'current_element_index', 'current_file_index', 'current_nav_index',
                        'navigation_list', 'element_list', 'rm_numbers_list', 'elements', 'file_ranges',
                        'manual_corrections', 'empty_rows_from_check', 'empty_pivot_set',
                        'ignored_pivots', 'corrected_drift', 'undo_stack', 'params',
                        'preview_blank', 'preview_scale', 'range_low', 'range_mid',
                        'range_high1', 'range_high2', 'range_high3', 'range_high4',
                        'scale_range_min', 'scale_range_max', 'scale_above_50', 'calibration_range',
                        'blank_labels', 'keyword', 'all_pivot_df', 'all_rm_df', 'all_initial_rm_df',
                        'all_positions_df', 'all_segments', 'pivot_df', 'rm_df', 'initial_rm_df','stepwise_cb',
                        'positions_df', 'segments', 'unique_rm_nums',
                        'display_rm_values', 'display_rm_times', 'display_pivot_values', 'display_pivot_times'
                    ]:
                        if key in state:
                            value = state[key]
                            # تبدیل نوع‌های خاص
                            if key in ['empty_pivot_set', 'ignored_pivots']:
                                value = set(value)
                            elif key == 'manual_corrections':
                                value = {int(k): float(v) for k, v in value.items()}
                            elif isinstance(value, dict) and key.endswith('_df'):
                                try:
                                    value = pd.DataFrame(value)
                                except:
                                    pass
                            setattr(tab_obj, key, value)

                    # 2. تابع کمکی: فقط روی ویجت واقعی کار کنه، نه روی bool
                    def safe_ui(attr_name, value=None, setter=None):
                        widget = getattr(tab_obj, attr_name, None)
                        if widget is None:
                            return False
                        if isinstance(widget, bool):
                            return False  # مهم: اگه هنوز bool هست، دست نزن!
                        if setter and callable(setter):
                            try:
                                setter(widget, value)
                                return True
                            except:
                                pass
                        return False

                    # 3. چک‌باکس‌ها — فقط اگه ویجت واقعی باشه
         
                    checkbox_states = {
                    'per_file_cb': state.get('per_file_cb', True),
                    'global_optimize_cb': state.get('global_optimize_cb', False),
                    'stepwise_cb': state.get('stepwise_cb', False),
                    'scale_above_50_cb': state.get('scale_above_50_cb', False),
                    'show_cert_cb': state.get('show_cert_cb', True),
                    'show_crm_cb': state.get('show_crm_cb', True),
                    'show_range_cb': state.get('show_range_cb', False),
                    }

                    for cb_name, checked in checkbox_states.items():
                        widget = getattr(tab_obj, cb_name, None)
                        if widget is not None and hasattr(widget, 'setChecked') and not isinstance(widget, bool):
                            try:
                                widget.blockSignals(True)
                                widget.setChecked(bool(checked))
                                widget.blockSignals(False)
                            except:
                                pass

                    # 4. ورودی‌های متنی
                    if 'filter_solution_text' in state:
                        safe_ui('filter_solution_edit', str(state['filter_solution_text']), lambda w, t: w.setText(t))
                    if 'keyword' in state:
                        safe_ui('keyword_entry2', str(state['keyword']), lambda w, t: w.setText(t))
                    if 'blank_edit_text' in state:
                        safe_ui('blank_edit', str(state['blank_edit_text']), lambda w, t: w.setText(t))

                    # 5. اسلایدر و اسپین
                    if 'scale_slider_value' in state:
                        safe_ui('scale_slider', int(state['scale_slider_value']), lambda w, v: w.setValue(v))
                    if 'slope_spin_value' in state:
                        safe_ui('slope_spin', float(state['slope_spin_value']), lambda w, v: w.setValue(v))

                    tab_obj.element_combo.setEnabled(True)
                    tab_obj.file_selector.setEnabled(True)
                    # 6. کامبوباکس‌ها
                    try:
                        # 1. پر کردن file_selector
                        if hasattr(tab_obj, 'file_selector') and tab_obj.file_selector is not None:
                            if not isinstance(tab_obj.file_selector, bool):
                                tab_obj.file_selector.blockSignals(True)
                                tab_obj.file_selector.clear()
                                tab_obj.file_selector.addItem("All Files")
                                if hasattr(tab_obj, 'file_ranges') and tab_obj.file_ranges:
                                    for fr in tab_obj.file_ranges:
                                        fname = fr.get('file_name', f"File {len(tab_obj.file_selector)}")
                                        tab_obj.file_selector.addItem(fname)
                                # حالا ایندکس ذخیره‌شده رو اعمال کن
                                saved_idx = state.get('file_selector_index', 0)
                                if 0 <= saved_idx < tab_obj.file_selector.count():
                                    tab_obj.file_selector.setCurrentIndex(saved_idx)
                                else:
                                    tab_obj.file_selector.setCurrentIndex(0)
                                tab_obj.file_selector.blockSignals(False)

                        # 2. پر کردن element_combo
                        if hasattr(tab_obj, 'element_combo') and tab_obj.element_combo is not None:
                            if not isinstance(tab_obj.element_combo, bool):
                                tab_obj.element_combo.blockSignals(True)
                                tab_obj.element_combo.clear()
                                if hasattr(tab_obj, 'elements') and tab_obj.elements:
                                    tab_obj.element_combo.addItems(tab_obj.elements)
                                # حالا متن ذخیره‌شده رو پیدا کن و انتخاب کن
                                saved_element = state.get('element_combo_text') or state.get('selected_element')
                                if saved_element:
                                    idx = tab_obj.element_combo.findText(str(saved_element))
                                    if idx >= 0:
                                        tab_obj.element_combo.setCurrentIndex(idx)
                                    else:
                                        tab_obj.element_combo.setCurrentIndex(0)
                                tab_obj.element_combo.blockSignals(False)

                    except Exception as e:
                        logger.warning(f"Failed to restore combo boxes: {e}")
                    # 7. لیبل‌ها
                    for label_key, widget_name in [
                        ('current_rm_label_text', 'current_rm_label'),
                        ('calib_range_label_text', 'calib_range_label'),
                        ('scale_label_text', 'scale_label'),
                        ('slope_display_text', 'slope_display'),
                    ]:
                        if label_key in state:
                            safe_ui(widget_name, str(state[label_key]), lambda w, t: w.setText(t))

                    # 8. بروزرسانی نهایی — فقط اگه همه چیز آماده باشه
                    try:
                        tab_obj.update_labels()
                    except: pass

                    try:
                        if hasattr(tab_obj, 'rm_handler') and tab_obj.rm_handler:
                            # این خط حیاتیه: اول مطمئن شو که ویجت‌ها واقعی هستن
                                tab_obj.rm_handler.update_displays()
                    except Exception as e:
                        logger.warning(f"RM Handler update skipped during load: {e}")

                    try:
                        if hasattr(tab_obj, 'crm_handler') and tab_obj.crm_handler:
                            tab_obj.crm_handler.update_pivot_plot()
                    except: pass

                    continue
        # Final refresh
        app.notify_data_changed()

        # Restore ResultsFrame
        if hasattr(app, 'results') and app.results:
            results = app.results
            if results.last_filtered_data is not None and not results.last_filtered_data.empty:
                results.update_table(results.last_filtered_data)
            else:
                results.show_processed_data()
            if hasattr(results, 'search_entry') and results.search_var:
                results.search_entry.setText(results.search_var)
            if hasattr(results, 'decimal_combo') and results.decimal_places:
                results.decimal_combo.setCurrentText(results.decimal_places)

        # === PivotTab: فقط نمایش بروز شود ===
        if hasattr(app, 'pivot_tab') and app.pivot_tab:
            app.pivot_tab.update_pivot_display()

        # Restore ElementsTab
        if hasattr(app, 'elements_tab') and app.elements_tab:
            app.elements_tab.process_blk_elements()

        # CRM Check: Update display
        if hasattr(app, 'crm_check') and app.crm_check:
            app.crm_check.update_pivot_display()

        # === Final Refresh: Master Verification ===
        if hasattr(app, 'master_verification') and app.master_verification:
            mv = app.master_verification
            mv.is_fully_loaded = True
            mv.update_labels()
            if mv.current_rm_num:
                mv.current_rm_label.setText(f"Current RM: {mv.current_rm_num}")
            mv.rm_handler.update_navigation_buttons()
            mv.rm_handler.update_slope_from_data()
            mv.crm_handler.update_pivot_plot()
            mv.rm_handler.update_displays()

        app.main_content.switch_tab("Process")

        QMessageBox.information(app, "Success", f"Project loaded:\n{project_name}")
        logger.info(f"Project fully loaded: {file_path}")

    except Exception as e:
        logger.error(f"Error loading project: {str(e)}", exc_info=True)
        QMessageBox.critical(app, "Error", f"Loading failed:\n{str(e)}")
        app.reset_app_state()