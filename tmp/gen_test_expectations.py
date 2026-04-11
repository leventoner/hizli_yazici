#!/usr/bin/env python3
"""Generate test expectations from the Python mintlemon library."""
import sys
sys.path.insert(0, r'C:\Users\lener\Desktop\code\python\yazici\venv\Lib\site-packages')

from mintlemon import Normalizer

test_cases = [
    "Opusmegi cagristiran catirtilar.",
    "Hadi bir masal uyduralim, icinde mutlu, doygun, telassiz durdugumuz.",
    "kiymetli umarim basarilar gormek calismalar tesekkur",
    "simdi cok is icin birsey hersey",
    "guzel bugun aksam oglen merhaba",
    "ulasmak yapmak ediyoruz tesekkurler lutfen donus",
    "nasil kizim senin hocanin kocanin babanin",
    "arkadasi degilim birak muptelasi ergenliginden dusmanim",
    "olmayi oyle kiymetli umarim basarilar",
    "Bu yazi icin cok calistim ama hala bitmedi",
    "Turkce karakter duzeltme fonksiyonu dogru calismiyor",
    "Gelecek hafta gorusmek uzere iyi gunler",
    "Ogrenciler sinifta ders calisiyorlar",
    "Ogle yemeginden sonra toplanti var",
    "Dusman askerlerini puskurttuler",
    "Cocuklarin gozleri parliyordu",
    "Gunun sonunda cok yoruldum",
    "O sirada bahcede cicekleri kokluyorduk",
    "Hersey bahcivanin islik calmasiyla yasandi",
    "Bu is gercekten cok onemli ve acil",
    "Hayatinda ilk kez boyle bir sey goruyordu",
    "Yarin sabah erken kalkacagim",
    "Turkiye'nin en guzel sehirlerinden biri",
    "Calisanlarin haklari korunmali",
    "Ogretmenler ogrencilere yardim etti",
]

print("// Auto-generated test expectations from mintlemon")
print("// Format: input | expected_output")
for tc in test_cases:
    result = Normalizer.deasciify(tc)
    print(f"{tc}|{result}")
