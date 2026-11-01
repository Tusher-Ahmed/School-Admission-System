namespace SchoolAdmission.Api.Data;

/// <summary>One admission class, i.e. one shift ("Class 6 - Morning Shift").</summary>
public class SchoolClass
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum HoldStatus
{
    Held,
    Confirmed,
    Expired,
    Cancelled,
}

/// <summary>One application: a seat held for a student until it is confirmed, cancelled or expires.</summary>
public class Hold
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string StudentId { get; set; } = "";
    public HoldStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
