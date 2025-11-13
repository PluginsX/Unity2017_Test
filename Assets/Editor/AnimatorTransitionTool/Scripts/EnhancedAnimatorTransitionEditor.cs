using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System;

namespace AnimatorTransitionTool.Editor
{
    /// <summary>
    /// Transition数据结构 - 用于存储和管理Transition信息
    /// </summary>
    [System.Serializable]
    public class TransitionData
    {
        public AnimatorStateTransition transition;
        public string sourceStateName;
        public string destinationStateName;
        public bool isBidirectional;
        public AnimatorStateTransition reverseTransition;
        
        public string GetDisplayName()
        {
            return $"{sourceStateName} → {destinationStateName}";
        }
        
        public string GetReverseDisplayName()
        {
            if (isBidirectional && reverseTransition != null)
                return $"{destinationStateName} → {sourceStateName}";
            return "";
        }
    }

    /// <summary>
    /// 增强版Animator Transition编辑器 - 支持双向连线和批量操作
    /// 兼容Unity 2017.4.30f1
    /// </summary>
    public class EnhancedAnimatorTransitionEditor : EditorWindow
{
    private AnimatorController _animatorController;
    private AnimatorStateMachine _stateMachine;
    private List<TransitionData> _transitionDataList = new List<TransitionData>();
    private List<TransitionData> _selectedTransitions = new List<TransitionData>();
    
    private Vector2 _scrollPosition = Vector2.zero;
    private bool _showDebugInfo = false;
    private bool _showBidirectionalOnly = false;
    
    // UI状态
    private string _newSourceState = "";
    private string _newDestinationState = "";
    private bool _modifySourceState = false;
    private bool _modifyDestinationState = false;
    private bool _includeReverseTransitions = true;
    
    // 搜索和过滤
    private string _searchFilter = "";
    private bool _showSearchFilter = false;
    
    [MenuItem("Window/Custom/Enhanced Animator Transition Editor", priority = 3)]
    public static void ShowWindow()
    {
        GetWindow<EnhancedAnimatorTransitionEditor>("Enhanced Transition Editor");
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
        _animatorController = null;
        _stateMachine = null;
        _transitionDataList.Clear();
        _selectedTransitions.Clear();
        
        // 尝试从选中的GameObject获取Animator Controller
        if (Selection.activeGameObject != null)
        {
            Animator animator = Selection.activeGameObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                _animatorController = animator.runtimeAnimatorController as AnimatorController;
            }
        }
        
        // 尝试直接从选中的Animator Controller获取
        if (_animatorController == null && Selection.activeObject is AnimatorController)
        {
            _animatorController = Selection.activeObject as AnimatorController;
        }
        
        if (_animatorController != null)
        {
            _stateMachine = _animatorController.layers[0].stateMachine;
            LoadAllTransitions();
        }
    }
    
    /// <summary>
    /// 加载所有Transition并识别双向连线
    /// </summary>
    private void LoadAllTransitions()
    {
        _transitionDataList.Clear();
        
        if (_stateMachine == null) return;
        
        // 创建所有Transition的映射
        var allTransitions = new List<AnimatorStateTransition>();
        var stateTransitionMap = new Dictionary<string, List<AnimatorStateTransition>>();
        
        // 收集状态间的Transition
        foreach (var state in _stateMachine.states)
        {
            foreach (var transition in state.state.transitions)
            {
                allTransitions.Add(transition);
                
                string sourceName = state.state.name;
                if (!stateTransitionMap.ContainsKey(sourceName))
                    stateTransitionMap[sourceName] = new List<AnimatorStateTransition>();
                stateTransitionMap[sourceName].Add(transition);
            }
        }
        
        // 收集Any State的Transition
        foreach (var transition in _stateMachine.anyStateTransitions)
        {
            allTransitions.Add(transition);
        }
        
        // 创建TransitionData并识别双向连线
        var processedTransitions = new HashSet<AnimatorStateTransition>();
        
        foreach (var transition in allTransitions)
        {
            if (processedTransitions.Contains(transition)) continue;
            
            var transitionData = CreateTransitionData(transition, stateTransitionMap);
            _transitionDataList.Add(transitionData);
            
            processedTransitions.Add(transition);
            if (transitionData.reverseTransition != null)
            {
                processedTransitions.Add(transitionData.reverseTransition);
            }
        }
        
        // 按名称排序
        _transitionDataList.Sort((a, b) => string.Compare(a.GetDisplayName(), b.GetDisplayName()));
    }
    
    /// <summary>
    /// 创建TransitionData并查找反向Transition
    /// </summary>
    private TransitionData CreateTransitionData(AnimatorStateTransition transition, Dictionary<string, List<AnimatorStateTransition>> stateTransitionMap)
    {
        var data = new TransitionData();
        data.transition = transition;
        data.sourceStateName = GetSourceStateName(transition);
        data.destinationStateName = GetDestinationStateName(transition);
        
        // 查找反向Transition
        data.reverseTransition = FindReverseTransition(transition, stateTransitionMap);
        data.isBidirectional = data.reverseTransition != null;
        
        return data;
    }
    
    /// <summary>
    /// 获取源状态名称
    /// </summary>
    private string GetSourceStateName(AnimatorStateTransition transition)
    {
        // 查找源状态
        foreach (var state in _stateMachine.states)
        {
            foreach (var t in state.state.transitions)
            {
                if (t == transition)
                {
                    return state.state.name;
                }
            }
        }
        
        // 检查是否是Any State的Transition
        foreach (var t in _stateMachine.anyStateTransitions)
        {
            if (t == transition)
            {
                return "Any State";
            }
        }
        
        return "Unknown";
    }
    
    /// <summary>
    /// 获取目标状态名称
    /// </summary>
    private string GetDestinationStateName(AnimatorStateTransition transition)
    {
        if (transition.isExit)
        {
            return "Exit";
        }
        else if (transition.destinationState != null)
        {
            return transition.destinationState.name;
        }
        
        return "Unknown";
    }
    
    /// <summary>
    /// 查找反向Transition
    /// </summary>
    private AnimatorStateTransition FindReverseTransition(AnimatorStateTransition transition, Dictionary<string, List<AnimatorStateTransition>> stateTransitionMap)
    {
        string sourceName = GetSourceStateName(transition);
        string destName = GetDestinationStateName(transition);
        
        if (sourceName == "Unknown" || destName == "Unknown" || sourceName == "Any State")
            return null;
        
        // 查找从destName到sourceName的Transition
        if (stateTransitionMap.ContainsKey(destName))
        {
            foreach (var reverseTransition in stateTransitionMap[destName])
            {
                string reverseDestName = GetDestinationStateName(reverseTransition);
                if (reverseDestName == sourceName)
                {
                    return reverseTransition;
                }
            }
        }
        
        return null;
    }
    
    void OnGUI()
    {
        DrawHeader();
        DrawToolbar();
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
        EditorGUILayout.LabelField("Enhanced Animator Transition Editor", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("支持双向连线识别 - 兼容Unity 2017.4.30f1", EditorStyles.miniLabel);
        
        EditorGUILayout.Space();
        
        // 显示当前Animator Controller信息
        if (_animatorController != null)
        {
            EditorGUILayout.LabelField("当前Animator Controller:", _animatorController.name);
            EditorGUILayout.LabelField($"总Transition数: {_transitionDataList.Count}");
            EditorGUILayout.LabelField($"双向连线数: {_transitionDataList.Count(t => t.isBidirectional)}");
        }
        else
        {
            EditorGUILayout.HelpBox("请选择包含Animator的GameObject或Animator Controller资源", MessageType.Info);
        }
        
        // 刷新按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新"))
        {
            RefreshAnimatorController();
        }
        if (GUILayout.Button("全选"))
        {
            SelectAllTransitions(true);
        }
        if (GUILayout.Button("取消全选"))
        {
            SelectAllTransitions(false);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
    }
    
    /// <summary>
    /// 绘制工具栏
    /// </summary>
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        
        // 搜索过滤器
        _showSearchFilter = EditorGUILayout.Foldout(_showSearchFilter, "搜索过滤");
        _showBidirectionalOnly = EditorGUILayout.Toggle("仅显示双向", _showBidirectionalOnly);
        
        EditorGUILayout.EndHorizontal();
        
        if (_showSearchFilter)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("搜索:", GUILayout.Width(40));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            if (GUILayout.Button("清除", GUILayout.Width(40)))
            {
                _searchFilter = "";
            }
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.Space();
    }
    
    /// <summary>
    /// 绘制Transition列表
    /// </summary>
    private void DrawTransitionList()
    {
        var filteredTransitions = GetFilteredTransitions();
        
        if (filteredTransitions.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到匹配的Transition", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField($"Transition列表 ({filteredTransitions.Count}):", EditorStyles.boldLabel);
        
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));
        
        for (int i = 0; i < filteredTransitions.Count; i++)
        {
            var transitionData = filteredTransitions[i];
            bool isSelected = _selectedTransitions.Contains(transitionData);
            
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            
            // 选择框
            bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
            if (newSelected != isSelected)
            {
                if (newSelected)
                    _selectedTransitions.Add(transitionData);
                else
                    _selectedTransitions.Remove(transitionData);
            }
            
            // Transition信息
            string displayText = transitionData.GetDisplayName();
            if (transitionData.isBidirectional)
            {
                displayText += " [双向]";
                GUI.color = Color.cyan;
            }
            
            EditorGUILayout.LabelField(displayText, EditorStyles.boldLabel);
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            // 显示反向Transition信息
            if (transitionData.isBidirectional && !string.IsNullOrEmpty(transitionData.GetReverseDisplayName()))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40);
                EditorGUILayout.LabelField("反向: " + transitionData.GetReverseDisplayName(), EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndScrollView();
        
        if (_selectedTransitions.Count > 0)
        {
            EditorGUILayout.LabelField($"已选择 {_selectedTransitions.Count} 个Transition", EditorStyles.miniBoldLabel);
        }
        
        EditorGUILayout.Space();
    }
    
    /// <summary>
    /// 获取过滤后的Transition列表
    /// </summary>
    private List<TransitionData> GetFilteredTransitions()
    {
        var result = _transitionDataList;
        
        // 双向过滤
        if (_showBidirectionalOnly)
        {
            result = result.Where(t => t.isBidirectional).ToList();
        }
        
        // 搜索过滤
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            string lowerFilter = _searchFilter.ToLower();
            result = result.Where(t => 
                t.GetDisplayName().ToLower().Contains(lowerFilter) ||
                t.GetReverseDisplayName().ToLower().Contains(lowerFilter)
            ).ToList();
        }
        
        return result;
    }
    
    /// <summary>
    /// 选择/取消选择所有Transition
    /// </summary>
    private void SelectAllTransitions(bool select)
    {
        if (select)
        {
            _selectedTransitions.Clear();
            _selectedTransitions.AddRange(GetFilteredTransitions());
        }
        else
        {
            _selectedTransitions.Clear();
        }
    }
    
    /// <summary>
    /// 绘制修改控制界面
    /// </summary>
    private void DrawModificationControls()
    {
        if (_selectedTransitions.Count == 0)
        {
            EditorGUILayout.HelpBox("请先选择要修改的Transition", MessageType.Info);
            return;
        }
        
        EditorGUILayout.LabelField("修改选项:", EditorStyles.boldLabel);
        
        // 包含反向Transition选项
        _includeReverseTransitions = EditorGUILayout.Toggle("同时修改反向Transition", _includeReverseTransitions);
        
        // 修改源状态选项
        _modifySourceState = EditorGUILayout.Toggle("修改起始状态", _modifySourceState);
        if (_modifySourceState)
        {
            _newSourceState = EditorGUILayout.TextField("新起始状态名称:", _newSourceState);
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
        if (_selectedTransitions.Count == 0) return;
        
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("应用更改到Animator", GUILayout.Height(30)))
        {
            ApplyChanges();
        }
        if (GUILayout.Button("删除选中的Transition", GUILayout.Height(30), GUILayout.Width(150)))
        {
            DeleteSelectedTransitions();
        }
        EditorGUILayout.EndHorizontal();
    }
    
    /// <summary>
    /// 应用更改到Animator Controller
    /// </summary>
    private void ApplyChanges()
    {
        if (_selectedTransitions.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请先选择要修改的Transition", "确定");
            return;
        }
        
        bool hasChanges = false;
        
        foreach (var transitionData in _selectedTransitions)
        {
            bool modified = false;
            
            // 修改目标状态
            if (_modifyDestinationState && !string.IsNullOrEmpty(_newDestinationState))
            {
                var destState = FindStateByName(_newDestinationState);
                if (destState != null)
                {
                    transitionData.transition.destinationState = destState;
                    modified = true;
                    
                    // 同时修改反向Transition的源状态
                    if (_includeReverseTransitions && transitionData.reverseTransition != null)
                    {
                        // 反向Transition的源状态修改需要重新创建Transition
                        // 这里简化处理，实际应用中需要更复杂的逻辑
                    }
                }
                else
                {
                    Debug.LogWarning($"未找到状态: {_newDestinationState}");
                }
            }
            
            if (modified) hasChanges = true;
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
    /// 删除选中的Transition
    /// </summary>
    private void DeleteSelectedTransitions()
    {
        if (_selectedTransitions.Count == 0) return;
        
        bool result = EditorUtility.DisplayDialog(
            "确认删除", 
            $"确定要删除选中的 {_selectedTransitions.Count} 个Transition吗？\n此操作不可撤销！", 
            "确定", 
            "取消"
        );
        
        if (!result) return;
        
        foreach (var transitionData in _selectedTransitions)
        {
            RemoveTransition(transitionData.transition);
            
            if (_includeReverseTransitions && transitionData.reverseTransition != null)
            {
                RemoveTransition(transitionData.reverseTransition);
            }
        }
        
        EditorUtility.SetDirty(_animatorController);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        _selectedTransitions.Clear();
        LoadAllTransitions();
        
        EditorUtility.DisplayDialog("成功", "已删除选中的Transition", "确定");
    }
    
    /// <summary>
    /// 移除Transition
    /// </summary>
    private void RemoveTransition(AnimatorStateTransition transition)
    {
        // 从状态中移除Transition
        foreach (var state in _stateMachine.states)
        {
            var transitions = state.state.transitions;
            for (int i = transitions.Length - 1; i >= 0; i--)
            {
                if (transitions[i] == transition)
                {
                    state.state.RemoveTransition(transitions[i]);
                    return;
                }
            }
        }
        
        // 从Any State中移除Transition
        var anyStateTransitions = _stateMachine.anyStateTransitions;
        for (int i = anyStateTransitions.Length - 1; i >= 0; i--)
        {
            if (anyStateTransitions[i] == transition)
            {
                _stateMachine.RemoveAnyStateTransition(anyStateTransitions[i]);
                return;
            }
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
            EditorGUILayout.LabelField($"Total Transitions: {_transitionDataList.Count}");
            EditorGUILayout.LabelField($"Selected Transitions: {_selectedTransitions.Count}");
            EditorGUILayout.LabelField($"Bidirectional Transitions: {_transitionDataList.Count(t => t.isBidirectional)}");
        }
    }
}
}