namespace InvoicesErp.Models;

/// <summary>
/// سجل تغييرات قيمة الدفعة على العقد.
/// يسجل كل إنشاء أو تعديل للقيمة مع المستخدم والتاريخ والقيمة القديمة/الجديدة.
/// </summary>
public class ContractPaymentValueLog
{
    public string Id { get; set; } = "";
    public string ContractId { get; set; } = "";
    public string ProjectId { get; set; } = "";

    public decimal? OldValue { get; set; }
    public decimal? NewValue { get; set; }

    public string? ChangedByUserId { get; set; }
    public string? ChangedByUsername { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>ملاحظة اختيارية (مثلاً: إنشاء / تعديل يدوي)</summary>
    public string? Note { get; set; }

    public Contract Contract { get; set; } = null!;
}
