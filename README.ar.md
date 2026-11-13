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

## النسخ الاحتياطي

البرنامج بياخد نسخة كاملة من الداتا بيز كل يوم بعد الساعة 2 بالليل، ويحتفظ بآخر 14 نسخة. تقدر تشوف الحالة وتاخد نسخة في أي وقت من **النسخ الاحتياطي** في القائمة (للأدمن بس). الإعدادات في `appsettings.json` في قسم `Backup`.

- ده بيشتغل على جهاز أو سيرفر خاص بيكم. **الاستضافة المشتركة (MonsterASP) مش بتسمح بيه**، وهناك النسخة بتتاخد من لوحة تحكم الاستضافة.
- لو سبت `Folder` فاضي، النسخ بتتحفظ في الفولدر الافتراضي بتاع SQL Server. ده شغال، بس البرنامج مش هيقدر ينزّل الملفات ولا يمسح القديم منها.
- الأحسن تعمل فولدر للنسخ (ويا ريت على هارد تاني) وتديله صلاحية من PowerShell كـ Administrator:

```powershell
New-Item -ItemType Directory -Force D:\EmkanBackups
icacls D:\EmkanBackups /grant 'NT Service\MSSQL$MSSQLSERVER_2022:(OI)(CI)M' /grant "$env:USERNAME:(OI)(CI)M"
```

  وبعدين حط `"Folder": "D:\\EmkanBackups"` في الإعدادات. (`MSSQLSERVER_2022` هو اسم الـ instance. ولو البرنامج شغال على IIS، ادي الصلاحية لحساب الـ App Pool بدل `$env:USERNAME`.)
- الرجوع لنسخة بيتعمل من SQL Server Management Studio: Databases ← Restore Database.

## ربط جهاز البصمة (ZKTeco)

البرنامج بيقرا أجهزة ZKTeco عن طريق الـ IP (بورت 4370)، من شاشة **أجهزة البصمة**. أول بصمة في اليوم بتبقى الحضور، وآخر بصمة الانصراف، والموظف بيتعرف من **رقم البصمة** اللي في ملفه.

- **البرنامج على سيرفر جوه الشركة:** ضيف الجهاز وعلّم على "السيرفر بيوصل للجهاز مباشرة". هيتقرا كل 10 دقايق، أو بزرار "اقرا دلوقتي".
- **البرنامج على الإنترنت (الاستضافة):** الموقع مش هيقدر يوصل لجهاز جوه شبكة الشركة. فبتشغّل **برنامج ربط البصمة** (`HRSystem.FingerprintAgent`) على أي كمبيوتر في الشركة:
  1. اعمل Publish ليه:

```powershell
dotnet publish HRSystem\HRSystem.FingerprintAgent -c Release -r win-x64 --self-contained -o Publish\FingerprintAgent
```

  2. افتح `agentsettings.json` وحط عنوان الموقع، والـ IP والـ Comm Key بتاع الجهاز، ومفتاح الربط من شاشة أجهزة البصمة (زرار المفتاح).
  3. شغّل `EmkanFingerprintAgent.exe` وسيبه مفتوح، أو اعمله Scheduled Task كل 10 دقايق بـ `--once`.
- كل اللي بيحصل بيتكتب في `agent.log` جنب البرنامج.
