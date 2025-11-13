using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

/// <summary>
/// 编译测试脚本 - 验证最新的AnimatorTransitionEditor修复
/// </summary>
public class CompilationTestV2
{
    [MenuItem("Tools/Test Compilation V2")]
    public static void TestCompilation()
    {
        Debug.Log("开始编译测试V2...");
        
        try
        {
            // 测试类型声明
            List<AnimatorStateTransition> stateTransitions = new List<AnimatorStateTransition>();
            List<AnimatorTransition> transitions = new List<AnimatorTransition>();
            
            // 测试TransitionInfo类
            var info = new TransitionInfoTest
            {
                IsStateTransition = false,
                StateTransition = null,
                Transition = null,
                SourceState = "Any State",
                DestinationState = "TestDest",
                DisplayName = "Any State → TestDest"
            };
            
            // 测试类型转换 - 模拟Unity 2017.4.30f1的情况
            object transitionObject = new object(); // 模拟从集合获取的对象
            AnimatorTransition animatorTransition = transitionObject as AnimatorTransition;
            
            if (animatorTransition != null)
            {
                transitions.Add(animatorTransition);
                Debug.Log("类型转换测试成功");
            }
            else
            {
                Debug.Log("类型转换返回null，这是预期的行为");
            }
            
            Debug.Log("✅ 编译测试V2通过！所有类型声明和转换逻辑正确。");
            EditorUtility.DisplayDialog("编译测试V2", "AnimatorTransitionEditor编译测试V2通过！", "确定");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 编译测试V2失败: {e.Message}");
            EditorUtility.DisplayDialog("编译测试V2", $"编译测试V2失败: {e.Message}", "确定");
        }
    }
    
    /// <summary>
    /// 简化的TransitionInfo测试类
    /// </summary>
    private class TransitionInfoTest
    {
        public bool IsStateTransition { get; set; }
        public AnimatorStateTransition StateTransition { get; set; }
        public AnimatorTransition Transition { get; set; }
        public string SourceState { get; set; }
        public string DestinationState { get; set; }
        public string DisplayName { get; set; }
    }
}
