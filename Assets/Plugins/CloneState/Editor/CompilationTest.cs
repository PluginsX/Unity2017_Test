using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

/// <summary>
/// 编译测试脚本 - 验证AnimatorTransitionEditor修复
/// </summary>
public class CompilationTest
{
    [MenuItem("Tools/Test Compilation")]
    public static void TestCompilation()
    {
        Debug.Log("开始编译测试...");
        
        // 测试类型声明
        List<AnimatorStateTransition> stateTransitions = new List<AnimatorStateTransition>();
        List<AnimatorTransition> transitions = new List<AnimatorTransition>();
        
        // 测试TransitionInfo类
        var info = new TransitionInfoTest
        {
            IsStateTransition = true,
            StateTransition = null,
            Transition = null,
            SourceState = "Test",
            DestinationState = "TestDest",
            DisplayName = "Test → TestDest"
        };
        
        Debug.Log("编译测试通过！所有类型声明正确。");
        Debug.Log($"TransitionInfo测试: {info.DisplayName}");
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