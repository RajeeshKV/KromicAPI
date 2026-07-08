using Kromic.Application.DTOs;
using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/telegram")]
public sealed class TelegramAdminController(ITelegramUserService telegramUserService) : ControllerBase
{
    [HttpGet("users")]
    public Task<IReadOnlyList<TelegramBotUserResponse>> Users(CancellationToken cancellationToken) =>
        telegramUserService.GetUsersWithEmailSubscriptionsAsync(cancellationToken);
}