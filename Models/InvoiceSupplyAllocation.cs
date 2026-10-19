namespace InvoicesErp.Models;

public class InvoiceSupplyAllocation
{
    public string Id { get; set; } = "";
    public string InvoiceId { get; set; } = "";
    public string ContractSupplyAllocationId { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Invoice Invoice { get; set; } = null!;
    public ContractSupplyAllocation ContractSupplyAllocation { get; set; } = null!;
}
