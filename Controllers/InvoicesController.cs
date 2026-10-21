using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvoicesErp.Data;
using InvoicesErp.Models;
using InvoicesErp.DTOs;
using System.Globalization;
using System.Text.Json;
using InvoicesErp.Auth;
using InvoicesErp.Common;

namespace InvoicesErp.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.View)]
    public async Task<IActionResult> Get(
      [FromQuery] string? contractId,
    [FromQuery] string? status,
    [FromQuery] string? paymentStatus,
    [FromQuery] DateOnly? from,
    [FromQuery] DateOnly? to,
    [FromQuery] int? page,
    [FromQuery] int? pageSize,
    [FromQuery] string? search,
    [FromQuery] string? sortBy,
    [FromQuery] string? sortDirection,
    CancellationToken ct)
    {
        // List endpoint: avoid heavy Includes unless full dump requested (no page).
        var q = db.Invoices.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(contractId)) q = q.Where(x => x.ContractId == contractId);
    if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
    if (!string.IsNullOrWhiteSpace(paymentStatus)) q = q.Where(x => x.PaymentStatus == paymentStatus);
    if (from.HasValue) q = q.Where(x => x.Date >= from.Value);
    if (to.HasValue) q = q.Where(x => x.Date <= to.Value);
       if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.Trim();
        q = q.Where(x => x.Number.Contains(s) || (x.Status != null && x.Status.Contains(s)));
    }

    var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

    q = (sortBy?.ToLowerInvariant()) switch
    {
        "number" => desc
            ? q.OrderByDescending(x => x.Number)
            : q.OrderBy(x => x.Number),

        "status" => desc
            ? q.OrderByDescending(x => x.Status)
            : q.OrderBy(x => x.Status),

        "paidamount" => desc
            ? q.OrderByDescending(x => x.PaidAmount)
            : q.OrderBy(x => x.PaidAmount),

        "date" => desc
            ? q.OrderByDescending(x => x.Date)
            : q.OrderBy(x => x.Date),

        _ => q
};

        if (page is null)
        {
            // Backward compatible full list with details (bootstrap-style consumers)
            var full = await q.Include(x => x.Items).Include(x => x.Deductions).ToListAsync(ct);
            return Ok(full.Select(Map).ToList());
        }

        var pageNum = page.Value < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1 ? 25 : (pageSize > 100 ? 100 : pageSize.Value);
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((pageNum - 1) * size).Take(size)
            .Include(x => x.Items).Include(x => x.Deductions)
            .ToListAsync(ct);
        var items = rows.Select(Map).ToList();
        return Ok(PagedResult<InvoiceDto>.Create(items, pageNum, size, total));
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.View)]
    public async Task<ActionResult<InvoiceDto>> GetById(string id, CancellationToken ct)
    {
        var x = await db.Invoices.AsNoTracking().Include(x => x.Items).Include(x => x.Deductions).FirstOrDefaultAsync(x => x.Id == id, ct);
        return x is null ? NotFound() : Ok(Map(x));
    }

    [HttpPost]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.Create)]
    public async Task<ActionResult<InvoiceDto>> Post(InvoiceDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ContractId))
            return BadRequest(new { success = false, message = "العقد مطلوب" });
        if (!dto.Date.HasValue)
            return BadRequest(new { success = false, message = "تاريخ المستخلص مطلوب" });
        if (string.IsNullOrWhiteSpace(dto.Number))
            return BadRequest(new { success = false, message = "رقم المستخلص مطلوب" });
        if (!await db.Contracts.AnyAsync(x => x.Id == dto.ContractId, ct))
            return BadRequest(new { success = false, message = "العقد غير موجود" });
        var amountErr = ValidateInvoiceAmounts(dto);
        if (amountErr is not null)
            return BadRequest(new { success = false, message = amountErr });
        var supplyErr = await ValidateMaterialsDeductions(dto, null, ct);
        if (supplyErr is not null)
            return BadRequest(new { success = false, message = supplyErr });

        var x = FromDto(dto);
        db.Invoices.Add(x);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict(new { success = false, message = "حدث خطأ أثناء حفظ المستخلص. تحقق من البيانات المرتبطة أو من وجود رقم مستخلص مكرر." }); }
        return CreatedAtAction(nameof(GetById), new { id = x.Id }, Map(x));
    }

    [HttpPut("{id}")]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.Edit)]
    public async Task<IActionResult> Put(string id, InvoiceDto dto, CancellationToken ct)
    {
        var x = await db.Invoices.Include(x => x.Items).Include(x => x.Deductions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (x is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.ContractId))
            return BadRequest(new { success = false, message = "العقد مطلوب" });
        if (!dto.Date.HasValue)
            return BadRequest(new { success = false, message = "تاريخ المستخلص مطلوب" });
        if (string.IsNullOrWhiteSpace(dto.Number))
            return BadRequest(new { success = false, message = "رقم المستخلص مطلوب" });

        var amountErrPut = ValidateInvoiceAmounts(dto);
        if (amountErrPut is not null)
            return BadRequest(new { success = false, message = amountErrPut });
        var supplyErrPut = await ValidateMaterialsDeductions(dto, id, ct);
        if (supplyErrPut is not null)
            return BadRequest(new { success = false, message = supplyErrPut });

        x.ContractId = dto.ContractId;
        x.Number = dto.Number.Trim();
        x.Date = dto.Date.Value;
        x.DueDate = dto.DueDate;
        x.Status = dto.Status;
        x.PaymentStatus = dto.PaymentStatus;
        x.PaidAmount = dto.PaidAmount;
        x.PaymentDate = dto.PaymentDate;
        x.WorkVolume = dto.WorkVolume;
        x.VariationOrders = dto.VariationOrders;
        x.Materials = dto.Materials;
        x.Claims = dto.Claims;
        x.Vat = dto.Vat;

        db.InvoiceItems.RemoveRange(x.Items);
        db.InvoiceDeductions.RemoveRange(x.Deductions);

        x.Items = (dto.Items ?? []).Select(i => new InvoiceItem
        {
            Id = string.IsNullOrWhiteSpace(i.Id) ? Guid.NewGuid().ToString("N") : i.Id,
            Description = i.Description ?? string.Empty,
            Qty = i.Qty, Rate = i.Rate, Total = i.Total, CurrentTotal = i.CurrentTotal
        }).ToList();

        x.Deductions = (dto.Deductions ?? []).Select(d => new InvoiceDeduction
        {
            Id = string.IsNullOrWhiteSpace(d.Id) ? Guid.NewGuid().ToString("N") : d.Id,
            Description = d.Description ?? string.Empty,
            CalcType = d.CalcType, Val = d.Val, Amount = d.Amount, IsRefundable = d.IsRefundable,
            CalcFromKeysJson = d.CalcFromKeys == null ? null : JsonSerializer.Serialize(d.CalcFromKeys)
        }).ToList();

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.Delete)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var x = await db.Invoices.FindAsync([id], ct);
        if (x is null) return NotFound();
        db.Remove(x);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id}/approve")]
    [RequirePermission(PermissionModules.Invoices, PermissionActions.Approve)]
    public async Task<IActionResult> Approve(string id, CancellationToken ct)
    {
        var x = await db.Invoices.FindAsync([id], ct);
        if (x is null) return NotFound();
        x.Status = "Approved";
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static InvoiceDto Map(Invoice x) => new InvoiceDto(
        x.Id, x.ContractId, x.Number, x.Date, x.DueDate, x.Status, x.PaymentStatus,
        x.PaidAmount, x.PaymentDate, x.WorkVolume, x.VariationOrders, x.Materials,
        x.Claims, x.Vat,
        (x.Items ?? []).Select(i => new InvoiceItemDto(i.Id, i.Description, i.Qty, i.Rate, i.Total, i.CurrentTotal)).ToList(),
        (x.Deductions ?? []).Select(d => new InvoiceDeductionDto(d.Id, d.Description, d.CalcType, d.Val, d.Amount, d.IsRefundable,
            string.IsNullOrWhiteSpace(d.CalcFromKeysJson) ? null : JsonSerializer.Deserialize<string[]>(d.CalcFromKeysJson))).ToList());

    private static Invoice FromDto(InvoiceDto d)
    {
        if (!d.Date.HasValue) throw new ArgumentException("Invoice date is required.");
        return new Invoice
        {
            Id = string.IsNullOrWhiteSpace(d.Id) ? Guid.NewGuid().ToString("N") : d.Id,
            ContractId = d.ContractId!, Number = d.Number!.Trim(), Date = d.Date.Value,
            DueDate = d.DueDate, Status = d.Status, PaymentStatus = d.PaymentStatus,
            PaidAmount = d.PaidAmount, PaymentDate = d.PaymentDate,
            WorkVolume = d.WorkVolume, VariationOrders = d.VariationOrders,
            Materials = d.Materials, Claims = d.Claims, Vat = d.Vat,
            Items = (d.Items ?? []).Select(i => new InvoiceItem
            {
                Id = string.IsNullOrWhiteSpace(i.Id) ? Guid.NewGuid().ToString("N") : i.Id,
                Description = i.Description ?? string.Empty, Qty = i.Qty, Rate = i.Rate,
                Total = i.Total, CurrentTotal = i.CurrentTotal
            }).ToList(),
            Deductions = (d.Deductions ?? []).Select(dd => new InvoiceDeduction
            {
                Id = string.IsNullOrWhiteSpace(dd.Id) ? Guid.NewGuid().ToString("N") : dd.Id,
                Description = dd.Description ?? string.Empty, CalcType = dd.CalcType,
                Val = dd.Val, Amount = dd.Amount, IsRefundable = dd.IsRefundable,
                CalcFromKeysJson = dd.CalcFromKeys == null ? null : JsonSerializer.Serialize(dd.CalcFromKeys)
            }).ToList()
        };
    }

    private const string MaterialsDeductionDescription = "خصم خامات";

    private async Task<string?> ValidateMaterialsDeductions(
        InvoiceDto dto,
        string? currentInvoiceId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ContractId)) return null;

        var projectId = await db.Contracts.AsNoTracking()
            .Where(c => c.Id == dto.ContractId)
            .Select(c => c.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(projectId)) return null;

        var requested = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var deduction in dto.Deductions ?? [])
        {
            if (!string.Equals(deduction.Description?.Trim(), MaterialsDeductionDescription, StringComparison.Ordinal))
                continue;
            foreach (var key in deduction.CalcFromKeys ?? [])
            {
                if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("supply:", StringComparison.Ordinal))
                    continue;
                if (!TryParseSupplyDeductionKey(key, out var supplyId, out var qty))
                    return "توجد كمية غير صحيحة في توريدات خصم الخامات.";
                requested[supplyId] = requested.GetValueOrDefault(supplyId) + qty;
            }
        }

        if (requested.Count == 0) return null;

        var supplyIds = requested.Keys.ToList();
        var supplyItems = await db.ProjectSupplyItems.AsNoTracking()
            .Where(s => s.ProjectId == projectId && supplyIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id, s.Description, s.Qty,
                // حالة التوريدة في سجل التوريدات (null إذا لم تكن مرتبطة بسجل التوريدات)
                SupplyStatus = db.SupplyCatalogAssignments.Where(a => a.ProjectSupplyItemId == s.Id)
                    .Select(a => a.SupplyCatalogItem.Status).FirstOrDefault()
            })
            .ToListAsync(ct);
        if (supplyItems.Count != requested.Count)
            return "إحدى التوريدات المختارة غير مرتبطة بالمشروع.";

        foreach (var item in supplyItems)
        {
            if (IsSupplyDisabled(item.SupplyStatus))
                return $"التوريدة \"{item.Description}\" غير مفعّلة ولا يمكن إضافتها إلى خصم الخامات.";
        }

        var usageQuery =
            from invoice in db.Invoices.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on invoice.ContractId equals contract.Id
            join deduction in db.InvoiceDeductions.AsNoTracking() on invoice.Id equals deduction.InvoiceId
            where contract.ProjectId == projectId
                && deduction.Description == MaterialsDeductionDescription
                && deduction.CalcFromKeysJson != null
                && deduction.CalcFromKeysJson != ""
            select new { invoice.Id, deduction.CalcFromKeysJson };
        if (!string.IsNullOrWhiteSpace(currentInvoiceId))
            usageQuery = usageQuery.Where(row => row.Id != currentInvoiceId);

        var usageRows = await usageQuery.ToListAsync(ct);
        var used = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var row in usageRows)
        {
            string[]? keys;
            try
            {
                keys = JsonSerializer.Deserialize<string[]>(row.CalcFromKeysJson!);
            }
            catch (JsonException)
            {
                continue;
            }

            foreach (var key in keys ?? [])
            {
                if (!TryParseSupplyDeductionKey(key, out var supplyId, out var qty)) continue;
                if (!requested.ContainsKey(supplyId)) continue;
                used[supplyId] = used.GetValueOrDefault(supplyId) + qty;
            }
        }

        foreach (var item in supplyItems)
        {
            var available = item.Qty - used.GetValueOrDefault(item.Id);
            if (available < 0) available = 0;
            if (requested[item.Id] <= available) continue;
            var allowed = available.ToString("0.##", CultureInfo.InvariantCulture);
            return $"الكمية المسموحة من التوريدة \"{item.Description}\" هي {allowed} فقط.";
        }

        return null;
    }

    private static bool IsSupplyDisabled(string? status)
    {
        // التوريد مرتبطة بسجل التوريدات: الحالة إما "Active" أو "Inactive".
        // أي حالة أخرى محفوظة تُعامل كغير مفعّلة حتى لا يتم تمرير بيانات غير معروفة.
        if (string.IsNullOrWhiteSpace(status)) return false;
        return !string.Equals(status.Trim(), "Active", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseSupplyDeductionKey(
        string? value,
        out string supplyId,
        out decimal qty)
    {
        supplyId = "";
        qty = 0m;
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith("supply:", StringComparison.Ordinal))
            return false;

        var quantitySeparator = value.LastIndexOf(':');
        if (quantitySeparator <= "supply:".Length) return false;

        var encodedId = value["supply:".Length..quantitySeparator];
        var quantityText = value[(quantitySeparator + 1)..];
        if (string.IsNullOrWhiteSpace(encodedId)) return false;

        try { supplyId = Uri.UnescapeDataString(encodedId); }
        catch { supplyId = encodedId; }

        return !string.IsNullOrWhiteSpace(supplyId)
            && decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out qty)
            && qty > 0m;
    }

   private static string? ValidateInvoiceAmounts(InvoiceDto dto)
{
    // Invoice components can be positive, zero, or negative.
    // No validation is applied to:
    // WorkVolume, VariationOrders, Materials, Claims, Vat.

    // if (dto.PaidAmount < 0)
    //     return "المبلغ المدفوع لا يمكن أن يكون سالباً";

    // foreach (var d in dto.Deductions ?? [])
    // {
    //     if (d.Val < 0 || d.Amount < 0)
    //         return "قيم الاستقطاعات لا يمكن أن تكون سالبة";
    // }

    // foreach (var i in dto.Items ?? [])
    // {
    //     if (i.Qty < 0 || i.Rate < 0 || i.Total < 0)
    //         return "قيم بنود المستخلص لا يمكن أن تكون سالبة";
    // }

    return null;
}
}
