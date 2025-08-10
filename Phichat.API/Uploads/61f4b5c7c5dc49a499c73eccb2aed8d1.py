import os
import subprocess
import pathlib
from rich.spinner import Spinner
from woocommerce import API
import json
import time
from datetime import datetime
import sys
import argparse
from time import sleep
import Categorizer
import Seleni
import Simplifier
from Categorizer import categorizer
from rich.console import Console
from rich.live import Live
from rich.text import Text
import questionary
import webbrowser

console = Console()

def check_playwright_installed():
    chromium_path = pathlib.Path.home() / ".cache/ms-playwright/chromium"
    if chromium_path.exists() and any(chromium_path.iterdir()):
        print("✅ Playwright browsers seem to be installed.")
    else:
        print("❌ Playwright browsers are not installed.")
        print("💡 Run 'Install Playwright browsers' to install them.")
def install_playwright_browsers():
    import shutil
    import pathlib

    python_exec = shutil.which("python") or shutil.which("python3")
    if not python_exec:
        print("❌ Python interpreter not found.")
        return

    try:
        print("📦 Installing required Python packages (playwright, webdriver-manager)...")
        subprocess.run([python_exec, "-m", "pip", "install", "playwright", "--no-cache-dir", "--no-warn-script-location"], check=True)
        subprocess.run([python_exec, "-m", "pip", "install", "webdriver-manager", "--no-cache-dir", "--no-warn-script-location"], check=True)

        print("🌐 Installing Playwright browsers (e.g., Chromium)...")
        subprocess.run([python_exec, "-m", "playwright", "install"], check=True)

        # حالا بررسی کن آیا واقعاً نصب شده یا نه
        chromium_path = pathlib.Path.home() / ".cache/ms-playwright/chromium"
        if chromium_path.exists() and any(chromium_path.iterdir()):
            print("✅ All components (including Chromium) installed successfully.")
        else:
            print("⚠️ Playwright was installed but browsers may not be fully installed.")
            print("   Try running: python -m playwright install chromium")

    except subprocess.CalledProcessError as e:
        print("❌ Installation failed at some step.")
        print(str(e))



def manage_playwright_menu():

    while True:

        action = questionary.select(
            "🧩 What do you want to do with Playwright?",
            choices=[
                "🌐 Install Playwright browsers",
                "🔍 Check if browsers are installed",
                "↩️ Back to main menu"
            ]
        ).ask()

        if action.startswith("🌐"):
            confirm = questionary.confirm("Are you sure you want to install playwright?").ask()
            if confirm:
                install_playwright_browsers()

        elif action.startswith("🔍"):
            check_playwright_installed()

        elif action.startswith("↩️"):
            break
def clear_screen():
    os.system('cls' if os.name == 'nt' else 'clear')


def show_checklist():
    clear_screen()
    print("📋 Checklist Orders\n" + "-" * 30)
    try:
        with open(resource_path("checklist.txt"), "r", encoding="utf-8") as f:
            order_ids = [line.strip() for line in f if line.strip().isdigit()]
        if not order_ids:
            print("\n[!] Checklist is empty.\n")
            return

        for i, oid in enumerate(order_ids, start=1):
            print(f"{i:>2}. Order ID: {oid}")
        print(f"\n🧮 Total: {len(order_ids)} order(s)\n")
    except FileNotFoundError:
        print("\n[!] checklist.txt not found.\n")


def checklist_menu():
    while True:

        action = questionary.select(
            "📋 What do you want to do with the checklist?",
            choices=[
                "🔗 Open links in browser",
                "🧹 Clear checklist",
                "📄 Show checklist content",
                "↩️ Back to main menu"
            ]
        ).ask()

        if action.startswith("🔗"):
            open_order_links_from_checklist()

        elif action.startswith("🧹"):
            confirm = questionary.confirm("Are you sure you want to clear the checklist?").ask()
            if confirm:
                clear_checklist()

        elif action.startswith("📄"):
            show_checklist()

        elif action.startswith("↩️"):
            break


def get_order_admin_link(order_id):
    return f"https://gamekey98.ir/wp-admin/admin.php?page=wc-orders&action=edit&id={order_id}"


def open_order_links_from_checklist():
    try:
        with open(resource_path("checklist.txt"), "r", encoding="utf-8") as f:
            order_ids = [line.strip() for line in f if line.strip().isdigit()]
        if not order_ids:
            print("[!] Checklist is empty.")
            return

        for oid in order_ids:
            url = get_order_admin_link(oid)
            webbrowser.open_new_tab(url)
        print(f"[+] Opened {len(order_ids)} order link(s) in browser.")

    except FileNotFoundError:
        print("[!] checklist.txt not found.")


def clear_checklist():
    with open(resource_path("checklist.txt"), "w", encoding="utf-8") as f:
        pass
    print("[✔] Checklist cleared.")


def interactive_menu():
    clear_screen()
    while True:
        choice = questionary.select(
            "🔧 Select an action:",
            choices=[
                "▶️ 1 - Run with default settings (20x every 20 minutes)",
                "⚙️ 2 - Run with custom settings",
                "🛠️ 3 - Set chromedriver path",
                "📂 4 - Show current chromedriver path",
                "⬇️ 5 - Install chromedriver",
                "📋️️  6 - Manage checklist",
                "🧩 7 - Manage Playwright",
                "❌ 8 - Exit"
            ]
        ).ask()

        if choice.startswith("▶️"):
            maini(20, 20)

        elif choice.startswith("⚙️"):
            count = questionary.text("How many times to run? (default: 20)").ask()
            delay = questionary.text("Delay between runs (in minutes)? (default: 20)").ask()

            try:
                count = int(count.strip()) if count.strip() else 20
                delay = int(delay.strip()) if delay.strip() else 20
                maini(count, delay)
            except ValueError:
                print("❌ Invalid input. Please enter numbers.")

        elif choice.startswith("🛠️"):
            path = questionary.text("Enter full path to chromedriver:").ask()
            if path:
                Seleni.set_driver_path(path)
                print(f"[+] Path set to: {path}")

        elif choice.startswith("📂"):
            path = Seleni.get_driver_path()
            if path:
                print(f"[+] Current chromedriver path: {path}")
            else:
                print("[!] No path is set yet.")

        elif choice.startswith("⬇️"):
            Seleni.install_driver()

        elif choice.startswith("📋"):
            checklist_menu()

        elif choice.startswith("🧩"):

            manage_playwright_menu()


        elif choice.startswith("❌"):
            print("👋 Exiting the program.")
            sys.exit()


def animated_timer(seconds):
    with Live(spinner := Spinner("dots", text=f"⏳ Time left: {seconds}s"), refresh_per_second=10):
        for i in range(seconds, 0, -1):
            spinner.text = f"⏳ Time left: {i}s"
            time.sleep(1)
        spinner.text = "⏰ Time's up!"


def simple_timer(seconds):
    spinner = ['/', '|', '\\', '-']
    for i in range(seconds, -1, -1):
        frame = spinner[i % len(spinner)]  # تغییر فریم در هر ثانیه
        sys.stdout.write(f"\r⏳ {frame} Time left: {i:02d} seconds")
        sys.stdout.flush()
        time.sleep(1)
    print("\n⏰ Time's up!")


def resource_path(relative_path):
    """Get path to resource (for pyinstaller onefile compatibility)"""
    try:
        base_path = sys._MEIPASS
    except AttributeError:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, relative_path)


def add_to_checklist(order_id):
    with open(resource_path("checklist.txt"), "a", encoding="utf-8") as f:
        f.write(f"{order_id}\n")


def is_in_checklist(order_id):
    try:
        with open("checklist.txt", "r", encoding="utf-8") as f:
            orders = f.read().splitlines()
        return str(order_id) in orders
    except FileNotFoundError:
        return False


def split_orders_by_id(orders):
    orders_by_id = {}
    for order in orders:
        order_id = order.get("order_id")
        if order_id:
            orders_by_id[order_id] = order
    return orders_by_id


wcapi = API(
    url="https://gamekey98.ir/",
    consumer_key="ck_0664cb2de47831bd7afc1639f7a938a1d3a3a2b0",
    consumer_secret="cs_aa1411ef9140e03ee2fc2956c46674c41b0530cf",
    version="wc/v3",
    timeout=30
)

OSs = ["laghv", "guardubisoft", "wrongubisoft", "russia", "verify", "steam_item",
       "problematic", "admin_help", "ehraz", "addfund", "credit_rainbow",
       "changeregion", "gamepass", "gtavmoney", "region", "final", "credentials", "guard"]


def change_order_status(order_id, new_status):
    try:
        data = {"status": new_status}
        response = wcapi.put(f"orders/{order_id}", data).json()
        print(f"Order #{order_id} status changed to {new_status}")
        return response
    except Exception as e:
        print(f"Error changing status of order #{order_id}: {str(e)}")
        return None


def mainer():
    orders = wcapi.get("orders", params={"status": "processing", "per_page": 60}).json()

    orders2 = Simplifier.simplify_orders(orders)
    # clean_json = json.dumps(orders2, indent=4,ensure_ascii=False)
    # print(clean_json)
    print("Orders Count : ")
    print(len(orders2))
    for order in orders2:
        oid = order.get("order_id")
        if not is_in_checklist(oid):
            catt = categorizer(order)
            if catt == "processing":
                print(str(oid) + " : Added to checklist")
                add_to_checklist(oid)
            print(str(oid) + " : " + catt)
            change_order_status(order.get("order_id"), catt)
        else:
            print(str(oid) + " Skipped")


def maini(maincount, delay_minutes):
    while maincount > 0:
        try:
            mainer()
        except Exception as e:
            print("Something went wrong:", e)
            maincount += 1
        print("------------------END of round------------------")
        maincount -= 1
        if maincount == 0:
            break
        simple_timer(60 * delay_minutes)


if __name__ == '__main__':
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument("-n", "--number", type=int, help="Number of times to run mainer()")
    parser.add_argument("-t", "--time", type=int, help="Delay between runs in minutes")
    parser.add_argument("-p", "--path", action="store_true")
    parser.add_argument("-s", "--set", type=str)
    parser.add_argument("-g", "--get", action="store_true")
    parser.add_argument("-i", "--install", action="store_true")
    parser.add_argument("-c", "--checklist-open", action="store_true", help="Open checklist links in browser")
    parser.add_argument("-cl", "--checklist-clear", action="store_true", help="Clear the checklist")
    parser.add_argument("-sa", "--steam-auth", nargs=2, metavar=("USERNAME", "PASSWORD"),
                        help="Check Steam login status with username and password")
    parser.add_argument("-ua", "--ubi-auth", nargs=2, metavar=("USERNAME", "PASSWORD"),
                        help="Check Ubisoft login status with username and password")
    args = parser.parse_args()

    if len(sys.argv) == 1:
        interactive_menu()

    elif args.steam_auth:
        username, password = args.steam_auth
        print("🔍 Checking Steam account login status...")
        result = Seleni.check_steam_account_status(username, password)
        print(f"🧪 Steam login result: {result}")

    elif args.ubi_auth:
        username, password = args.ubi_auth
        from SeleUbi import check_ubisoft_login

        print("🔍 Checking Ubisoft account login status...")
        result = check_ubisoft_login(username, password)
        print(f"🧪 Ubisoft login result: {result}")

    elif args.checklist_open:
        open_order_links_from_checklist()

    elif args.checklist_clear:
        clear_checklist()

    elif args.path:
        if args.set:
            Seleni.set_driver_path(args.set)
            print(f"[+] Driver path set to: {args.set}")
        elif args.get:
            path = Seleni.get_driver_path()
            print(f"[+] Current path: {path}" if path else "[!] Path not set.")
        elif args.install:
            Seleni.install_driver()
        else:
            print("[!] Use -s, -g, or -i with -p")

    else:
        counter = args.number if args.number else 20
        delay = args.time if args.time else 20
        maini(counter, delay)
