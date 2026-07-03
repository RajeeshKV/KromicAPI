using Kromic.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Kromic.Api.Controllers;

[ApiController]
[Route("api/gold-rate-email-alerts")]
public sealed class GoldRateEmailAlertsController(
    IGoldRateEmailSubscriptionService emailSubscriptionService) : ControllerBase
{
    [HttpGet("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string token, CancellationToken cancellationToken)
    {
        var unsubscribed = await emailSubscriptionService.UnsubscribeAsync(token, cancellationToken);
        var heading = unsubscribed ? "Email alerts unsubscribed" : "Unsubscribe link is invalid or already used";
        var body = unsubscribed
            ? "You will no longer receive gold-rate email alerts from your Telegram subscription. You can subscribe again anytime from Telegram with /emailalerts."
            : "No active email alert subscription was found for this link.";

        var html = $"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{heading}</title>
            </head>
            <body style="font-family: Arial, sans-serif; margin: 40px; line-height: 1.5;">
                <h1>{heading}</h1>
                <p>{body}</p>
            </body>
            </html>
            """;

        return Content(html, "text/html");
    }
}
