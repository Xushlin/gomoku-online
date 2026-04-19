using System.Net;
using System.Text.Json;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ValidationException = Gomoku.Application.Common.Exceptions.ValidationException;

namespace Gomoku.Api.Middleware;

/// <summary>
/// 全局异常中间件。把领域 / 应用异常映射为 HTTP + <see cref="ProblemDetails"/>(RFC 7807)响应。
/// 未知异常一律返回 500 + 静态文案,不泄漏内部细节。5xx 用 <c>LogError</c>,4xx 用 <c>LogInformation</c>。
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <inheritdoc />
    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, title, detail) = Map(ex);

        if (status >= 500)
        {
            _logger.LogError(ex, "Unhandled exception in request pipeline.");
        }
        else
        {
            _logger.LogInformation("Request failed with {Status}: {Message}", status, ex.Message);
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        object payload;
        if (ex is ValidationException validation)
        {
            payload = new ValidationProblemDetails(validation.Errors.ToDictionary(
                kvp => kvp.Key, kvp => kvp.Value))
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.io/{status}",
            };
        }
        else
        {
            payload = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.io/{status}",
            };
        }

        await JsonSerializer.SerializeAsync(context.Response.Body, payload, payload.GetType(), JsonOptions);
    }

    private static (int Status, string Title, string Detail) Map(Exception ex) => ex switch
    {
        ValidationException v => (
            (int)HttpStatusCode.BadRequest,
            "Validation failed.",
            v.Message),

        InvalidEmailException or
        InvalidUsernameException or
        InvalidMoveException or
        InvalidRoomNameException or
        InvalidRoomStatusTransitionException or
        InvalidChatContentException => (
            (int)HttpStatusCode.BadRequest,
            "Bad request.",
            ex.Message),

        InvalidCredentialsException or
        InvalidRefreshTokenException => (
            (int)HttpStatusCode.Unauthorized,
            "Unauthorized.",
            ex.Message),

        UserNotActiveException or
        NotAPlayerException or
        PlayerCannotPostSpectatorChannelException => (
            (int)HttpStatusCode.Forbidden,
            "Forbidden.",
            ex.Message),

        UserNotFoundException or
        RoomNotFoundException or
        NotInRoomException or
        NotSpectatingException => (
            (int)HttpStatusCode.NotFound,
            "Not found.",
            ex.Message),

        EmailAlreadyExistsException or
        UsernameAlreadyExistsException or
        RoomNotWaitingException or
        RoomNotInPlayException or
        RoomFullException or
        AlreadyInRoomException or
        HostCannotLeaveWaitingRoomException or
        PlayerCannotSpectateException or
        NotYourTurnException or
        NotOpponentsTurnException => (
            (int)HttpStatusCode.Conflict,
            "Conflict.",
            ex.Message),

        UrgeTooFrequentException => (
            (int)HttpStatusCode.TooManyRequests,
            "Too many requests.",
            ex.Message),

        DbUpdateConcurrencyException => (
            (int)HttpStatusCode.Conflict,
            "Concurrent modification.",
            "The room state changed concurrently; reload and retry."),

        _ => (
            (int)HttpStatusCode.InternalServerError,
            "Internal server error.",
            "An unexpected error occurred."),
    };
}
