using System.Data;
using Content.Server.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SS14.Admin.Helpers;
using SS14.Admin.Pages.Connections;

namespace SS14.Admin.Pages.Bans;

[ValidateAntiForgeryToken]
public class Hits : PageModel
{
    private readonly PostgresServerDbContext _dbContext;
    private readonly BanHelper _banHelper;

    public BanHelper.BanJoin Ban { get; set; } = default!;

    public ISortState SortState { get; private set; } = default!;
    public PaginationState<ConnectionsIndexModel.Connection> Pagination { get; } = new(100);
    public Dictionary<string, string?> AllRouteData { get; } = new();

    public string? CurrentFilter { get; set; }

    public Hits(PostgresServerDbContext dbContext, BanHelper banHelper)
    {
        _dbContext = dbContext;
        _banHelper = banHelper;
    }

    public async Task<IActionResult> OnGetAsync(
        int ban,
        string? sort,
        string? search,
        int? pageIndex,
        int? perPage)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var banEntry = await _banHelper.CreateServerBanJoin().SingleOrDefaultAsync(b => b.Ban.Id == ban);

        if (banEntry == null)
            return NotFound();

        Ban = banEntry;

        Pagination.Init(pageIndex, perPage, AllRouteData);

        CurrentFilter = search;
        AllRouteData.Add("search", CurrentFilter);

        var logQuery = _dbContext.ServerBanHit
            .Include(b => b.Connection)
            .ThenInclude(c => c.Server)
            .Where(bh => bh.BanId == banEntry.Ban.Id)
            .Select(bh => bh.Connection);

        logQuery = SearchHelper.SearchConnectionLog(logQuery, search, User);

        SortState = await ConnectionsIndexModel.LoadSortConnectionsTableData(
            Pagination,
            _dbContext,
            logQuery,
            sort,
            AllRouteData);

        return Page();
    }

    public async Task<IActionResult> OnPostUnbanIpAsync(int ban)
    {
        if (!User.IsInRole("BAN"))
            return Forbid();

        var banEntity = await _dbContext.Ban
            .Include(b => b.Addresses)
            .SingleOrDefaultAsync(b => b.Id == ban);

        if (banEntity == null)
        {
            TempData.SetStatusError("Unable to find ban");
            return RedirectToPage(new { ban });
        }

        if (banEntity.Addresses is not { Count: > 0 })
        {
            TempData.SetStatusError("Ban has no IP address to unban");
            return RedirectToPage(new { ban });
        }

        _dbContext.BanAddress.RemoveRange(banEntity.Addresses);
        await _dbContext.SaveChangesAsync();

        TempData.SetStatusInformation("IP address removed from ban");
        return RedirectToPage(new { ban });
    }

    public async Task<IActionResult> OnPostUnbanHwidAsync(int ban)
    {
        if (!User.IsInRole("BAN"))
            return Forbid();

        var banEntity = await _dbContext.Ban
            .Include(b => b.Hwids)
            .SingleOrDefaultAsync(b => b.Id == ban);

        if (banEntity == null)
        {
            TempData.SetStatusError("Unable to find ban");
            return RedirectToPage(new { ban });
        }

        if (banEntity.Hwids is not { Count: > 0 })
        {
            TempData.SetStatusError("Ban has no HWID to unban");
            return RedirectToPage(new { ban });
        }

        _dbContext.BanHwid.RemoveRange(banEntity.Hwids);
        await _dbContext.SaveChangesAsync();

        TempData.SetStatusInformation("HWID removed from ban");
        return RedirectToPage(new { ban });
    }
}
