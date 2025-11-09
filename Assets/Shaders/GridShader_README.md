# 栅格Shader使用说明

## 概述

`GridShader` 是一个用于在模型上显示栅格（网格）的Shader，无论模型面的朝向如何，都能正确显示栅格。

## 特性

- ✅ 使用世界空间坐标，确保栅格位置一致
- ✅ 自动检测面的朝向，选择最合适的栅格平面
- ✅ 平滑的线条边缘（使用smoothstep）
- ✅ 可自定义所有参数
- ✅ **支持标准光照模型**（Standard Shader）
- ✅ **完全支持阴影**（接收和投射阴影）
- ✅ 兼容Unity 2017 Built-in渲染管线

## Shader参数

在Material的Inspector中可以调整以下参数：

### Grid Size（栅格尺寸）
- **类型**: Float
- **默认值**: 1.0
- **说明**: 控制栅格的大小。值越大，栅格越大。

### Line Width（线宽）
- **类型**: Range(0.001, 0.1)
- **默认值**: 0.01
- **说明**: 控制栅格线的宽度。值越大，线条越粗。

### Background Color（背景色）
- **类型**: Color
- **默认值**: (0.1, 0.1, 0.1, 1.0) - 深灰色
- **说明**: 栅格背景的颜色。

### Line Color（线颜色）
- **类型**: Color
- **默认值**: (0.5, 0.5, 0.5, 1.0) - 中灰色
- **说明**: 栅格线的颜色。

### Texture（纹理）
- **类型**: 2D Texture
- **默认值**: white
- **说明**: 可选纹理，会轻微混合到栅格颜色中（20%混合度）。

### Smoothness（光滑度）
- **类型**: Range(0, 1)
- **默认值**: 0.5
- **说明**: 控制表面的光滑度，影响反射和高光。

### Metallic（金属度）
- **类型**: Range(0, 1)
- **默认值**: 0.0
- **说明**: 控制表面的金属度，影响反射特性。

## 使用方法

1. **创建Material**:
   - 在Project窗口中右键 → Create → Material
   - 将Shader设置为 `Custom/GridShader`

2. **应用到模型**:
   - 将Material拖到模型的MeshRenderer组件上
   - 或者直接在模型的MeshRenderer组件中选择该Material

3. **调整参数**:
   - 在Material的Inspector中调整各种参数
   - 实时预览效果

## 工作原理

1. **世界空间坐标**: Shader使用世界空间坐标来计算栅格位置，确保无论模型如何旋转或缩放，栅格都保持一致。

2. **多平面支持**: Shader计算了三个平面的栅格（XZ、XY、YZ），然后根据面的法线方向自动选择最合适的平面。

3. **平滑过渡**: 使用smoothstep函数实现线条的平滑边缘，避免锯齿。

## 示例设置

### 精细栅格（小网格）
- Grid Size: 0.5
- Line Width: 0.005
- Background Color: 深色（如黑色）
- Line Color: 亮色（如白色或黄色）

### 粗大栅格（大网格）
- Grid Size: 2.0
- Line Width: 0.02
- Background Color: 浅色（如浅灰色）
- Line Color: 深色（如深灰色或黑色）

### 高对比度栅格
- Grid Size: 1.0
- Line Width: 0.01
- Background Color: 黑色 (0, 0, 0, 1)
- Line Color: 白色 (1, 1, 1, 1)

## 光照和阴影

Shader使用Standard光照模型，完全支持：
- ✅ **接收光照**：会响应场景中的方向光、点光源等
- ✅ **接收阴影**：会显示其他物体投射的阴影
- ✅ **投射阴影**：可以投射阴影到其他物体上
- ✅ **高光反射**：根据Smoothness参数显示高光
- ✅ **金属反射**：根据Metallic参数显示金属反射效果

### 阴影设置

要启用阴影，确保：
1. 场景中有Directional Light或其他光源
2. Light组件的Shadows设置为"Hard Shadows"或"Soft Shadows"
3. 模型的MeshRenderer组件已启用"Receive Shadows"和"Cast Shadows"

## 注意事项

- Shader使用世界空间坐标，所以如果模型移动，栅格会保持在世界空间中的位置
- 如果模型有多个面朝向不同方向，每个面会显示对应朝向的栅格
- 线宽参数是相对于世界空间的，所以如果模型很大，可能需要调整线宽
- 光照会影响栅格的颜色，所以背景色和线颜色会与光照混合
- 调整Smoothness和Metallic可以改变表面的反射特性

## 兼容性

- Unity 2017.4.30f1 及以上版本
- 支持所有平台（PC、移动端等）

