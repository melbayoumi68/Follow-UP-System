namespace InvoicesErp.Models;

/// <summary>
/// بند توريد خامات مربوط بعقد. مستقل عن CostControl والـ Invoices.
/// كل بند يُسجل مرة واحدة وقابل للتعديل. Total يُحسب في الباك إند.
/// </summary>
public class ContractSupplyItem
{
    public string Id { get; set; } = "";
    public string ContractId { get; set; } = "";
    public string ProjectId { get; set; } = "";

    /// <summary>وصف حر للخامة (رمل، أسمنت، حديد...)</summary>
    public string Description { get; set; } = "";

    /// <summary>الوحدة (متر³، شيكارة، طن، عدد...)</summary>
    public string Unit { get; set; } = "";

    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Qty * UnitPrice — يُحسب في الباك إند فقط</summary>
    public decimal Total { get; set; }

    public string SupplierName { get; set; } = "";
    public DateOnly SupplyDate { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = null!;
}
