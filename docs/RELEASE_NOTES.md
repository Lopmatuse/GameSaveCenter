# 0.1.0 Development Preview

## 已实现源码

- Playnite 10 GenericPlugin 与 Apple HIG 启发的统一面板；
- Playnite 游戏库导出、Game Action/MOD loader 识别；
- Worker 命名管道、SQLite、任务、日志和审计；
- Ludusavi 匹配、单游戏/全部/定时/退出备份；
- 版本备注、锁定、历史索引、差异和保留预览；
- PreRestore、安全恢复、回滚和撤销流程；
- 外部游戏进程与多进程 MOD 启动链兜底；
- Steam、Game Bar、Windows 和自定义平台媒体增量同步；
- SHA-256 去重、稳定写入检测、原子复制和误归类修正；
- Rclone `copy/check` 安全适配；
- 候选存档路径评分、WGS 辅助扫描和规则草案；
- Windows 构建/测试/打包/安装脚本；
- Core xUnit 测试源码与跨平台结构校验。

## 尚未声明完成

- Windows 编译、Playnite 真机加载和真实游戏端到端验证；
- Worker 主动推送后台通知；
- 公共截图目录的完整会话时间归类；
- 默认会话的启动前/退出后文件快照差异；
- Rclone 远端设备摘要摄取及完整多设备冲突 UI；
- 未知进程映射学习 UI。

详见 `IMPLEMENTATION_LIMITATIONS.md`。
