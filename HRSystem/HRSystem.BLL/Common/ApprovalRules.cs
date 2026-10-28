using HRSystem.BLL.DTOs;
using HRSystem.Domain.Enums;

namespace HRSystem.BLL.Common;

public enum ApprovalStep
{
    Manager,
    Hr
}

// The manager → HR approval flow shared by leave requests and attendance requests (permissions / missions)
public static class ApprovalRules
{
    // Which step (if any) this person can take on a request
    public static ApprovalStep? StepFor(RequestStatus status, int employeeId, int? employeeManagerId, ApproverContext approver)
    {
        if (approver.EmployeeId == employeeId)
            return null; // nobody decides on their own request

        if (status == RequestStatus.Pending && approver.IsManager && approver.EmployeeId != null && employeeManagerId == approver.EmployeeId)
            return ApprovalStep.Manager;

        // Someone without a manager skips the first step
        if (approver.IsHr && (status == RequestStatus.ManagerApproved || (status == RequestStatus.Pending && employeeManagerId == null)))
            return ApprovalStep.Hr;

        return null;
    }

    // Owner or HR; waiting requests any time, approved ones only before they start
    public static bool CanCancel(RequestStatus status, bool hasStarted, int employeeId, ApproverContext actor)
    {
        var ownerOrHr = actor.EmployeeId == employeeId || actor.IsHr;
        return ownerOrHr && (status is RequestStatus.Pending or RequestStatus.ManagerApproved
            || (status == RequestStatus.Approved && !hasStarted));
    }
}
