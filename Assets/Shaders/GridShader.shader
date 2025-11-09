Shader "Custom/GridShader"
{
    Properties
    {
        _GridSize ("Grid Size", Float) = 1.0
        _LineWidth ("Line Width", Range(0.001, 0.1)) = 0.01
        _BackgroundColor ("Background Color", Color) = (0.1, 0.1, 0.1, 1.0)
        _LineColor ("Line Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _MainTex ("Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        CGPROGRAM
        // 使用Surface Shader以支持标准光照和阴影
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        
        sampler2D _MainTex;
        // 注意：_MainTex_ST 由Unity自动生成，不需要手动定义
        float _GridSize;
        float _LineWidth;
        float4 _BackgroundColor;
        float4 _LineColor;
        half _Glossiness;
        half _Metallic;
        
        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };
        
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // 使用世界空间坐标计算栅格
            float3 worldPos = IN.worldPos;
            
            // 计算每个轴上的栅格位置（取小数部分，范围0-1）
            float2 gridXZ = frac(worldPos.xz / _GridSize);
            float2 gridXY = frac(worldPos.xy / _GridSize);
            float2 gridYZ = frac(worldPos.yz / _GridSize);
            
            // 计算到栅格线的距离（使用对称距离，确保线条居中）
            float2 distXZ = min(gridXZ, 1.0 - gridXZ) * _GridSize;
            float2 distXY = min(gridXY, 1.0 - gridXY) * _GridSize;
            float2 distYZ = min(gridYZ, 1.0 - gridYZ) * _GridSize;
            
            // 计算每个平面的最小距离
            float minDistXZ = min(distXZ.x, distXZ.y);
            float minDistXY = min(distXY.x, distXY.y);
            float minDistYZ = min(distYZ.x, distYZ.y);
            
            // 根据法线方向选择主要平面
            float3 absNormal = abs(IN.worldNormal);
            
            // 根据主要朝向选择对应的栅格距离
            float minDist = minDistXZ;
            if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
            {
                // 主要朝向Y轴（水平面），使用XZ平面栅格
                minDist = minDistXZ;
            }
            else if (absNormal.z > absNormal.x)
            {
                // 主要朝向Z轴，使用XY平面栅格
                minDist = minDistXY;
            }
            else
            {
                // 主要朝向X轴，使用YZ平面栅格
                minDist = minDistYZ;
            }
            
            // 使用平滑过渡而不是硬边缘
            float lineFactor = 1.0 - smoothstep(0.0, _LineWidth * 2.0, minDist);
            
            // 混合背景色和线颜色
            fixed4 gridColor = lerp(_BackgroundColor, _LineColor, lineFactor);
            
            // 可选：混合纹理
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            gridColor = lerp(gridColor, tex, 0.2);
            
            // 设置Surface Shader的输出
            o.Albedo = gridColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = gridColor.a;
        }
        ENDCG
    }
    
    FallBack "Diffuse"
}

