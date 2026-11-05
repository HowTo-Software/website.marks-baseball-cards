using MarksBaseballCards.Api.Data;
using MarksBaseballCards.Api.Entities;
using MarksBaseballCards.Api.Payments;
using MarksBaseballCards.Api.Services;
using MarksBaseballCards.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using Card = MarksBaseballCards.Api.Entities.Card;

namespace MarksBaseballCards.Api.Controllers;

[ApiController]
[Route("api/checkout")]
[AllowAnonymous]
public class CheckoutController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly HistoryWriter _history;
    private readonly StripeOptions _stripe;
    private readonly ILogger<CheckoutController> _logger;
    private readonly string? _publicBaseUrl;

    public CheckoutController(
        AppDbContext db, HistoryWriter history, IOptions<StripeOptions> stripe, ILogger<CheckoutController> logger, IConfiguration configuration)
    {
        _db = db;
        _history = history;
        _stripe = stripe.Value;
        _logger = logger;
        _publicBaseUrl = configuration["PublicBaseUrl"];
    }

    /// <summary>Creates a Stripe Checkout Session for a card and returns the hosted-page URL.</summary>
    [HttpPost("{cardId:int}")]
    public async Task<ActionResult<CheckoutSessionResponse>> CreateSession(int cardId)
    {
        if (!_stripe.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Online payments are not configured." });
        }

        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null)
        {
            return NotFound();
        }
        if (!card.IsPublished || !card.IsForSale || card.IsSold)
        {
            return Conflict(new { error = "This card is not available for purchase." });
        }
        if (card.Price is null or <= 0m)
        {
            return Conflict(new { error = "This card does not have a price set." });
        }

        if (!Uri.TryCreate(_publicBaseUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != "https"
            || !string.IsNullOrEmpty(publicUri.UserInfo) || !string.IsNullOrEmpty(publicUri.Query) || !string.IsNullOrEmpty(publicUri.Fragment))
            return StatusCode(503, new { error = "The public checkout return address is not configured securely." });
        var origin = publicUri.AbsoluteUri.TrimEnd('/');
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{origin}/buy/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{origin}/marketplace",
            ClientReferenceId = card.Id.ToString(),
            Metadata = new Dictionary<string, string> { ["cardId"] = card.Id.ToString() },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _stripe.Currency,
                        UnitAmountDecimal = card.Price.Value * 100m,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"#{card.CardNumber} {card.PlayerName}",
                            Description = string.IsNullOrWhiteSpace(card.Condition)
                                ? "1991 Topps baseball card"
                                : $"1991 Topps - {card.Condition}"
                        }
                    }
                }
            }
        };

        var service = new SessionService(new StripeClient(_stripe.SecretKey));
        var session = await service.CreateAsync(options);

        card.StripeCheckoutSessionId = session.Id;
        await _db.SaveChangesAsync();

        return Ok(new CheckoutSessionResponse { Url = session.Url, SessionId = session.Id });
    }

    /// <summary>Receives Stripe webhook events. Signature is verified before any action is taken.</summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        if (string.IsNullOrWhiteSpace(_stripe.WebhookSecret))
        {
            _logger.LogWarning("Received a Stripe webhook but no webhook secret is configured.");
            return BadRequest();
        }

        var json = await new StreamReader(Request.Body).ReadToEndAsync();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json, Request.Headers["Stripe-Signature"], _stripe.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Rejected Stripe webhook with invalid signature.");
            return BadRequest();
        }

        if (stripeEvent.Type == "checkout.session.completed" &&
            stripeEvent.Data.Object is Session session &&
            session.PaymentStatus == "paid")
        {
            await FulfillAsync(session);
        }

        return Ok();
    }

    /// <summary>Used by the success page to confirm payment state for a session.</summary>
    [HttpGet("status/{sessionId}")]
    public async Task<ActionResult<CheckoutStatusDto>> Status(string sessionId)
    {
        if (!_stripe.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Online payments are not configured." });
        }

        var service = new SessionService(new StripeClient(_stripe.SecretKey));
        Session session;
        try
        {
            session = await service.GetAsync(sessionId);
        }
        catch (StripeException)
        {
            return NotFound();
        }

        // Fallback fulfillment: if the webhook hasn't arrived yet but payment is complete,
        // mark the card sold here. FulfillAsync is idempotent so double-processing is safe.
        if (session.PaymentStatus == "paid")
        {
            await FulfillAsync(session);
        }

        var card = await ResolveCardAsync(session);
        return Ok(new CheckoutStatusDto
        {
            Status = session.Status,
            PaymentStatus = session.PaymentStatus,
            CardId = card?.Id,
            CardNumber = card?.CardNumber,
            PlayerName = card?.PlayerName
        });
    }

    private async Task FulfillAsync(Session session)
    {
        var card = await ResolveCardAsync(session, tracking: true);
        if (card is null || card.IsSold)
        {
            return; // already fulfilled or unknown card - safe to ignore (idempotent)
        }

        card.IsSold = true;
        card.IsForSale = false;
        card.SoldAtUtc = DateTime.UtcNow;
        card.SoldPrice = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : card.Price;
        card.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _history.LogAsync("Sold", "stripe",
            $"Online purchase via Stripe ({session.Id}) for {card.SoldPrice:C}", card);
        _logger.LogInformation("Fulfilled Stripe order for card {CardId} ({Session}).", card.Id, session.Id);
    }

    private async Task<Card?> ResolveCardAsync(Session session, bool tracking = false)
    {
        int? cardId = null;
        if (session.Metadata is not null &&
            session.Metadata.TryGetValue("cardId", out var raw) &&
            int.TryParse(raw, out var fromMeta))
        {
            cardId = fromMeta;
        }
        else if (int.TryParse(session.ClientReferenceId, out var fromRef))
        {
            cardId = fromRef;
        }

        if (cardId is null)
        {
            return null;
        }

        var query = tracking ? _db.Cards : _db.Cards.AsNoTracking();
        return await query.FirstOrDefaultAsync(c => c.Id == cardId);
    }
}
