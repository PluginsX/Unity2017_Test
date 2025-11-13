using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

namespace AnimatorTransitionTool
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
            // 双向Transition使用←→，单向使用→
            string arrow = isBidirectional ? " ←→ " : " → "; 
            return string.Format("{0}{1}{2}", sourceStateName, arrow, destinationStateName);
        }
        
        public string GetReverseDisplayName()
        {
            // 不再使用反向显示，双向Transition已用←→表示
            return "";
        }
    }
    
    /// <summary>
    /// 状态信息数据结构 - 包含状态和路径信息
    /// </summary>
    [System.Serializable]
    public class StateInfo
    {
        public AnimatorState state;
        public AnimatorStateMachine stateMachine;
        public AnimatorStateMachine subStateMachine; // 如果是子状态机节点，存储子状态机引用
        public string path;
        public string displayName;
        public bool isSubStateMachine; // 标识是否为子状态机节点
        
        public StateInfo(AnimatorState state, AnimatorStateMachine stateMachine, string path)
        {
            this.state = state;
            this.stateMachine = stateMachine;
            this.subStateMachine = null;
            this.path = path;
            this.isSubStateMachine = false;
            
            // 直接使用state.name，这应该已经包含Unity自动添加的后缀数字
            // 不再使用ToString()方法，因为它会包含额外的类信息
            this.displayName = string.IsNullOrEmpty(path) ? state.name : string.Format("{0}/{1}", path, state.name);
        }
        
        public StateInfo(AnimatorStateMachine subStateMachine, AnimatorStateMachine parentStateMachine, string path)
        {
            this.state = null;
            this.stateMachine = parentStateMachine;
            this.subStateMachine = subStateMachine;
            this.path = path;
            this.isSubStateMachine = true;
            this.displayName = string.IsNullOrEmpty(path) ? subStateMachine.name : string.Format("{0}/{1}", path, subStateMachine.name);
        }
    }
    
    /// <summary>
    /// 树形节点结构 - 用于表示状态机的层级关系
    /// </summary>
    [System.Serializable]
    public class StateTreeNode
    {
        public string name;
        public string fullPath;
        public AnimatorState state;
        public AnimatorStateMachine stateMachine;
        public List<StateTreeNode> children = new List<StateTreeNode>();
        public bool isExpanded = false;
        public bool isStateMachine = false;
        
        public StateTreeNode(string name, string fullPath, AnimatorState state = null, AnimatorStateMachine stateMachine = null)
        {
            this.name = name;
            this.fullPath = fullPath;
            this.state = state;
            this.stateMachine = stateMachine;
            this.isStateMachine = (stateMachine != null);
        }
    }

    /// <summary>
    /// 增强版Animator Transition编辑器 - 支持双向连线和批量操作
    /// 兼容Unity 2017.4.30f1
    /// </summary>
    public class AnimatorTransitionTool : EditorWindow
{
    private AnimatorController _animatorController;
    private AnimatorStateMachine _stateMachine;
    private List<TransitionData> _transitionDataList = new List<TransitionData>();
    private List<TransitionData> _selectedTransitions = new List<TransitionData>();
    
    // 状态列表缓存
    private List<StateInfo> _allStates = new List<StateInfo>();
    private List<string> _stateNameList = new List<string>();
    private StateTreeNode _stateTreeRoot = null;
    
    private enum ControllerSource
    {
        None,
        Manual,
        PickedFromSelection
    }

    private ControllerSource _controllerSource = ControllerSource.None;
    
    private Vector2 _scrollPosition = Vector2.zero;
    private bool _showDebugInfo = false;
    private bool _showBidirectionalOnly = false;
    private bool _mergeBidirectional = false; // 合并双向，默认不勾选
    
    // UI状态
    private string _newSourceState = "";
    private string _newDestinationState = "";
    private bool _modifySourceState = false;
    private bool _modifyDestinationState = false;
    
    // 下拉列表状态
    private int _sourceStatePopupIndex = 0;
    private int _destinationStatePopupIndex = 0;
    private bool _showSourceStatePopup = false;
    private bool _showDestinationStatePopup = false;
    
    // 输入框焦点状态
    private bool _sourceStateFieldFocused = false;
    private bool _destinationStateFieldFocused = false;
    
    // 树形列表滚动位置
    private Vector2 _sourceStateTreeScroll = Vector2.zero;
    private Vector2 _destinationStateTreeScroll = Vector2.zero;
    
    // 树形列表位置和大小
    private Rect _sourceStateTreeRect = Rect.zero;
    private Rect _destinationStateTreeRect = Rect.zero;
    
    // 纯色背景纹理
    private Texture2D _solidBackgroundTexture = null;
    private Texture2D _borderTexture = null;
    
    // 搜索和过滤
    private string _searchFilter = "";
    
    [MenuItem("Window/Custom/AnimatorTransitionEditor", priority = 3)]
    public static void ShowWindow()
    {
        GetWindow<AnimatorTransitionTool>("TransitionEditor");
    }
    
    void OnEnable()
    {

        ReloadControllerData();
    }
    
    private void ReloadControllerData()
    {
        _transitionDataList.Clear();
        _selectedTransitions.Clear();
        _allStates.Clear();
        _stateNameList.Clear();
        _stateTreeRoot = null;
        
        if (_animatorController != null)
        {
            _stateMachine = GetRootStateMachine(_animatorController);
            if (_stateMachine != null)
            {
                CollectAllStates(_stateMachine, "");
                _stateTreeRoot = BuildStateTree(_stateMachine, "");
                LoadAllTransitions();
                return;
            }
        }
        
        _stateMachine = null;
    }
    
    /// <summary>
    /// 递归收集所有状态和子状态机（包括子状态机中的状态）
    /// </summary>
    private void CollectAllStates(AnimatorStateMachine stateMachine, string parentPath)
    {
        if (stateMachine == null) return;
        
        string currentPath = string.IsNullOrEmpty(parentPath) ? "" : parentPath;
        
        // 收集当前状态机中的状态
        foreach (var childState in stateMachine.states)
        {
            if (childState.state != null)
            {
                StateInfo stateInfo = new StateInfo(childState.state, stateMachine, currentPath);
                _allStates.Add(stateInfo);
                _stateNameList.Add(stateInfo.displayName);
            }
        }
        
        // 收集当前状态机中的子状态机节点（作为可选择的节点）
        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine != null)
            {
                string childPath = string.IsNullOrEmpty(parentPath) 
                    ? childStateMachine.stateMachine.name 
                    : string.Format("{0}/{1}", parentPath, childStateMachine.stateMachine.name);
                
                // 将子状态机节点也添加到列表中
                // 注意：StateInfo的path参数应该是currentPath（父状态机的path），这样displayName会是"{currentPath}/{subStateMachine.name}"
                // 但为了与BuildStateTree中的fullPath保持一致，我们需要确保displayName的格式正确
                StateInfo subStateMachineInfo = new StateInfo(childStateMachine.stateMachine, stateMachine, currentPath);
                _allStates.Add(subStateMachineInfo);
                _stateNameList.Add(subStateMachineInfo.displayName);
                
                // 递归收集子状态机中的状态
                CollectAllStates(childStateMachine.stateMachine, childPath);
            }
        }
    }
    
    /// <summary>
    /// 构建状态树形结构
    /// </summary>
    private StateTreeNode BuildStateTree(AnimatorStateMachine stateMachine, string parentPath)
    {
        if (stateMachine == null) return null;
        
        string currentPath = string.IsNullOrEmpty(parentPath) ? "" : parentPath;
        string nodeName = string.IsNullOrEmpty(parentPath) ? "Root" : stateMachine.name;
        
        // 对于子状态机节点，fullPath应该与StateInfo.displayName格式一致
        // 在CollectAllStates中，子状态机的path是currentPath（父状态机的path），displayName是"{currentPath}/{subStateMachine.name}"
        // 在BuildStateTree中，parentPath是递归传递的完整路径
        // 对于第一层子状态机，parentPath就是子状态机名称，所以fullPath = parentPath
        // 对于更深层的子状态机，parentPath已经是"{currentPath}/{subStateMachine.name}"，所以fullPath = parentPath
        string fullPath = string.IsNullOrEmpty(parentPath) ? "" : parentPath;
        
        StateTreeNode node = new StateTreeNode(nodeName, fullPath, null, stateMachine);
        
        // 添加当前状态机中的状态
        foreach (var childState in stateMachine.states)
        {
            if (childState.state != null)
            {
                string statePath = string.IsNullOrEmpty(currentPath) 
                    ? childState.state.name 
                    : string.Format("{0}/{1}", currentPath, childState.state.name);
                StateTreeNode stateNode = new StateTreeNode(childState.state.name, statePath, childState.state, stateMachine);
                node.children.Add(stateNode);
            }
        }
        
        // 递归添加子状态机
        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine != null)
            {
                // 计算子状态机的路径，与CollectAllStates中的逻辑保持一致
                // 在CollectAllStates中，childPath是"{currentPath}/{childStateMachine.stateMachine.name}"（如果currentPath不为空）
                // 或者就是childStateMachine.stateMachine.name（如果currentPath为空）
                string childPath = string.IsNullOrEmpty(currentPath) 
                    ? childStateMachine.stateMachine.name 
                    : string.Format("{0}/{1}", currentPath, childStateMachine.stateMachine.name);
                StateTreeNode childNode = BuildStateTree(childStateMachine.stateMachine, childPath);
                if (childNode != null)
                {
                    node.children.Add(childNode);
                }
            }
        }
        
        return node;
    }
    
    private void SetAnimatorController(AnimatorController controller, ControllerSource source)
    {
        bool controllerChanged = _animatorController != controller;
        
        _animatorController = controller;
        _controllerSource = controller != null ? source : ControllerSource.None;
        
        if (controllerChanged || controller == null)
        {
            ReloadControllerData();
        }
        
        Repaint();
    }
    
    private AnimatorController GetAnimatorControllerFromCurrentSelection()
    {
        if (Selection.activeGameObject != null)
        {
            Animator animator = Selection.activeGameObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                return animator.runtimeAnimatorController as AnimatorController;
            }
        }
        
        if (Selection.activeObject is AnimatorController)
        {
            return Selection.activeObject as AnimatorController;
        }
        
        return null;
    }
    
    private AnimatorStateMachine GetRootStateMachine(AnimatorController controller)
    {
        if (controller == null)
        {
            return null;
        }
        
        AnimatorControllerLayer[] layers = controller.layers;
        if (layers == null || layers.Length == 0)
        {
            return null;
        }
        
        return layers[0].stateMachine;
    }
    
    private string GetControllerSourceDescription()
    {
        switch (_controllerSource)
        {
            case ControllerSource.Manual:
                return "手动指定";
            case ControllerSource.PickedFromSelection:
                return "拾取自选择";
            default:
                return "未设置";
        }
    }
    
    // 保存stateTransitionMap以便GetSourceStateName使用
    private Dictionary<string, List<AnimatorStateTransition>> _cachedStateTransitionMap = null;
    
    /// <summary>
    /// 加载所有Transition并识别双向连线（支持子状态机）
    /// </summary>
    private void LoadAllTransitions()
    {
        _transitionDataList.Clear();
        _entryTransitionMap.Clear();
        _entryStateTransitionMap.Clear();
        _cachedStateTransitionMap = null;
        
        if (_stateMachine == null) return;
        
        // 创建所有Transition的映射（使用显示名称作为key）
        var allTransitions = new List<AnimatorStateTransition>();
        var stateTransitionMap = new Dictionary<string, List<AnimatorStateTransition>>();
        
        // 递归收集所有状态机中的Transition
        CollectTransitionsFromStateMachine(_stateMachine, "", allTransitions, stateTransitionMap);
        
        // 缓存stateTransitionMap以便GetSourceStateName使用
        _cachedStateTransitionMap = stateTransitionMap;
        
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
    
    // Entry Transition映射表，用于跟踪Entry Transition
    // 使用两个映射表：一个用于AnimatorTransition，一个用于AnimatorStateTransition
    private Dictionary<AnimatorTransition, string> _entryTransitionMap = new Dictionary<AnimatorTransition, string>();
    private Dictionary<AnimatorStateTransition, string> _entryStateTransitionMap = new Dictionary<AnimatorStateTransition, string>();
    
    /// <summary>
    /// 递归收集状态机中的所有Transition
    /// </summary>
    private void CollectTransitionsFromStateMachine(AnimatorStateMachine stateMachine, string parentPath, 
        List<AnimatorStateTransition> allTransitions, Dictionary<string, List<AnimatorStateTransition>> stateTransitionMap)
    {
        if (stateMachine == null) return;
        
        string currentPath = string.IsNullOrEmpty(parentPath) ? "" : parentPath;
        
        // 收集Entry Transition（所有层级的状态机都有Entry）
        // 注意：Entry Transition是AnimatorTransition类型，但实际运行时可能是AnimatorStateTransition
        string entryDisplayName = string.IsNullOrEmpty(currentPath) ? "Entry" : string.Format("{0}/Entry", currentPath);
        if (stateMachine.entryTransitions != null && stateMachine.entryTransitions.Length > 0)
        {
            foreach (AnimatorTransition transition in stateMachine.entryTransitions)
            {
                // 检查是否是AnimatorStateTransition类型
                // 在Unity中，entryTransitions数组虽然声明为AnimatorTransition[]，但实际元素可能是AnimatorStateTransition
                // 使用GetType()来检查实际类型
                if (transition.GetType() == typeof(AnimatorStateTransition))
                {
                    // 使用反射或直接转换（因为我们已经确认了类型）
                    // 由于编译器不允许直接转换，我们使用System.Convert或者直接强制转换
                    AnimatorStateTransition stateTransition = (AnimatorStateTransition)(object)transition;
                    if (stateTransition != null)
                    {
                        // 是AnimatorStateTransition类型，添加到列表和两个映射表中
                        allTransitions.Add(stateTransition);
                        _entryTransitionMap[transition] = entryDisplayName;
                        _entryStateTransitionMap[stateTransition] = entryDisplayName;
                        
                        // 映射Entry Transition
                        if (!stateTransitionMap.ContainsKey(entryDisplayName))
                            stateTransitionMap[entryDisplayName] = new List<AnimatorStateTransition>();
                        stateTransitionMap[entryDisplayName].Add(stateTransition);
                    }
                }
                else
                {
                    // 如果不是AnimatorStateTransition，只记录到AnimatorTransition映射表中（用于显示，但不添加到列表）
                    _entryTransitionMap[transition] = entryDisplayName;
                }
            }
        }
        
        // 收集当前状态机中状态的Transition
        foreach (var state in stateMachine.states)
        {
            if (state.state != null)
            {
                string sourceDisplayName = string.IsNullOrEmpty(currentPath) 
                    ? state.state.name 
                    : string.Format("{0}/{1}", currentPath, state.state.name);
                
                foreach (var transition in state.state.transitions)
                {
                    allTransitions.Add(transition);
                    
                    // 检查Transition的目标是否在子状态机外
                    // 如果是，应该将Transition映射到子状态机名称上
                    bool shouldMapToSubStateMachine = false;
                    string subStateMachineDisplayName = null;
                    
                    // 获取源状态所在的状态机
                    AnimatorStateMachine sourceStateMachine = GetStateMachineForState(state.state);
                    
                    // 如果源状态在子状态机中（不是根状态机），且目标在子状态机外，应该映射到子状态机名称
                    if (sourceStateMachine != null && sourceStateMachine != _stateMachine)
                    {
                        // 源状态在子状态机中
                        bool targetIsOutside = false;
                        
                        if (transition.destinationState != null)
                        {
                            // 检查目标状态是否在源状态机外
                            AnimatorStateMachine destStateMachine = GetStateMachineForState(transition.destinationState);
                            if (destStateMachine == null || destStateMachine != sourceStateMachine)
                            {
                                // 目标状态在源状态机外（可能在父状态机或其他子状态机中）
                                targetIsOutside = true;
                            }
                        }
                        else if (transition.destinationStateMachine != null)
                        {
                            // 目标是子状态机节点，肯定在源状态机外
                            targetIsOutside = true;
                        }
                        else if (transition.isExit)
                        {
                            // 目标是Exit，肯定在源状态机外
                            targetIsOutside = true;
                        }
                        
                        if (targetIsOutside)
                        {
                            // 查找对应的子状态机信息
                            foreach (var subStateInfo in _allStates)
                            {
                                if (subStateInfo.isSubStateMachine && subStateInfo.subStateMachine == sourceStateMachine)
                                {
                                    shouldMapToSubStateMachine = true;
                                    subStateMachineDisplayName = subStateInfo.displayName;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (shouldMapToSubStateMachine && !string.IsNullOrEmpty(subStateMachineDisplayName))
                    {
                        // 映射到子状态机名称
                        if (!stateTransitionMap.ContainsKey(subStateMachineDisplayName))
                            stateTransitionMap[subStateMachineDisplayName] = new List<AnimatorStateTransition>();
                        stateTransitionMap[subStateMachineDisplayName].Add(transition);
                    }
                    else
                    {
                        // 映射到状态名称
                        if (!stateTransitionMap.ContainsKey(sourceDisplayName))
                            stateTransitionMap[sourceDisplayName] = new List<AnimatorStateTransition>();
                        stateTransitionMap[sourceDisplayName].Add(transition);
                    }
                }
            }
        }
        
        // 收集Any State的Transition（所有层级的状态机都有Any State）
        string anyStateDisplayName = string.IsNullOrEmpty(currentPath) ? "Any State" : string.Format("{0}/Any State", currentPath);
        if (stateMachine.anyStateTransitions != null && stateMachine.anyStateTransitions.Length > 0)
        {
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                allTransitions.Add(transition);
                
                // 映射Any State Transition
                if (!stateTransitionMap.ContainsKey(anyStateDisplayName))
                    stateTransitionMap[anyStateDisplayName] = new List<AnimatorStateTransition>();
                stateTransitionMap[anyStateDisplayName].Add(transition);
            }
        }
        
        // 递归收集子状态机中的Transition
        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine != null)
            {
                string childPath = string.IsNullOrEmpty(currentPath) 
                    ? childStateMachine.stateMachine.name 
                    : string.Format("{0}/{1}", currentPath, childStateMachine.stateMachine.name);
                CollectTransitionsFromStateMachine(childStateMachine.stateMachine, childPath, allTransitions, stateTransitionMap);
            }
        }
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
    /// 获取源状态名称（支持子状态机，显示路径）
    /// </summary>
    private string GetSourceStateName(AnimatorStateTransition transition)
    {
        // 首先检查是否是Entry Transition
        // 先检查AnimatorStateTransition映射表（更精确）
        if (_entryStateTransitionMap.ContainsKey(transition))
        {
            return _entryStateTransitionMap[transition];
        }
        
        // 如果AnimatorStateTransition映射表中没有，尝试检查AnimatorTransition映射表
        // 需要遍历查找，因为Dictionary的键类型不同
        foreach (var kvp in _entryTransitionMap)
        {
            // 如果transition是AnimatorStateTransition，且kvp.Key也是同一个对象（作为AnimatorTransition）
            if (kvp.Key == transition)
            {
                return kvp.Value;
            }
        }
        
        // 检查是否是Any State的Transition（所有层级的状态机都有Any State）
        // 需要递归查找所有状态机
        string anyStateName = FindAnyStateTransitionSource(transition, _stateMachine, "");
        if (!string.IsNullOrEmpty(anyStateName))
        {
            return anyStateName;
        }
        
        // 首先检查stateTransitionMap，看是否有子状态机映射的Transition
        // 但不再直接返回子状态机名称，而是继续查找实际的状态
        // 这样可以确保子状态机中的状态名正确显示
        
        // 使用已收集的状态列表查找
        foreach (var stateInfo in _allStates)
        {
            if (stateInfo.state != null)
            {
                var transitions = stateInfo.state.transitions;
                for (int i = 0; i < transitions.Length; i++)
                {
                    if (transitions[i] == transition)
                    {
                        // 获取源状态所在的状态机
                        AnimatorStateMachine sourceStateMachine = GetStateMachineForState(stateInfo.state);
                        
                        // 修复：总是显示实际的状态名称，而不是子状态机名称
                        // 这样可以确保子状态机中的状态名正确显示
                        
                        return stateInfo.displayName;
                    }
                }
            }
        }
        
        // 尝试直接获取源状态名称（如果可能）
        AnimatorState sourceState = GetSourceState(transition);
        if (sourceState != null)
        {
            // 直接使用sourceState.name，避免ToString()带来的额外类信息
            return sourceState.name;
        }
        
        return "Unknown";
    }
    
    /// <summary>
    /// 递归查找Any State Transition的源状态名称
    /// </summary>
    private string FindAnyStateTransitionSource(AnimatorStateTransition transition, AnimatorStateMachine stateMachine, string parentPath)
    {
        if (stateMachine == null) return null;
        
        string currentPath = string.IsNullOrEmpty(parentPath) ? "" : parentPath;
        string anyStateDisplayName = string.IsNullOrEmpty(currentPath) ? "Any State" : string.Format("{0}/Any State", currentPath);
        
        // 检查当前状态机的Any State Transition
        if (stateMachine.anyStateTransitions != null)
        {
            foreach (var t in stateMachine.anyStateTransitions)
            {
                if (t == transition)
                {
                    return anyStateDisplayName;
                }
            }
        }
        
        // 递归检查子状态机
        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine != null)
            {
                string childPath = string.IsNullOrEmpty(currentPath) 
                    ? childStateMachine.stateMachine.name 
                    : string.Format("{0}/{1}", currentPath, childStateMachine.stateMachine.name);
                string result = FindAnyStateTransitionSource(transition, childStateMachine.stateMachine, childPath);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 获取目标状态名称（支持子状态机，显示路径）
    /// </summary>
    private string GetDestinationStateName(AnimatorStateTransition transition)
    {
        if (transition.isExit)
        {
            // Exit属于源状态所在的状态机
            // 需要找到源状态，然后确定Exit的路径
            AnimatorState sourceState = GetSourceState(transition);
            if (sourceState != null)
            {
                AnimatorStateMachine sourceStateMachine = GetStateMachineForState(sourceState);
                if (sourceStateMachine != null && sourceStateMachine != _stateMachine)
                {
                    // 源状态在子状态机中，Exit应该是子状态机的Exit
                    foreach (var subStateInfo in _allStates)
                    {
                        if (subStateInfo.isSubStateMachine && subStateInfo.subStateMachine == sourceStateMachine)
                        {
                            return string.Format("{0}/Exit", subStateInfo.displayName);
                        }
                    }
                }
            }
            // 默认返回根状态机的Exit
            return "Exit";
        }
        else if (transition.destinationState != null)
        {
            // 查找目标状态在已收集列表中的显示名称
            foreach (var stateInfo in _allStates)
            {
                if (stateInfo.state == transition.destinationState)
                {
                    return stateInfo.displayName;
                }
            }
            // 直接使用destinationState.name，避免ToString()带来的额外类信息
            return transition.destinationState.name;
        }
        else if (transition.destinationStateMachine != null)
        {
            // 查找目标子状态机在已收集列表中的显示名称
            foreach (var stateInfo in _allStates)
            {
                if (stateInfo.isSubStateMachine && stateInfo.subStateMachine == transition.destinationStateMachine)
                {
                    return stateInfo.displayName;
                }
            }
            // 如果没找到，返回子状态机名称（带路径前缀）
            return transition.destinationStateMachine.name;
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
        
        if (sourceName == "Unknown" || destName == "Unknown")
            return null;
        
        // Exit不能作为反向Transition的源或目标
        if (destName == "Exit" || destName.Contains("/Exit"))
            return null;
        if (sourceName == "Exit" || sourceName.Contains("/Exit"))
            return null;
        
        // Any State不能作为反向Transition的源（Any State是特殊的，不能有反向）
        if (sourceName == "Any State" || sourceName.Contains("/Any State"))
            return null;
        
        // Entry不能作为反向Transition的目标（Entry是特殊的，不能有反向）
        if (destName == "Entry" || destName.Contains("/Entry"))
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
    
    /// <summary>
    /// 获取Transition的源状态（支持子状态机）
    /// </summary>
    private AnimatorState GetSourceState(AnimatorStateTransition transition)
    {
        if (_stateMachine == null || transition == null)
        {
            return null;
        }
        
        // 使用已收集的状态列表查找
        foreach (var stateInfo in _allStates)
        {
            if (stateInfo.state != null)
            {
                var transitions = stateInfo.state.transitions;
                for (int i = 0; i < transitions.Length; i++)
                {
                    if (transitions[i] == transition)
                    {
                        return stateInfo.state;
                    }
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 获取状态所在的状态机（支持子状态机）
    /// </summary>
    private AnimatorStateMachine GetStateMachineForState(AnimatorState state)
    {
        if (state == null) return null;
        
        foreach (var stateInfo in _allStates)
        {
            if (stateInfo.state == state)
            {
                return stateInfo.stateMachine;
            }
        }
        
        return null;
    }
    
    private bool IsAnyStateTransition(AnimatorStateTransition transition)
    {
        if (_stateMachine == null || transition == null)
        {
            return false;
        }
        
        // 递归检查所有层级的状态机
        return IsAnyStateTransitionRecursive(transition, _stateMachine);
    }
    
    /// <summary>
    /// 递归检查是否是Any State Transition
    /// </summary>
    private bool IsAnyStateTransitionRecursive(AnimatorStateTransition transition, AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null || transition == null)
        {
            return false;
        }
        
        // 检查当前状态机的Any State Transition
        if (stateMachine.anyStateTransitions != null)
        {
            foreach (var t in stateMachine.anyStateTransitions)
            {
                if (t == transition)
                {
                    return true;
                }
            }
        }
        
        // 递归检查子状态机
        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine != null)
            {
                if (IsAnyStateTransitionRecursive(transition, childStateMachine.stateMachine))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 将Transition移动到新的源状态（支持跨状态机）
    /// </summary>
    private AnimatorStateTransition MoveTransitionToNewSource(AnimatorStateTransition transition, AnimatorState newSourceState)
    {
        if (transition == null || newSourceState == null)
        {
            return transition;
        }
        
        AnimatorState currentSource = GetSourceState(transition);
        if (currentSource == null || currentSource == newSourceState)
        {
            return transition;
        }
        
        // 获取新源状态所在的状态机
        AnimatorStateMachine newSourceStateMachine = GetStateMachineForState(newSourceState);
        if (newSourceStateMachine == null)
        {
            Debug.LogWarning(string.Format("无法找到状态 {0} 所在的状态机", newSourceState.name));
            return transition;
        }
        
        AnimatorStateTransition newTransition;
        if (transition.isExit)
        {
            newTransition = newSourceState.AddExitTransition();
        }
        else if (transition.destinationStateMachine != null)
        {
            newTransition = newSourceState.AddTransition(transition.destinationStateMachine);
        }
        else if (transition.destinationState != null)
        {
            newTransition = newSourceState.AddTransition(transition.destinationState);
        }
        else
        {
            Debug.LogWarning("Transition没有有效的目标状态");
            return transition;
        }
        
        CopyTransitionSettings(transition, newTransition);
        currentSource.RemoveTransition(transition);
        return newTransition;
    }
    
    /// <summary>
    /// 将AnyState Transition迁移到新的源状态
    /// </summary>
    private AnimatorStateTransition MoveAnyStateTransitionToNewSource(AnimatorStateTransition anyStateTransition, AnimatorState newSourceState)
    {
        if (anyStateTransition == null || newSourceState == null || _stateMachine == null)
        {
            return null;
        }
        
        // 保存目标状态信息
        bool isExit = anyStateTransition.isExit;
        AnimatorState destinationState = anyStateTransition.destinationState;
        AnimatorStateMachine destinationStateMachine = anyStateTransition.destinationStateMachine;
        
        // 创建新Transition
        AnimatorStateTransition newTransition;
        if (isExit)
        {
            newTransition = newSourceState.AddExitTransition();
        }
        else if (destinationStateMachine != null)
        {
            newTransition = newSourceState.AddTransition(destinationStateMachine);
        }
        else if (destinationState != null)
        {
            newTransition = newSourceState.AddTransition(destinationState);
        }
        else
        {
            Debug.LogWarning("AnyState Transition没有有效的目标状态");
            return null;
        }
        
        // 复制所有设置（这会复制条件）
        CopyTransitionSettings(anyStateTransition, newTransition);
        
        // 删除原AnyState Transition
        var anyStateTransitions = _stateMachine.anyStateTransitions;
        for (int i = anyStateTransitions.Length - 1; i >= 0; i--)
        {
            if (anyStateTransitions[i] == anyStateTransition)
            {
                _stateMachine.RemoveAnyStateTransition(anyStateTransitions[i]);
                break;
            }
        }
        
        return newTransition;
    }
    
    private void CopyTransitionSettings(AnimatorStateTransition sourceTransition, AnimatorStateTransition targetTransition)
    {
        if (sourceTransition == null || targetTransition == null)
        {
            return;
        }
        
        targetTransition.canTransitionToSelf = sourceTransition.canTransitionToSelf;
        targetTransition.duration = sourceTransition.duration;
        targetTransition.exitTime = sourceTransition.exitTime;
        targetTransition.hasExitTime = sourceTransition.hasExitTime;
        targetTransition.hasFixedDuration = sourceTransition.hasFixedDuration;
        targetTransition.offset = sourceTransition.offset;
        targetTransition.orderedInterruption = sourceTransition.orderedInterruption;
        targetTransition.mute = sourceTransition.mute;
        targetTransition.solo = sourceTransition.solo;
        targetTransition.isExit = sourceTransition.isExit;
        targetTransition.interruptionSource = sourceTransition.interruptionSource;
        targetTransition.destinationState = sourceTransition.destinationState;
        targetTransition.destinationStateMachine = sourceTransition.destinationStateMachine;
        
        while (targetTransition.conditions.Length > 0)
        {
            var lastCondition = targetTransition.conditions[targetTransition.conditions.Length - 1];
            targetTransition.RemoveCondition(lastCondition);
        }
        
        var sourceConditions = sourceTransition.conditions;
        for (int i = 0; i < sourceConditions.Length; i++)
        {
            var condition = sourceConditions[i];
            targetTransition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }
    }
    
    void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        // 顶部固定内容
        DrawHeader();
        DrawToolbar();
        
        // 列表区域 - 占据剩余空间
        DrawTransitionList();
        
        // 弹性空间，将底部内容推到底部
        GUILayout.FlexibleSpace();
        
        // 底部固定内容
        DrawModificationControls();
        DrawApplyButton();
        DrawDebugInfo();
        
        EditorGUILayout.EndVertical();
    }
    
    /// <summary>
    /// 绘制头部信息
    /// </summary>
    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enhanced Animator Transition Editor", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        AnimatorController controllerInput = EditorGUILayout.ObjectField("Animator Controller", _animatorController, typeof(AnimatorController), false) as AnimatorController;
        if (controllerInput != _animatorController)
        {
            SetAnimatorController(controllerInput, ControllerSource.Manual);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("拾取当前选择", GUILayout.Width(150)))
        {
            AnimatorController picked = GetAnimatorControllerFromCurrentSelection();
            if (picked != null)
            {
                SetAnimatorController(picked, ControllerSource.PickedFromSelection);
            }
            else
            {
                EditorUtility.DisplayDialog("未找到Animator Controller", "当前选择未包含Animator Controller。", "确定");
            }
        }
        if (GUILayout.Button("清除", GUILayout.Width(80)))
        {
            SetAnimatorController(null, ControllerSource.None);
        }
        if (GUILayout.Button("重新加载", GUILayout.Width(100)))
        {
            ReloadControllerData();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 显示当前Animator Controller信息
        if (_animatorController != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Format("Transition: {0}", _transitionDataList.Count));
            EditorGUILayout.LabelField(string.Format("双向连线数: {0}", _transitionDataList.Count(t => t.isBidirectional)));
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("请通过上方对象框或“拾取当前选择”按钮手动指定Animator Controller。", MessageType.Info);
        }
        
        EditorGUILayout.BeginHorizontal();
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
        
        // 搜索标签（靠左）
        EditorGUILayout.LabelField("搜索:", GUILayout.Width(40), GUILayout.ExpandWidth(false));
        
        // 搜索输入框，填充满中间可用空间
        _searchFilter = EditorGUILayout.TextField(_searchFilter, GUILayout.ExpandWidth(true));
        
        // 清除按钮（靠右）
        if (GUILayout.Button("清除", GUILayout.Width(40), GUILayout.ExpandWidth(false)))
        {
            _searchFilter = "";
        }
        
        // 合并双向选项
        _mergeBidirectional = EditorGUILayout.Toggle("合并双向", _mergeBidirectional, GUILayout.ExpandWidth(false));
        
        // 只有勾选了合并双向才显示"只看双向"选项
        if (_mergeBidirectional)
        {
            _showBidirectionalOnly = EditorGUILayout.Toggle("只看双向", _showBidirectionalOnly, GUILayout.ExpandWidth(false));
        }
        else
        {
            // 如果取消合并双向，也取消只看双向
            _showBidirectionalOnly = false;
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
    }
    
    /// <summary>
    /// 绘制Transition列表
    /// </summary>
    private void DrawTransitionList()
    {
        var filteredTransitions = GetFilteredTransitions();
        
        EditorGUILayout.LabelField(string.Format("Transition列表 ({0}):", filteredTransitions.Count), EditorStyles.boldLabel);
        
        if (filteredTransitions.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到匹配的Transition", MessageType.Warning);
            if (_selectedTransitions.Count > 0)
            {
                EditorGUILayout.LabelField(string.Format("已选择 {0} 个Transition", _selectedTransitions.Count), EditorStyles.miniBoldLabel);
            }
            return;
        }
        
        // 计算所有过渡的源状态和目标状态名称的最大宽度
        float maxSourceStateWidth = 0f;
        float maxDestStateWidth = 0f;
        float maxAllowedWidth = 500f; // 设置一个合理的最大宽度限制，避免UI过度拉伸
        
        foreach (var transitionData in filteredTransitions)
        {
            float sourceWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(transitionData.sourceStateName)).x;
            float destWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(transitionData.destinationStateName)).x;
            maxSourceStateWidth = Mathf.Max(maxSourceStateWidth, sourceWidth);
            maxDestStateWidth = Mathf.Max(maxDestStateWidth, destWidth);
        }
        
        // 应用最大宽度限制，但确保最小宽度以避免过窄
        maxSourceStateWidth = Mathf.Clamp(maxSourceStateWidth, 100f, maxAllowedWidth);
        maxDestStateWidth = Mathf.Clamp(maxDestStateWidth, 100f, maxAllowedWidth);
        
        // 使用ExpandHeight让ScrollView占据剩余空间
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
        
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
            
            // Transition信息 - 优化布局：起始状态靠左，箭头居中，终止状态靠右
            if (transitionData.isBidirectional)
            {
                GUI.color = Color.cyan;
            }
            
            // 起始状态名靠左，使用计算出的最大宽度确保所有名称都能完整显示
            EditorGUILayout.LabelField(transitionData.sourceStateName, EditorStyles.boldLabel, GUILayout.Width(maxSourceStateWidth), GUILayout.ExpandWidth(false));
            
            // 弹性空间，让箭头居中
            GUILayout.FlexibleSpace();
            
            // 箭头居中显示，减少占用宽度
            string arrow = transitionData.isBidirectional ? "←→" : "→";
            float arrowWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(arrow)).x;
            EditorGUILayout.LabelField(arrow, EditorStyles.boldLabel, GUILayout.Width(arrowWidth), GUILayout.ExpandWidth(false));
            
            // 弹性空间，让终止状态靠右
            GUILayout.FlexibleSpace();
            
            // 终止状态名靠右，使用计算出的最大宽度确保所有名称都能完整显示
            EditorGUILayout.LabelField(transitionData.destinationStateName, EditorStyles.boldLabel, GUILayout.Width(maxDestStateWidth), GUILayout.ExpandWidth(false));
            
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndScrollView();
        
        if (_selectedTransitions.Count > 0)
        {
            EditorGUILayout.LabelField(string.Format("已选择 {0} 个Transition", _selectedTransitions.Count), EditorStyles.miniBoldLabel);
        }
    }
    
    /// <summary>
    /// 获取过滤后的Transition列表
    /// </summary>
    private List<TransitionData> GetFilteredTransitions()
    {
        var result = new List<TransitionData>();
        
        // 根据合并双向选项处理Transition列表
        if (_mergeBidirectional)
        {
            // 合并模式：使用原始列表
            result = _transitionDataList.ToList();
        }
        else
        {
            // 不合并模式：将双向Transition拆分成两个单向Transition
            var processedReverseTransitions = new HashSet<AnimatorStateTransition>();
            
            foreach (var transitionData in _transitionDataList)
            {
                if (transitionData.isBidirectional && transitionData.reverseTransition != null)
                {
                    // 双向Transition：创建两个单向TransitionData
                    // 正向Transition
                    var forwardData = new TransitionData
                    {
                        transition = transitionData.transition,
                        sourceStateName = transitionData.sourceStateName,
                        destinationStateName = transitionData.destinationStateName,
                        isBidirectional = false,
                        reverseTransition = null
                    };
                    result.Add(forwardData);
                    
                    // 反向Transition
                    if (!processedReverseTransitions.Contains(transitionData.reverseTransition))
                    {
                        var reverseData = new TransitionData
                        {
                            transition = transitionData.reverseTransition,
                            sourceStateName = transitionData.destinationStateName,
                            destinationStateName = transitionData.sourceStateName,
                            isBidirectional = false,
                            reverseTransition = null
                        };
                        result.Add(reverseData);
                        processedReverseTransitions.Add(transitionData.reverseTransition);
                    }
                }
                else
                {
                    // 单向Transition：直接添加
                    result.Add(transitionData);
                }
            }
        }
        
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
                (t.isBidirectional && !string.IsNullOrEmpty(t.GetReverseDisplayName()) && t.GetReverseDisplayName().ToLower().Contains(lowerFilter))
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
        EditorGUILayout.Space();
        
        if (_selectedTransitions.Count == 0)
        {
            return;
        }
        
        EditorGUILayout.LabelField("修改选项:", EditorStyles.boldLabel);
        
        // 修改源状态选项
        _modifySourceState = EditorGUILayout.Toggle("修改起始状态", _modifySourceState);
        if (_modifySourceState)
        {
            EditorGUILayout.BeginHorizontal();
            Rect labelRect = EditorGUILayout.GetControlRect();
            labelRect = EditorGUI.PrefixLabel(labelRect, new GUIContent("新起始状态名称:"));
            
            // 检测焦点变化
            bool wasFocused = _sourceStateFieldFocused;
            GUI.SetNextControlName("SourceStateField");
            _newSourceState = EditorGUI.TextField(labelRect, _newSourceState);
            
            string focusedControl = GUI.GetNameOfFocusedControl();
            _sourceStateFieldFocused = (focusedControl == "SourceStateField");
            
            // 如果获得焦点，自动显示列表，并关闭另一个列表
            if (_sourceStateFieldFocused && !wasFocused && _stateTreeRoot != null)
            {
                _showSourceStatePopup = true;
                // 关闭目标状态提示列表
                _showDestinationStatePopup = false;
                Repaint();
            }
            
            if (GUILayout.Button(_showSourceStatePopup ? "▲" : "▼", GUILayout.Width(25)))
            {
                _showSourceStatePopup = !_showSourceStatePopup;
            }
            EditorGUILayout.EndHorizontal();
            
            // 绘制树形列表（包含Entry选项）
            if (_showSourceStatePopup && _stateTreeRoot != null)
            {
                DrawStateTreeList(_stateTreeRoot, ref _sourceStateTreeScroll, ref _sourceStateTreeRect, labelRect, 
                    (string selectedPath) => {
                        _newSourceState = selectedPath;
                        _showSourceStatePopup = false;
                        GUI.FocusControl(null);
                        Repaint();
                    }, false, true);
            }
        }
        else
        {
            _showSourceStatePopup = false;
            _sourceStateFieldFocused = false;
        }
        
        // 修改目标状态选项
        _modifyDestinationState = EditorGUILayout.Toggle("修改终止状态", _modifyDestinationState);
        if (_modifyDestinationState)
        {
            EditorGUILayout.BeginHorizontal();
            Rect labelRect = EditorGUILayout.GetControlRect();
            labelRect = EditorGUI.PrefixLabel(labelRect, new GUIContent("新终止状态名称:"));
            
            // 检测焦点变化
            bool wasFocused = _destinationStateFieldFocused;
            GUI.SetNextControlName("DestinationStateField");
            _newDestinationState = EditorGUI.TextField(labelRect, _newDestinationState);
            
            string focusedControl = GUI.GetNameOfFocusedControl();
            _destinationStateFieldFocused = (focusedControl == "DestinationStateField");
            
            // 如果获得焦点，自动显示列表，并关闭另一个列表
            if (_destinationStateFieldFocused && !wasFocused && _stateTreeRoot != null)
            {
                _showDestinationStatePopup = true;
                // 关闭源状态提示列表
                _showSourceStatePopup = false;
                Repaint();
            }
            
            if (GUILayout.Button(_showDestinationStatePopup ? "▲" : "▼", GUILayout.Width(25)))
            {
                _showDestinationStatePopup = !_showDestinationStatePopup;
            }
            EditorGUILayout.EndHorizontal();
            
            // 绘制树形列表（包含Exit选项）
            if (_showDestinationStatePopup && _stateTreeRoot != null)
            {
                DrawStateTreeList(_stateTreeRoot, ref _destinationStateTreeScroll, ref _destinationStateTreeRect, labelRect, 
                    (string selectedPath) => {
                        _newDestinationState = selectedPath;
                        _showDestinationStatePopup = false;
                        GUI.FocusControl(null);
                        Repaint();
                    }, true, false);
            }
        }
        else
        {
            _showDestinationStatePopup = false;
            _destinationStateFieldFocused = false;
        }
        
        // 处理点击外部关闭列表
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Vector2 mousePos = Event.current.mousePosition;
            if (_showSourceStatePopup && _sourceStateTreeRect.width > 0 && !_sourceStateTreeRect.Contains(mousePos))
            {
                _showSourceStatePopup = false;
                Repaint();
            }
            if (_showDestinationStatePopup && _destinationStateTreeRect.width > 0 && !_destinationStateTreeRect.Contains(mousePos))
            {
                _showDestinationStatePopup = false;
                Repaint();
            }
        }
    }
    
    /// <summary>
    /// 绘制状态树形列表
    /// </summary>
    private void DrawStateTreeList(StateTreeNode root, ref Vector2 scrollPos, ref Rect listRect, Rect inputRect, 
        System.Action<string> onSelect, bool includeExit = false, bool includeEntry = false)
    {
        if (root == null) return;
        
        // 计算列表位置和大小
        // 提示框左侧与本工具左侧对齐
        // 提示框底部对齐输入框顶部
        float listWidth = 300f;
        float contentHeight = GetTreeHeight(root, 0, includeExit, includeEntry);
        float listHeight = Mathf.Min(300f, contentHeight + 20f);
        
        // 左侧与工具窗口左侧对齐（考虑边距）
        float listX = 0f;
        
        // 底部对齐输入框顶部
        float listY = inputRect.y - listHeight;
        
        // 确保列表不超出窗口底部
        if (listY < 0) listY = 0f;
        
        listRect = new Rect(listX, listY, listWidth, listHeight);
        
        // 绘制不透明的纯色背景
        if (_solidBackgroundTexture == null)
        {
            _solidBackgroundTexture = new Texture2D(1, 1);
            _solidBackgroundTexture.SetPixel(0, 0, new Color(0.22f, 0.22f, 0.22f, 1f)); // Unity编辑器背景色（不透明）
            _solidBackgroundTexture.Apply();
        }
        GUI.DrawTexture(listRect, _solidBackgroundTexture);
        
        // 绘制边框线
        if (_borderTexture == null)
        {
            _borderTexture = new Texture2D(1, 1);
            _borderTexture.SetPixel(0, 0, new Color(0.4f, 0.4f, 0.4f, 1f)); // 边框颜色
            _borderTexture.Apply();
        }
        GUI.DrawTexture(new Rect(listRect.x, listRect.y, listRect.width, 1), _borderTexture); // 上边框
        GUI.DrawTexture(new Rect(listRect.x, listRect.yMax - 1, listRect.width, 1), _borderTexture); // 下边框
        GUI.DrawTexture(new Rect(listRect.x, listRect.y, 1, listRect.height), _borderTexture); // 左边框
        GUI.DrawTexture(new Rect(listRect.xMax - 1, listRect.y, 1, listRect.height), _borderTexture); // 右边框
        
        // 绘制滚动视图
        Rect scrollViewRect = new Rect(listRect.x + 2, listRect.y + 2, listRect.width - 4, listRect.height - 4);
        Rect viewRect = new Rect(0, 0, listWidth - 20, contentHeight);
        scrollPos = GUI.BeginScrollView(scrollViewRect, scrollPos, viewRect);
        
        float yOffset = 0;
        
        // 绘制Entry选项（如果需要）
        if (includeEntry)
        {
            Rect entryRect = new Rect(0, yOffset, listWidth - 20, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(entryRect, "Entry", EditorStyles.label))
            {
                onSelect("Entry");
            }
            yOffset += EditorGUIUtility.singleLineHeight + 2;
        }
        
        // 绘制Exit选项（如果需要）
        if (includeExit)
        {
            Rect exitRect = new Rect(0, yOffset, listWidth - 20, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(exitRect, "Exit", EditorStyles.label))
            {
                onSelect("Exit");
            }
            yOffset += EditorGUIUtility.singleLineHeight + 2;
        }
        
        // 绘制树形节点（传递includeExit和includeEntry参数）
        DrawTreeNode(root, 0, yOffset, listWidth - 20, onSelect, includeExit, includeEntry);
        
        GUI.EndScrollView();
    }
    
    /// <summary>
    /// 绘制树形节点
    /// </summary>
    private float DrawTreeNode(StateTreeNode node, int depth, float yOffset, float width, System.Action<string> onSelect, bool includeExit = false, bool includeEntry = false)
    {
        if (node == null) return yOffset;
        
        float indent = depth * 15f;
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float currentY = yOffset;
        
        // 只显示状态机节点和状态节点，不显示Root
        if (node.name != "Root")
        {
            Rect nodeRect = new Rect(indent, currentY, width - indent, lineHeight);
            
            // 如果是状态机，显示展开/折叠按钮，同时可点击选择
            if (node.isStateMachine && node.stateMachine != null)
            {
                Rect foldoutRect = new Rect(indent, currentY, 15f, lineHeight);
                if (node.children.Count > 0)
                {
                    node.isExpanded = EditorGUI.Foldout(foldoutRect, node.isExpanded, "", false);
                }
                else
                {
                    // 没有子节点时，留出空白空间
                    GUI.Label(foldoutRect, "");
                }
                
                Rect labelRect = new Rect(indent + 15f, currentY, width - indent - 15f, lineHeight);
                // 子状态机节点可点击选择
                if (GUI.Button(labelRect, node.name, EditorStyles.boldLabel))
                {
                    onSelect(node.fullPath);
                }
                
                currentY += lineHeight + 2;
                
                // 如果展开，显示该状态机的Entry、Any State和Exit选项
                if (node.isExpanded)
                {
                    string stateMachinePath = string.IsNullOrEmpty(node.fullPath) ? "" : node.fullPath;
                    
                    // Entry选项
                    if (includeEntry)
                    {
                        string entryPath = string.IsNullOrEmpty(stateMachinePath) ? "Entry" : string.Format("{0}/Entry", stateMachinePath);
                        Rect entryRect = new Rect(indent + 15f, currentY, width - indent - 15f, lineHeight);
                        if (GUI.Button(entryRect, "  Entry", EditorStyles.label))
                        {
                            onSelect(entryPath);
                        }
                        currentY += lineHeight + 2;
                    }
                    
                    // Any State选项
                    string anyStatePath = string.IsNullOrEmpty(stateMachinePath) ? "Any State" : string.Format("{0}/Any State", stateMachinePath);
                    Rect anyStateRect = new Rect(indent + 15f, currentY, width - indent - 15f, lineHeight);
                    if (GUI.Button(anyStateRect, "  Any State", EditorStyles.label))
                    {
                        onSelect(anyStatePath);
                    }
                    currentY += lineHeight + 2;
                    
                    // Exit选项
                    if (includeExit)
                    {
                        string exitPath = string.IsNullOrEmpty(stateMachinePath) ? "Exit" : string.Format("{0}/Exit", stateMachinePath);
                        Rect exitRect = new Rect(indent + 15f, currentY, width - indent - 15f, lineHeight);
                        if (GUI.Button(exitRect, "  Exit", EditorStyles.label))
                        {
                            onSelect(exitPath);
                        }
                        currentY += lineHeight + 2;
                    }
                }
            }
            else if (node.state != null)
            {
                // 状态节点可点击
                if (GUI.Button(nodeRect, node.name, EditorStyles.label))
                {
                    onSelect(node.fullPath);
                }
                currentY += lineHeight + 2;
            }
        }
        
        // 如果展开，绘制子节点
        if (node.isExpanded || node.name == "Root")
        {
            foreach (var child in node.children)
            {
                currentY = DrawTreeNode(child, depth + (node.name == "Root" ? 0 : 1), currentY, width, onSelect, includeExit, includeEntry);
            }
        }
        
        return currentY;
    }
    
    /// <summary>
    /// 计算树形结构的总高度
    /// </summary>
    private float GetTreeHeight(StateTreeNode node, int depth, bool includeExit, bool includeEntry = false)
    {
        if (node == null) return 0;
        
        float height = 0;
        float lineHeight = EditorGUIUtility.singleLineHeight + 2;
        
        // 根级别的Entry和Exit（如果需要）
        if (depth == 0)
        {
            if (includeEntry)
            {
                height += lineHeight;
            }
            if (includeExit)
            {
                height += lineHeight;
            }
        }
        
        if (node.name != "Root")
        {
            height += lineHeight;
            
            // 如果是展开的状态机节点，需要添加Entry、Any State和Exit的高度
            if (node.isStateMachine && node.isExpanded)
            {
                if (includeEntry)
                {
                    height += lineHeight; // Entry
                }
                height += lineHeight; // Any State（总是显示）
                if (includeExit)
                {
                    height += lineHeight; // Exit
                }
            }
        }
        
        if (node.isExpanded || node.name == "Root")
        {
            foreach (var child in node.children)
            {
                height += GetTreeHeight(child, depth + (node.name == "Root" ? 0 : 1), includeExit, includeEntry);
            }
        }
        
        return height;
    }
    
    /// <summary>
    /// 绘制应用按钮
    /// </summary>
    private void DrawApplyButton()
    {
        if (_selectedTransitions.Count == 0) return;
        
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
        bool hasSourceLookup = false;
        StateInfo cachedNewSourceStateInfo = null;
        bool hasDestinationLookup = false;
        StateInfo cachedNewDestinationStateInfo = null;
        bool destinationIsExit = false;
        
        // 当合并双向时，需要找到原始的双向Transition并同时修改两个方向
        var processedTransitions = new HashSet<AnimatorStateTransition>();
        
        foreach (var transitionData in _selectedTransitions)
        {
            if (transitionData == null || transitionData.transition == null)
            {
                continue;
            }
            
            // 找到原始的双向TransitionData（如果存在）
            TransitionData originalTransitionData = transitionData;
            foreach (var originalData in _transitionDataList)
            {
                if (originalData.transition == transitionData.transition || 
                    (originalData.isBidirectional && originalData.reverseTransition == transitionData.transition))
                {
                    originalTransitionData = originalData;
                    break;
                }
            }
            
            // 如果已经处理过（合并双向时可能重复），跳过
            if (processedTransitions.Contains(transitionData.transition))
            {
                continue;
            }
            
            // 标记为已处理
            processedTransitions.Add(transitionData.transition);
            
            AnimatorStateTransition transition = transitionData.transition;
            bool modified = false;
            bool reverseModified = false;
            
            // 当合并双向时，如果原始Transition是双向的，需要同时修改两个方向
            bool shouldModifyBothDirections = _mergeBidirectional && originalTransitionData.isBidirectional && originalTransitionData.reverseTransition != null;
            
            bool isAnyStateTransition = IsAnyStateTransition(transition);
            AnimatorState currentSourceState = GetSourceState(transition);
            AnimatorState desiredSourceState = currentSourceState;
            AnimatorStateMachine desiredSourceStateMachine = null;
            
            if (_modifySourceState && !string.IsNullOrEmpty(_newSourceState))
            {
                if (!hasSourceLookup)
                {
                    hasSourceLookup = true;
                    cachedNewSourceStateInfo = FindStateOrStateMachineByName(_newSourceState);
                    if (cachedNewSourceStateInfo == null)
                    {
                        Debug.LogWarning(string.Format("未找到状态或子状态机: {0}", _newSourceState));
                    }
                }
                
                if (cachedNewSourceStateInfo != null)
                {
                    if (cachedNewSourceStateInfo.isSubStateMachine)
                    {
                        // 子状态机作为源：需要找到子状态机的默认状态或Entry状态
                        desiredSourceStateMachine = cachedNewSourceStateInfo.subStateMachine;
                        if (desiredSourceStateMachine != null && desiredSourceStateMachine.defaultState != null)
                        {
                            desiredSourceState = desiredSourceStateMachine.defaultState;
                        }
                        else
                        {
                            Debug.LogWarning(string.Format("子状态机 {0} 没有默认状态，无法作为Transition源", _newSourceState));
                            continue;
                        }
                    }
                    else
                    {
                        desiredSourceState = cachedNewSourceStateInfo.state;
                    }
                    
                    if (desiredSourceState != null)
                    {
                        AnimatorStateTransition newTransition = null;
                        
                        if (isAnyStateTransition)
                        {
                            // AnyState Transition需要特殊处理
                            newTransition = MoveAnyStateTransitionToNewSource(transition, desiredSourceState);
                        }
                        else
                        {
                            // 普通Transition处理
                            if (desiredSourceState != currentSourceState)
                            {
                                newTransition = MoveTransitionToNewSource(transition, desiredSourceState);
                            }
                        }
                        
                        if (newTransition != null)
                        {
                            transitionData.transition = newTransition;
                            transition = newTransition;
                            currentSourceState = desiredSourceState;
                            modified = true;
                        }
                    }
                }
            }
            
            bool shouldSetExit = false;
            bool currentIsExit = transition.isExit;
            AnimatorState desiredDestinationState = transition.destinationState;
            AnimatorStateMachine desiredDestinationStateMachine = null;
            
            if (_modifyDestinationState && !string.IsNullOrEmpty(_newDestinationState))
            {
                if (!hasDestinationLookup)
                {
                    hasDestinationLookup = true;
                    destinationIsExit = string.Equals(_newDestinationState, "Exit", StringComparison.OrdinalIgnoreCase);
                    if (!destinationIsExit)
                    {
                        cachedNewDestinationStateInfo = FindStateOrStateMachineByName(_newDestinationState);
                        if (cachedNewDestinationStateInfo == null)
                        {
                            Debug.LogWarning(string.Format("未找到状态或子状态机: {0}", _newDestinationState));
                        }
                    }
                    else
                    {
                        cachedNewDestinationStateInfo = null;
                    }
                }
                
                if (destinationIsExit)
                {
                    shouldSetExit = true;
                }
                else if (cachedNewDestinationStateInfo != null)
                {
                    if (cachedNewDestinationStateInfo.isSubStateMachine)
                    {
                        // 子状态机作为目标
                        desiredDestinationStateMachine = cachedNewDestinationStateInfo.subStateMachine;
                        desiredDestinationState = null;
                    }
                    else
                    {
                        desiredDestinationState = cachedNewDestinationStateInfo.state;
                        desiredDestinationStateMachine = null;
                    }
                    
                    // 如果当前是Exit状态，需要修改为普通状态或子状态机
                    if (currentIsExit || transition.destinationState != desiredDestinationState || transition.destinationStateMachine != desiredDestinationStateMachine)
                    {
                        transition.destinationState = desiredDestinationState;
                        transition.destinationStateMachine = desiredDestinationStateMachine;
                        transition.isExit = false;
                        modified = true;
                    }
                }
            }
            
            if (shouldSetExit)
            {
                // 如果当前不是Exit状态，需要修改为Exit
                if (!currentIsExit || transition.destinationState != null || transition.destinationStateMachine != null)
                {
                    transition.isExit = true;
                    transition.destinationState = null;
                    transition.destinationStateMachine = null;
                    modified = true;
                }
            }
            
            // 处理反向Transition：合并双向时自动处理（默认一起修改）
            bool shouldProcessReverse = shouldModifyBothDirections;
            
            if (shouldProcessReverse && originalTransitionData.reverseTransition != null)
            {
                // 标记反向Transition为已处理，避免重复处理
                processedTransitions.Add(originalTransitionData.reverseTransition);
                
                AnimatorStateTransition reverseTransition = originalTransitionData.reverseTransition;
                
                if (_modifyDestinationState && !destinationIsExit && cachedNewDestinationStateInfo != null)
                {
                    AnimatorState reverseSourceState = null;
                    if (cachedNewDestinationStateInfo.isSubStateMachine)
                    {
                        // 如果目标是子状态机，反向Transition的源应该是子状态机的默认状态
                        if (cachedNewDestinationStateInfo.subStateMachine != null && cachedNewDestinationStateInfo.subStateMachine.defaultState != null)
                        {
                            reverseSourceState = cachedNewDestinationStateInfo.subStateMachine.defaultState;
                        }
                    }
                    else
                    {
                        reverseSourceState = cachedNewDestinationStateInfo.state;
                    }
                    
                    if (reverseSourceState != null)
                    {
                        AnimatorStateTransition updatedReverse = MoveTransitionToNewSource(reverseTransition, reverseSourceState);
                        if (updatedReverse != null)
                        {
                            originalTransitionData.reverseTransition = updatedReverse;
                            reverseTransition = updatedReverse;
                            reverseModified = true;
                        }
                    }
                }
                
                if (_modifySourceState && cachedNewSourceStateInfo != null && reverseTransition != null)
                {
                    AnimatorState reverseDestState = null;
                    AnimatorStateMachine reverseDestStateMachine = null;
                    
                    if (cachedNewSourceStateInfo.isSubStateMachine)
                    {
                        // 如果源是子状态机，反向Transition的目标应该是子状态机
                        reverseDestStateMachine = cachedNewSourceStateInfo.subStateMachine;
                        reverseDestState = null;
                    }
                    else
                    {
                        reverseDestState = cachedNewSourceStateInfo.state;
                        reverseDestStateMachine = null;
                    }
                    
                    if (reverseTransition.destinationState != reverseDestState || reverseTransition.destinationStateMachine != reverseDestStateMachine)
                    {
                        reverseTransition.destinationState = reverseDestState;
                        reverseTransition.destinationStateMachine = reverseDestStateMachine;
                        reverseTransition.isExit = false;
                        reverseModified = true;
                    }
                }
            }
            
            if (modified || reverseModified)
            {
                hasChanges = true;
            }
        }
        
        if (hasChanges)
        {
            EditorUtility.SetDirty(_animatorController);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("成功", "更改已应用到Animator Controller", "确定");
            LoadAllTransitions();
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
            string.Format("确定要删除选中的 {0} 个Transition吗？\n此操作不可撤销！", _selectedTransitions.Count), 
            "确定", 
            "取消"
        );
        
        if (!result) return;
        
        foreach (var transitionData in _selectedTransitions)
        {
            RemoveTransition(transitionData.transition);
            
            // 如果合并双向，同时删除反向Transition
            if (_mergeBidirectional && transitionData.reverseTransition != null)
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
    /// 通过名称或路径查找状态（支持子状态机）
    /// </summary>
    private AnimatorState FindStateByName(string stateNameOrPath)
    {
        StateInfo stateInfo = FindStateOrStateMachineByName(stateNameOrPath);
        return stateInfo != null ? stateInfo.state : null;
    }
    
    /// <summary>
    /// 通过名称或路径查找状态或子状态机
    /// </summary>
    private StateInfo FindStateOrStateMachineByName(string stateNameOrPath)
    {
        if (string.IsNullOrEmpty(stateNameOrPath) || _allStates.Count == 0)
        {
            return null;
        }
        
        // 首先尝试精确匹配显示名称（包含路径）
        foreach (var stateInfo in _allStates)
        {
            if (stateInfo.displayName == stateNameOrPath)
            {
                return stateInfo;
            }
        }
        
        // 如果包含路径分隔符，尝试路径查找
        if (stateNameOrPath.Contains("/"))
        {
            string[] pathParts = stateNameOrPath.Split('/');
            string name = pathParts[pathParts.Length - 1];
            string path = string.Join("/", pathParts, 0, pathParts.Length - 1);
            
            foreach (var stateInfo in _allStates)
            {
                if (stateInfo.isSubStateMachine)
                {
                    if (stateInfo.subStateMachine != null && stateInfo.subStateMachine.name == name && stateInfo.path == path)
                    {
                        return stateInfo;
                    }
                }
                else
                {
                    if (stateInfo.state != null && stateInfo.state.name == name && stateInfo.path == path)
                    {
                        return stateInfo;
                    }
                }
            }
        }
        else
        {
            // 只匹配名称（不包含路径）
            foreach (var stateInfo in _allStates)
            {
                if (stateInfo.isSubStateMachine)
                {
                    if (stateInfo.subStateMachine != null && stateInfo.subStateMachine.name == stateNameOrPath)
                    {
                        return stateInfo;
                    }
                }
                else
                {
                    if (stateInfo.state != null && stateInfo.state.name == stateNameOrPath)
                    {
                        return stateInfo;
                    }
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 通过路径递归查找状态机中的状态
    /// </summary>
    private AnimatorState FindStateByPath(AnimatorStateMachine rootStateMachine, string stateNameOrPath)
    {
        if (rootStateMachine == null || string.IsNullOrEmpty(stateNameOrPath))
        {
            return null;
        }
        
        // 如果包含路径分隔符，需要递归查找
        if (stateNameOrPath.Contains("/"))
        {
            string[] pathParts = stateNameOrPath.Split('/');
            AnimatorStateMachine currentStateMachine = rootStateMachine;
            
            // 遍历路径，找到目标状态机
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                string stateMachineName = pathParts[i];
                bool found = false;
                
                foreach (var childStateMachine in currentStateMachine.stateMachines)
                {
                    if (childStateMachine.stateMachine != null && childStateMachine.stateMachine.name == stateMachineName)
                    {
                        currentStateMachine = childStateMachine.stateMachine;
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    return null;
                }
            }
            
            // 在目标状态机中查找状态
            string targetStateName = pathParts[pathParts.Length - 1];
            foreach (var childState in currentStateMachine.states)
            {
                if (childState.state != null && childState.state.name == targetStateName)
                {
                    return childState.state;
                }
            }
        }
        else
        {
            // 没有路径，先在根状态机中查找
            foreach (var childState in rootStateMachine.states)
            {
                if (childState.state != null && childState.state.name == stateNameOrPath)
                {
                    return childState.state;
                }
            }
            
            // 如果根状态机中没找到，递归查找所有子状态机
            return FindStateInSubStateMachines(rootStateMachine, stateNameOrPath);
        }
        
        return null;
    }
    
    /// <summary>
    /// 递归在所有子状态机中查找状态
    /// </summary>
    private AnimatorState FindStateInSubStateMachines(AnimatorStateMachine stateMachine, string stateName)
    {
        if (stateMachine == null) return null;
        
        // 先查找当前状态机
        foreach (var childState in stateMachine.states)
        {
            if (childState.state != null && childState.state.name == stateName)
            {
                return childState.state;
            }
        }
        
        // 递归查找子状态机
        foreach (var childStateMachine in stateMachine.stateMachines)
        {
            if (childStateMachine.stateMachine != null)
            {
                AnimatorState found = FindStateInSubStateMachines(childStateMachine.stateMachine, stateName);
                if (found != null)
                {
                    return found;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 创建示例Animator Controller，帮助快速测试工具功能
    /// </summary>
    [MenuItem("Tools/Animator Transition Tool/创建示例Animator Controller", priority = 201)]
    private static void CreateSampleAnimatorControllerMenu()
    {
        string basePath = "Assets/TestController.controller";
        string controllerPath = AssetDatabase.GenerateUniqueAssetPath(basePath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        if (controller == null)
        {
            EditorUtility.DisplayDialog("创建失败", "无法创建示例Animator Controller。", "确定");
            return;
        }
        
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle");
        AnimatorState walkState = stateMachine.AddState("Walk");
        AnimatorState runState = stateMachine.AddState("Run");
        AnimatorState jumpState = stateMachine.AddState("Jump");
        
        stateMachine.defaultState = idleState;
        
        CreateBidirectionalTransition(idleState, walkState, 0.2f, 0.1f);
        CreateBidirectionalTransition(walkState, runState, 0.15f, 0.15f);
        CreateBidirectionalTransition(idleState, jumpState, 0.25f, 0.2f);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Selection.activeObject = controller;
        EditorUtility.DisplayDialog("创建完成", string.Format("已创建示例Animator Controller：\n{0}", controllerPath), "确定");
    }
    
    private static void CreateBidirectionalTransition(AnimatorState fromState, AnimatorState toState, float forwardDuration, float backwardDuration)
    {
        if (fromState == null || toState == null) return;
        
        var forward = fromState.AddTransition(toState);
        forward.duration = forwardDuration;
        var backward = toState.AddTransition(fromState);
        backward.duration = backwardDuration;
    }
    
    /// <summary>
    /// 运行兼容性诊断，验证在Unity 2017.4.30f1环境下的关键API
    /// </summary>
    [MenuItem("Tools/Animator Transition Tool/运行兼容性诊断", priority = 200)]
    private static void RunCompatibilityDiagnosticsMenu()
    {
        RunCompatibilityDiagnostics();
    }
    
    private static void RunCompatibilityDiagnostics()
    {
        var steps = new List<string>();
        
        try
        {
            steps.Add(TestBasicAPIAvailability());
            steps.Add(TestAnimatorControllerAccess());
            steps.Add(TestStateMachineOperations());
            steps.Add(TestTransitionOperations());
            steps.Add(TestEditorWindowCreation());
            
            string summary = string.Join("\n", steps.ToArray());
            Debug.Log("=== Enhanced Animator Transition Editor 兼容性诊断完成 ===");
            foreach (var step in steps)
            {
                Debug.Log(step);
            }
            
            EditorUtility.DisplayDialog("兼容性诊断", string.Format("所有测试已通过：\n{0}", summary), "确定");
        }
        catch (Exception ex)
        {
            string partialResult = steps.Count > 0 ? string.Join("\n", steps.ToArray()) + "\n" : "";
            Debug.LogError(string.Format("兼容性诊断失败: {0}\n{1}", ex.Message, ex.StackTrace));
            EditorUtility.DisplayDialog("兼容性诊断失败", string.Format("{0}失败原因：\n{1}", partialResult, ex.Message), "确定");
        }
    }
    
    private static string TestBasicAPIAvailability()
    {
        string unityVersion = Application.unityVersion;
        var window = GetWindow<AnimatorTransitionTool>("Diagnostics");
        window.Close();
        
        return string.Format("✓ 基础API可用，当前Unity版本：{0}", unityVersion);
    }
    
    private static string TestAnimatorControllerAccess()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/EnhancedTransitionEditor_Temp.controller");
        AnimatorController controller = null;
        
        try
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (controller == null)
                throw new Exception("无法创建Animator Controller。");
            
            if (controller.layers == null || controller.layers.Length == 0)
                throw new Exception("Animator Controller缺少默认Layer。");
            
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            if (stateMachine == null)
                throw new Exception("无法访问Animator State Machine。");
            
            return string.Format("✓ Animator Controller访问正常（路径：{0}）", path);
        }
        finally
        {
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }
    }
    
    private static string TestStateMachineOperations()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/EnhancedTransitionEditor_StateTest.controller");
        
        try
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            
            AnimatorState state1 = stateMachine.AddState("State1");
            AnimatorState state2 = stateMachine.AddState("State2");
            
            if (state1 == null || state2 == null)
                throw new Exception("无法创建测试状态。");
            
            if (stateMachine.states.Length < 2)
                throw new Exception("状态创建数量不符合预期。");
            
            return string.Format("✓ 状态机操作正常，已创建 {0} 个状态", stateMachine.states.Length);
        }
        finally
        {
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }
    }
    
    private static string TestTransitionOperations()
    {
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/EnhancedTransitionEditor_TransitionTest.controller");
        
        try
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            
            AnimatorState state1 = stateMachine.AddState("State1");
            AnimatorState state2 = stateMachine.AddState("State2");
            
            AnimatorStateTransition forward = state1.AddTransition(state2);
            if (forward == null)
                throw new Exception("无法创建正向Transition。");
            
            forward.duration = 0.1f;
            forward.exitTime = 0.9f;
            
            AnimatorStateTransition reverse = state2.AddTransition(state1);
            if (reverse == null)
                throw new Exception("无法创建反向Transition。");
            
            if (state1.transitions.Length == 0)
                throw new Exception("未检测到创建的Transition。");
            
            return string.Format("✓ Transition操作正常，状态State1拥有 {0} 个Transition", state1.transitions.Length);
        }
        finally
        {
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.Refresh();
            }
        }
    }
    
    private static string TestEditorWindowCreation()
    {
        var window = GetWindow<AnimatorTransitionTool>("诊断测试");
        if (window == null)
            throw new Exception("无法创建AnimatorTransitionTool窗口。");
        
        window.Close();
        return "✓ 编辑器窗口创建正常";
    }
    
    /// <summary>
    /// 绘制调试信息
    /// </summary>
    private void DrawDebugInfo()
    {
        _showDebugInfo = EditorGUILayout.Foldout(_showDebugInfo, "调试信息");
        
        if (_showDebugInfo)
        {
            string controllerName = _animatorController != null ? _animatorController.name : "None";
            string stateMachineName = _stateMachine != null ? _stateMachine.name : "None";
            EditorGUILayout.LabelField(string.Format("Animator Controller: {0}", controllerName));
            EditorGUILayout.LabelField(string.Format("State Machine: {0}", stateMachineName));
            EditorGUILayout.LabelField(string.Format("Total Transitions: {0}", _transitionDataList.Count));
            EditorGUILayout.LabelField(string.Format("Selected Transitions: {0}", _selectedTransitions.Count));
            EditorGUILayout.LabelField(string.Format("Bidirectional Transitions: {0}", _transitionDataList.Count(t => t.isBidirectional)));
        }
    }
}
}