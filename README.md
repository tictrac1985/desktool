# DeskMate — ديسك ميت

أداة سطح مكتب لويندوز: ساعة وتاريخ في منتصف الشاشة مع أدوات سريعة أسفلها — تظهر كجزء من سطح المكتب (خلف كل النوافذ دائماً)، بالعربية والإنجليزية.

A Windows desktop widget: a beautiful clock & date with quick tools beneath it — lives on the desktop layer (always below your apps), in Arabic and English.

![DeskMate](docs/screenshot.png)

## الملفات النهائية · Final files

| File | What it is |
|---|---|
| `dist/DeskMate.exe` | **نسخة محمولة** — شغّلها من أي مكان، بلا تثبيت (~110 KB) · Portable — run from anywhere, no install |
| `dist/DeskMateSetup.exe` | **المُثبّت** — تثبيت دائم بأيقونة سطح مكتب وقائمة ابدأ وتشغيل تلقائي · Installer — permanent install with desktop icon, Start Menu and autostart |

لا يحتاج أي متطلبات — يعمل على ويندوز 10/11 مباشرة (.NET Framework 4.8 مدمج في النظام).
No prerequisites — runs on Windows 10/11 as-is (.NET Framework 4.8 is built into Windows).

## الأدوات · Tools

| | الأداة | Tool |
|---|---|---|
| 🕐 | ساعة وتاريخ (رقمية / عقارب / بسيطة) + التاريخ الهجري | Clock & date (digital / analog / minimal) + Hijri date |
| 🗑 | تنظيف سريع لمخلفات ويندوز مع خيار إفراغ سلة المحذوفات | Quick clean of Windows leftovers + optional Recycle Bin |
| 🧮 | آلة حاسبة بسيطة تتحول لعلمية بزر واحد | Calculator, one click switches to scientific |
| 🖼 | تغيير خلفية سطح المكتب مع طريقة العرض | Wallpaper changer with fit style |
| ⏻ | ضبط وقت دخول السكون (شاحن / بطارية) | Sleep timeout presets (AC / battery) |
| 📋 | حافظة نصوص مؤقتة مع تثبيت وحذف | Clipboard text keeper with pin/delete |
| ☀ | الطقس الحالي + 3 أيام (تحديد موقع تلقائي أو مدينة يدوية) | Current weather + 3-day forecast (auto or manual location) |
| 📂 | قائمة برامج ويندوز: فتح أو **إضافة لسطح المكتب** بنقرة | Installed apps list: open or **add to desktop** in one click |
| 🌙 | مواقيت الصلاة (حساب فلكي محلي، 5 طرق حساب) مع تنبيه عند الوقت وتنبيه مسبق | Prayer times (offline astronomical calc, 5 methods) with at-time & pre-alerts |
| ⚙ | إعدادات: اللغة، شكل الساعة، إظهار/إخفاء كل أداة، التشغيل مع ويندوز | Settings: language, clock style, show/hide any tool, autostart |

## سلوك الأداة · Behavior

- تبقى دائماً **خلف** النوافذ المفتوحة — لا تزاحم عملك.
- اسحبها لأي مكان؛ موضعها يُحفظ.
- أيقونة في منطقة الإشعارات (Tray): إظهار/إخفاء، الإعدادات، إغلاق.
- تشغيل نسخة ثانية يُظهر الأداة فقط (نسخة واحدة دائماً).
- الإعدادات في `%AppData%\DeskMate\settings.json`.

## البناء من المصدر · Build from source

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

يتطلب VS 2022 Build Tools (مترجم Roslyn) فقط — بدون ‎.NET SDK وبدون NuGet. يقوم السكربت بتوليد الأيقونة، تشغيل 77 اختباراً ذاتياً للمنطق، بناء `DeskMate.exe` ثم `DeskMateSetup.exe`.

Requires only VS 2022 Build Tools (Roslyn csc) — no .NET SDK, no NuGet. The script generates the icon, runs 77 logic self-tests, builds `DeskMate.exe` then `DeskMateSetup.exe`.

### خيارات المُثبّت · Installer switches

- `DeskMateSetup.exe --silent` — تثبيت صامت بالخيارات الافتراضية · silent install
- `Uninstall.exe --uninstall` — إزالة (تُنشأ داخل مجلد التثبيت) · uninstall
