using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Animator状态机Transition编辑器工具 - 兼容Unity 2017.4.30f1
/// 用于快速复制和修改Animator Controller中的Transition
/// </summary>
public class AnimatorTransitionEditor : EditorWindow
{
    private AnimatorController _animatorController;
    private AnimatorStateMachine _stateMachine;
    
    // 分别存储不同类型的Transition
    private List<AnimatorStateTransition> _selectedStateTransitions = new List<AnimatorStateTransition>();
    private List<AnimatorTransition> _selectedTransitions = new List<AnimatorTransition>();
    
    private List<AnimatorStateTransition> _allStateTransitions = new List<AnimatorStateTransition>();
    private List<AnimatorTransition> _allTransitions = new List<AnimatorTransition>();
    
    // 用于显示的统一列表
    private List<TransitionInfo> _allTransitionInfos = new List<TransitionInfo>();
    private List<TransitionInfo> _selectedTransitionInfos = new List<TransitionInfo>();
    
    private Vector2 _scrollPosition = Vector2.zero;
    private bool _showDebugInfo = false;
    
    // UI状态
    private string _newSourceState = "";
    private string _newDestinationState = "";
    private bool _modifySourceState = false;
    private bool _modifyDestinationState = false;
    
    /// <summary>
    /// Transition信息包装类 - Unity 2017.4.30f1兼容版本
    /// </summary>
    private class TransitionInfo
    {
        public bool IsStateTransition { get; set; }
        public bool IsAnyStateTransition { get; set; }
        public bool IsEntryTransition { get; set; }
        public AnimatorStateTransition StateTransition { get; set; }
        public AnimatorTransition Transition { get; set; }
        public object RawTransition { get; set; } // 用于存储Unity 2017.4.30f1中的原始transition对象
        public string SourceState { get; set; }
        public string DestinationState { get; set; }
        public string DisplayName { get; set; }
    }
    
    [MenuItem("Window/Animator Transition Editor")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorTransitionEditor>("Animator Transition Editor");
    }
    
    void OnEnable()
    {
        RefreshAnimatorController();
    }
    
    void OnSelectionChange()
    {
        RefreshAnimatorController();
    }
    
    /// <summary>
    /// 刷新Animator Controller引用
    /// </summary>
    private void RefreshAnimatorController()
    {
        // 尝试从选中的GameObject获取Animator Controller
        if (Selection.activeGameObject != null)
        {
            Animator animator = Selection.activeGameObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                _animatorController = animator.runtimeAnimatorController as AnimatorController;
                if (_animatorController != null)
                {
                    _stateMachine = _animatorController.layers[0].stateMachine;
                    LoadAllTransitions();
                }
            }
        }
        
        // 尝试直接从选中的Animator Controller获取
        if (_animatorController == null && Selection.activeObject is AnimatorController)
        {
            _animatorController = Selection.activeObject as AnimatorController;
            if (_animatorController != null)
            {
                _stateMachine = _animatorController.layers[0].stateMachine;
                LoadAllTransitions();
            }
        }
    }
    
    /// <summary>
    /// 加载所有Transition
    /// </summary>
    private void LoadAllTransitions()
    {
        _allStateTransitions.Clear();
        _allTransitions.Clear();
        _allTransitionInfos.Clear();
        _selectedStateTransitions.Clear();
        _selectedTransitions.Clear();
        _selectedTransitionInfos.Clear();
        
        if (_stateMachine == null) return;
        
        // 加载状态间的Transition
        foreach (var state in _stateMachine.states)
        {
            foreach (AnimatorStateTransition transition in state.state.transitions)
            {
                _allStateTransitions.Add(transition);
                
                var info = new TransitionInfo
                {
                    IsStateTransition = true,
                    IsAnyStateTransition = false,
                    IsEntryTransition = false,
                    StateTransition = transition,
                    Transition = null,
                    RawTransition = transition,
                    SourceState = state.state.name,
                    DestinationState = transition.isExit ? "Exit" : 
                                   (transition.destinationState != null ? transition.destinationState.name : "Unknown"),
                    DisplayName = $"{state.state.name} → {(transition.isExit ? "Exit" : (transition.destinationState != null ? transition.destinationState.name : "Unknown"))}"
                };
                _allTransitionInfos.Add(info);
            }
        }
        
        // 加载Any State的Transition - Unity 2017.4.30f1兼容处理
        // 在Unity 2017.4.30f1中，anyStateTransitions返回的类型不能直接转换为AnimatorTransition
        // 我们需要使用反射或其他方法来获取信息
        try
        {
            var anyStateTransitions = _stateMachine.anyStateTransitions;
            if (anyStateTransitions != null)
            {
                foreach (var transition in anyStateTransitions)
                {
                    // 使用反射获取transition信息，避免直接类型转换
                    var transitionType = transition.GetType();
                    var destinationStateProperty = transitionType.GetProperty("destinationState");
                    var destinationState = destinationStateProperty?.GetValue(transition) as AnimatorState;
                    
                    var info = new TransitionInfo
                    {
                        IsStateTransition = false,
                        IsAnyStateTransition = true,
                        IsEntryTransition = false,
                        StateTransition = null,
                        Transition = null, // 在Unity 2017.4.30f1中无法存储
                        RawTransition = transition, // 存储原始对象
                        SourceState = "Any State",
                        DestinationState = destinationState != null ? destinationState.name : "Unknown",
                        DisplayName = $"Any State → {(destinationState != null ? destinationState.name : "Unknown")}"
                    };
                    _allTransitionInfos.Add(info);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"无法加载Any State Transitions: {ex.Message}");
        }
        
        // 加载Entry Transition - Unity 2017.4.30f1兼容处理
        try
        {
            var entryTransitions = _stateMachine.entryTransitions;
            if (entryTransitions != null)
            {
                foreach (var transition in entryTransitions)
                {
                    // 使用反射获取transition信息，避免直接类型转换
                    var transitionType = transition.GetType();
                    var destinationStateProperty = transitionType.GetProperty("destinationState");
                    var destinationState = destinationStateProperty?.GetValue(transition) as AnimatorState;
                    
                    var info = new TransitionInfo
                    {
                        IsStateTransition = false,
                        IsAnyStateTransition = false,
                        IsEntryTransition = true,
                        StateTransition = null,
                        Transition = null, // 在Unity 2017.4.30f1中无法存储
                        RawTransition = transition, // 存储原始对象
                        SourceState = "Entry",
                        DestinationState = destinationState != null ? destinationState.name : "Unknown",
                        DisplayName = $"Entry → {(destinationState != null ? destinationState.name : "Unknown")}"
                    };
                    _allTransitionInfos.Add(info);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"无法加载Entry Transitions: {ex.Message}");
        }
    }
    
    void OnGUI()
    {
        DrawHeader();
        DrawTransitionList();
        DrawModificationControls();
        DrawApplyButton();
        DrawDebugInfo();
    }
    
    /// <summary>
    /// 绘制头部信息
    /// </summary>
    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animator Transition Editor", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("兼容Unity 2017.4.30f1", EditorStyles.miniLabel);
        
        EditorGUILayout.Space();
        
        // 显示当前Animator Controller信息
        if (_animatorController != null)
        {
            EditorGUILayout.LabelField("当前Animator Controller:", _animatorController.name);
        }
        else
        {
            EditorGUILayout.HelpBox("请选择包含Animator的GameObject或Animator Controller资源", MessageType.Info);
            
            // 刷新按钮
            if (GUILayout.Button("刷新"))
            {
                RefreshAnimatorController();
            }
        }
        
        EditorGUILayout.Space();
    }
    
    /// <summary>
    /// 绘制Transition列表
    /// </summary>
    private void DrawTransitionList()
    {
        if (_allTransitionInfos.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到任何Transition", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField($"找到 {_allTransitionInfos.Count} 个Transition:", EditorStyles.boldLabel);
        
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
        
        for (int i = 0; i < _allTransitionInfos.Count; i++)
        {
            var transitionInfo = _allTransitionInfos[i];
            bool isSelected = _selectedTransitionInfos.Contains(transitionInfo);
            
            EditorGUILayout.BeginHorizontal();
            
            // 选择框
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
            if (newSelected != isSelected)
            {
                if (newSelected)
                {
                    _selectedTransitionInfos.Add(transitionInfo);
                    if (transitionInfo.IsStateTransition)
                        _selectedStateTransitions.Add(transitionInfo.StateTransition);
                    // 注意：在Unity 2017.4.30f1中，Any State和Entry Transitions无法直接存储到_selectedTransitions
                    // 我们只处理State Transitions的直接编辑
                }
                else
                {
                    _selectedTransitionInfos.Remove(transitionInfo);
                    if (transitionInfo.IsStateTransition)
                        _selectedStateTransitions.Remove(transitionInfo.StateTransition);
                }
            }
            
            // Transition信息
            EditorGUILayout.LabelField(transitionInfo.DisplayName);
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        
        if (_selectedTransitionInfos.Count > 0)
        {
            EditorGUILayout.LabelField($"已选择 {_selectedTransitionInfos.Count} 个Transition", EditorStyles.miniBoldLabel);
        }
        
        EditorGUILayout.Space();
    }
    
    /// <summary>
    /// 绘制修改控制界面
    /// </summary>
    private void DrawModificationControls()
    {
        if (_selectedTransitionInfos.Count == 0)
        {
            EditorGUILayout.HelpBox("请先选择要修改的Transition", MessageType.Info);
            return;
        }
        
        EditorGUILayout.LabelField("修改选项:", EditorStyles.boldLabel);
        
        // 修改源状态选项
        _modifySourceState = EditorGUILayout.Toggle("修改起始状态", _modifySourceState);
        if (_modifySourceState)
        {
            _newSourceState = EditorGUILayout.TextField("新起始状态名称:", _newSourceState);
            EditorGUILayout.HelpBox("注意：源状态修改仅对State Transition有效", MessageType.Info);
        }
        
        // 修改目标状态选项
        _modifyDestinationState = EditorGUILayout.Toggle("修改终止状态", _modifyDestinationState);
        if (_modifyDestinationState)
        {
            _newDestinationState = EditorGUILayout.TextField("新终止状态名称:", _newDestinationState);
        }
        
        EditorGUILayout.Space();
    }
    
    /// <summary>
    /// 绘制应用按钮
    /// </summary>
    private void DrawApplyButton()
    {
        if (_selectedTransitionInfos.Count == 0) return;
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("应用更改到Animator", GUILayout.Height(30)))
        {
            ApplyChanges();
        }
    }
    
    /// <summary>
    /// 应用更改到Animator Controller - Unity 2017.4.30f1兼容版本
    /// </summary>
    private void ApplyChanges()
    {
        if (_selectedTransitionInfos.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先选择要修改的Transition", "确定");
            return;
        }
        
        bool hasChanges = false;
        
        // 处理选中的Transitions
        foreach (var transitionInfo in _selectedTransitionInfos)
        {
            if (transitionInfo.IsStateTransition && transitionInfo.StateTransition != null)
            {
                // 处理State Transitions
                bool modified = false;
                
                // 修改目标状态
                if (_modifyDestinationState && !string.IsNullOrEmpty(_newDestinationState))
                {
                    var destState = FindStateByName(_newDestinationState);
                    if (destState != null)
                    {
                        transitionInfo.StateTransition.destinationState = destState;
                        modified = true;
                    }
                    else
                    {
                        Debug.LogWarning($"未找到状态: {_newDestinationState}");
                    }
                }
                
                if (modified) hasChanges = true;
            }
            else if (transitionInfo.IsAnyStateTransition || transitionInfo.IsEntryTransition)
            {
                // 在Unity 2017.4.30f1中，Any State和Entry Transitions的修改需要特殊处理
                if (_modifyDestinationState && !string.IsNullOrEmpty(_newDestinationState))
                {
                    // 使用反射修改目标状态
                    try
                    {
                        var destState = FindStateByName(_newDestinationState);
                        if (destState != null && transitionInfo.RawTransition != null)
                        {
                            var transitionType = transitionInfo.RawTransition.GetType();
                            var destinationStateProperty = transitionType.GetProperty("destinationState");
                            if (destinationStateProperty != null)
                            {
                                destinationStateProperty.SetValue(transitionInfo.RawTransition, destState);
                                hasChanges = true;
                            }
                        }
                        else if (destState == null)
                        {
                            Debug.LogWarning($"未找到状态: {_newDestinationState}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"无法修改Transition目标状态: {ex.Message}");
                    }
                }
                
                // 源状态修改对Any State和Entry Transitions无效
                if (_modifySourceState)
                {
                    Debug.LogWarning("Any State和Entry Transition的源状态无法修改");
                }
            }
        }
        
        if (hasChanges)
        {
            EditorUtility.SetDirty(_animatorController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("成功", "更改已应用到Animator Controller", "确定");
            LoadAllTransitions(); // 重新加载Transition列表
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "没有应用任何更改", "确定");
        }
    }
    
    /// <summary>
    /// 根据名称查找状态
    /// </summary>
    private AnimatorState FindStateByName(string stateName)
    {
        if (_stateMachine == null) return null;
        
        foreach (var state in _stateMachine.states)
        {
            if (state.state.name == stateName)
            {
                return state.state;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 绘制调试信息
    /// </summary>
    private void DrawDebugInfo()
    {
        EditorGUILayout.Space();
        _showDebugInfo = EditorGUILayout.Foldout(_showDebugInfo, "调试信息");
        
        if (_showDebugInfo)
        {
            EditorGUILayout.LabelField($"Animator Controller: {_animatorController?.name ?? "None"}");
            EditorGUILayout.LabelField($"State Machine: {_stateMachine?.name ?? "None"}");
            EditorGUILayout.LabelField($"Total Transitions: {_allTransitions.Count}");
            EditorGUILayout.LabelField($"Selected Transitions: {_selectedTransitions.Count}");
        }
    }
}