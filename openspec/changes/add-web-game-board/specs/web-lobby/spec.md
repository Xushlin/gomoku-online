## REMOVED Requirements

### Requirement: `/rooms/:id` 临时占位组件
**Reason**: `add-web-game-board` 交付真实的 `RoomPage`,临时 placeholder 的存在价值消失。原 `RoomPlaceholder` 组件连同其目录、模板、测试一并删除。

**Migration**: 路由路径 `/rooms/:id` 不变;`app.routes.ts` 的 `loadComponent` 目标从 `RoomPlaceholder` 换为 `RoomPage`;所有外部链接、大厅的 Join/Watch/Resume 跳转、书签继续有效。没有需要迁移的客户端持久化状态。新 `RoomPage` 的 404 / Leave 能力保留(详见 `web-game-board` capability 的 ADDED Requirements)。
