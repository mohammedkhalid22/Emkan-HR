# إمكان HR | Emkan HR

كل إمكانيات فريقك في مكان واحد. برنامج موارد بشرية بـ ASP.NET Core MVC (.NET 9) و SQL Server.

## الفولدرات

| الفولدر | فيه إيه | على Git؟ |
|---|---|---|
| `HRSystem/` | الكود كله: الـ Solution والمشاريع الخمسة (Domain, DAL, BLL, Web, Tests) | ✅ |
| `Docs/` | التوثيق: `HRSystem-Documentation.pdf`، و`HRSystem-Phases.pdf`، و`دليل التجربة.pdf` | ✅ |
| `Deploy/` | سكريبتات النشر: `Copy-DataToHost` بينقل البيانات من جهازك لداتا بيز الاستضافة | ✅ |
| `TestData/` | بيانات للتجربة: `CVs/` فيها CVs وهمية ومتطلبات وظيفة مدير مالي | ✅ |
| `Publish/Hosting/` | كل حاجة للاستضافة (MonsterASP): آخر تحديث `Update-*.zip`، وملفات الموقع الحالية `Current-Site-Files`، ومفتاح الـ AI، وخطوات الرفع | ❌ |
| `Publish/ToTester/` | الحزمة اللي بتتبعت للتستر يشغّلها على جهازه (البرنامج، طريقة التشغيل، الداتا التجريبية، الدليل، الحسابات) | ❌ |

`Publish/` مستبعد من Git لأنه فيه ملفات كبيرة وإعدادات سرية (Connection String ومفتاح الـ AI).

## التشغيل على الجهاز

1. افتح `HRSystem/HRSystem.sln` في Visual Studio 2022.
2. الداتا بيز بتتعمل لوحدها أول ما البرنامج يشتغل (`.\MSSQLSERVER_2022` → `HRSystemDb`).
3. مفتاح Gemini للذكاء الاصطناعي في `HRSystem/HRSystem.Web/appsettings.Local.json` (مستبعد من Git).
4. شغّل بـ F5.

## النشر على الاستضافة

اعمل Publish (Portable)، وارفع الـ zip جوه `wwwroot` على MonsterASP، واعمله Unzip. الخطوات بالتفصيل في `Publish/Hosting/خطوات الرفع على الاستضافة.txt`.
