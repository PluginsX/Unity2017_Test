# Animator Transition编辑器工具使用说明

## 概述

本工具为Unity 2017.4.30f1开发的动画状态机Transition编辑辅助工具，支持快速复制、识别和修改Animator Controller中的Transition，特别优化了双向连线的识别和管理。

## 功能特性

### 核心功能
- **快速识别Transition**: 自动识别Animator Controller中的所有Transition
- **双向连线支持**: 智能识别和标记双向Transition连线
- **批量操作**: 支持同时选择和修改多个Transition
- **实时应用**: 修改后可立即应用到Animator Controller中
- **搜索过滤**: 支持按名称搜索和过滤Transition

### 高级功能
- **反向Transition处理**: 可选择同时修改反向Transition
- **删除功能**: 支持批量删除选中的Transition
- **调试信息**: 提供详细的调试和状态信息
- **兼容性测试**: 内置兼容性验证功能

## 安装说明

1. 将所有脚本文件复制到 `Assets/Plugins/CloneState/Editor/` 目录下
2. 确保目录结构如下：
   ```
   Assets/
   └── Plugins/
       └── CloneState/
           └── Editor/
               ├── AnimatorTransitionEditor.cs
               ├── EnhancedAnimatorTransitionEditor.cs
               └── AnimatorTransitionEditorCompatibilityTest.cs
   ```

## 使用方法

### 1. 打开编辑器窗口

有两种方式打开编辑器：

**方法一**: 通过菜单栏
```
Window → Enhanced Transition Editor
```

**方法二**: 通过菜单栏（基础版本）
```
Window → Animator Transition Editor
```

### 2. 加载Animator Controller

工具会自动尝试从以下来源加载Animator Controller：
- 选中的GameObject上的Animator组件
- 直接选中的Animator Controller资源

如果自动加载失败，请确保：
- 选中了包含Animator的GameObject
- 或直接选中了Animator Controller资源

### 3. 识别和查看Transition

加载成功后，工具会显示：
- 总Transition数量
- 双向连线数量
- 详细的Transition列表

**双向连线识别**:
- 带有 `[双向]` 标记的表示存在反向Transition
- 会同时显示正向和反向Transition信息

### 4. 选择Transition

**单个选择**: 勾选对应Transition前的复选框

**批量选择**:
- 使用"全选"按钮选择所有过滤后的Transition
- 使用"取消全选"按钮清除所有选择

### 5. 过滤和搜索

**显示过滤**:
- 勾选"仅显示双向"只显示双向连线
- 展开"搜索过滤"可按名称搜索

**搜索功能**:
- 支持按Transition名称的部分匹配
- 同时搜索正向和反向Transition名称

### 6. 修改Transition

选择Transition后，可以设置修改选项：

**修改选项**:
- `同时修改反向Transition`: 勾选后会同时处理对应的反向Transition
- `修改起始状态`: 设置新的源状态名称
- `修改终止状态`: 设置新的目标状态名称

### 7. 应用更改

**应用更改**: 点击"应用更改到Animator"按钮将修改应用到Controller

**删除Transition**: 点击"删除选中的Transition"按钮删除选中的Transition（不可撤销）

## 兼容性测试

### 运行兼容性测试

通过菜单运行兼容性测试：
```
Tools → Test Animator Transition Editor Compatibility
```

测试内容包括：
- 基础API可用性
- Animator Controller访问
- State Machine操作
- Transition操作
- Editor窗口创建

### 创建测试数据

通过菜单创建测试用的Animator Controller：
```
Tools → Create Test Animator Controller
```

会创建包含以下内容的测试Controller：
- 4个状态：Idle, Walk, Run, Jump
- 多个Transitions（包括双向连线）

## 技术说明

### 兼容性设计

- **API兼容**: 使用Unity 2017.4.30f1支持的API
- **异常处理**: 完善的错误处理和用户提示
- **资源管理**: 自动清理测试资源
- **性能优化**: 避免频繁的资源重载

### 数据结构

**TransitionData类**:
```csharp
public class TransitionData
{
    public AnimatorStateTransition transition;     // Transition引用
    public string sourceStateName;                 // 源状态名称
    public string destinationStateName;             // 目标状态名称
    public bool isBidirectional;                   // 是否为双向
    public AnimatorStateTransition reverseTransition; // 反向Transition引用
}
```

### 核心算法

**双向连线识别**:
1. 遍历所有Transition建立状态映射
2. 对每个Transition查找对应的反向Transition
3. 标记双向关系并建立配对

**状态查找**:
- 支持普通状态、Any State和Exit状态
- 处理未知状态的异常情况

## 注意事项

### 使用限制

1. **Unity版本**: 仅支持Unity 2017.4.30f1
2. **备份建议**: 修改前建议备份Animator Controller
3. **撤销操作**: 删除操作不可撤销，请谨慎使用

### 常见问题

**Q: 工具无法识别我的Animator Controller？**
A: 请确保选中了正确的GameObject或Animator Controller资源，并点击"刷新"按钮。

**Q: 修改后没有生效？**
A: 请确保点击了"应用更改到Animator"按钮，并检查是否有错误提示。

**Q: 双向连线识别不准确？**
A: 检查Transition的方向性设置，确保确实是双向的Transition关系。

**Q: 兼容性测试失败？**
A: 请确认Unity版本为2017.4.30f1，并检查是否有其他插件冲突。

## 更新日志

### v1.0.0
- 初始版本发布
- 支持基础的Transition识别和修改
- 实现双向连线识别
- 添加兼容性测试功能

## 联系支持

如遇到问题或需要功能建议，请提供：
- Unity版本信息
- 错误日志
- 复现步骤
- 相关的Animator Controller文件（如果可能）