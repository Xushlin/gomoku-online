using FluentValidation;
using FluentValidation.Results;
using Gomoku.Application.Common.Behaviors;
using MediatR;
using ValidationException = Gomoku.Application.Common.Exceptions.ValidationException;

namespace Gomoku.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    private sealed record DummyRequest(string Name) : IRequest<string>;

    private sealed class NameValidator : AbstractValidator<DummyRequest>
    {
        public NameValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    private sealed class LengthValidator : AbstractValidator<DummyRequest>
    {
        public LengthValidator()
        {
            RuleFor(x => x.Name).MinimumLength(3).WithMessage("Name must be at least 3 chars.");
        }
    }

    [Fact]
    public async Task No_Validators_Passes_Through()
    {
        var sut = new ValidationBehavior<DummyRequest, string>(Array.Empty<IValidator<DummyRequest>>());

        var result = await sut.Handle(new DummyRequest("x"), () => Task.FromResult("ok"), default);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Single_Validator_Pass_Calls_Next()
    {
        var sut = new ValidationBehavior<DummyRequest, string>(new[] { new NameValidator() });

        var result = await sut.Handle(new DummyRequest("alice"), () => Task.FromResult("ok"), default);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task Single_Validator_Fail_Throws_ValidationException()
    {
        var sut = new ValidationBehavior<DummyRequest, string>(new[] { new NameValidator() });

        var act = () => sut.Handle(
            new DummyRequest(""),
            () => Task.FromResult("should-not-run"),
            default);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("Name");
        ex.Which.Errors["Name"].Should().Contain("Name is required.");
    }

    [Fact]
    public async Task Multiple_Validators_Merge_Failures()
    {
        var sut = new ValidationBehavior<DummyRequest, string>(
            new IValidator<DummyRequest>[] { new NameValidator(), new LengthValidator() });

        var act = () => sut.Handle(new DummyRequest(""), () => Task.FromResult("x"), default);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors["Name"].Should().HaveCountGreaterThanOrEqualTo(2);
        ex.Which.Errors["Name"].Should().Contain("Name must be at least 3 chars.");
    }

    [Fact]
    public void ValidationException_Groups_By_Property()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "required"),
            new("Name", "too short"),
            new("Email", "bad format"),
        };

        var ex = new ValidationException(failures);

        ex.Errors.Should().HaveCount(2);
        ex.Errors["Name"].Should().BeEquivalentTo("required", "too short");
        ex.Errors["Email"].Should().BeEquivalentTo("bad format");
    }
}
