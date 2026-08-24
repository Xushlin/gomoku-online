# tasks — room-list-seats

## 1. 接线

- [x] `ActiveRoomsCard` 注入 `GameCapabilitiesService` 并在构造里 `ensureLoaded()`。
- [x] `seatCountOf(gameKey)` 未到达时返回 `null`,**不退化成 `seats.length`**。
- [x] 纹章复用 `GameEmblem` + `GAME_REGISTRY`,零新增资源。

## 2. 模板

- [x] 在座 = 凸起圆片(首字),空位 = 凹槽,未知 = 脉动占位且 `aria-hidden`。
- [x] 圆片是 `/users/:id` 链接,带 `aria-label` 与 `title` 的**全名**。
- [x] 名字那行保留,`hidden sm:flex`。
- [x] `lobby.rooms.seat-vacant` 双语键。**没有重用退役的 `seat-empty`** —— 见 §5。

## 3. 测试

- [x] 一个等待中的三人房画三个位子(2 在座 + 1 空),而不是两个。
- [x] 满座房间**没有**空位圆片(正面对照)。
- [x] 座位数未到达时画占位,不画空位,也不画成满座。
- [x] 占位 `aria-hidden`。
- [x] 圆片显示一个字,而 `aria-label` / `title` / `href` 是全的。
- [x] 行里画得出棋种纹章。
- [x] **用真的 `DefaultGameCapabilitiesService`**,只在 HTTP 边界打桩 —— 桩下的测试证明模板逻辑,证明不了接线。

## 4. 变异

- [x] `seatCount` 退化成 0 → 红。
- [x] 空位数不减在座数 → 红。
- [x] 圆片放全名 → 红。
- [x] 行丢掉纹章 → 红。
- [x] 忘了调 `ensureLoaded()` → 红(桩下这条抓不到)。
- [x] 退役键名从 `aria-label` 溜回来 → 红。

## 5. 计划之外

- [x] **我差点重用一个规格明写着已退役的键名。** `web-lobby` 记着 `seat-black` / `seat-white` /
      `seat-empty` 被 `players` 取代,而我给空位加的第一个键就叫 `seat-empty`。含义上说得通
      (现在真的有空位要标),但**重用一个退役的名字会让规格自己的历史读不懂** —— 下一个人
      读到「seat-empty 被取代」再在 JSON 里看到它,得自己去推哪个意思是活的。改名 `seat-vacant`。

- [x] **而那条「行里不许提颜色」的测试,是因为与它用意无关的理由而通过的。** 它只查
      `textContent`,而我的键在 `aria-label` 属性里 —— 属性它看不见。加强成查 `outerHTML`。

- [x] **加强之后的第一次变异仍然判绿,而原因是 fixture 而不是断言。** 那条测试用 3 个在座
      配一个 2 座位的棋种(一个不可能的局面),于是空位数是 −1、一个圆片都不画,把退役键名
      放回 `aria-label` 的变异照样绿。**一条负向断言在「什么都没发生」时恒真** —— 现在它先
      断言「确实画出了一个空位」,再断言那三个名字不在。

- [x] **第二次变异又判绿,而这次撞的是我自己的 `data-testid`。** 我把空位的 testid 也叫
      `seat-empty`,于是那条退役键名检查在**我的测试 id** 上就先失败了 —— 一个真信号被自己的
      命名噪音盖住。testid 改成 `seat-vacant` 之后,那条变异红了,消息是
      「retired key seat-empty came back」。

- [x] **审稿稿和它自己的说明矛盾。** 限制卡片写着「375 px 下只剩首字」,暗示宽屏有名字;
      而稿子任何宽度都只画首字。**按说明实现,不按稿子实现** —— 而这一点是既有测试指出来的,
      不是我看出来的:`every player name is a link to their profile` 在名字被换掉时会红。

- [x] **一句过期的文档:** `RoomSummary.seats` 说「`GET /api/games` 今天不发座位总数」,
      而 `publish-seat-count` 之后它是假的。本变更是那个字段的第一个消费者,所以顺手改对。

- [x] **浏览器端到端没验成。** 起了隔离的 API(5245)与前端(4205),`GET /api/games` 确认
      发 `seatCount=3`,两个房间也建成了;但 pane 的视口是 0、`read_page` 读到空页,界面登录
      没走通。**没有硬说验过** —— 换成一条用真 `DefaultGameCapabilitiesService`、只在 HTTP
      边界打桩的测试,它能抓到「忘了调 ensureLoaded()」(已变异确认)。它是近似,不是替代:
      它证明得到「描述符到达后圆片补齐」,证明不了「真实屏幕上好看」。

- [x] **两个环境上的小坑,都记过:** 签名密钥必须是 **Base64**,纯文本会让 host 起不来
      (`FormatException`,而异常信息没说期望什么);以及带中文的 JSON 经壳层会被打坏,
      得写成 UTF-8 文件用 `--data-binary` 传。
