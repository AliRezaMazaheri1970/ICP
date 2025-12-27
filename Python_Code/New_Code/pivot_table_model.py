from PyQt6.QtCore import Qt, QAbstractTableModel, QModelIndex
from PyQt6.QtGui import QColor
import pandas as pd
import logging

class PivotTableModel(QAbstractTableModel):
    """Custom table model for pivot table, optimized for large datasets with editable cells."""
    def __init__(self, pivot_tab, df=None, crm_rows=None):
        super().__init__()
        self.logger = logging.getLogger(__name__)
        self.pivot_tab = pivot_tab
        self._df = df if df is not None else pd.DataFrame()
        self._crm_rows = crm_rows if crm_rows is not None else []
        self._row_info = []
        self._column_widths = {}
        self._build_row_info()

    def set_data(self, df, crm_rows=None):
        self.logger.debug("Setting new data in PivotTableModel")
        self.beginResetModel()
        self._df = df.copy()
        self._crm_rows = crm_rows if crm_rows is not None else []
        self._build_row_info()
        self.endResetModel()

    def _build_row_info(self):
        self._row_info = []
        for row_idx in range(len(self._df)):
            self._row_info.append({'type': 'pivot', 'index': row_idx})
            sol_label = self._df.iloc[row_idx]['Solution Label']
            for grp_idx, (sl, cdata) in enumerate(self._crm_rows):
                if sl == sol_label:
                    for sub in range(len(cdata)):
                        self._row_info.append({'type': 'crm', 'group': grp_idx, 'sub': sub})
                    break

    def rowCount(self, parent=QModelIndex()):
        return len(self._row_info)

    def columnCount(self, parent=QModelIndex()):
        return self._df.shape[1]

    def data(self, index, role=Qt.ItemDataRole.DisplayRole):
        if not index.isValid() or index.row() >= len(self._row_info):
            return None

        row = index.row()
        col = index.column()
        col_name = self._df.columns[col]
        info = self._row_info[row]

        is_crm_row = False
        is_diff_row = False
        crm_row_data = None
        tags = None
        pivot_row = row

        if info['type'] == 'pivot':
            pivot_row = info['index']
        else:
            grp = info['group']
            sub = info['sub']
            _, crm_data = self._crm_rows[grp]
            if sub == 0:
                is_crm_row = True
                crm_row_data = crm_data[0][0]
                tags = crm_data[0][1]
            elif sub == 1:
                is_diff_row = True
                crm_row_data = crm_data[1][0]
                tags = crm_data[1][1]
            pivot_row = self._df.index[self._df['Solution Label'] == self._crm_rows[grp][0]].tolist()[0]

        if role == Qt.ItemDataRole.DisplayRole or role == Qt.ItemDataRole.EditRole:
            # استفاده از self.pivot_tab.results_frame.decimal_combo برای تعداد اعشار
            try:
                dec = int(self.pivot_tab.results_frame.decimal_combo.currentText())
            except (AttributeError, ValueError):
                self.logger.warning("decimal_combo not available or invalid, using default decimal places (1)")
                dec = 1  # مقدار پیش‌فرض در صورت عدم وجود decimal_combo

            if is_crm_row or is_diff_row:
                value = crm_row_data[col]
                return str(value) if value else ""
            else:
                value = self._df.iloc[pivot_row, col]
                if col_name != "Solution Label" and pd.notna(value):
                    try:
                        return f"{float(value):.{dec}f}"
                    except (ValueError, TypeError):
                        return "" if pd.isna(value) else str(value)
                return str(value) if pd.notna(value) else ""

        elif role == Qt.ItemDataRole.BackgroundRole:
            if is_crm_row:
                return QColor("#FFF5E4")
            elif is_diff_row and tags:
                if tags[col] == "in_range":
                    return QColor("#ECFFC4")
                elif tags[col] == "out_range":
                    return QColor("#FFCCCC")
                return QColor("#E6E6FA")
            return QColor("#f9f9f9") if pivot_row % 2 == 0 else QColor("white")

        elif role == Qt.ItemDataRole.TextAlignmentRole:
            return Qt.AlignmentFlag.AlignLeft if col_name == "Solution Label" else Qt.AlignmentFlag.AlignCenter

        return None

    def flags(self, index):
        """Make all cells editable."""
        if not index.isValid():
            return Qt.ItemFlag.NoItemFlags
        return Qt.ItemFlag.ItemIsEnabled | Qt.ItemFlag.ItemIsSelectable | Qt.ItemFlag.ItemIsEditable

    def setData(self, index, value, role=Qt.ItemDataRole.EditRole):
        """Update the underlying DataFrame with edited values."""
        if not index.isValid() or role != Qt.ItemDataRole.EditRole:
            self.logger.debug(f"Invalid index or role: {index}, {role}")
            return False

        row = index.row()
        col = index.column()
        col_name = self._df.columns[col]
        info = self._row_info[row]
        self.logger.debug(f"setData called for row {row}, col {col} ({col_name}), value: '{value}'")

        try:
            if info['type'] == 'pivot':
                # Get the solution label from the view
                solution_label = self._df.iloc[info['index']]['Solution Label']
                # Find the row in the full pivot_data
                full_df = self.pivot_tab.results_frame.last_filtered_data
                full_row_idx = full_df[full_df['Solution Label'] == solution_label].index
                if full_row_idx.empty:
                    self.logger.warning(f"Solution Label '{solution_label}' not found in last_filtered_data")
                    return False
                full_row_idx = full_row_idx[0]

                if value.strip() == "":
                    full_df.at[full_row_idx, col_name] = pd.NA
                    self.logger.debug(f"Set value at {full_row_idx}, {col_name} to NA")
                else:
                    if col_name == 'Solution Label':
                        full_df.at[full_row_idx, col_name] = str(value).strip()
                        self.logger.debug(f"Updated Solution Label at {full_row_idx} to '{value}'")
                    else:
                        try:
                            full_df.at[full_row_idx, col_name] = float(value)
                            self.logger.debug(f"Updated numeric value at {full_row_idx}, {col_name} to {value}")
                        except ValueError:
                            self.logger.warning(f"Invalid numeric value '{value}' for column {col_name}")
                            return False
            else:
                self.logger.warning("Editing CRM rows is not allowed")
                return False

            # Emit dataChanged and refresh UI
            self.dataChanged.emit(index, index, [Qt.ItemDataRole.DisplayRole, Qt.ItemDataRole.BackgroundRole])
            self.logger.debug("Emitted dataChanged signal")
            self.pivot_tab.update_pivot_display()
            # اطلاع‌رسانی تغییرات به ResultsFrame
            self.pivot_tab.data_changed.emit()
            self.logger.debug("Emitted pivot_tab.data_changed signal")
            return True
        except Exception as e:
            self.logger.error(f"Failed to set data at row {row}, col {col}: {str(e)}")
            return False

    def headerData(self, section, orientation, role=Qt.ItemDataRole.DisplayRole):
        if role == Qt.ItemDataRole.DisplayRole:
            if orientation == Qt.Orientation.Horizontal:
                return str(self._df.columns[section])
            return str(section + 1)
        return None

    def set_column_width(self, col, width):
        self._column_widths[col] = width