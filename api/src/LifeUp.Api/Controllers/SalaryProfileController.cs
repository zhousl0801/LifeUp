using LifeUp.Application.Ledgers;
using LifeUp.Application.Salary;
using Microsoft.AspNetCore.Mvc;

namespace LifeUp.Api.Controllers;

[ApiController]
[Route("api/ledger/salary-profile")]
public sealed class SalaryProfileController : AssetOperationsControllerBase
{
    private readonly SalaryProfileService _salaryProfileService;

    public SalaryProfileController(SalaryProfileService salaryProfileService)
    {
        _salaryProfileService = salaryProfileService;
    }

    [HttpGet]
    [ProducesResponseType<SalaryProfileOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var overview = await _salaryProfileService.GetAsync(cancellationToken);

        return overview is null ? LedgerNotFound() : Ok(overview);
    }

    [HttpPut]
    [ProducesResponseType<SalaryProfileOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Save(
        [FromBody] SaveSalaryProfileRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salaryProfileService.SaveAsync(request, cancellationToken);

        return ToActionResult(result);
    }

    [HttpPost("disable")]
    [ProducesResponseType<SalaryProfileOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(
        [FromBody] SalaryProfileActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salaryProfileService.DisableAsync(request, cancellationToken);

        return ToActionResult(result);
    }
}
