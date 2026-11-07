namespace HRSystem.Domain.Enums;

public enum Gender : byte
{
    Male = 1,
    Female = 2
}

public enum MaritalStatus : byte
{
    Single = 1,
    Married = 2,
    Divorced = 3,
    Widowed = 4
}

public enum ContractType : byte
{
    Permanent = 1,
    Temporary = 2,
    PartTime = 3
}

public enum CandidateStatus : byte
{
    New = 1,
    Interview = 2,
    Accepted = 3,
    Rejected = 4,
    Hired = 5
}

public enum EmployeeStatus : byte
{
    Active = 1,
    Suspended = 2,
    Terminated = 3
}

// Top of the org tree (إدارة) or a unit under it (قسم)
public enum DepartmentType : byte
{
    Administration = 1,
    Section = 2
}

public enum Religion : byte
{
    Muslim = 1,
    Christian = 2,
    Other = 3
}

public enum MilitaryStatus : byte
{
    Completed = 1,
    Exempted = 2,
    Postponed = 3,
    NotApplicable = 4
}

public enum FamilyRelation : byte
{
    Spouse = 1,
    Son = 2,
    Daughter = 3,
    Father = 4,
    Mother = 5,
    Other = 6
}

public enum QualificationType : byte
{
    Degree = 1,
    Course = 2,
    Certificate = 3
}
