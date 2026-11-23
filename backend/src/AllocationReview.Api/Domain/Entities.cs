namespace AllocationReview.Api.Domain;

/// <summary>A saved allocation run (a proposed dispatch from the central warehouse).</summary>
public class AllocationRun
{
    public int Id { get; set; }

    /// <summary>Business identifier shown to users, e.g. "R-102". Unique.</summary>
    public required string RunCode { get; set; }

    public DateOnly RunDate { get; set; }

    public List<AllocationGap> Gaps { get; set; } = [];
}

/// <summary>
/// A replenishment gap inside a run: one store + brand + product type with an original shortfall.
/// GapCode is unique within its run only (G-01 can exist in R-101 and R-102).
/// </summary>
public class AllocationGap
{
    public int Id { get; set; }

    public int RunId { get; set; }
    public AllocationRun? Run { get; set; }

    public required string GapCode { get; set; }
    public required string Store { get; set; }
    public required string Brand { get; set; }
    public required string ProductType { get; set; }

    /// <summary>Stored as decimal so a fractional source value is kept; rounded up when reviewed.</summary>
    public decimal Shortfall { get; set; }

    /// <summary>Zero or more warehouse batch rows allocated to this gap.</summary>
    public List<BatchAllocation> Allocations { get; set; } = [];
}

/// <summary>One warehouse batch row allocated to a gap.</summary>
public class BatchAllocation
{
    public int Id { get; set; }

    public int GapId { get; set; }
    public AllocationGap? Gap { get; set; }

    public required string BatchCode { get; set; }
    public int Quantity { get; set; }
}
