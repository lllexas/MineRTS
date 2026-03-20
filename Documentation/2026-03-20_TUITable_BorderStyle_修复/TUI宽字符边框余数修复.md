# TUI 宽字符边框余数修复

**日期**：2026-03-20
**涉及文件**：`Assets/Scripts/Common/TUITable.cs`

---

## 问题描述

`ls` 命令输出时，边框行（`GenerateTop` / `GenerateBottom` / `GenerateDivider`）
与内容行（`RenderHeaderRow` / `RenderDataRow`）在视觉上宽度不一致：
**边框行比内容行短 1 个视觉单位**。

---

## 根本原因

制表符横线 `─`（U+2500，Box Drawing）在我们的系统中视觉宽度为 **2**。
边框填充的计算公式是：

```
fillCharCount = fillVisualWidth / charWidth   // 整数除法
```

当 `totalWidth` 为**奇数**（如 93）时：

```
fillVisualWidth = 93 - fixedWidth   // 可能是奇数，如 89
fillCharCount   = 89 / 2 = 44       // 截断，实际填充 88 视觉单位
余数            = 89 - 44 × 2 = 1   // 丢失 1 个视觉单位
```

内容行（`RenderDataRow`）使用 **空格**（宽度 1）填充，可以精确命中任意整数宽度，不存在截断问题。
因此边框行始终比内容行少 1 个视觉单位，造成视觉错位。

### 触发条件

`totalWidth`（即 `console.ConsoleWidth`）为奇数 **且** 横线字符视觉宽度 > 1 时必然触发。
实测：`totalWidth = 93`，所有边框行均短 1 宽。

---

## 修复方案

**在 `TUITable.Render()` 入口处强制将总宽截断为偶数。**

```csharp
// 总宽强制偶数（制表符横线 ─ 是 2 宽，奇数总宽无法精确填充）
if (totalWidth % 2 != 0) totalWidth--;
```

约束在调用方（`Render`）统一处理，`BorderStyle` 各方法无需关心奇偶问题。

### 为什么不在 BorderStyle 里补余数？

曾考虑过在 `GenerateTop` / `GenerateBottom` / `GenerateDivider` 中
计算余数并用 `-`（ASCII 连字符，宽度 1）补足。
虽然技术上可行，但视觉上太丑——在 `┌───────┐` 末尾夹一个 `-` 不可接受。

约束输入比修补输出更干净。

---

## 修复位置

**`Assets/Scripts/Common/TUITable.cs`** — `Render()` 方法开头：

```csharp
public string[] Render(int totalWidth)
{
    // ...
    // 总宽强制偶数（制表符横线 ─ 是 2 宽，奇数总宽无法精确填充）
    if (totalWidth % 2 != 0) totalWidth--;
    // ...
}
```

---

## 验证

修复后，`totalWidth = 93`（奇数）传入时实际按 92 渲染：

| 行类型 | 修复前（93传入） | 修复后（92实际） |
|--------|---------------|---------------|
| 顶栏 `┌...┐` | 92（短1） | 92 ✓ |
| 分隔线 `├...┤` | 92（短1） | 92 ✓ |
| 底栏 `└...┘` | 92（短1） | 92 ✓ |
| 内容行 `│...│` | 93（多1） | 92 ✓ |

所有行统一对齐。
