namespace CRM.Domain.Enums;

public enum CallStatus
{
    Queued = 1,
    Ringing = 2,
    InProgress = 3,
    Completed = 4,
    Busy = 5,
    Failed = 6,
    NoAnswer = 7,
    Canceled = 8,
}
