using System.Reflection;
using System.Text.RegularExpressions;
using Gewu.Domain.Exceptions;

namespace Gewu.Domain.Tests.Exceptions;

/// <summary>
/// 错误码是客户端唯一会看到的东西,所以它必须是稳定、唯一、可预测的。
/// <para>
/// 这些用例**遍历程序集**而不是手写清单。手写清单是「需要记得扩充的东西」,而这个仓库
/// 已经为那种东西付过几次账 —— 最近一次是 <c>AllBuiltInRules()</c> 自称遍历注册表、
/// 数据源却是一个手写数组,于是象棋悄悄绕过了它本该守住的不变量。
/// </para>
/// </summary>
public class DomainErrorCodeTests
{
    private static readonly Regex KebabCase = new("^[a-z0-9]+(-[a-z0-9]+)*$");

    /// <summary>Domain 与 Application 两个程序集里所有的 <see cref="DomainException"/> 子类。</summary>
    public static TheoryData<Type> AllDomainExceptions()
    {
        var data = new TheoryData<Type>();
        foreach (var type in DomainExceptionTypes())
        {
            data.Add(type);
        }
        return data;
    }

    private static IReadOnlyList<Type> DomainExceptionTypes()
    {
        // Application 的异常也继承 DomainException,但 Domain.Tests 不引用 Application。
        // 从 DomainException 所在程序集出发能拿到 Domain 那一半;Application 那一半由
        // Gewu.Application.Tests 里的同名用例覆盖。
        return [.. typeof(DomainException).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(DomainException)) && !t.IsAbstract)
            .OrderBy(t => t.Name, StringComparer.Ordinal)];
    }

    [Fact]
    public void There_are_domain_exceptions_to_check()
    {
        // 断言列表非空 —— 一个反射走空了的测试会全绿地什么都不验。
        DomainExceptionTypes().Should().HaveCountGreaterThan(15);
    }

    [Theory]
    [MemberData(nameof(AllDomainExceptions))]
    public void Every_domain_exception_carries_a_kebab_case_code(Type type)
    {
        var instance = Construct(type);

        instance.Code.Should().NotBeNullOrWhiteSpace();
        instance.Code.Should().MatchRegex(KebabCase.ToString());
    }

    /// <summary>
    /// 每一个**具名静态工厂**产出的码 —— 即 <c>public static</c>、无参数以外只收字符串、
    /// 返回某个 <see cref="DomainException"/> 子类的方法。
    /// <para>
    /// 这一半此前**不在遍历范围内**。<c>Codes_are_unique</c> 走的是类型,而
    /// <c>InvalidMoveException.SelfCheck</c> 不是一个类型 —— 所以 <c>self-check</c> 从
    /// <c>add-web-xiangqi</c> 引入起,就从未被那条唯一性断言覆盖过。一个码溜过遍历是潜在问题;
    /// 成语接龙一次要加三个,那就是把同一个洞扩大三倍。**先补遍历,再加码。**
    /// </para>
    /// </summary>
    private static IReadOnlyList<(string Owner, string Code)> FactoryCodes()
    {
        var results = new List<(string, string)>();
        foreach (var type in DomainExceptionTypes())
        {
            var factories = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => typeof(DomainException).IsAssignableFrom(m.ReturnType))
                .Where(m => m.GetParameters().All(p => p.ParameterType == typeof(string)));

            foreach (var factory in factories)
            {
                var args = factory.GetParameters().Select(object? (_) => "probe").ToArray();
                var produced = (DomainException)factory.Invoke(null, args)!;
                results.Add(($"{type.Name}.{factory.Name}", produced.Code));
            }
        }
        return results;
    }

    [Fact]
    public void There_are_factory_codes_to_check()
    {
        // 一个反射走空了的测试会全绿地什么都不验 —— 而这正是本文件已经记过一次的失效方式。
        FactoryCodes().Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Theory]
    [MemberData(nameof(AllFactoryCodes))]
    public void Every_factory_code_is_kebab_case(string owner, string code)
    {
        code.Should().MatchRegex(KebabCase.ToString(), $"{owner} produces it");
    }

    /// <summary>工厂码的 theory 数据。</summary>
    public static TheoryData<string, string> AllFactoryCodes()
    {
        var data = new TheoryData<string, string>();
        foreach (var (owner, code) in FactoryCodes())
        {
            data.Add(owner, code);
        }
        return data;
    }

    [Fact]
    public void Codes_are_unique()
    {
        // 两个错误共用一个码,客户端就分不开它们,而症状是「某一类失败突然说错了话」。
        //
        // 遍历**类型与工厂两半**。只走类型时,`self-check` 与成语接龙那三个都在断言之外 ——
        // 而工厂正是"这种拒绝需要自己的文案、但不值得多一个异常类型"时的既定做法,
        // 也就是说,最需要唯一码的那些恰好都在洞里。
        var codes = DomainExceptionTypes().Select(t => Construct(t).Code)
            .Concat(FactoryCodes().Select(f => f.Code))
            .ToList();

        codes.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_idiom_chain_rules_have_three_distinct_codes()
    {
        // 「不是成语」「接不上」「说过了」是三种不同的纠正。接龙的界面故意不在客户端判合法性,
        // 所以服务端的拒绝是玩家了解规则的唯一途径 —— 共用一个码等于什么都没说。
        var codes = new[]
        {
            InvalidMoveException.IdiomNotFound("x").Code,
            InvalidMoveException.IdiomDoesNotLink("x").Code,
            InvalidMoveException.IdiomAlreadyUsed("x").Code,
        };

        codes.Should().Equal("idiom-not-found", "idiom-does-not-link", "idiom-already-used");
        codes.Should().NotContain("invalid-move");
    }

    [Fact]
    public void The_base_refuses_a_code_that_is_not_kebab_case()
    {
        // 构造函数是这条规则的机制。一条注释不是。
        var act = () => new ProbeException("Not Kebab Case");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Self_check_is_its_own_code_even_though_it_is_an_invalid_move()
    {
        // 象棋里最常见的一种拒绝。「这步不合法」不告诉玩家他漏看了什么。
        InvalidMoveException.SelfCheck("x").Code.Should().Be("self-check");
        new InvalidMoveException("x").Code.Should().Be("invalid-move");
    }

    private static DomainException Construct(Type type)
    {
        var withMessage = type.GetConstructor([typeof(string)]);
        if (withMessage is not null)
        {
            return (DomainException)withMessage.Invoke(["probe"]);
        }

        var parameterless = type.GetConstructor(Type.EmptyTypes);
        parameterless.Should().NotBeNull($"{type.Name} needs a constructible shape for this test");
        return (DomainException)parameterless!.Invoke([]);
    }

    private sealed class ProbeException(string code) : DomainException(code, "probe");
}
