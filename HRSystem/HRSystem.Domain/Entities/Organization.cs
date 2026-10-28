using HRSystem.Domain.Common;

namespace HRSystem.Domain.Entities;

public class Branch : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Address { get; set; }

    public ICollection<Department> Departments { get; set; } = new List<Department>();
}

public class Department : BaseEntity
{
    public string Name { get; set; } = null!;

    public int? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }
    public ICollection<Department> SubDepartments { get; set; } = new List<Department>();

    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class JobTitle : BaseEntity
{
    public string Name { get; set; } = null!;
    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
