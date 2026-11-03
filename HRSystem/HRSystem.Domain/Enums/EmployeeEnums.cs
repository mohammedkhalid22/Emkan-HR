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
