# HyperPuzzle2D

Unity **6000.5.8f1** · 物理反馈型 2D 益智（炮台瞄准）· Android / iOS / macOS

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

2. 打开 `HyperPuzzle2D` 工程目录。

3. 任意 Scene（或空场景）点 **Play**  
   `RuntimeBootstrap` 会自动生成炮台关卡。

## 操作

- 按住拖拽瞄准，松开发射  
- 弹药打光可看广告复活（Mock 立即成功）  
- 清台后点 Next 开下一局

## 移动端构建

工程使用固定包名 `com.hyperpuzzle.hypersmash`，Android 与 iOS 均仅使用竖屏。

```bash
# Android ARM64 调试安装包
../tools/build.sh android

# Google Play AAB
../tools/build.sh android-aab

# iOS 真机 Xcode 工程（签名由 Xcode/CI 注入）
../tools/build.sh ios

# Apple Silicon iOS 模拟器 Xcode 工程
../tools/build.sh ios-sim
```

要求：

- Android：JDK 17、NDK r27c（27.2.12479018）、SDK Build Tools 36；
- iOS：Xcode 26+，部署目标 iOS 15+；
- 发布包须配置正式 Android keystore；iOS 真机安装须配置 Apple Developer Team 和 provisioning profile。

产物默认生成在 `HyperPuzzle2D/Builds/`，不会提交到 Git。

## 文档

- 需求：[../docs/PRD-HyperPuzzle2D.md](../docs/PRD-HyperPuzzle2D.md)
