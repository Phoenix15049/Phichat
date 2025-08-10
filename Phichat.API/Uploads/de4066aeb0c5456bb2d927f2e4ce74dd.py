from woocommerce import API
import re
from Seleni import check_steam_account_status
from SeleUbi import check_ubisoft_login

wcapi = API(
    url="https://gamekey98.ir/",
    consumer_key="ck_0664cb2de47831bd7afc1639f7a938a1d3a3a2b0",
    consumer_secret="cs_aa1411ef9140e03ee2fc2956c46674c41b0530cf",
    version="wc/v3",
    timeout=30
)

wallet_ids = [
                "1157d", "1156d", "1155d", "1154d", "1152d", "1153d", "1151d",
                "1150d", "1149d", "1148d", "1147d", "1146d", "1138d", "1137d",
                "1136d", "1135d", "847d", "846d", "715d", "714d", "845d", "485d",
                "200d", "106d", "67d", "731d"
            ]
def MG_Check(text: str) -> bool:
    text = text.strip().upper()

    parts = re.split(r"[\s\-_/.:]+", text)

    codes = [part for part in parts if re.fullmatch(r"[A-Z0-9]{7}", part)]
    print(codes)
    return len(codes) >= 2


def add_order_note(order_id, note_text, customer_note=False):
    note_data = {
        "note": note_text,
        "customer_note": customer_note
    }

    response = wcapi.post(f"orders/{order_id}/notes", data=note_data)

    if response.status_code == 201:
        print(f"یادداشت با موفقیت اضافه شد به سفارش {order_id}")
        return response.json()
    else:
        print(f"خطا در افزودن یادداشت به سفارش {order_id}: {response.status_code}")
        print(response.json())
        return None


def StatChecker(astat, isubi=False, iscre=False):
    if astat == "EG":
        if isubi or iscre:
            return "guardubisoft"
        else:
            return "guard"
    elif astat == "WI":
        if isubi or iscre:
            return "wrongubisoft"
        else:
            return "credentials"

    elif astat == "NA":
        return "processing"
    elif astat == "MG":
        return "MG"
    else:
        if iscre:
            return "credit_rainbow"
        elif isubi:
            return "admin_help"
        else:
            return "final"


def categorizer(order):
    AccText = ("وقت بخیر‌ خدمت شما از گیم کی ۹۸ مزاحم میشیم بابت سفارش ساخت اکانت استیم که خریداری کردید لطفا فورا با "
               "رسید خرید خود به ایدی @Gamekey98sup پیام بدید و بسپرید اکانتتونو بسازن با تشکر از شما ❤️🙏")

    OSs = ["laghv", "guardubisoft", "wrongubisoft", "russia", "verify", "steam_item",
           "problematic", "admin_help", "ehraz", "addfund", "credit_rainbow",
           "changeregion", "gamepass", "gtavmoney", "region", "final", "credentials", "guard"]
    total = order.get("total_price")
    t = int(total)

    print(total)
    order_id = order.get("order_id")
    items = order.get("items", [])
    item_count = len(items)
    astat = ""
    # Default status
    new_status = "processing"

    # Separate orders based on item count
    if item_count == 1:
        if t > 5000000:
            return "processing"
        item = items[0]
        region = item.get("region")
        email = item.get("email")

        # GTA V Money orders (sku == "453d")
        if item.get("product_id") in ["453d"] and item.get("VMoney") != "STEAM / استیم":
            new_status = "gtavmoney"
            return new_status

        # BO6 Battle pass(sku == "574d")
        if item.get("product_id") == "574d":
            p = item.get("Platform")
            if p in ["پلی استیشن / PSN"]:
                return "admin_help"
            else:
                return "processing"

        # Straight Admin HELP orders #Minecraft , #Faceit , #FIFA Point #UBI plus #PS plus #The Division #GearupBooster #win10 #ping #Win 11
        # #CallofBattlepass
        if item.get("product_id") in ["293d", "1169d", "226d", "859d", "256d", "1048d", "215d", "1096d", "419d",
                                      "329d", "1095xd", "188d", "150d", "784d"]:
            new_status = "admin_help"
            return new_status

        # Straight addfund orders #Antivirus #EA #GPT #Fortnite #Spotify #NitroDiscord #utube premium #GPT
        if item.get("product_id") in ["166d", "109d", "417d", "1089d", "438d", "271d", "433d",
                                      "156d", "275d", "164d", "1033d"]:
            new_status = "addfund"
            return new_status

        # GaurdCheck if Exists
        if item.get("username") and item.get("password"):
            password = item.get("password")
            astat = check_steam_account_status(item.get("username"), password)
            res = StatChecker(astat)
            print(res)
            if res not in ["final"]:
                if res == "MG":
                    print("CHECKING")
                    if item.get("backup_code"):
                        print(item.get("backup_code"))
                        if not MG_Check(item.get("backup_code")):
                            return "guard"
                    else:
                        return "guard"
                else:
                    return res

        # GTA V Money orders (sku == "453d")
        if item.get("product_id") in ["453d"]:
            new_status = "gtavmoney"
            return new_status

        # GamePass orders (sku == "267d")
        if item.get("product_id") == "267d":

            if item.get("email") or item.get("GamePassType") == "ظرفیت کامل":
                new_status = "admin_help"
            else:
                new_status = "gamepass"
            return new_status



        # Steam Games orders (sku == NUMBERS)
        elif item.get("product_type") == "بازی اصلی / تمام ادیشن ها/ باندل ها" or item.get(
                "product_type") == "دی ال سی / افزودنی بازی":

            if region == "روسیه":
                new_status = "russia"
            else:
                new_status = "final"
            return new_status

        # Customized Purchase (sku == "512d")
        elif item.get("product_id") == "512d":
            new_status = "final"
            return new_status


        # RANDOM Key orders (sku == "8d")
        elif item.get("product_id") == "8d":
            if item.get("username"):
                new_status = "steam_item"
            else:
                new_status = "completed"
            return new_status

        # TF2 Key orders (sku == "1026d")
        elif item.get("product_id") == "1026d":
            new_status = "steam_item"
            return new_status

        # Russia orders (sku == "546d")
        elif item.get("product_id") == "546d":
            new_status = "russia"
            return new_status

        # Verfiy orders (sku == "187d")
        elif item.get("product_id") == "187d":
            new_status = "verify"
            return new_status

        # BO6 Battle pass(sku == "574d")
        elif item.get("product_id") == "574d":
            p = item.get("Platform")
            if p in ["پلی استیشن / PSN"]:
                return "admin_help"
            else:
                return "processing"



        # Region change orders
        elif item.get("product_id") in [
            "1215d", "1191d", "1129d", "1128d", "1127d", "1126d", "1125d",
            "782d", "641d", "781d", "639d", "397d", "396d", "152d", "151d", "77d"
        ]:
            new_status = "changeregion"
            return new_status

        # Wallet orders
        elif item.get("product_id") in [
            "1157d", "1156d", "1155d", "1154d", "1152d", "1153d", "1151d",
            "1150d", "1149d", "1148d", "1147d", "1146d", "1138d", "1137d",
            "1136d", "1135d", "847d", "846d", "715d", "714d", "845d", "485d",
            "200d", "106d", "67d", "731d"
        ]:
            if int(total) <= 1500000:
                new_status = "final"
                return new_status

        # UPLAY Games Order
        elif item.get("product_id") in [
            "1143d", "1144d", "1142d", "1119d", "1118d", "1117d", "1116d",
            "1112d", "1113d", "1114d", "1111d", "1115d", "1110d", "626d",
            "286d", "285d", "284d", "283d", "282d", "255d", "241d", "229d",
            "228d", "210d"
        ]:
            astat = check_ubisoft_login(email, item.get("password"))
            return StatChecker(astat, True, False)

        # R6 Credit
        elif item.get("product_id") in ["327d", "417463d"]:
            astat = check_ubisoft_login(email, item.get("password"))
            return StatChecker(astat, False, True)

        # Account Create
        elif item.get("product_id") in ["194d"]:
            add_order_note(int(order_id), AccText, True);
            return "completed"
        # Other single-item orders

    elif item_count > 1:

        #All RandomKeys
        all_random_keys = all(item.get("product_id") == "8d" for item in items)
        if all_random_keys:
            has_username = any(item.get("username") for item in items)
            if has_username:
                return "steam_item"
            else:
                return "completed"

        #All Create
        elif all(item.get("product_id") == "194d" for item in items):
            new_status = "steam_item"
            return new_status

        #All TF2
        elif all(item.get("product_id") == "1026d" for item in items):
            add_order_note(int(order_id), AccText, True)
            return "completed"

        #One region Change
        elif any(item.get("product_id") in [
            "1215d", "1191d", "1129d", "1128d", "1127d", "1126d", "1125d",
            "782d", "641d", "781d", "639d", "397d", "396d", "152d", "151d", "77d"
        ] for item in items):
            for item in items:
                if item.get("product_id") in [
                    "1215d", "1191d", "1129d", "1128d", "1127d", "1126d", "1125d",
                    "782d", "641d", "781d", "639d", "397d", "396d", "152d", "151d", "77d"
                ]:
                    if item.get("username") and item.get("password"):
                        astat = check_steam_account_status(item.get("username"), item.get("password"))
                        res = StatChecker(astat)
                        if res not in ["final"]:
                            if res == "MG":
                                if item.get("backup_code"):
                                    if not MG_Check(item.get("backup_code")):
                                        return "guard"
                                else:
                                    return "guard"
                            else:
                                return res
                    break
            return "changeregion"

        #One Russia
        elif any(item.get("product_id") == "546d" for item in items):
            return "russia"


        # All Customized
        elif all(item.get("product_id") == "512d" for item in items):
            for item in items:
                if item.get("username") and item.get("password"):
                    astat = check_steam_account_status(item.get("username"), item.get("password"))
                    res = StatChecker(astat)
                    if res not in ["final"]:
                        if res == "MG":
                            if item.get("backup_code"):
                                if not MG_Check(item.get("backup_code")):
                                    return "guard"
                            else:
                                return "guard"
                        else:
                            return res
            return "final"

        #Wallets
        elif all(item.get("product_id") in wallet_ids for item in items):
            for item in items:
                if item.get("username") and item.get("password"):
                    astat = check_steam_account_status(item.get("username"), item.get("password"))
                    res = StatChecker(astat)
                    if res not in ["final"]:
                        if res == "MG":
                            if item.get("backup_code"):
                                if not MG_Check(item.get("backup_code")):
                                    return "guard"
                            else:
                                return "guard"
                        else:
                            return res
            return "final"

        #STEAM GAMES
        elif all(item.get("product_type") in [
            "بازی اصلی / تمام ادیشن ها/ باندل ها",
            "دی ال سی / افزودنی بازی"
        ] for item in items):
            for item in items:
                if item.get("username") and item.get("password"):
                    astat = check_steam_account_status(item.get("username"), item.get("password"))
                    res = StatChecker(astat)
                    if res != "final":
                        if res == "MG":
                            if item.get("backup_code"):
                                if not MG_Check(item.get("backup_code")):
                                    return "guard"
                            else:
                                return "guard"
                        else:
                            return res
                else:
                    return "credentials"
            return "final"
        elif all(
                item.get("product_id") in wallet_ids or
                item.get("product_type") in [
                    "بازی اصلی / تمام ادیشن ها/ باندل ها",
                    "دی ال سی / افزودنی بازی"
                ]
                for item in items
        ):
            for item in items:
                if item.get("username") and item.get("password"):
                    astat = check_steam_account_status(item.get("username"), item.get("password"))
                    res = StatChecker(astat)
                    if res != "final":
                        if res == "MG":
                            if item.get("backup_code"):
                                if not MG_Check(item.get("backup_code")):
                                    return "guard"
                            else:
                                return "guard"
                        else:
                            return res
                else:
                    return "credentials"
            return "final"
        else:
            new_status = "processing"

    return new_status
