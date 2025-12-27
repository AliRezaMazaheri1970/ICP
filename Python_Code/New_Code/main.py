# main.py
import sys
import logging
from PyQt6.QtWidgets import QApplication
from screens.login_window import LoginWindow
from app import MainWindow  # تغییر: از main_window.py ایمپورت کن
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

if __name__ == "__main__":
    app = QApplication(sys.argv)
    app.setStyle("Fusion")

    login = LoginWindow()

    def open_main(username: str, name: str, position: str, user_id: str):
        """
        باز کردن پنجره اصلی پس از لاگین موفق
        """
        logger.info(f"Login successful: {name} ({position}) - Username: {username} (ID: {user_id})")
        
        window = MainWindow(
            username=username,
            user_name=name,
            user_position=position,
            user_id=user_id
        )
        window.setWindowTitle(f"RASF Data Processor - {name} ({username})")
        window.resize(1200, 750)
        window.show()
        login.close()

    # اتصال سیگنال به اسلات
    login.login_successful.connect(open_main)
    login.show()

    sys.exit(app.exec())