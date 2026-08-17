# HyperPuzzle2D

Unity **6000.5.8f1** · 物理反馈型 2D 益智（炮台瞄准）· Android / iOS / macOS

## 已锁定

- 输入：炮台拖拽瞄准发射
- 广告：Mock → 真机 LevelPlay（`IAdService`）

## 流程

```text
启动页 → 主页 → 闯关选择 / 每日挑战 / 无尽
          │      ↓
        设置弹窗  局内对战 → 失败（复活/重试）或通关（下一关）
```

- **主页**：独立页面，展示闯关进度、无尽最高分、每日最佳与三个模式入口
- **设置**：主页右上角按钮打开的弹窗，含音效、震动、语言
- **闯关**：固定 8 关顺序推进，本地解锁进度。8 关全部按「一到两炮打出大面积坍塌」调校：
  每关都有可读的弱点（脆块面、外露爆炸核心、承重梁或独腿），目标分定在一发像样的炮弹已能拿到的水平
- **每日挑战**：按日期种子固定一关
- **无尽**：随机构造，记最高分

## 通关条件

每关有目标分（`LevelLayout.TargetScore`），HUD 顶部以「得分 / 目标」和进度条呈现，
达标后进度条转为青色。
每一炮的连锁结束后立即判定：达到目标分就通关，并把剩余弹药换算成奖励；弹药耗尽仍未达标才失败。
清空全部目标是额外表现，不再是另一套未说明的胜利条件。
主要分数来自**碎裂、撞落和爆炸**；同一炮内真实破坏越多，倍率越高，依次显示
`SMASH`、`CHAIN xN`、`TOTAL COLLAPSE`。直接接触只给少量反馈分，避免擦边高于大面积坍塌。

材料耐久（`DestructibleBlock.Configure`）按「打中就该有事发生」标定：脆块和爆炸核心一碰即碎，
承重梁断裂是整层塌落的爽点，只有重块是打不烂的战利品——它靠被撞下去得分，而不是靠被磨掉。
高抛的炮弹落点速度低，所以耐久上限受这类弱击约束，而不是按正面直击来定。

## 打开方式

1. **激活国际版许可证（必须）**  
   ```bash
   export PATH="$HOME/.unity/bin:$PATH"
   unity auth login
   ```
   浏览器登录后，用 Unity Hub / Editor 确认 Personal 或 Pro 许可证可用。

2. 打开 `HyperPuzzle2D` 工程目录。

3. 任意 Scene（或空场景）点 **Play**  
   `RuntimeBootstrap` 会自动进入启动页。

## 操作

- 在屏幕任意位置按住，向左下拖动来瞄准，松开发射；轨迹末端会标出首个目标接触点
- 弹药打光可看广告复活（Mock 立即成功）  
- 闯关通关后点「下一关」进入下一解锁关

## 屏幕适配

只面向手机竖屏。相机**只锁宽**：`ViewportFitter.OrthoSizeFor` 按当前比例算出恰好铺满场地宽度的
`orthographicSize`，因此 16:9 到 21:9 的任何机型都不会在两侧留空档，炮台到目标的距离与拖拽手感
也完全一致。纵向的差异由刻意画到画面外的支柱吸收，最方的 16:9 是唯一的紧凑档，五层方块仍能避开 HUD。

场地两侧不设墙，靠锁宽让炮弹自然打出画面即回收；落块判定线 `ClearY` 与炮弹回收线都放在
最高机型的视口下沿之外。目标一离开货架有效区就立即计入撞落和得分，但仍会继续下落到画面外再回收，
因此奖励及时、画面也不会凭空消失。

HUD 顶部是一行等高、等间距的三块：左侧返回主页、中间目标进度、右侧弹药，共用
`HudLeft`/`HudRight`/`HudRowBottom`/`HudRowTop` 四个常量对齐。目标进度条画在得分块内部而不是
单独横跨屏幕——它属于得分，独立成条时反而成了画面上最抢眼却信息量最低的元素。
这些块用不透明的 `Palette.HudPanel`，因为它们直接压在场景上，半透明会让背景光点透过来。

## 图标与启动页

图标不是导入的图片，而是由 `BrandMark` 程序化绘制：倾斜的三层方块加一颗拖着尾迹的橙色炮弹，
用的就是 `Palette` 里那套颜色，所以图标永远不会和游戏内美术脱节，仓库里也不需要放二进制美术源文件。

`BrandAssets` 负责把它渲染成 PNG 并绑定到 Player Settings，构建时由 `BuildPlayer` 自动调用；
想手动重刷用菜单 **HyperSmash → Regenerate Brand Assets**，或批处理模式执行
`HyperPuzzle2D.Editor.BrandAssets.RegenerateBatch`。产出物在 `Assets/Art/Brand/`：

| 文件 | 用途 |
| --- | --- |
| `AppIcon.png` | iOS 全部尺寸、Android Legacy/Round |
| `AppIconBackground.png` / `AppIconForeground.png` | Android 自适应图标的两层 |
| `SplashLogo.png` | 引擎启动画面的 Logo |

自适应图标的前景按 `BrandMark.AdaptiveSafeScale` 收进安全区，任何形状的桌面遮罩都不会切到图形。

启动分两段且刻意做成连贯的一屏：引擎启动画面用品牌底色加同一图形（Unity Logo 已关闭），
覆盖引擎初始化；随后游戏内启动页用同样的底色和图形接上，只是多了标题与进度条，点击可跳过。
引擎那段最短只能设 2 秒，所以游戏内那段压到了 0.9 秒。

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
