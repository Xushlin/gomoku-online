using Gomoku.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gomoku.Application.Tests.Features.Common.Behaviors;

public class LoggingBehaviorTests
{
    private sealed record TestRequest(string Payload) : IRequest<string>;

    // Moq 对泛型 ILogger<T> + 扩展方法(LogInformation / LogError)的组合处理有兼容问题,
    // 用一个轻量级 test logger 直接拦截 Log 方法。
    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    [Fact]
    public async Task Success_Logs_Enter_And_Exit_Information()
    {
        var logger = new TestLogger<LoggingBehavior<TestRequest, string>>();
        var behavior = new LoggingBehavior<TestRequest, string>(logger);

        var result = await behavior.Handle(
            new TestRequest("hi"),
            next: () => Task.FromResult("ok"),
            default);

        result.Should().Be("ok");

        logger.Entries.Count(e => e.Level == LogLevel.Information).Should().Be(2);
        logger.Entries.Any(e => e.Level == LogLevel.Error).Should().BeFalse();
        logger.Entries[0].Message.Should().Contain("Handling").And.Contain("TestRequest");
        logger.Entries[1].Message.Should().Contain("Handled").And.Contain("TestRequest").And.Contain("ms");
    }

    [Fact]
    public async Task Exception_Logs_Enter_Then_Error_Then_Rethrows()
    {
        var logger = new TestLogger<LoggingBehavior<TestRequest, string>>();
        var behavior = new LoggingBehavior<TestRequest, string>(logger);

        var act = () => behavior.Handle(
            new TestRequest("bomb"),
            next: () => throw new InvalidOperationException("boom"),
            default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");

        logger.Entries.Count(e => e.Level == LogLevel.Information).Should().Be(1);
        var errorEntries = logger.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        errorEntries.Should().HaveCount(1);
        errorEntries[0].Exception.Should().BeOfType<InvalidOperationException>();
        errorEntries[0].Message.Should().Contain("TestRequest").And.Contain("failed");
    }

    [Fact]
    public async Task Logs_Contain_RequestName_Type_Short_Name()
    {
        var logger = new TestLogger<LoggingBehavior<TestRequest, string>>();
        var behavior = new LoggingBehavior<TestRequest, string>(logger);

        await behavior.Handle(new TestRequest("x"), () => Task.FromResult("ok"), default);

        logger.Entries.Should().AllSatisfy(e => e.Message.Should().Contain("TestRequest"));
    }
}
