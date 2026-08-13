# HyperPuzzle2D

Unity **6000.5.8f1** · 物理反馈型 2D 益智（炮台瞄准）· MVP W1 骨架

## 已锁定

- 输入：炮台拖拽瞄准发射
- 广告：Mock → 真机 LevelPlay（`IAdService`）

## 打开方式

1. **激活国际版许可证（必须）**  
   ```bash
   export PATH="$HOME/.unity/bin:$PATH"
   unity auth login
   ```
   浏览器登录后，用 Unity Hub / Editor 确认 Personal 或 Pro 许可证可用。

2. 打开工程目录：`/Users/a/Develop/project/unity/HyperPuzzle2D`

3. 任意 Scene（或空场景）点 **Play**  
   `RuntimeBootstrap` 会自动生成炮台关卡。

> 当前机器尚未登录 Unity 账号，命令行/`-batchmode` 无法编译验证；登录后首次打开会自动生成 `.meta` 与缺失的 ProjectSettings。

## 操作

- 按住拖拽瞄准，松开发射  
- 弹药打光可看广告复活（Mock 立即成功）  
- 清台后点 Next 开下一局

## 文档

- 需求：[../docs/PRD-HyperPuzzle2D.md](../docs/PRD-HyperPuzzle2D.md)
