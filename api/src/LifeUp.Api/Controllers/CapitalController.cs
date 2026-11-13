using LifeUp.Application.Capital;
using LifeUp.Application.Ledgers;
using LifeUp.Domain.Ledgers;
using Microsoft.AspNetCore.Mvc;

namespace LifeUp.Api.Controllers;

[ApiController]
[Route("api/ledger/capital")]
public sealed class CapitalController : ControllerBase
{
    private readonly CapitalGoalService _capitalGoalService;

    public CapitalController(CapitalGoalService capitalGoalService)
    {
        _capitalGoalService = capitalGoalService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var overview = await _capitalGoalService.GetAsync(cancellationToken);

        return overview is null ? LedgerNotFound() : Ok(overview);
    }

    [HttpPut]
    public async Task<IActionResult> Save(
        [FromBody] UpdateCapitalGoalRequest request,
        CancellationToken cancellationToken)
    {
        var goal = Map(request);

        if (!CapitalGoal.TryValidate(goal, out var errors))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "资本目标设置无效",
                Detail = string.Join(" ", errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var overview = await _capitalGoalService.SaveAsync(goal, cancellationToken);

            return overview is null ? LedgerNotFound() : Ok(overview);
        }
        catch (ConcurrencyConflictException)
        {
            return Conflict(new ProblemDetails
            {
                Detail = "账本有新变化，请刷新后重试。",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] UpdateCapitalGoalRequest request,
        CancellationToken cancellationToken)
    {
        var goal = Map(request);

        if (!CapitalGoal.TryValidate(goal, out var errors))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "资本目标设置无效",
                Detail = string.Join(" ", errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        var overview = await _capitalGoalService.PreviewAsync(goal, cancellationToken);

        return overview is null ? LedgerNotFound() : Ok(overview);
    }

    private ObjectResult LedgerNotFound()
    {
        return NotFound(new ProblemDetails
        {
            Title = "账本尚未初始化",
            Detail = "请先设置账本起始日期和现金余额。",
            Status = StatusCodes.Status404NotFound
        });
    }

    private static CapitalGoal Map(UpdateCapitalGoalRequest request)
    {
        return new CapitalGoal(
            request.TargetAmount,
            request.MonthlyIncome,
            request.MonthlyExpense,
            request.MonthlyProvidentDeposit,
            request.IncomeGrowthPercent,
            request.ExpenseGrowthPercent,
            request.InvestmentAnnualReturnPercent,
            request.ProvidentAnnualReturnPercent);
    }
}
