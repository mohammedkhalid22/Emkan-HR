using HRSystem.BLL.Common;
using HRSystem.BLL.DTOs;
using HRSystem.BLL.Services;
using HRSystem.DAL.Data;
using HRSystem.Domain.Entities;
using HRSystem.Domain.Enums;
using HRSystem.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRSystem.Web.Services;

// Fills an EMPTY database with a believable demo company (people, three months of attendance,
// leaves, payroll, applicants) for showing the system to clients.
// Run: dotnet HRSystem.Web.dll --seed-demo   (with the connection string pointing at a demo database)
// Every name, number and ID here is made up.
public static class DemoDataSeeder
{
    public const string DemoPassword = "Demo@2026";

    private static readonly DateOnly From = new(2026, 8, 1);

    private record Person(string Key, string NameAr, string NameEn, Gender Gender, DateOnly Birth, string Dept, string Job,
        string? Manager, DateOnly Hire, decimal Salary, string Profile = "steady", bool Remote = false,
        ContractType Contract = ContractType.Permanent, DateOnly? ContractEnd = null, DateOnly? Left = null);

    private static readonly Person[] People =
    [
        new("gm", "هشام عبد الرحمن فؤاد", "Hesham Fouad", Gender.Male, new(1972, 3, 14), "gm", "المدير العام", null, new(2012, 1, 15), 85000),
        new("hrm", "نهى سمير الشريف", "Noha El-Sherif", Gender.Female, new(1984, 7, 22), "hr", "مدير الموارد البشرية", "gm", new(2016, 3, 1), 42000),
        new("hr1", "ياسمين أحمد فتحي", "Yasmin Fathy", Gender.Female, new(1995, 11, 5), "hr", "أخصائي موارد بشرية", "hrm", new(2021, 6, 13), 17000),
        new("hr2", "رنا طارق الحسيني", "Rana El-Husseiny", Gender.Female, new(1997, 9, 9), "hr", "أخصائي موارد بشرية", "hrm", new(2023, 8, 20), 14500),
        new("fm", "طارق محمود الجندي", "Tarek El-Gendy", Gender.Male, new(1980, 2, 18), "acc", "مدير مالي", "gm", new(2015, 9, 1), 55000),
        new("acc1", "منة الله خالد سعيد", "Menna Saeed", Gender.Female, new(1993, 4, 9), "acc", "محاسب", "fm", new(2019, 2, 10), 16500),
        new("acc2", "عمرو حسن عبد الله", "Amr Abdallah", Gender.Male, new(1990, 12, 1), "acc", "محاسب", "fm", new(2018, 8, 5), 18000, "late"),
        new("acc3", "سلمى إبراهيم يوسف", "Salma Youssef", Gender.Female, new(1999, 8, 27), "acc", "محاسب", "fm", new(2024, 1, 14), 11000, Contract: ContractType.Temporary, ContractEnd: new(2026, 12, 31)),
        new("acc4", "خالد ممدوح سليم", "Khaled Selim", Gender.Male, new(1988, 6, 6), "acc", "محاسب", "fm", new(2016, 5, 22), 21000, "overtime"),
        new("sm", "كريم عبد العزيز ناصر", "Karim Nasser", Gender.Male, new(1983, 5, 30), "sales", "مدير مبيعات", "gm", new(2014, 4, 20), 48000),
        new("s1", "محمد السيد رمضان", "Mohamed Ramadan", Gender.Male, new(1992, 1, 19), "sales", "مندوب مبيعات", "sm", new(2020, 3, 1), 12000, "overtime", Remote: true),
        new("s2", "أحمد مصطفى كامل", "Ahmed Kamel", Gender.Male, new(1996, 10, 10), "sales", "مندوب مبيعات", "sm", new(2022, 5, 15), 10500, "late", Remote: true),
        new("s3", "مريم عادل حلمي", "Mariam Helmy", Gender.Female, new(1997, 6, 3), "sales", "مندوب مبيعات", "sm", new(2023, 2, 1), 10000, Remote: true),
        new("s4", "يوسف وليد شاكر", "Youssef Shaker", Gender.Male, new(1998, 9, 21), "sales", "مندوب مبيعات", "sm", new(2025, 7, 1), 9000, Contract: ContractType.Temporary, ContractEnd: new(2026, 11, 30)),
        new("s5", "هبة الله فوزي عيسى", "Heba Eissa", Gender.Female, new(1994, 2, 2), "sales", "مندوب مبيعات", "sm", new(2021, 1, 17), 11500, Remote: true),
        new("s6", "سامح نبيل عوض", "Sameh Awad", Gender.Male, new(1990, 4, 4), "sales", "مندوب مبيعات", "sm", new(2019, 4, 1), 10000, "absent", Left: new(2026, 9, 30)),
        new("itm", "مصطفى أشرف البنا", "Mostafa El-Banna", Gender.Male, new(1987, 3, 8), "it", "مدير تكنولوجيا المعلومات", "gm", new(2017, 10, 1), 50000, "overtime"),
        new("dev1", "عبد الرحمن جمال زكي", "Abdelrahman Zaki", Gender.Male, new(1994, 12, 12), "it", "مطور برمجيات", "itm", new(2020, 9, 6), 26000, "overtime"),
        new("dev2", "نور الهدى محمد صبري", "Nour Sabry", Gender.Female, new(1998, 2, 25), "it", "مطور برمجيات", "itm", new(2023, 4, 2), 19000),
        new("dev3", "زياد هاني لطفي", "Ziad Lotfy", Gender.Male, new(2000, 7, 7), "it", "مطور برمجيات", "itm", new(2025, 3, 1), 14000),
        new("sup1", "إسلام فاروق عطية", "Islam Attia", Gender.Male, new(1991, 11, 30), "it", "مسؤول دعم فني", "itm", new(2019, 11, 17), 13500, "late"),
        new("sup2", "عمر شريف الحناوي", "Omar El-Hennawy", Gender.Male, new(2001, 10, 30), "it", "مسؤول دعم فني", "itm", new(2026, 9, 14), 9000),
        new("csm", "داليا رأفت منصور", "Dalia Mansour", Gender.Female, new(1986, 1, 11), "cs", "مشرف خدمة عملاء", "gm", new(2017, 2, 5), 24000),
        new("cs1", "آية محمود عبد الحميد", "Aya Abdelhamid", Gender.Female, new(1997, 5, 16), "cs", "ممثل خدمة عملاء", "csm", new(2021, 10, 3), 9500),
        new("cs2", "حسام الدين علي مرسي", "Hossam Morsy", Gender.Male, new(1995, 8, 8), "cs", "ممثل خدمة عملاء", "csm", new(2020, 12, 6), 10000, "absent"),
        new("cs3", "شيماء عصام فريد", "Shaimaa Farid", Gender.Female, new(1999, 3, 29), "cs", "ممثل خدمة عملاء", "csm", new(2024, 6, 2), 8500),
        new("cs4", "مينا ماجد جرجس", "Mina Gerges", Gender.Male, new(1996, 4, 14), "cs", "ممثل خدمة عملاء", "csm", new(2022, 1, 9), 9200, "late"),
    ];

    private static readonly (string Name, decimal Min, decimal Max, string? Requirements)[] Jobs =
    [
        ("المدير العام", 60000, 120000, null),
        ("مدير الموارد البشرية", 30000, 60000, null),
        ("أخصائي موارد بشرية", 10000, 25000, "- بكالوريوس تجارة أو آداب أو حقوق\n- خبرة سنتين على الأقل في شؤون العاملين أو التوظيف\n- معرفة بقانون العمل والتأمينات الاجتماعية\n- إجادة Excel"),
        ("مدير مالي", 35000, 70000, "- بكالوريوس تجارة شعبة محاسبة أو مالية\n- خبرة لا تقل عن 10 سنين في الحسابات والإدارة المالية، منها 4 سنين على الأقل في منصب إشرافي\n- إعداد القوائم المالية طبقاً لمعايير المحاسبة المصرية والدولية (EAS / IFRS)\n- إعداد الموازنات التقديرية وتقارير التدفقات النقدية\n- خبرة قوية في الضرائب المصرية: الدخل، القيمة المضافة، كسب العمل، والفاتورة الإلكترونية\n- خبرة على نظام ERP (SAP أو Oracle أو Odoo) وإجادة Excel المتقدم\n- قيادة فريق محاسبين لا يقل عن 5 أفراد\n- يُفضّل شهادة مهنية (CMA أو CPA أو ACCA)\n- إنجليزي جيد جداً"),
        ("محاسب", 8000, 25000, "- بكالوريوس تجارة شعبة محاسبة\n- خبرة سنتين على الأقل في الحسابات العامة\n- التسويات البنكية وحسابات العملاء والموردين\n- خبرة على برنامج محاسبي وإجادة Excel"),
        ("مدير مبيعات", 30000, 60000, null),
        ("مندوب مبيعات", 7000, 18000, "- مؤهل عالي\n- خبرة سنة على الأقل في المبيعات الميدانية\n- رخصة قيادة سارية\n- مهارات تواصل وتفاوض قوية"),
        ("مدير تكنولوجيا المعلومات", 35000, 70000, null),
        ("مطور برمجيات", 12000, 35000, "- بكالوريوس حاسبات ومعلومات أو هندسة\n- خبرة 3 سنين على الأقل في C# و ASP.NET Core\n- SQL Server و Entity Framework\n- خبرة في REST APIs و Git"),
        ("مسؤول دعم فني", 8000, 18000, null),
        ("مشرف خدمة عملاء", 15000, 30000, null),
        ("ممثل خدمة عملاء", 7000, 14000, null),
    ];

    public static async Task RunAsync(IServiceProvider services, string? cvFolder)
    {
        var db = services.GetRequiredService<HRDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoData");

        // Never touch a database that already has people in it
        if (await db.Employees.IgnoreQueryFilters().AnyAsync())
        {
            log.LogError("The database already has employees. The demo data is only written to an empty database.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var random = new Random(2026);

        // ---------- Settings ----------
        await SetAsync(db, "InsuranceMinSalary", "2300");
        await SetAsync(db, "InsuranceMaxSalary", "14500");

        // ---------- Organization ----------
        var cairo = new Branch { Name = "المقر الرئيسي - القاهرة", Address = "مدينة نصر، القاهرة", Latitude = 30.056600, Longitude = 31.330100, AttendanceRadiusMeters = 200 };
        var alex = new Branch { Name = "فرع الإسكندرية", Address = "سموحة، الإسكندرية", Latitude = 31.215600, Longitude = 29.955300, AttendanceRadiusMeters = 200 };
        db.Branches.AddRange(cairo, alex);

        var departments = new Dictionary<string, Department>
        {
            ["gm"] = new() { Name = "الإدارة العامة", Branch = cairo },
            ["hr"] = new() { Name = "الموارد البشرية", Branch = cairo },
            ["acc"] = new() { Name = "الحسابات", Branch = cairo },
            ["sales"] = new() { Name = "المبيعات", Branch = cairo },
            ["it"] = new() { Name = "تكنولوجيا المعلومات", Branch = cairo },
            ["cs"] = new() { Name = "خدمة العملاء", Branch = alex },
        };
        db.Departments.AddRange(departments.Values);

        var jobs = Jobs.ToDictionary(j => j.Name, j => new JobTitle { Name = j.Name, MinSalary = j.Min, MaxSalary = j.Max, Requirements = j.Requirements });
        db.JobTitles.AddRange(jobs.Values);

        var morning = await db.Shifts.FirstAsync();
        var csShift = new Shift { Name = "وردية خدمة العملاء", StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(18, 0), GraceMinutes = 10 };
        db.Shifts.Add(csShift);

        db.PublicHolidays.AddRange(
            new PublicHoliday { Date = new DateOnly(2026, 7, 23), Name = "عيد ثورة 23 يوليو" },
            new PublicHoliday { Date = new DateOnly(2026, 10, 6), Name = "عيد القوات المسلحة" },
            new PublicHoliday { Date = new DateOnly(2027, 1, 7), Name = "عيد الميلاد المجيد" });
        await db.SaveChangesAsync();

        // ---------- Employees ----------
        var employees = new Dictionary<string, Employee>();
        var seq = 0;
        foreach (var p in People)
        {
            seq++;
            var employee = new Employee
            {
                Code = $"EMP-{seq:0000}",
                FullNameAr = p.NameAr,
                FullNameEn = p.NameEn,
                NationalId = NationalIdFor(p.Birth, p.Gender, seq),
                BirthDate = p.Birth,
                Gender = p.Gender,
                MaritalStatus = p.Birth.Year < 1995 ? MaritalStatus.Married : MaritalStatus.Single,
                Phone = $"010{seq:00}{random.Next(100000, 999999)}",
                Email = $"{p.NameEn.Split(' ')[0].ToLowerInvariant()}.{p.Key}@demo-company.com",
                Address = p.Dept == "cs" ? "الإسكندرية" : "القاهرة",
                HireDate = p.Hire,
                ContractType = p.Contract,
                ContractEndDate = p.ContractEnd,
                BasicSalary = p.Salary,
                InsuranceSalary = p.Salary,
                Department = departments[p.Dept],
                JobTitle = jobs[p.Job],
                Shift = p.Dept == "cs" ? csShift : morning,
                AllowRemoteAttendance = p.Remote,
                Status = p.Left is null ? EmployeeStatus.Active : EmployeeStatus.Terminated,
                TerminationDate = p.Left,
                TerminationReason = p.Left is null ? null : "استقالة"
            };
            employee.History.Add(new EmployeeHistory { ChangeType = "تعيين", NewValue = p.Job, EffectiveDate = p.Hire });
            employees[p.Key] = employee;
            db.Employees.Add(employee);
        }
        await db.SaveChangesAsync();

        foreach (var p in People.Where(p => p.Manager is not null))
            employees[p.Key].ManagerId = employees[p.Manager!].Id;
        departments["gm"].ManagerId = employees["gm"].Id;
        departments["hr"].ManagerId = employees["hrm"].Id;
        departments["acc"].ManagerId = employees["fm"].Id;
        departments["sales"].ManagerId = employees["sm"].Id;
        departments["it"].ManagerId = employees["itm"].Id;
        departments["cs"].ManagerId = employees["csm"].Id;

        // Salary components: transport for field and front-line staff, sales incentive, medical insurance for all
        var components = await db.SalaryComponents.ToListAsync();
        var transport = components.First(c => c.Name == "بدل انتقال");
        var incentive = components.First(c => c.Name == "حافز شهري");
        var medical = components.First(c => c.Name == "اشتراك تأمين طبي");
        foreach (var p in People.Where(p => p.Left is null))
        {
            var e = employees[p.Key];
            if (p.Dept is "sales" or "cs")
                db.EmployeeSalaryComponents.Add(new EmployeeSalaryComponent { Employee = e, SalaryComponent = transport, Value = 600 });
            if (p.Dept == "sales")
                db.EmployeeSalaryComponents.Add(new EmployeeSalaryComponent { Employee = e, SalaryComponent = incentive, Value = 10 });
            db.EmployeeSalaryComponents.Add(new EmployeeSalaryComponent { Employee = e, SalaryComponent = medical, Value = 250 });
        }
        await db.SaveChangesAsync();

        // ---------- Accounts ----------
        await AccountAsync(users, employees["hrm"], "hr@demo-company.com", Roles.HR);
        await AccountAsync(users, employees["sm"], "manager@demo-company.com", Roles.Manager);
        await AccountAsync(users, employees["acc1"], "employee@demo-company.com", Roles.Employee);
        await AccountAsync(users, employees["s1"], "sales@demo-company.com", Roles.Employee);
        await db.SaveChangesAsync();

        // ---------- Leaves ----------
        await services.GetRequiredService<ILeaveBalanceService>().GenerateAsync(2026);
        var leaveTypes = await db.LeaveTypes.ToDictionaryAsync(t => t.Name);
        var holidays = await db.PublicHolidays.Select(h => h.Date).ToListAsync();
        bool Working(DateOnly d) => d.DayOfWeek is not (DayOfWeek.Friday or DayOfWeek.Saturday) && !holidays.Contains(d);
        decimal WorkDays(DateOnly a, DateOnly b) { var n = 0; for (var d = a; d <= b; d = d.AddDays(1)) if (Working(d)) n++; return n; }

        var leavePlan = new (string Who, string Type, DateOnly Start, DateOnly End, RequestStatus Status, string Reason)[]
        {
            ("acc1", "سنوية", new(2026, 8, 9), new(2026, 8, 13), RequestStatus.Approved, "مصيف مع الأسرة"),
            ("dev1", "سنوية", new(2026, 8, 16), new(2026, 8, 20), RequestStatus.Approved, "سفر"),
            ("hr1", "عارضة", new(2026, 8, 24), new(2026, 8, 24), RequestStatus.Approved, "ظرف عائلي"),
            ("s3", "مرضية", new(2026, 9, 1), new(2026, 9, 3), RequestStatus.Approved, "دور برد"),
            ("cs1", "سنوية", new(2026, 9, 13), new(2026, 9, 17), RequestStatus.Approved, "زواج أخويا"),
            ("fm", "سنوية", new(2026, 9, 20), new(2026, 9, 24), RequestStatus.Approved, "إجازة سنوية"),
            ("acc4", "عارضة", new(2026, 9, 28), new(2026, 9, 28), RequestStatus.Approved, "مشوار حكومي"),
            ("dev2", "سنوية", new(2026, 10, 11), new(2026, 10, 15), RequestStatus.Approved, "سفر"),
            ("cs3", "عارضة", new(2026, 10, 19), new(2026, 10, 19), RequestStatus.Approved, "ظرف طارئ"),
            ("sup1", "سنوية", new(2026, 10, 25), new(2026, 10, 27), RequestStatus.Approved, "إجازة"),
            ("s2", "سنوية", new(2026, 10, 12), new(2026, 10, 15), RequestStatus.Rejected, "سفر"),
            // Waiting, for the approvals part of the demo
            ("s3", "سنوية", new(2026, 11, 22), new(2026, 11, 26), RequestStatus.Pending, "سفر للأهل"),
            ("s2", "عارضة", new(2026, 11, 9), new(2026, 11, 10), RequestStatus.ManagerApproved, "ظرف عائلي"),
            ("acc1", "سنوية", new(2026, 11, 16), new(2026, 11, 18), RequestStatus.Pending, "إجازة"),
        };
        foreach (var l in leavePlan)
        {
            var e = employees[l.Who];
            var days = WorkDays(l.Start, l.End);
            db.LeaveRequests.Add(new LeaveRequest
            {
                EmployeeId = e.Id, LeaveTypeId = leaveTypes[l.Type].Id, StartDate = l.Start, EndDate = l.End, Days = days,
                Reason = l.Reason, Status = l.Status,
                RejectionReason = l.Status == RequestStatus.Rejected ? "ضغط شغل آخر الربع، اختار ميعاد تاني" : null,
                ManagerActionAt = l.Status is RequestStatus.Approved or RequestStatus.ManagerApproved ? l.Start.AddDays(-5).ToDateTime(TimeOnly.MinValue) : null,
                HrActionAt = l.Status == RequestStatus.Approved ? l.Start.AddDays(-4).ToDateTime(TimeOnly.MinValue) : null
            });
            if (l.Status == RequestStatus.Approved)
            {
                var balance = await db.LeaveBalances.FirstOrDefaultAsync(b => b.EmployeeId == e.Id && b.LeaveTypeId == leaveTypes[l.Type].Id && b.Year == 2026);
                if (balance is not null)
                    balance.Used += days;
            }
        }

        // Missions (field visits) and permissions
        db.AttendanceRequests.AddRange(
            new AttendanceRequest { EmployeeId = employees["s1"].Id, Type = AttendanceRequestType.Mission, Date = new(2026, 9, 8), FromTime = new(9, 0), ToTime = new(17, 0), Reason = "زيارة عميل في العاشر من رمضان", Status = RequestStatus.Approved },
            new AttendanceRequest { EmployeeId = employees["s5"].Id, Type = AttendanceRequestType.Mission, Date = new(2026, 10, 20), FromTime = new(9, 0), ToTime = new(17, 0), Reason = "معرض في الإسكندرية", Status = RequestStatus.Approved },
            new AttendanceRequest { EmployeeId = employees["dev2"].Id, Type = AttendanceRequestType.Permission, Date = new(2026, 10, 7), FromTime = new(9, 0), ToTime = new(11, 0), Reason = "ميعاد دكتور", Status = RequestStatus.Approved },
            new AttendanceRequest { EmployeeId = employees["cs1"].Id, Type = AttendanceRequestType.Permission, Date = new(2026, 11, 5), FromTime = new(16, 0), ToTime = new(18, 0), Reason = "مشوار ضروري", Status = RequestStatus.Pending });
        await db.SaveChangesAsync();

        // ---------- Attendance ----------
        var approvedLeaves = await db.LeaveRequests.Where(l => l.Status == RequestStatus.Approved).Select(l => new { l.EmployeeId, l.StartDate, l.EndDate }).ToListAsync();
        var missions = await db.AttendanceRequests.Where(r => r.Type == AttendanceRequestType.Mission && r.Status == RequestStatus.Approved).Select(r => new { r.EmployeeId, r.Date }).ToListAsync();
        var permissions = await db.AttendanceRequests.Where(r => r.Type == AttendanceRequestType.Permission && r.Status == RequestStatus.Approved).ToListAsync();

        for (var day = From; day <= today; day = day.AddDays(1))
        {
            if (!Working(day))
                continue;

            foreach (var p in People)
            {
                var e = employees[p.Key];
                if (e.HireDate > day || (p.Left is DateOnly left && day > left))
                    continue;
                if (approvedLeaves.Any(l => l.EmployeeId == e.Id && l.StartDate <= day && l.EndDate >= day) || missions.Any(m => m.EmployeeId == e.Id && m.Date == day))
                    continue;

                var absentChance = p.Profile == "absent" ? 0.09 : 0.012;
                if (random.NextDouble() < absentChance)
                    continue;

                var shift = e.Shift!;
                var lateChance = p.Profile == "late" ? 0.38 : 0.06;
                var arrive = random.NextDouble() < lateChance ? random.Next(17, 65) : random.Next(-20, 12);
                var checkIn = shift.StartTime.AddMinutes(arrive);

                TimeOnly? checkOut = null;
                if (day < today)
                {
                    var leave = p.Profile == "overtime" && random.NextDouble() < 0.45 ? random.Next(45, 160)
                        : random.NextDouble() < 0.04 ? -random.Next(30, 75)
                        : random.Next(0, 22);
                    checkOut = shift.EndTime.AddMinutes(leave);
                }
                else if (random.NextDouble() < 0.12)
                {
                    continue; // not in yet this morning
                }

                checkIn = new TimeOnly(checkIn.Hour, checkIn.Minute);
                var dayPermissions = permissions.Where(r => r.EmployeeId == e.Id && r.Date == day).Select(r => new TimeRange(r.FromTime, r.ToTime)).ToList();
                var calc = AttendanceCalculator.Calculate(new ShiftTimes(shift.StartTime, shift.EndTime, shift.GraceMinutes), checkIn, checkOut, true, dayPermissions, 30);

                var record = new AttendanceRecord
                {
                    EmployeeId = e.Id, Date = day, CheckIn = checkIn, CheckOut = checkOut, Status = AttendanceStatus.Present,
                    LateMinutes = calc.LateMinutes, EarlyLeaveMinutes = calc.EarlyLeaveMinutes, OvertimeMinutes = calc.OvertimeMinutes,
                    Source = day.Month < 11 ? AttendanceSource.Device : AttendanceSource.Web
                };

                // From November people check in from the app: GPS near their branch, or out in the field for sales
                if (day.Month >= 11)
                {
                    var branch = p.Dept == "cs" ? alex : cairo;
                    var (lat, lng, meters) = p.Remote
                        ? Offset(branch, random.Next(2500, 14000), random)
                        : Offset(branch, random.Next(15, 120), random);
                    record.CheckInLatitude = lat;
                    record.CheckInLongitude = lng;
                    record.CheckInDistanceMeters = meters;
                    if (checkOut is not null)
                    {
                        var (lat2, lng2, meters2) = p.Remote ? Offset(branch, random.Next(2500, 14000), random) : Offset(branch, random.Next(15, 120), random);
                        record.CheckOutLatitude = lat2;
                        record.CheckOutLongitude = lng2;
                        record.CheckOutDistanceMeters = meters2;
                    }
                }
                db.AttendanceRecords.Add(record);
            }
        }
        await db.SaveChangesAsync();
        log.LogInformation("Attendance written: {Count} days", await db.AttendanceRecords.CountAsync());

        // Absences, leaves, missions and holidays for every past day
        await services.GetRequiredService<IAttendanceService>().CloseDaysAsync(From, today.AddDays(-1));

        // ---------- Payroll ----------
        var loans = services.GetRequiredService<ILoanService>();
        await loans.CreateAsync(new CreateLoanDto { EmployeeId = employees["cs2"].Id, Amount = 12000, InstallmentsCount = 12, StartMonth = new(2026, 9, 1) });
        await loans.CreateAsync(new CreateLoanDto { EmployeeId = employees["s2"].Id, Amount = 6000, InstallmentsCount = 6, StartMonth = new(2026, 10, 1) });
        await loans.CreateAsync(new CreateLoanDto { EmployeeId = employees["dev3"].Id, Amount = 9000, InstallmentsCount = 9, StartMonth = new(2026, 8, 1) });

        db.PayrollAdjustments.AddRange(
            new PayrollAdjustment { EmployeeId = employees["s1"].Id, Year = 2026, Month = 9, Type = AdjustmentType.Bonus, Amount = 2000, Reason = "تحقيق تارجت المبيعات" },
            new PayrollAdjustment { EmployeeId = employees["dev1"].Id, Year = 2026, Month = 10, Type = AdjustmentType.Bonus, Amount = 1500, Reason = "تسليم مشروع العميل في ميعاده" },
            new PayrollAdjustment { EmployeeId = employees["acc2"].Id, Year = 2026, Month = 10, Type = AdjustmentType.Penalty, Days = 1, Reason = "تأخير متكرر" },
            new PayrollAdjustment { EmployeeId = employees["cs2"].Id, Year = 2026, Month = 10, Type = AdjustmentType.Penalty, Days = 1, Reason = "غياب من غير إذن" });
        await db.SaveChangesAsync();

        var payroll = services.GetRequiredService<IPayrollService>();
        var admin = await users.Users.FirstAsync(u => u.Email!.StartsWith("admin"));
        foreach (var month in new[] { 8, 9, 10 })
        {
            var run = await payroll.CalculateAsync(2026, month);
            if (!run.Succeeded)
                log.LogWarning("Payroll {Month}: {Error}", month, run.Error);
            else if (month < 10)
                await payroll.ApproveAsync(run.Value, admin.Id); // October stays a draft to approve live
        }

        // ---------- Recruitment ----------
        if (cvFolder is not null && Directory.Exists(cvFolder))
        {
            var candidates = services.GetRequiredService<ICandidateService>();
            var files = Directory.GetFiles(cvFolder, "*.pdf").OrderBy(f => f).ToList();
            var uploads = files.Select(f => new FileUpload(File.OpenRead(f), Path.GetFileName(f), new FileInfo(f).Length)).ToList();
            await candidates.UploadAsync(jobs["مدير مالي"].Id, uploads);
            uploads.ForEach(u => u.Content.Dispose());

            if (candidates.IsAvailable)
            {
                foreach (var id in await db.Candidates.Select(c => c.Id).ToListAsync())
                {
                    var result = await candidates.AnalyzeAsync(id);
                    log.LogInformation("Candidate {Id}: {Result}", id, result.Succeeded ? "analysed" : result.Error);
                }
            }
        }

        log.LogInformation("Demo company ready: {Employees} employees", await db.Employees.CountAsync());
    }

    // 14 digits: century, birth date, governorate, sequence, gender digit (odd = male), check digit
    private static string NationalIdFor(DateOnly birth, Gender gender, int seq)
    {
        var genderDigit = gender == Gender.Male ? (seq % 5) * 2 + 1 : (seq % 5) * 2;
        return $"{(birth.Year >= 2000 ? 3 : 2)}{birth:yyMMdd}{(seq % 2 == 0 ? "01" : "02")}{seq % 1000:000}{genderDigit}{seq % 10}";
    }

    // A point about `meters` away from the branch in a random direction
    private static (double Lat, double Lng, int Meters) Offset(Branch branch, int meters, Random random)
    {
        var angle = random.NextDouble() * 2 * Math.PI;
        var lat = branch.Latitude!.Value + meters * Math.Cos(angle) / 111_195.0;
        var lng = branch.Longitude!.Value + meters * Math.Sin(angle) / (111_195.0 * Math.Cos(branch.Latitude.Value * Math.PI / 180));
        var actual = (int)Math.Round(GeoCalculator.DistanceMeters(lat, lng, branch.Latitude.Value, branch.Longitude.Value));
        return (Math.Round(lat, 6), Math.Round(lng, 6), actual);
    }

    private static async Task SetAsync(HRDbContext db, string key, string value)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting is not null)
            setting.Value = value;
    }

    private static async Task AccountAsync(UserManager<ApplicationUser> users, Employee employee, string email, string role)
    {
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = employee.FullNameAr };
        var result = await users.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        employee.UserId = user.Id;
    }
}
