using InvoicesErp.Auth;
using InvoicesErp.Common;
using InvoicesErp.Data;
using InvoicesErp.DTOs;
using InvoicesErp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoicesErp.Controllers;

[ApiController]
[Route("api/projects/{projectId}/supply-items")]
public class ProjectSupplyItemsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionModules.Projects, PermissionActions.View)]
    public async Task<IActionResult> List(string projectId, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct)) return NotFound();
        var rows = await db.ProjectSupplyItems.AsNoTracking().Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.SupplyDate).Select(s => new ProjectSupplyItemDto(
                s.Id, s.ProjectId, s.Description, s.Unit, s.Qty, s.UnitPrice, s.Total,
                db.ContractSupplyAllocations.Where(a => a.ProjectSupplyItemId == s.Id).Sum(a => (decimal?)a.Qty) ?? 0m,
                s.Qty - (db.ContractSupplyAllocations.Where(a => a.ProjectSupplyItemId == s.Id).Sum(a => (decimal?)a.Qty) ?? 0m),
                s.SupplyDate, s.Note, s.CreatedAt, s.UpdatedAt)).ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost]
    [RequirePermission(PermissionModules.Projects, PermissionActions.Create)]
    public async Task<ActionResult<ProjectSupplyItemDto>> Create(string projectId, ProjectSupplyWriteDto dto, CancellationToken ct)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct)) return NotFound(new { message = "المشروع غير موجود" });
        if (string.IsNullOrWhiteSpace(dto.Description) || string.IsNullOrWhiteSpace(dto.Unit) || dto.Qty <= 0 || dto.UnitPrice < 0)
            return BadRequest(new { message = "أدخل وصفاً ووحدة وكمية صحيحة وسعر وحدة لا يقل عن صفر" });
        var now = DateTime.UtcNow;
        var item = new ProjectSupplyItem { Id = Guid.NewGuid().ToString("N"), ProjectId = projectId, Description = dto.Description.Trim(), Unit = dto.Unit.Trim(), Qty = dto.Qty, UnitPrice = dto.UnitPrice, Total = Math.Round(dto.Qty * dto.UnitPrice, 2), SupplyDate = dto.SupplyDate, Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(), CreatedAt = now, UpdatedAt = now };
        db.ProjectSupplyItems.Add(item);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { projectId }, new ProjectSupplyItemDto(item.Id, item.ProjectId, item.Description, item.Unit, item.Qty, item.UnitPrice, item.Total, 0, item.Qty, item.SupplyDate, item.Note, item.CreatedAt, item.UpdatedAt));
    }
}
