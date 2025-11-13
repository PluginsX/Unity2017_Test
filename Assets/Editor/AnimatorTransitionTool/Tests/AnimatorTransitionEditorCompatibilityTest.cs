using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Animator Transition编辑器兼容性测试脚本
/// 用于验证EnhancedAnimatorTransitionEditor在Unity 2017.4.30f1中的兼容性
/// </summary>
public static class AnimatorTransitionEditorCompatibilityTest
{
    [MenuItem("Tools/Test Animator Transition Editor Compatibility")]
    public static void TestCompatibility()
    {
        Debug.Log("=== 开始测试Animator Transition Editor兼容性 ===");
        
        try
        {
            // 测试1: 基础API可用性
            TestBasicAPIAvailability();
            
            // 测试2: Animator Controller创建和访问
            TestAnimatorControllerAccess();
            
            // 测试3: State Machine操作
            TestStateMachineOperations();
            
            // 测试4: Transition操作
            TestTransitionOperations();
            
            // 测试5: Editor窗口创建
            TestEditorWindowCreation();
            
            Debug.Log("=== 所有兼容性测试通过 ===");
            EditorUtility.DisplayDialog("兼容性测试", "所有兼容性测试已通过！\n工具可以在Unity 2017.4.30f1中正常使用。", "确定");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"兼容性测试失败: {e.Message}\n{e.StackTrace}");
            EditorUtility.DisplayDialog("兼容性测试失败", $"测试过程中发现兼容性问题:\n{e.Message}", "确定");
        }
    }
    
    /// <summary>
    /// 测试基础API可用性
    /// </summary>
    private static void TestBasicAPIAvailability()
    {
        Debug.Log("测试1: 基础API可用性");
        
        // 测试Unity版本
        string unityVersion = Application.unityVersion;
        Debug.Log($"Unity版本: {unityVersion}");
        
        // 测试Editor API
        bool editorAPIAvailable = true;
        try
        {
            var selection = Selection.activeGameObject;
            EditorWindow.GetWindow<AnimatorTransitionEditor>("Test");
        }
        catch
        {
            editorAPIAvailable = false;
        }
        
        if (!editorAPIAvailable)
        {
            throw new System.Exception("Editor API不可用");
        }
        
        Debug.Log("✓ 基础API可用性测试通过");
    }
    
    /// <summary>
    /// 测试Animator Controller访问
    /// </summary>
    private static void TestAnimatorControllerAccess()
    {
        Debug.Log("测试2: Animator Controller访问");
        
        // 创建测试用的Animator Controller
        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/TestAnimatorController.controller");
        
        if (controller == null)
        {
            throw new System.Exception("无法创建Animator Controller");
        }
        
        // 测试访问layers
        if (controller.layers.Length == 0)
        {
            throw new System.Exception("无法访问Animator Controller layers");
        }
        
        // 测试访问stateMachine
        var stateMachine = controller.layers[0].stateMachine;
        if (stateMachine == null)
        {
            throw new System.Exception("无法访问State Machine");
        }
        
        Debug.Log($"✓ Animator Controller访问测试通过 - Layers: {controller.layers.Length}, States: {stateMachine.states.Length}");
        
        // 清理测试资源
        AssetDatabase.DeleteAsset("Assets/TestAnimatorController.controller");
    }
    
    /// <summary>
    /// 测试State Machine操作
    /// </summary>
    private static void TestStateMachineOperations()
    {
        Debug.Log("测试3: State Machine操作");
        
        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/TestStateMachine.controller");
        var stateMachine = controller.layers[0].stateMachine;
        
        // 创建测试状态
        var state1 = stateMachine.AddState("State1");
        var state2 = stateMachine.AddState("State2");
        
        if (state1 == null || state2 == null)
        {
            throw new System.Exception("无法创建状态");
        }
        
        // 测试状态访问
        if (stateMachine.states.Length < 2)
        {
            throw new System.Exception("状态创建失败");
        }
        
        Debug.Log($"✓ State Machine操作测试通过 - 状态数量: {stateMachine.states.Length}");
        
        // 清理测试资源
        AssetDatabase.DeleteAsset("Assets/TestStateMachine.controller");
    }
    
    /// <summary>
    /// 测试Transition操作
    /// </summary>
    private static void TestTransitionOperations()
    {
        Debug.Log("测试4: Transition操作");
        
        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/TestTransition.controller");
        var stateMachine = controller.layers[0].stateMachine;
        
        var state1 = stateMachine.AddState("State1");
        var state2 = stateMachine.AddState("State2");
        
        // 创建Transition
        var transition = state1.AddTransition(state2);
        if (transition == null)
        {
            throw new System.Exception("无法创建Transition");
        }
        
        // 测试Transition属性
        transition.duration = 0.1f;
        transition.exitTime = 0.9f;
        
        // 创建反向Transition
        var reverseTransition = state2.AddTransition(state1);
        if (reverseTransition == null)
        {
            throw new System.Exception("无法创建反向Transition");
        }
        
        // 测试Transition访问
        var transitions = state1.transitions;
        if (transitions.Length == 0)
        {
            throw new System.Exception("无法访问Transitions");
        }
        
        Debug.Log($"✓ Transition操作测试通过 - State1 Transitions: {transitions.Length}");
        
        // 清理测试资源
        AssetDatabase.DeleteAsset("Assets/TestTransition.controller");
    }
    
    /// <summary>
    /// 测试Editor窗口创建
    /// </summary>
    private static void TestEditorWindowCreation()
    {
        Debug.Log("测试5: Editor窗口创建");
        
        try
        {
            // 测试基础编辑器窗口
            var window1 = EditorWindow.GetWindow<AnimatorTransitionEditor>("Test Basic");
            if (window1 == null)
            {
                throw new System.Exception("无法创建基础编辑器窗口");
            }
            window1.Close();
            
            // 测试增强版编辑器窗口
            var window2 = EditorWindow.GetWindow<EnhancedAnimatorTransitionEditor>("Test Enhanced");
            if (window2 == null)
            {
                throw new System.Exception("无法创建增强版编辑器窗口");
            }
            window2.Close();
            
            Debug.Log("✓ Editor窗口创建测试通过");
        }
        catch (System.Exception e)
        {
            throw new System.Exception($"Editor窗口创建失败: {e.Message}");
        }
    }
    
    [MenuItem("Tools/Create Test Animator Controller")]
    public static void CreateTestAnimatorController()
    {
        Debug.Log("创建测试用的Animator Controller");
        
        var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/TestController.controller");
        var stateMachine = controller.layers[0].stateMachine;
        
        // 创建多个状态
        var idleState = stateMachine.AddState("Idle");
        var walkState = stateMachine.AddState("Walk");
        var runState = stateMachine.AddState("Run");
        var jumpState = stateMachine.AddState("Jump");
        
        // 设置默认状态
        stateMachine.defaultState = idleState;
        
        // 创建Transitions
        idleState.AddTransition(walkState);
        walkState.AddTransition(idleState);
        walkState.AddTransition(runState);
        runState.AddTransition(walkState);
        idleState.AddTransition(jumpState);
        jumpState.AddTransition(idleState);
        
        // 创建一些双向Transitions
        var idleToWalk = idleState.AddTransition(walkState);
        var walkToIdle = walkState.AddTransition(idleState);
        
        // 设置Transition属性
        idleToWalk.duration = 0.2f;
        walkToIdle.duration = 0.1f;
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("✓ 测试Animator Controller创建完成");
        EditorUtility.DisplayDialog("测试创建完成", "已创建测试用的Animator Controller\n包含4个状态和多个Transitions", "确定");
        
        // 自动选中新创建的Controller
        Selection.activeObject = controller;
    }
}
