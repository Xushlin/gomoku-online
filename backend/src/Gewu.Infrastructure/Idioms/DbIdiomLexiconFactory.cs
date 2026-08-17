using Gewu.Domain.Idioms;
using Gewu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gewu.Infrastructure.Idioms;

/// <summary>
/// 从库里把全部成语原文读出来,构造一本内存词典。
/// <para>
/// 只在词典第一次被需要时执行一次(约 3 万行的单列读取),之后 <c>Contains</c> 是纯内存
/// O(1) —— 落子路径上不能有 I/O,那是 <see cref="IIdiomLexicon"/> 与异步的
/// <c>IIdiomRepository</c> 分成两个口的全部理由。
/// </para>
/// <para>
/// 单例要读一个 scoped 的 <c>DbContext</c>,所以这里自己开一个 scope。
/// </para>
/// </summary>
public static class DbIdiomLexiconFactory
{
    /// <summary>读库并构造词典。</summary>
    /// <param name="services">根服务提供者。</param>
    public static IIdiomLexicon Create(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var words = db.Set<Domain.Idioms.Idiom>()
            .AsNoTracking()
            .Select(i => i.Word)
            .ToList();
        return new InMemoryIdiomLexicon(words);
    }
}
