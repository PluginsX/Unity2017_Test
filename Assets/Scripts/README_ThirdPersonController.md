# 第三人称角色控制系统使用说明

## 概述

这是一个完整的第三人称角色控制系统，包含角色移动、跳跃、攻击和摄像机控制功能。

## 组件说明

### 1. ThirdPersonController.cs
角色控制器，负责：
- 角色移动（行走、跑步、冲刺）
- 跳跃控制
- 攻击输入
- 动画参数更新

**参数配置：**
- `walkSpeed`: 行走速度（默认2）
- `runSpeed`: 跑步速度（默认5）
- `sprintSpeed`: 冲刺速度（默认8）
- `jumpHeight`: 跳跃高度（默认2）
- `moveSpeedParam`: Animator中移动速度参数名（默认"MoveSpeed"）
- `isGroundedParam`: Animator中是否在地面参数名（默认"IsGrounded"）
- `jumpTriggerParam`: Animator中跳跃触发器参数名（默认"Jump"）
- `attackTriggerParam`: Animator中攻击触发器参数名（默认"Attack"）
- `sprintBoolParam`: Animator中冲刺布尔参数名（默认"Sprint"）

### 2. ThirdPersonCamera.cs
第三人称摄像机控制器，负责：
- 跟随目标（角色）
- 鼠标控制旋转
- 滚轮缩放
- 碰撞检测

**参数配置：**
- `target`: 要跟随的目标Transform
- `distance`: 摄像机距离（默认5）
- `height`: 摄像机高度（默认2）
- `mouseSensitivity`: 鼠标灵敏度（默认2）

### 3. PlayerSetup.cs
自动设置脚本，用于：
- 自动配置CharacterController
- 自动设置Animator
- 自动创建和配置摄像机

## 使用方法

### 方法1：使用PlayerSetup（推荐）

1. 将 `PlayerSetup.cs` 添加到角色Prefab的根对象上
2. 在Inspector中配置参数
3. 将Prefab拖入场景即可使用

### 方法2：手动设置

1. 确保角色Prefab有：
   - CharacterController组件
   - Animator组件
   - ThirdPersonController组件

2. 创建或配置摄像机：
   - 添加Camera组件
   - 添加ThirdPersonCamera组件
   - 设置target为角色Transform

## AnimatorController配置

需要在AnimatorController中创建以下参数：

### Float参数：
- `MoveSpeed` (Float) - 移动速度，范围0-8

### Bool参数：
- `IsGrounded` (Bool) - 是否在地面
- `Sprint` (Bool) - 是否冲刺
- `InAir` (Bool) - 是否在空中（可选）

### Trigger参数：
- `Jump` (Trigger) - 跳跃触发器
- `Attack` (Trigger) - 攻击触发器

## 输入设置

确保在Unity的Input Manager中配置了以下输入：
- Horizontal (A/D 或 左/右箭头)
- Vertical (W/S 或 上/下箭头)
- Jump (空格键)
- Mouse X (鼠标X轴)
- Mouse Y (鼠标Y轴)
- Mouse ScrollWheel (鼠标滚轮)

## 控制说明

- **WASD**: 移动
- **Left Shift**: 冲刺
- **空格**: 跳跃
- **鼠标左键/J键**: 攻击
- **鼠标移动**: 旋转摄像机
- **鼠标滚轮**: 缩放摄像机距离

## 注意事项

1. 确保角色Prefab有正确的骨骼结构
2. AnimatorController需要正确配置动画状态和过渡
3. 摄像机需要正确设置Layer，避免与角色碰撞
4. 建议将角色设置为"Player"标签，方便摄像机自动查找

