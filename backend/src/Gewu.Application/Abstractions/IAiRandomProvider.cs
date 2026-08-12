namespace Gomoku.Application.Abstractions;

/// <summary>
/// AI 决策所需随机源的抽象。生产实现返回一个 <see cref="Random.Shared"/> 包装;
/// 单元测试注入固定种子以得到确定输出。
/// </summary>
public interface IAiRandomProvider
{
    /// <summary>返回当前可用的 <see cref="Random"/> 实例。</summary>
    Random Get();
}
