# سجل التغييرات · Changelog

## v1.7.0 — 2026-08-23 — اللوحات المثبّتة، 4 ثيمات جديدة، نمطا ساعة جديدان
- **اللوحات لم تعد تُغلق عند النقر خارجها (المطلوب الأهم).** كل لوحة أداة صارت «مثبّتة» افتراضياً: تبقى مفتوحة أثناء عملك في أي برنامج آخر، ولا تُغلق إلا بزر ✕ أو Esc أو زر التثبيت 📌 في الترويسة. زر 📌 بجوار ✕ يقلب الوضع للوحة الحالية ويحفظ اختيارك للوحات القادمة، وخيار «اللوحات تبقى مفتوحة حتى تغلقها بنفسك» في الإعدادات يضبط السلوك العام (ومن ضمنه اللوحة المفتوحة حالياً فوراً). من يفضّل السلوك القديم يعيده من نفس الخيار.
- **أربعة ثيمات جديدة (المجموع 12):** «بنفسجي» Violet · «نحاسي» Copper · «رملي» Sand (فاتح) · «ياقوتي» Ruby. جميعها تعمل مع الثيم التلقائي (نهار/ليل) و«ثيم من الخلفية» وقائمة ثيم النهار.
- **نمطا ساعة جديدان (المجموع 8):** «ثنائية» — أضواء BCD لكل رقم مع سطر التاريخ تحتها، و«حلقة اليوم» — قوس لوني يكتمل دورته من منتصف الليل إلى منتصف الليل فتعرف بنظرة كم تبقى من يومك. كلاهما يحترم نظام 12/24 ساعة والثواني والتاريخ الهجري، ويتحول تلقائياً لعقارب مضغوطة في تخطيطي الشريط الجانبي والدوك (مساحة ضيقة).
- **اختبارات** — +13 (حدود الفهرسة الجديدة، جولة إعدادات كاملة للقيم الجديدة، التثبيت افتراضي).
- **UiSmoke** — لقطات جديدة: `clock6_binary` · `clock7_dayring` · الثيمات الأربعة على الأداة.

## v1.6.0 — 2026-08-16 — Contextual panel placement, undo, command bar
- **Panels now open *beside* the tool, themed per layout.** Clicking a tool icon in a vertical layout (Sidebar) used to drop the panel straight under the icon, covering the very tool you were using. `PanelWindow.PlaceNear` now detects vertical strips (Height > Width × 1.25) and places the panel to the side; square widgets get overlap-rescue (if the panel would cover more than 20% of the anchor it shifts away); the caret arrow points in all four directions with RTL mirroring, and the entrance animation plays from the actual opening direction.
- **Undo for destructive actions.** Deleting a task/note now shows a toast with a «تراجع» button that re-inserts it — a misclick no longer costs data.
- **Daily progress strip.** Tasks completed today are counted (`NoteItem.DoneAt`, new `"da"` storage key) and a progress bar above the task list shows «أُنجزت اليوم X من Y».
- **Calculator: Arabic percentages, unit conversion, pinned results.** «20% من 450» now evaluates to 90. A collapsible unit-conversion strip (8 conversions: length, weight, temperature, data…) sits next to the «علمية» toggle. A 📌 pin button floats the result in a small always-on-top glass window (drag/copy/close, cascading multiple pins).
- **Quick command bar (new tool).** A spotlight-style panel (icon in every layout, toggleable in Settings) parses Arabic and English: «ذكّرني ب… غداً الساعة 9» creates a reminder, «= …» calculates, «افتح …» launches a matching app, and bare text becomes a task — with a live preview line under the input. Arabic-Indic digits (٠-٩) and diacritics are normalised before parsing.
- **Ramadan mode.** During the Hijri month of Ramadan (Um Al-Qura calendar) the prayer panel shows a warm banner: «باقي على الإفطار H:MM» between Fajr and Maghrib, «باقي على الإمساك» overnight, «تقبّل الله طاعتكم» after Maghrib, then counts down to Eid.
- **Settings backup.** Export/import buttons in Settings — export snapshots the encrypted settings file; import validates, restores, re-applies the theme and autostart, and reloads. (Same-user only: the file is DPAPI-encrypted.)
- **Tests** — 260 (was 236): 9 command-parser cases, 6 unit-conversion, 4 Arabic-percent, plus DoneAt/DailyProgress coverage.
- **UiSmoke** — new captures: `panel_command.png`; a done-today seeded task so the progress strip is exercised.

## v1.5.7 — 2026-08-16 — Tasks no longer vanish, calculator facelift
- **Root cause of "tasks disappear when clicked".** `PanelWindow` runs `DragMove()` on any unhandled `MouseLeftButtonDown`, and the task row only handled `MouseLeftButtonUp` — so pressing on a task was captured by the window drag and the click never completed (the editor never opened). The one spot in the row that did handle Down was the small completion circle — at the leading (right) edge in Arabic — and it **permanently deleted** the task. The row now handles Down, so clicking a task opens its editor as intended.
- **Completion is no longer deletion.** `NoteItem.Done` is a real, restorable state: the circle toggles it, done tasks are struck through, dimmed and sunk to the bottom, and their reminders stay quiet (`DueIndices` skips them). The editor's «تم الإنجاز» button marks done instead of removing, and can restore. Only the trash button (and emptying the text) deletes. Storage is backward-compatible (`"d"` key, defaults to false).
- **Calculator facelift.** Wider panel (340), larger readout (34 pt) with a stable well height, live preview now in the accent colour, taller keys (52 px) with larger glyphs, stronger accent tint and border on operator keys, brighter top-light on digit caps, recessed fill for the utility keys (C / ( ) / ⌫ — they read as holes before), and a stronger glow on the equals key.
- **Version** — `AppVersion` realigned with the changelog (had drifted to 1.5.2).
- **Tests** — 236 (was 232): 4 new — done-flag round-trip, done-tasks sink to the bottom, done-task reminders stay quiet, default-not-done.

## v1.5.6 — 2026-08-04 — ClockStyle honoured in every layout
- **Root cause of the "clock does not change" report.** Tri-panel, Sidebar and Dock were ignoring the user's `ClockStyle = Analog` choice — Tri-panel forced `TimeOnlyClock` and Sidebar/Dock forced `MinimalClock` or `Digital`. Picking Analog in Settings did nothing in those three layouts. All six layouts now honour the chosen style.
- **`AnalogClock(double size)` constructor.** A new overload accepts the dial diameter so Tri-panel (120 px), Sidebar (104 px) and Dock (100 px) can host a compact analog dial that fits the surrounding card. Hand lengths, tick marks, dot and stroke thickness all scale with `size`. The original 168-px default is preserved.
- **Layout mapping clarified.** Tri-panel: Analog → compact 120 px dial; Digital/Flip/Word/Neon at default size; Minimal unchanged. Sidebar: Analog → 104 px dial; Flip/Word fall back to Digital (too tall for the strip). Dock: Analog → 100 px dial; Flip/Word/Neon at default; Minimal unchanged.
- **Tests** — 232 (unchanged: the fix is in the layout builder dispatch, no new logic worth a test).

## v1.5.5 — 2026-08-04 — Clock fix and Sidebar/Dock polish
- **OnTick Material You loop fix.** `Theme.AppliedIndex` is now set to the effective theme index after `ApplyMaterial`, so the "eff != AppliedIndex" branch does not fire every tick when Material You is on. Without this, every second the visual tree was rebuilt and the clock lost its per-tick state, making the clock face look stuck.
- **Sidebar and Dock honour ClockStyle.** Both layouts previously hard-coded `MinimalClock`; they now pick the chosen style (with a sensible size/fallback for the narrower strip).
- **AnalogClock uses RotateTransform.** The hands used to be redrawn by setting `X2`/`Y2` on a `Line`, which WPF sometimes skipped repainting after a Reload. The hands are now `Line` elements with `RenderTransform = new RotateTransform(deg)`, which always invalidates the visual. A small digital readout is shown beneath the dial so the user can confirm `Tick()` is firing.
- **Tests** — 232 (unchanged: the on-tick bug was a UI-only issue).

## v1.5.4 — 2026-08-04 — Reverts and Material You fix
- **Removed Crystal layout.** The user reported it was too busy and the Settings icon was hard to reach. Back to 6 layouts (0..5).
- **Material You light-wallpaper fix.** `BuildPalette` and `ApplyMaterial` now detect a light accent and produce a *light* card with *dark* text, so white text never sits on a white background (the user reported this regression on a light wallpaper). Default: 6 dark palettes still picked for dark wallpapers, no change.
- **Tests** — 232 (was 229): 3 new for the light-wallpaper path of `BuildPalette`.

## v1.5.3 — 2026-08-04 — Design expansion pack
- **Themes** — three new palettes: `Sahara` (navy + gold), `Mono Ink` (high-contrast black/white minimalist), `Aurora` (cyan + violet). Total now 8 (0..7).
- **LightThemeIndex** — separate setting for the day side of AutoTheme (was hard-coded to 4 = Light). Backward-compatible default.
- **Clocks** — three new styles: `Flip` (split-flap digits with 280 ms rotation), `Word` ("الساعة الثالثة وعشر دقائق" + EN bilingual, 5-min buckets), `Neon` (digit glow matching the accent). Settings.ClockStyle now 0..5.
- **Layout** — `Crystal Cluster` (index 6): central card + 7 small tool shards scattered around with a per-shard tilt.
- **Material You** — extract the dominant colour of the current wallpaper and build a custom palette from it. Cached by path + mtime so the per-tick cost is a `File.GetLastWriteTime` call. `Settings.MaterialYou` opt-in, off by default.
- **Arabic font** — `Tajawal` listed first in the font fallback chain, with `Segoe UI → Tahoma → Arial` after it. Falls back gracefully on systems without Tajawal.
- **Tests** — 229 (was 217): 4 new for LightThemeIndex, 8 new for Material You.
- **UiSmoke** — added `widget_layout6_crystal.png` capture step.

## v1.5.2 — 2026-08-04

الملاحظات صارت **قائمة مهام** مفهومة.

### المهام · Tasks
الأداة كانت تعرض قائمة يوم وخانتَي أرقام بلا أي وسم — لا شيء يقول إن الخانتين ساعة ودقيقة،
ولا ما الذي سيحدث بعد الحفظ. أُعيد بناؤها بمنطق قائمة مهام:

- **دائرة إتمام في بداية كل صف** — نقرة واحدة تُنهي المهمة. هذا وحده يقول «هذه أشياء تُشطب»
  بلا حاجة إلى شرح.
- **أزرار لكل مهمة**: تأجيل · تثبيت · حذف، و«تم الإنجاز» داخل المحرر أيضاً.
- **محرر موسوم بالكامل**: أزرار [بلا تذكير] [اليوم] [غداً] [تاريخ آخر]، ثم **التاريخ** مكتوباً
  بالحروف («غداً · ٥ أغسطس»)، و**الوقت** كخانتين تحت كل منهما اسمها (ساعة / دقيقة) بينهما «:».
- **جملة تلخيص حية**: «سيُنبّهك: غداً · ٠٩:٠٠» — تقول بالضبط ما سيحدث قبل الحفظ.
- **تأجيل سريع**: ‎+١٠ د · +ساعة · +٣ ساعات · غداً ٩ ص.
- حقول التاريخ والوقت تظهر فقط بعد اختيار تذكير، فلا تزدحم اللوحة بلا سبب.
- «اليوم» يقترح أقرب وقت معقول (بعد نصف ساعة مقرَّباً) بدل ٩ صباحاً التي تكون قد مضت.
- الأداة صارت تُسمّى **المهام** بدل «ملاحظة سريعة».

### أدوات التطوير · Tooling
- UiSmoke يزرع مهام حقيقية ويلتقط المحرر مفتوحاً على مهمة لها تذكير — لقطة القائمة الفارغة
  كانت تُثبت وجود النص البديل فقط، لا شكل الصف نفسه.

## v1.5.1 — 2026-08-04

تنفّس في التخطيط، مقبض في مكانه، ومواقيت وملاحظات وحاسبة تُستعمل فعلاً.

### التخطيط · Layout
- **تسميات الأيقونات لم تعد تتداخل** — كانت التسمية أعرض من خليتها بـ 20px بهامشين سالبين،
  فتقتحم جارتها: «تنظيف سريع» تلتصق بـ «الآلة الحاسبة». صارت كل تسمية داخل خليتها وتلتف
  لسطرين عند اللزوم، مع فجوة واضحة بين الخلايا.
- **الأداة تستغل الشاشة** — عرض الصف صار نسبة من مساحة العمل (‏88% بحد أقصى 1500px) بدل رقم
  ثابت 620px كان يخنق 14 أداة، والتصغير بالمقبض ما زال يضغطها كما هي.
- **مقبض التكبير/التصغير على حافة الأداة** في كل التخطيطات الستة — كان في زاوية صندوق
  الإحاطة، فيظهر معلقاً في فراغ بعيداً عن الأداة (تخطيط ثلاثي المقاطع). صار على البطاقة
  السفلية نفسها، وفي التخطيط الدائري على الحلقة عند 45°.
- **إصلاح: البطاقة الدائرية كانت تُرسم مربّعة** — `BuildOrbit` يغيّر نصف القطر بعد البناء،
  وطبقات الضوء الجديدة كانت تحتفظ بالقيمة الأصلية فيظهر إطار مربّع داخل الدائرة.
  أُضيف `Theme.SetCardRadius` ليعيد تشكيل الطبقات معاً.

### مواقيت الصلاة · Prayer
- **تقديم وتأخير يدوي لكل صلاة** (±60 دقيقة) لمطابقة أذان مسجدك. يُطبَّق من مصدر واحد على
  العرض وسطر الأداة **والتنبيه** معاً، فلا يمكن أن يختلف الإشعار عن اللوحة.
  الحساب الفلكي نفسه لم يُمسّ — الإزاحة دالة نقية `PrayerCalc.ApplyAdjust` بـ 19 اختباراً
  تشمل الالتفاف حول منتصف الليل و NaN القطبي.

### الملاحظات · Notes
- **ملاحظات متعددة بدل حقل واحد**: تثبيت، حذف، تحرير، و**تذكير بوقت** ينبّه بصوت وإشعار
  عند حلوله ثم يُمسح فلا يكرر. يعمل واللوحة مغلقة.
- الملاحظة القديمة تُنقل تلقائياً إلى القائمة عند أول تشغيل، والتشفير كما هو.
- 25 اختباراً للنموذج، منها أن التذكير المتأخر **يُطلق** ولا يُهمل — نبضة متأخرة تحت الحِمل
  كانت ستُسقطه للأبد لو اشترطنا تطابق الدقيقة.

### الحاسبة · Calculator
- **شريط سجل النتائج** — انقر نتيجة سابقة لتُضاف إلى ما تكتبه، بدل إعادة كتابتها.
- **زر نسخ** داخل الشاشة.
- **لوحة المفاتيح**: `*` و `/` تتحولان إلى `×` و `÷`، و`=` و Enter تحسبان، و Esc تمسح.

### أدوات التطوير · Tooling
- **UiSmoke لم يعد يلمس إعدادات المستخدم** — كان يبني `MainWindow` حقيقياً فيكتب في
  `settings.json` الحيّ، و`SetAutoStart` كانت **تعيد توجيه مفتاح التشغيل التلقائي إلى ملف
  مؤقت**. صار يعمل على نسخة معزولة تُحذف عند الخروج.
- الاختبارات: 217 (كانت 181).

## v1.5.0 — 2026-08-04

الطقس على الأداة، لوحات مرتبطة بأيقوناتها، وتمريرة عمق ثلاثي الأبعاد.

### إصلاحات حرجة · Critical fixes
- **الملاحظة والقصاصات لم تعد تُمحى بصمت** — عند تعذّر فك تشفير DPAPI (ملف إعدادات من حساب
  ويندوز آخر، أو نسخة احتياطية مستعادة، أو إعادة تعيين كلمة المرور بواسطة مسؤول) كانت
  `Secret.Unprotect` تُرجع نصاً فارغاً، ثم يكتبه أول حفظ فوق البيانات **نهائياً**. صار النص
  المشفَّر يُحفظ كما هو بايتاً ببايت، وتعرض اللوحة حالة «محتوى مقفل» مع زر صريح للمسح.
  8 اختبارات جديدة، منها دورة تحميل‑حفظ كاملة لملف من حساب آخر.
- **دفاع junction امتدّ إلى الملفات** — كان الفحص للمجلدات فقط، بينما `GetFiles` تُرجع روابط
  الملفات الرمزية أيضاً، و`SetFileAttributes` تتبع الرابط: فكان مسح ReadOnly قبل الحذف يصل
  إلى **الملف الهدف** خارج شجرة التنظيف. الآن يُحذف الرابط دون لمس ما يشير إليه.

### جديد · New
- **سطر الطقس على الأداة نفسها** — «☀ ٢٧° المدينة» تحت الساعة في التخطيطات الستة، بتحديث خلفي
  كل 10 دقائق. لا يُرسل أي طلب شبكي إن لم يكن الموقع معروفاً مسبقاً: تحديد الموقع يبقى إجراءً
  صريحاً من لوحة الطقس أو الإعدادات. يُطفأ بمفتاح `ShowWeatherLine`.
- **اللوحة تفتح بجوار أيقونتها** مع ذيل يشير إليها، وتنقلب فوق الأيقونة إن ضاقت المساحة أسفلها،
  وتدخل من اتجاه الأيقونة. بدل التوسيط تحت الأداة كلها.
- **المناسبة المخصصة تقبل سنة ماضية** (قرن كامل) — «تتكرر كل سنة» صار صالحاً لأعياد الميلاد فعلاً.

### تصميم · Design
- **نظام ارتفاعات موحّد** (`Theme.Elevation`) بثلاث درجات بدل ظلال متفرقة بأرقام مختلفة.
- **حافة ضوئية نوعية** (`RimLight`) على البطاقات والمفاتيح والأيقونات — الخط الذي يجعل السطح
  يبدو ذا سُمك بدل مستطيل مظلَّل.
- **الحاسبة بمفاتيح keycap حقيقية**: وجه مضاء من أعلى، وضغطة تُنزل المفتاح فعلياً ويختفي ظله.
- تفعيل `GlassTop` الذي كان معرَّفاً وغير مستعمل منذ v1.4.
- إصلاح `Lift` الذي كان يُسقط قناة الشفافية فيحوّل سطحاً زجاجياً 12% إلى أبيض صلب.

### هندسة الإصدار · Release engineering
- المُثبّت صار يحمل بيانات إصدار حقيقية (كان `0.0.0.0` في خصائص الملف بينما التطبيق 1.4.2).
- `SECURITY.md` صار مطابقاً للكود بخصوص TLS، ومحاولتا 1.2 و1.3 منفصلتان حتى لا تُسقط قيمة
  1.3 غير المعروفة على بعض الأنظمة تفعيلَ 1.2 معها.
- كل أحرف MDL2 صارت `\uXXXX` بدل أحرف PUA خام — كما كان تعليقها يزعم أصلاً.
- الاختبارات: 181 (كانت 171) + خطوة معاينة جديدة للوحة المرتبطة بأيقونتها.

## v1.4.2 — 2026-08-03

إصلاح حرف ص/م المقلوب + إدخال مناسبات احترافي.

### إصلاحات · Fixes
- **علامة صباحاً/مساءً لم تعد مقلوبة** — المشكلة الجذرية: مُشكّل النص يختار شكلاً موضعياً خاطئاً للحرف العربي المنفرد على بعض الأجهزة. صارت العلامة تُرسم كمسار متجهي (vector) مستخرج من GDI+ بدل نص مُشكَّل، مع كسر قلب Viewbox في RTL بفرض LTR — محصّنة تماماً ضد اختلاف الخطوط والأجهزة.
- **اختبار الواجهة الدخاني أقل هشاشة** — اللوحة البطيئة تحت الحمل تحصل على فرصة انتظار إضافية قبل احتسابها فاشلة (كانت تُسقط البناء عشوائياً).

### جديد · New
- **المناسبات المخصصة بإدخال احترافي**: حقل لاسم المناسبة، ومنتقي يوم/شهر/سنة بقوائم منسدلة (بلا كتابة نص)، وخيار «تتكرر كل سنة» ليُحسب الموعد القادم تلقائياً، والعداد التنازلي يُحسب آلياً كما كان.
- حقلان جديدان في الإعدادات `OccasionCustomName` / `OccasionCustomYearly` مع اختبار ذهاب-وإياب لهما.
- الاختبارات: 171.

## v1.4.1 — 2026-08-03

حزمة أمنة واستقرار شاملة، مبنية على مراجعة كود خارجية.

### إصلاحات حرجة · Critical fixes
- **لوحة التركيز لم تعد تنهار** — تحديث نصوص الأزرار أصبح عبر مرجع آمن (`Tag`/`SetButtonText`) بدل صبّ `Child` الذي تغيّرت بنيته في v1.4.
- **قيمة `ThemeIndex` غير صالحة لا تقتل التطبيق** — تُقيَّد في `Settings.Normalize` وفي `AutoThemeCore.EffectiveIndex` معاً (كانت تدور Apply → Reload حتى StackOverflow).
- **إلغاء التثبيت لا يحذف إلا ملفاته** — `DeskMate.exe` و`Uninstall.exe` فقط، ويُزال المجلد إن أصبح فارغاً فقط (`rd` بلا `/s`) — ملفات المستخدم الشخصية في مجلد التثبيت لم تعد في خطر.
- **التنظيف يعيد فحص junction عند كل نزول** — يغلق نافذة سباق الفحص-ثم-الحذف (TOCTOU) في Temp المشترك.

### خصوصية · Privacy
- **تشفير DPAPI أصبح fail-closed** — عند تعذّر التشفير تُسقط القيمة بدل تخزينها نصاً صريحاً.
- كاش الطقس مرتبط بالإحداثيات — تغيير المدينة لا يعرض طقس المدينة السابقة.
- رد الطقس الناقص (بلا `current`) يُرفض بدل عرض «0° وصافٍ».

### تشخيص وتجربة · Diagnostics & UX
- سجل الأخطاء يحفظ **stack trace** كاملاً (كان نوع الخطأ ورسالته فقط).
- اللوحات تنفتح على الشاشة التي عليها الودجت فعلاً (دعم تعدد الشاشات مع تحويل DPI).
- UiSmoke يحذف اللقطات القديمة أولاً، ويفشل برمز خروج غير صفري عند أي لوحة مكسورة — و`build.ps1` يوقف البناء عندها.

### هندسة الإصدار · Release engineering
- رقم النسخة من مصدر واحد `src/Core/AppVersion.cs` يُترجم في التطبيق والمثبت معاً.
- تنظيف `dist/` (حذف نسخ GUID المؤقتة)، إزالة ملف `.claude/launch.json` الدخيل.
- إضافة `LICENSE` (MIT) و`CHANGELOG.md` و`SECURITY.md`.
- الاختبارات: 169 (كانت 125 في v1.4، و42 أُضيفت في جولة 2026-08-01).

### يتضمن حزمة 2026-08-01 الموثقة في `docs/SECURITY-FIXES-2026-08-01.md`
منع تتبع junctions في التنظيف، حارس مجلد التثبيت، حد عمق JSON، نقطة موقع HTTPS فقط، احترام علامات سرية الحافظة + حد 8KB، مسارات مؤهلة للعمليات الخارجية، حد 256KB لردود الشبكة، إصلاح ضياع تنبيه الصلاة وانقطاع الصوت وتجمّد مؤقت التركيز، موضع الشاشات المتعددة، إصلاح تسريب قائمة Tray وتسابق الحفظ، وإعادة هيكلة Settings لمصدر واحد.

## v1.4.0 — 2026-07-26
عمق ثلاثي الأبعاد في التصميم كله (حواف مضيئة وتظليل)، آلة حاسبة مُعاد بناؤها كوحدة زجاجية، إصلاح ثواني التخطيط الدائري وصفوف الأيقونات المتوازنة.

## v1.3.0 — 2026-07-25
إعادة تصميم كاملة بلغة الزجاج الطبقي، تعريفات دائمة تحت الأيقونات، 5 أدوات جديدة (تركيز، مناسبات، ملاحظة، موارد، ساعات عالمية)، الثيم التلقائي الاختياري، صوت تنبيه الصلاة.
