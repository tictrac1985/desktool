# سياسة الأمان · Security Policy

## الإبلاغ عن ثغرة · Reporting a Vulnerability

إذا وجدت ثغرة أمنية في DeskMate، فضلًا لا تفتح Issue عامة.
افتح تقريرًا عبر **GitHub Security Advisories** في هذا المستودع، أو راسل المالك مباشرة، وسنرد خلال أيام قليلة.

Please report vulnerabilities privately through **GitHub Security Advisories** on this
repository instead of opening a public issue. We aim to respond within a few days.

## نطاق حساس خاص بالمشروع · Project-specific surface

هذه الأداة تعمل على سطح المكتب وتلمس مناطق حساسة، لذلك نهتم خصوصاً بـ:

- **حذف الملفات** (أداة التنظيف وإلغاء التثبيت): لا يجوز أن يتبع الروابط (junction/symlink)
  أو يحذف أي ملف لا يملكه البرنامج.
- **الحافظة والملاحظات**: القصاصات المثبتة والملاحظة اللاصقة تُشفَّر بـ DPAPI
  (لكل مستخدم Windows) وتُخزَّن fail-closed — لا نص صريح أبداً عند وعد بالتشفير.
- **الشبكة**: HTTPS فقط، بحد أقصى 256KB للرد، وبلا تثبيت إصدار TLS.
- **العمليات الخارجية**: تُستدعى بمسارات مؤهلة بالكامل دائماً (لا بحث في مجلد التطبيق).

## النسخ المدعومة · Supported versions

| الإصدار | الحالة |
|---|---|
| v1.4.1 | ✅ المدعوم حالياً |
| ≤ v1.4.0 | ❌ يحوي ثغرات مُصلَحة — حدّث فوراً |
