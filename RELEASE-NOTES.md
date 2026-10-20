# 专注时光 v1.2.0

简单、离线、本地运行的 Windows 专注计时器。

**[下载 Windows 免安装包 ZIP](https://github.com/Batman-hu/FocusTime/raw/refs/tags/v1.2.0/downloads/FocusTime-v1.2.0-Windows.zip)** · **[直接下载 EXE](https://github.com/Batman-hu/FocusTime/raw/refs/tags/v1.2.0/FocusTime.exe)**

解压后双击 `FocusTime.exe` 即可使用，无需账号、联网或安装开发工具。

## 本次更新

- 统计、每日进度、昨日累计和项目累计统一以小时显示；45 分钟显示为 0.75 小时。
- 每日目标支持 0.5、1.5 等小数小时，按分钟精度保存。
- 项目新增删除按钮，可清理写错的项目；确认窗口展示受影响记录数与时长。删除前创建完整备份，保存失败自动回滚。
- 置顶小窗支持拖动四边与四角加宽、拉长或缩放，计时盘自适应，窗口记住上次宽高。
- 小窗左上角新增“返回完整视图”，右上角关闭，切换视图继续计时。

继续支持正计时、倒计时、暂停续计、项目归档、自动保存和 CSV 导出。

数据仍保存在 `D:\FocusTime\Data`，删除前备份位于其中的 `Backups` 文件夹，CSV 默认导出到 `D:\FocusTime\Exports`。升级前关闭旧程序，替换程序文件即可；下载包不包含个人记录。

运行环境：Windows 10 / 11、.NET Framework 4.8、可写的 D 盘。

验证：41 项核心检查通过，并完成计时、项目删除及回滚、小时目标、统计筛选、小窗缩放与尺寸恢复等界面检查。
