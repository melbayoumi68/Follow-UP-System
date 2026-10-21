using InvoicesErp.Auth;
using InvoicesErp.Data;
using InvoicesErp.DTOs;
using InvoicesErp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoicesErp.Controllers;

[ApiController]
[Route("api/supply-catalog")]
public class SupplyCatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionModules.Projects, PermissionActions.View)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await db.SupplyCatalogItems.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.Type, x.Unit, x.Status, x.CreatedAt }).ToListAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var links = await db.SupplyCatalogAssignments.AsNoTracking().Where(x => ids.Contains(x.SupplyCatalogItemId))
            .Select(x => new { x.SupplyCatalogItemId, ProjectName = x.Project.Name }).ToListAsync(ct);
        return Ok(items.Select(x => new SupplyCatalogItemDto(x.Id, x.Type, x.Unit, x.Status, x.CreatedAt,
            links.Where(a => a.SupplyCatalogItemId == x.Id).Select(a => a.ProjectName).Distinct().ToArray())));
    }

    [HttpPost]
    [RequirePermission(PermissionModules.Projects, PermissionActions.Create)]
    public async Task<IActionResult> Create(SupplyCatalogWriteDto dto, CancellationToken ct)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Type) || string.IsNullOrWhiteSpace(dto.Unit))
            return BadRequest(new { message = "أدخل نوع التوريدة ووحدة القياس" });
        var now = DateTime.UtcNow;
        var item = new SupplyCatalogItem
        {
            Id = Guid.NewGuid().ToString("N"), Type = dto.Type.Trim(), Material = "", Unit = dto.Unit.Trim(),
            Qty = 0, UnitPrice = 0, Total = 0, SupplyDate = DateOnly.FromDateTime(now),
            Status = "Active", CreatedAt = now
        };
        db.SupplyCatalogItems.Add(item);
        await db.SaveChangesAsync(ct);
        return Ok(new SupplyCatalogItemDto(item.Id, item.Type, item.Unit, item.Status, item.CreatedAt, []));
    }

    [HttpPatch("{id}/status")]
    [RequirePermission(PermissionModules.Projects, PermissionActions.Edit)]
    public async Task<IActionResult> UpdateStatus(string id, SupplyCatalogStatusUpdateDto dto, CancellationToken ct)
    {
        var status = dto?.Status?.Trim();
        if (status is not ("Active" or "Inactive"))
            return BadRequest(new { message = "حالة التوريدة يجب أن تكون مفعّلة أو غير مفعّلة" });

        var item = await db.SupplyCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound(new { message = "التوريدة غير موجودة" });

        item.Status = status;
        await db.SaveChangesAsync(ct);
        return Ok(new SupplyCatalogItemDto(item.Id, item.Type, item.Unit, item.Status, item.CreatedAt, []));
    }

    [HttpGet("{id}/assignments")]
    [RequirePermission(PermissionModules.Projects, PermissionActions.View)]
    public async Task<IActionResult> ListAssignments(string id, CancellationToken ct)
    {
        if (!await db.SupplyCatalogItems.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var rows = await db.SupplyCatalogAssignments.AsNoTracking().Where(x => x.SupplyCatalogItemId == id)
            .OrderBy(x => x.Project.Name)
            .Select(x => new SupplyCatalogAssignmentDto(x.Id, x.SupplyCatalogItemId, x.ProjectId, x.Project.Name, x.ProjectSupplyItemId, x.Qty, x.ProjectSupplyItem.SupplyDate, x.CreatedAt))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("{id}/assignments")]
    [RequirePermission(PermissionModules.Projects, PermissionActions.Create)]
    public async Task<IActionResult> Assign(string id, List<SupplyCatalogAssignmentWriteDto> assignments, CancellationToken ct)
    {
        if (assignments is null || assignments.Count == 0) return BadRequest(new { message = "اختر مشروعًا واحدًا على الأقل وحدد الكمية" });
        if (assignments.Any(x => string.IsNullOrWhiteSpace(x.ProjectId) || x.Qty <= 0 || x.UnitPrice < 0 || x.SupplyDate == default)) return BadRequest(new { message = "كل مشروع يحتاج كمية أكبر من صفر وسعر وحدة وتاريخ توريد صحيح" });
        if (assignments.Select(x => x.ProjectId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != assignments.Count)
            return BadRequest(new { message = "المشروع مضاف أكثر من مرة" });

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var catalogItem = await db.SupplyCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (catalogItem is null) return NotFound(new { message = "التوريدة غير موجودة" });
        var projectIds = assignments.Select(x => x.ProjectId).ToArray();
        var projects = await db.Projects.Where(x => projectIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (projects.Count != projectIds.Length) return BadRequest(new { message = "أحد المشروعات المختارة غير موجود" });

        var now = DateTime.UtcNow;
        var result = new List<SupplyCatalogAssignmentDto>();
        foreach (var allocation in assignments)
        {
            var projectSupply = new ProjectSupplyItem
            {
                Id = Guid.NewGuid().ToString("N"), ProjectId = allocation.ProjectId,
                Description = catalogItem.Type, Unit = catalogItem.Unit,
                Qty = allocation.Qty, UnitPrice = allocation.UnitPrice, Total = Math.Round(allocation.Qty * allocation.UnitPrice, 2),
                SupplyDate = allocation.SupplyDate, Note = null, CreatedAt = now, UpdatedAt = now
            };
            var row = new SupplyCatalogAssignment
            {
                Id = Guid.NewGuid().ToString("N"), SupplyCatalogItemId = catalogItem.Id,
                ProjectId = allocation.ProjectId, ProjectSupplyItemId = projectSupply.Id, Qty = allocation.Qty, CreatedAt = now
            };
            db.ProjectSupplyItems.Add(projectSupply);
            db.SupplyCatalogAssignments.Add(row);
            result.Add(new SupplyCatalogAssignmentDto(row.Id, row.SupplyCatalogItemId, row.ProjectId, projects[row.ProjectId].Name, row.ProjectSupplyItemId, row.Qty, projectSupply.SupplyDate, row.CreatedAt));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(result);
    }
}
