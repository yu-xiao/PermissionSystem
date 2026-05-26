namespace PermissionSystem.Domain.Enums;

public enum WorkflowDefinitionStatus
{
    Draft = 0,
    Published = 1,
    Disabled = 2,
    Archived = 3
}

public enum WorkflowNodeType
{
    Start = 0,
    Approver = 1,
    Cc = 2,
    Condition = 3,
    End = 4
}

public enum WorkflowApproverType
{
    Users = 0,
    Roles = 1,
    DepartmentManager = 2,
    Positions = 3,
    Initiator = 4,
    InitiatorDirectLeader = 5,
    InitiatorDepartmentManager = 6,
    FormFieldUser = 7
}

public enum WorkflowApprovalMode
{
    Single = 0,
    Countersign = 1,
    OrSign = 2,
    Sequential = 3
}

public enum WorkflowInstanceStatus
{
    Running = 0,
    Approved = 1,
    Rejected = 2,
    Withdrawn = 3,
    Canceled = 4,
    Exception = 5
}

public enum WorkflowTaskStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Transferred = 3,
    Added = 4,
    Canceled = 5,
    Expired = 6
}

public enum WorkflowActionType
{
    Start = 0,
    Approve = 1,
    Reject = 2,
    Withdraw = 3,
    Transfer = 4,
    AddSign = 5,
    Cc = 6,
    Complete = 7,
    System = 8
}

public enum WorkflowConditionOperator
{
    Equals = 0,
    NotEquals = 1,
    GreaterThan = 2,
    GreaterThanOrEqual = 3,
    LessThan = 4,
    LessThanOrEqual = 5,
    Contains = 6,
    NotContains = 7,
    In = 8,
    NotIn = 9,
    Between = 10,
    IsEmpty = 11,
    IsNotEmpty = 12
}
