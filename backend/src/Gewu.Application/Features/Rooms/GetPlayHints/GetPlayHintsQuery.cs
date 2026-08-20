using System.Collections.Generic;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetPlayHints;

/// <summary>
/// 「我现在能出哪些牌」—— 调用者自己那一份候选出法。
/// <para>
/// <b>它是按需的,而那是刻意的。</b> 候选出法可能有几十项;把它塞进每一次 <c>RoomState</c>
/// 广播,就是给每个人的每一帧都付一次钱 —— <c>generalize-lobby</c> 记过那笔账
/// (「一个没人渲染但所有人付钱的切片,是一个只在网络面板里才出现的缺陷」)。
/// 广播里带的只是一个布尔 <c>canFollow</c>。
/// </para>
/// <para>
/// <b>它只回答调用者自己的那一份。</b> 候选出法由这个座位的手牌决定,所以它是私有信息 ——
/// 一个能查别人候选的端点,等于把别人的手牌算出来给你。围观者与非玩家拿到的是空。
/// </para>
/// </summary>
/// <param name="UserId">调用者。</param>
/// <param name="RoomId">房间。</param>
public sealed record GetPlayHintsQuery(UserId UserId, RoomId RoomId) : IRequest<PlayHintsDto>;

/// <summary>
/// 候选出法,按**先弱后强**排。
/// <para>
/// 每一项是一串**牌的编码**(与 <c>play:&lt;cards&gt;</c> 里那一段同一个格式),所以客户端
/// 可以原样回传,也可以解出来在屏幕上把牌点起来。
/// </para>
/// <para>
/// 空列表的含义是「你要不起」,而它与 <c>seatView.canFollow == false</c> 是**同一个事实的
/// 两个出口** —— 一条断言把它们钉在一起。
/// </para>
/// </summary>
/// <param name="Plays">候选出法;要不起时为空。</param>
public sealed record PlayHintsDto(IReadOnlyList<string> Plays);
