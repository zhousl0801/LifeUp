using System.Security.Cryptography;
using System.Text;
using AssetKeeper.Application.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetKeeper.Api.Controllers;

/// <summary>
/// 供外部定时任务（GitHub Actions、cron-job.org 等）调用的机器入口。
/// 不走登录 Cookie，改用共享令牌校验；令牌只从配置读取，不写日志、不回显。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/cron")]
public sealed class CronController : ControllerBase
{
    /// <summary>承载共享令牌的请求头。</summary>
    public const string TokenHeader = "X-Cron-Token";

    /// <summary>配置键，对应环境变量 <c>Cron__Token</c>。</summary>
    public const string TokenKey = "Cron:Token";

    private readonly PlanExecutionService _executionService;
    private readonly IConfiguration _configuration;

    public CronController(PlanExecutionService executionService, IConfiguration configuration)
    {
        _executionService = executionService;
        _configuration = configuration;
    }

    /// <summary>
    /// 执行所有到期计划。重复调用是安全的：已生成的期次不会重复扣款。
    /// </summary>
    [HttpPost("plans/run")]
    [ProducesResponseType<PlanRunResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RunPlans(CancellationToken cancellationToken)
    {
        var expected = _configuration[TokenKey];

        if (string.IsNullOrWhiteSpace(expected))
        {
            return Problem(
                "未配置定时调用令牌（Cron__Token），该入口已关闭。",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!TokenMatches(expected, Request.Headers[TokenHeader].ToString()))
        {
            return Problem("定时调用令牌无效。", statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(await _executionService.RunDueAsync(cancellationToken));
    }

    private static bool TokenMatches(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        // 长度不同直接失败；长度相同时按固定时间比较，避免逐字节试探。
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
