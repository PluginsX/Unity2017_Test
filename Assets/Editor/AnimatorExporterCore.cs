using UnityEngine;
using UnityEditor.Animations;
using System.Collections.Generic;
using System;
using System.Text;

/// <summary>
    /// Animator导出核心逻辑类
    /// </summary>
    public static class AnimatorExporterCore
    {
        // 控制是否为子状态机内节点添加前缀
        public static bool AddPrefix = true;
    
    /// <summary>
    /// 导出Animator控制器为JSON字符串
    /// </summary>
    public static string ExportToJson(AnimatorController controller)
    {
        if (controller == null)
        {
            throw new ArgumentNullException("controller", "AnimatorController不能为空");
        }
        
        if (controller.layers == null || controller.layers.Length == 0)
        {
            throw new InvalidOperationException("AnimatorController没有Layer");
        }
        
        // 创建导出数据结构
        StateMachineData data = new StateMachineData();
        
        // 处理根状态机
        ProcessStateMachine(controller.layers[0].stateMachine, data, "", controller, true);
        
        // 使用自定义序列化方法，确保Trigger类型不输出Compare和Value字段
        return SerializeToJson(data);
    }
    
    /// <summary>
    /// 导出Animator控制器为JSON字符串（支持状态机筛选）
    /// </summary>
    /// <param name="controller">动画控制器</param>
    /// <param name="selectedStateMachineName">选中的状态机名称（"ROOT"表示根状态机）</param>
    /// <param name="includeSubStateMachines">是否包含子状态机</param>
    public static string ExportToJson(AnimatorController controller, string selectedStateMachineName, bool includeSubStateMachines)
    {
        if (controller == null)
        {
            throw new ArgumentNullException("controller", "AnimatorController不能为空");
        }
        
        if (controller.layers == null || controller.layers.Length == 0)
        {
            throw new InvalidOperationException("AnimatorController没有Layer");
        }
        
        // 创建导出数据结构
        StateMachineData data = new StateMachineData();
        
        // 查找目标状态机
        AnimatorStateMachine targetStateMachine = null;
        bool isRootStateMachine = false;
        
        if (selectedStateMachineName == "ROOT")
        {
            // 根状态机
            targetStateMachine = controller.layers[0].stateMachine;
            isRootStateMachine = true;
        }
        else
        {
            // 查找子状态机
            targetStateMachine = FindStateMachineByName(controller.layers[0].stateMachine, selectedStateMachineName);
            if (targetStateMachine == null)
            {
                throw new ArgumentException("未找到名称为 '" + selectedStateMachineName + "' 的状态机", "selectedStateMachineName");
            }
        }
        
        // 处理目标状态机
        ProcessStateMachine(targetStateMachine, data, isRootStateMachine ? "" : targetStateMachine.name, controller, includeSubStateMachines);
        
        // 使用自定义序列化方法，确保Trigger类型不输出Compare和Value字段
        return SerializeToJson(data);
    }
    
    /// <summary>
    /// 根据名称查找状态机
    /// </summary>
    private static AnimatorStateMachine FindStateMachineByName(AnimatorStateMachine rootStateMachine, string targetName)
    {
        if (rootStateMachine == null)
        {
            return null;
        }
        
        // 检查当前状态机
        if (rootStateMachine.name == targetName)
        {
            return rootStateMachine;
        }
        
        // 递归检查子状态机
        foreach (var childMachine in rootStateMachine.stateMachines)
        {
            if (childMachine.stateMachine != null)
            {
                if (childMachine.stateMachine.name == targetName)
                {
                    return childMachine.stateMachine;
                }
                
                // 递归查找
                AnimatorStateMachine result = FindStateMachineByName(childMachine.stateMachine, targetName);
                if (result != null)
                {
                    return result;
                }
            }
        }
        
        return null;
    }
    
    // 处理状态机（包括子状态机）
    // parentPath为空表示是Base Layer的根状态机
    // includeSubStateMachines: 是否处理子状态机
    private static void ProcessStateMachine(AnimatorStateMachine stateMachine, StateMachineData data, string parentPath, AnimatorController controller, bool includeSubStateMachines = true)
    {
        bool isRootStateMachine = string.IsNullOrEmpty(parentPath);
        
        // 添加特殊节点：Entry/Exit/AnyState
        // Base Layer的根状态机不需要前缀，直接使用Entry、Exit和AnyState
        if (isRootStateMachine)
        {
            AddNodeIfNotExists(data, "Entry", "#1D7436");  // Entry颜色
            AddNodeIfNotExists(data, "Exit", "#991F1F");   // Exit颜色
            AddNodeIfNotExists(data, "AnyState", "#60BEA6"); // AnyState颜色
        }
        else
        {
            // 子状态机需要添加前缀：子状态机名_Entry, 子状态机名_Exit
            // 注意：子状态机的AnyState节点不导出，因为其Transition会合并到根状态机的AnyState
            string subMachineName = stateMachine.name;
            string subEntryName = subMachineName + "_Entry";
            AddNodeIfNotExists(data, subEntryName, "#1D7436");  // Entry颜色
                
            string subExitName = subMachineName + "_Exit";
            AddNodeIfNotExists(data, subExitName, "#991F1F");  // Exit颜色
        }

        // 处理普通状态节点
        // Base Layer的根状态机中的状态直接使用状态名
        // 子状态机中的状态需要添加前缀：子状态机名_状态名
        foreach (var state in stateMachine.states)
        {
            string stateName = isRootStateMachine ? state.state.name : (AddPrefix ? (stateMachine.name + "_" + state.state.name) : state.state.name);
            // 普通状态节点没有color字段
            AddNodeIfNotExists(data, stateName, null);
        }

        // 处理子状态机（根据includeSubStateMachines参数决定）
        if (includeSubStateMachines)
        {
            foreach (var childMachine in stateMachine.stateMachines)
            {
                // 递归处理子状态机，传递子状态机名作为parentPath
                ProcessStateMachine(childMachine.stateMachine, data, childMachine.stateMachine.name, controller, true);
            }
        }

        // 处理过渡
        ProcessTransitions(stateMachine, data, isRootStateMachine, controller);
    }

    // 添加节点（如果不存在）
    private static void AddNodeIfNotExists(StateMachineData data, string nodeName, string color)
    {
        // 检查节点是否已存在
        if (!data.Nodes.Exists(n => n.Name == nodeName))
        {
            NodeData node = new NodeData();
            node.Name = nodeName;
            node.color = color;  // 如果color为null，JSON序列化时会忽略或为null
            data.Nodes.Add(node);
        }
    }
    
    // 处理所有过渡
    // isRootStateMachine: 是否为Base Layer的根状态机
    private static void ProcessTransitions(AnimatorStateMachine stateMachine, StateMachineData data, bool isRootStateMachine, AnimatorController controller)
    {
        // 处理Entry节点的过渡
        // 首先处理Entry到默认状态的过渡（如果存在defaultState）
        bool hasDefaultStateTransition = false;
        if (stateMachine.defaultState != null)
        {
            TransitionData entryTransition = new TransitionData();
            // Base Layer的根状态机中，Entry直接使用"Entry"
            entryTransition.From = isRootStateMachine ? "Entry" : (stateMachine.name + "_Entry");
            // 默认状态：根状态机直接使用状态名，子状态机需要加前缀
            entryTransition.To = isRootStateMachine ? stateMachine.defaultState.name : (AddPrefix ? (stateMachine.name + "_" + stateMachine.defaultState.name) : stateMachine.defaultState.name);
            entryTransition.Name = entryTransition.From + "->" + entryTransition.To;
            // Entry到默认状态的过渡通常没有条件
            entryTransition.Conditions = new List<ConditionData>();
            
            // 避免重复添加
            if (!data.Transitions.Exists(t => t.Name == entryTransition.Name))
            {
                data.Transitions.Add(entryTransition);
                hasDefaultStateTransition = true;
            }
        }
        
        // 处理Entry节点的其他过渡（如果有entryTransitions）
        if (stateMachine.entryTransitions != null && stateMachine.entryTransitions.Length > 0)
        {
            foreach (var transition in stateMachine.entryTransitions)
            {
                TransitionData transitionData = new TransitionData();
                // Base Layer的根状态机中，Entry直接使用"Entry"
                transitionData.From = isRootStateMachine ? "Entry" : (stateMachine.name + "_Entry");
                
                // 处理目标节点
                if (transition.isExit)
                {
                    // 终止节点是Exit，To字段应为：状态机名_Exit
                    transitionData.To = isRootStateMachine ? "Exit" : (stateMachine.name + "_Exit");
                }
                else if (transition.destinationStateMachine != null)
                {
                    // 终止节点是子状态机，To字段应为：子状态机名_Entry
                    transitionData.To = transition.destinationStateMachine.name + "_Entry";
                }
                else if (transition.destinationState != null)
                {
                    // 终止节点是普通状态
                    // Entry的过渡：如果是子状态机的Entry，目标状态需要加前缀
                    transitionData.To = isRootStateMachine ? transition.destinationState.name : (AddPrefix ? (stateMachine.name + "_" + transition.destinationState.name) : transition.destinationState.name);
                }
                
                // 设置过渡名称：起始节点名->终止节点名
                transitionData.Name = transitionData.From + "->" + transitionData.To;
                
                // 处理过渡条件
                ProcessTransitionConditions(transition, transitionData, controller);
                
                // 避免重复添加（包括与defaultState的过渡重复）
                if (!data.Transitions.Exists(t => t.Name == transitionData.Name))
                {
                    data.Transitions.Add(transitionData);
                }
            }
        }
        
        // 处理AnyState的过渡
        // 所有AnyState的过渡（包括子状态机的）都合并为从根状态机的AnyState出发
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            TransitionData transitionData = new TransitionData();
            
            // 所有AnyState的过渡都从根状态机的AnyState出发
            transitionData.From = "AnyState";

            // 处理目标节点
            if (transition.isExit)
            {
                // 终止节点是Exit
                // 如果是根状态机的AnyState，Exit直接使用"Exit"
                // 如果是子状态机的AnyState，Exit应该是：子状态机名_Exit
                transitionData.To = isRootStateMachine ? "Exit" : (stateMachine.name + "_Exit");
            }
            else if (transition.destinationStateMachine != null)
            {
                // 终止节点是子状态机，To字段应为：子状态机名_Entry
                transitionData.To = transition.destinationStateMachine.name + "_Entry";
            }
            else if (transition.destinationState != null)
            {
                // 终止节点是普通状态
                // 关键修复：确保目标状态名称始终带有正确的子状态机前缀（只添加直接父状态机的前缀）
                string toStateName = transition.destinationState.name;
                
                // 获取根状态机
                AnimatorStateMachine rootStateMachine = null;
                if (controller != null && controller.layers.Length > 0)
                {
                    rootStateMachine = controller.layers[0].stateMachine;
                }
                
                // 查找状态所属的直接父状态机
                AnimatorStateMachine parentStateMachine = FindDirectParentStateMachine(transition.destinationState, rootStateMachine);
                
                if (parentStateMachine == rootStateMachine)
                {
                    // 目标状态在根状态机中，直接使用状态名
                    transitionData.To = toStateName;
                }
                else if (parentStateMachine != null)
                {
                    // 如果目标状态在某个子状态机中且AddPrefix为true，只添加直接父状态机的前缀
                    transitionData.To = AddPrefix ? (parentStateMachine.name + "_" + toStateName) : toStateName;
                }
                else
                {
                    // 如果查找失败，使用当前状态机作为默认值
                    transitionData.To = isRootStateMachine ? toStateName : (AddPrefix ? (stateMachine.name + "_" + toStateName) : toStateName);
                }
            }

            // 设置过渡名称：起始节点名->终止节点名
            transitionData.Name = transitionData.From + "->" + transitionData.To;

            // 处理过渡条件
            ProcessTransitionConditions(transition, transitionData, controller);

            // 避免重复添加
            if (!data.Transitions.Exists(t => t.Name == transitionData.Name))
            {
                data.Transitions.Add(transitionData);
            }
        }

        // 处理普通状态的过渡
        foreach (var state in stateMachine.states)
        {
            foreach (var transition in state.state.transitions)
            {
                TransitionData transitionData = new TransitionData();
                // Base Layer的根状态机中的状态直接使用状态名
                // 子状态机中的状态需要加前缀：子状态机名_状态名
                transitionData.From = isRootStateMachine ? state.state.name : (AddPrefix ? (stateMachine.name + "_" + state.state.name) : state.state.name);

                // 处理目标节点
                if (transition.isExit)
                {
                    // 终止节点是Exit，To字段应为：状态机名_Exit
                    // Base Layer的根状态机中，Exit直接使用"Exit"
                    transitionData.To = isRootStateMachine ? "Exit" : (stateMachine.name + "_Exit");
                }
                else if (transition.destinationStateMachine != null)
                {
                    // 终止节点是子状态机，To字段应为：子状态机名_Entry
                    transitionData.To = transition.destinationStateMachine.name + "_Entry";
                }
                else if (transition.destinationState != null)
                {
                    // 终止节点是普通状态
                    // 需要判断目标状态属于哪个状态机
                    // 如果目标状态在当前状态机中，需要加前缀（如果是子状态机）
                    // 如果目标状态在其他状态机中，需要找到其所属状态机
                    string toStateName = transition.destinationState.name;
                    
                    // 检查目标状态是否在当前状态机中
                    bool foundInCurrentMachine = false;
                    foreach (var s in stateMachine.states)
                    {
                        if (s.state == transition.destinationState)
                        {
                            foundInCurrentMachine = true;
                            break;
                        }
                    }
                    
                    if (foundInCurrentMachine)
                    {
                        // 目标状态在当前状态机中，需要加前缀（如果是子状态机）
                        transitionData.To = isRootStateMachine ? toStateName : (AddPrefix ? (stateMachine.name + "_" + toStateName) : toStateName);
                    }
                    else
                    {
                        // 目标状态在其他状态机中（可能是根状态机），直接使用状态名
                        transitionData.To = toStateName;
                    }
                }

                // 设置过渡名称：起始节点名->终止节点名
                transitionData.Name = transitionData.From + "->" + transitionData.To;

                // 处理过渡条件
                ProcessTransitionConditions(transition, transitionData, controller);

                // 避免重复添加
                if (!data.Transitions.Exists(t => t.Name == transitionData.Name))
                {
                    data.Transitions.Add(transitionData);
                }
            }
        }

        // 处理从子状态机节点出发的过渡
        // 在Unity 2017中，m_StateMachineTransitions是一个字典结构：key是子状态机，value是过渡列表
        try
        {
            UnityEditor.SerializedObject serializedStateMachine = new UnityEditor.SerializedObject(stateMachine);
            UnityEditor.SerializedProperty stateMachineTransitionsProp = serializedStateMachine.FindProperty("m_StateMachineTransitions");
            
            if (stateMachineTransitionsProp != null && stateMachineTransitionsProp.isArray)
            {
                // m_StateMachineTransitions是一个数组，每个元素是一个键值对（first是key，second是value数组）
                for (int i = 0; i < stateMachineTransitionsProp.arraySize; i++)
                {
                    UnityEditor.SerializedProperty pairProp = stateMachineTransitionsProp.GetArrayElementAtIndex(i);
                    
                    // 获取first（源子状态机）
                    UnityEditor.SerializedProperty firstProp = pairProp.FindPropertyRelative("first");
                    if (firstProp != null && firstProp.objectReferenceValue != null)
                    {
                        AnimatorStateMachine fromStateMachine = firstProp.objectReferenceValue as AnimatorStateMachine;
                        if (fromStateMachine != null)
                        {
                            // 获取second（过渡列表）
                            UnityEditor.SerializedProperty secondProp = pairProp.FindPropertyRelative("second");
                            if (secondProp != null && secondProp.isArray)
                            {
                                for (int j = 0; j < secondProp.arraySize; j++)
                                {
                                    UnityEditor.SerializedProperty transitionRefProp = secondProp.GetArrayElementAtIndex(j);
                                    if (transitionRefProp != null && transitionRefProp.objectReferenceValue != null)
                                    {
                                        // 获取过渡对象
                                        var transitionObj = transitionRefProp.objectReferenceValue;
                                        UnityEditor.SerializedObject serializedTransition = new UnityEditor.SerializedObject(transitionObj);
                                        
                                        TransitionData transitionData = new TransitionData();
                                        // 从子状态机的Exit节点出发
                                        transitionData.From = fromStateMachine.name + "_Exit";
                                        
                                        // 获取m_DstStateMachine（目标子状态机）
                                        UnityEditor.SerializedProperty dstStateMachineProp = serializedTransition.FindProperty("m_DstStateMachine");
                                        if (dstStateMachineProp != null && dstStateMachineProp.objectReferenceValue != null)
                                        {
                                            AnimatorStateMachine toStateMachine = dstStateMachineProp.objectReferenceValue as AnimatorStateMachine;
                                            if (toStateMachine != null)
                                            {
                                                // 目标也是子状态机，To字段应为：目标子状态机名_Entry
                                                transitionData.To = toStateMachine.name + "_Entry";
                                            }
                                        }
                                        
                                        // 获取m_DstState（目标状态）
                                        if (string.IsNullOrEmpty(transitionData.To))
                                        {
                                            UnityEditor.SerializedProperty dstStateProp = serializedTransition.FindProperty("m_DstState");
                                            if (dstStateProp != null && dstStateProp.objectReferenceValue != null)
                                            {
                                                AnimatorState toState = dstStateProp.objectReferenceValue as AnimatorState;
                                                if (toState != null)
                                                {
                                                    // 目标是普通状态，直接使用状态名（因为目标状态在根状态机中）
                                                    transitionData.To = toState.name;
                                                }
                                            }
                                        }
                                        
                                        // 获取m_IsExit
                                        if (string.IsNullOrEmpty(transitionData.To))
                                        {
                                            UnityEditor.SerializedProperty isExitProp = serializedTransition.FindProperty("m_IsExit");
                                            if (isExitProp != null && isExitProp.boolValue)
                                            {
                                                // 目标是Exit节点
                                                transitionData.To = isRootStateMachine ? "Exit" : (stateMachine.name + "_Exit");
                                            }
                                        }
                                        
                                        if (!string.IsNullOrEmpty(transitionData.To))
                                        {
                                            // 设置过渡名称：起始节点名->终止节点名
                                            transitionData.Name = transitionData.From + "->" + transitionData.To;
                                            
                                            // 处理过渡条件
                                            UnityEditor.SerializedProperty conditionsProp = serializedTransition.FindProperty("m_Conditions");
                                            if (conditionsProp != null && conditionsProp.isArray && conditionsProp.arraySize > 0)
                                            {
                                                for (int k = 0; k < conditionsProp.arraySize; k++)
                                                {
                                                    UnityEditor.SerializedProperty conditionProp = conditionsProp.GetArrayElementAtIndex(k);
                                                    
                                                    // 获取parameter
                                                    UnityEditor.SerializedProperty paramProp = conditionProp.FindPropertyRelative("m_ConditionEvent");
                                                    string paramName = paramProp != null ? paramProp.stringValue : "";
                                                    
                                                    // 只有当parameter不为空时才处理条件（避免添加无效的默认条件）
                                                    // 同时检查mode是否有效（modeValue应该大于等于0）
                                                    if (!string.IsNullOrEmpty(paramName))
                                                    {
                                                        ConditionData conditionData = new ConditionData();
                                                        conditionData.Parameter = paramName;
                                                        
                                                        // 获取mode（在Unity 2017中，m_ConditionMode存储为int值）
                                                        UnityEditor.SerializedProperty modeProp = conditionProp.FindPropertyRelative("m_ConditionMode");
                                                        int modeValue = modeProp != null ? modeProp.intValue : -1;
                                                        
                                                        // 验证mode值是否有效（AnimatorConditionMode的有效值范围是0-5）
                                                        // 如果modeValue无效（-1或超出范围），跳过这个条件
                                                        if (modeValue < 0 || modeValue > 5)
                                                        {
                                                            continue; // 跳过无效的条件
                                                        }
                                                        
                                                        // 获取threshold
                                                        UnityEditor.SerializedProperty thresholdProp = conditionProp.FindPropertyRelative("m_EventTreshold");
                                                        float threshold = thresholdProp != null ? thresholdProp.floatValue : 0f;
                                                        
                                                        // 判断条件类型
                                                        string conditionTypeStr = GetConditionTypeFromMode(modeValue, conditionData.Parameter, controller);
                                                        
                                                        // 如果类型是Unknown，说明条件无效，跳过
                                                        if (conditionTypeStr == "Unknown")
                                                        {
                                                            continue; // 跳过无效的条件
                                                        }
                                                        
                                                        conditionData.Type = conditionTypeStr;
                                                        
                                                        if (conditionTypeStr != "Trigger")
                                                        {
                                                            if (conditionTypeStr == "Bool")
                                                            {
                                                                conditionData.Compare = "Equal";
                                                                conditionData.Value = (modeValue == (int)UnityEditor.Animations.AnimatorConditionMode.If);
                                                            }
                                                            else if (conditionTypeStr == "Int")
                                                            {
                                                                conditionData.Compare = GetComparisonOperatorFromMode(modeValue);
                                                                conditionData.Value = (int)threshold;
                                                            }
                                                            else if (conditionTypeStr == "Float")
                                                            {
                                                                conditionData.Compare = GetComparisonOperatorFromMode(modeValue);
                                                                conditionData.Value = threshold;
                                                            }
                                                        }
                                                        
                                                        transitionData.Conditions.Add(conditionData);
                                                    }
                                                }
                                            }
                                            
                                            // 避免重复添加
                                            if (!data.Transitions.Exists(t => t.Name == transitionData.Name))
                                            {
                                                data.Transitions.Add(transitionData);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            // 如果访问失败，记录警告但不中断导出
            UnityEngine.Debug.LogWarning("无法访问子状态机过渡: " + ex.Message + "\n" + ex.StackTrace);
        }
        
        // 处理子状态机的过渡（递归处理）
        foreach (var childMachine in stateMachine.stateMachines)
        {
            ProcessTransitions(childMachine.stateMachine, data, false, controller);
        }
    }
    
    // 递归查找状态所属的直接父状态机
    // 返回状态所属的直接父状态机，如果是根状态机则返回根状态机，如果未找到则返回null
    private static AnimatorStateMachine FindDirectParentStateMachine(AnimatorState targetState, AnimatorStateMachine stateMachine)
    {
        // 检查当前状态机中的状态
        foreach (var state in stateMachine.states)
        {
            if (state.state == targetState)
            {
                // 找到状态，返回当前状态机（直接父状态机）
                return stateMachine;
            }
        }
        
        // 递归检查所有子状态机
        foreach (var childMachine in stateMachine.stateMachines)
        {
            // 在子状态机中递归查找
            AnimatorStateMachine result = FindDirectParentStateMachine(targetState, childMachine.stateMachine);
            if (result != null)
            {
                // 找到了，直接返回结果（直接父状态机）
                return result;
            }
        }
        
        // 没有找到
        return null;
    }

    // 处理过渡条件（AnimatorStateTransition重载）
    private static void ProcessTransitionConditions(AnimatorStateTransition transition, TransitionData transitionData, AnimatorController controller)
    {
        ProcessTransitionConditionsInternal(transition, transitionData, controller);
    }
    
    // 处理过渡条件（AnimatorTransition重载）
    private static void ProcessTransitionConditions(UnityEditor.Animations.AnimatorTransition transition, TransitionData transitionData, AnimatorController controller)
    {
        ProcessTransitionConditionsInternal(transition, transitionData, controller);
    }
    
    // 处理过渡条件的内部实现（支持所有类型的基类）
    private static void ProcessTransitionConditionsInternal(UnityEditor.Animations.AnimatorTransitionBase transition, TransitionData transitionData, AnimatorController controller)
    {
        foreach (var condition in transition.conditions)
        {
            ConditionData conditionData = new ConditionData();
            conditionData.Parameter = condition.parameter;
            string conditionType = GetConditionType(condition, controller);
            conditionData.Type = conditionType;
            
            // 如果不是Trigger类型，必须完整输出4个字段
            if (conditionType != "Trigger")
            {
                // Bool类型：Compare默认为Equal，Value为true/false
                if (conditionType == "Bool")
                {
                    conditionData.Compare = "Equal";
                    // If表示true，IfNot表示false
                    conditionData.Value = (condition.mode == UnityEditor.Animations.AnimatorConditionMode.If);
                }
                // Int和Float类型：Compare和Value使用原本transition的条件
                else if (conditionType == "Int")
                {
                    conditionData.Compare = GetComparisonOperator(condition);
                    conditionData.Value = (int)condition.threshold;
                }
                else if (conditionType == "Float")
                {
                    conditionData.Compare = GetComparisonOperator(condition);
                    conditionData.Value = condition.threshold;
                }
            }
            // Trigger类型：只写入Parameter和Type，不写入Compare字段
            // Compare和Value字段在JSON序列化时会为null，但我们需要确保它们不被序列化
            // 由于C#的JsonUtility会自动序列化所有字段，我们需要在ConditionData类中处理

            transitionData.Conditions.Add(conditionData);
        }
    }

    // 从mode值获取条件类型（用于反射场景）
    private static string GetConditionTypeFromMode(int modeValue, string parameterName, AnimatorController controller)
    {
        // 检查参数类型以区分 Trigger 和 Bool
        if (controller != null && !string.IsNullOrEmpty(parameterName))
        {
            foreach (var param in controller.parameters)
            {
                if (param.name == parameterName && param.type == AnimatorControllerParameterType.Trigger)
                {
                    return "Trigger";
                }
            }
        }
        
        UnityEditor.Animations.AnimatorConditionMode mode = (UnityEditor.Animations.AnimatorConditionMode)modeValue;
        switch (mode)
        {
            case UnityEditor.Animations.AnimatorConditionMode.If:
            case UnityEditor.Animations.AnimatorConditionMode.IfNot:
                return "Bool";
            case UnityEditor.Animations.AnimatorConditionMode.Equals:
            case UnityEditor.Animations.AnimatorConditionMode.NotEqual:
                return "Int";
            case UnityEditor.Animations.AnimatorConditionMode.Less:
            case UnityEditor.Animations.AnimatorConditionMode.Greater:
                return "Float";
            default:
                return "Unknown";
        }
    }
    
    // 从mode值获取比较运算符（用于反射场景）
    private static string GetComparisonOperatorFromMode(int modeValue)
    {
        UnityEditor.Animations.AnimatorConditionMode mode = (UnityEditor.Animations.AnimatorConditionMode)modeValue;
        switch (mode)
        {
            case UnityEditor.Animations.AnimatorConditionMode.If:
                return "Equal";
            case UnityEditor.Animations.AnimatorConditionMode.IfNot:
                return "NotEqual";
            case UnityEditor.Animations.AnimatorConditionMode.Equals:
                return "Equal";
            case UnityEditor.Animations.AnimatorConditionMode.NotEqual:
                return "NotEqual";
            case UnityEditor.Animations.AnimatorConditionMode.Less:
                return "Less";
            case UnityEditor.Animations.AnimatorConditionMode.Greater:
                return "Greater";
            default:
                return "Unknown";
        }
    }
    
    // 获取条件类型
    private static string GetConditionType(UnityEditor.Animations.AnimatorCondition condition, AnimatorController controller)
    {
        UnityEditor.Animations.AnimatorConditionMode mode = condition.mode;
        
        // 检查参数类型以区分 Trigger 和 Bool
        // 在 Unity 2017 中，Trigger 参数也使用 If 模式
        if (controller != null && !string.IsNullOrEmpty(condition.parameter))
        {
            foreach (var param in controller.parameters)
            {
                if (param.name == condition.parameter && param.type == AnimatorControllerParameterType.Trigger)
                {
                    return "Trigger";
                }
            }
        }
        
        switch (mode)
        {
            case UnityEditor.Animations.AnimatorConditionMode.If:
            case UnityEditor.Animations.AnimatorConditionMode.IfNot:
                return "Bool";
            case UnityEditor.Animations.AnimatorConditionMode.Equals:
            case UnityEditor.Animations.AnimatorConditionMode.NotEqual:
                return "Int";
            case UnityEditor.Animations.AnimatorConditionMode.Less:
            case UnityEditor.Animations.AnimatorConditionMode.Greater:
                return "Float";
            default:
                return "Unknown";
        }
    }

    // 获取比较运算符
    private static string GetComparisonOperator(UnityEditor.Animations.AnimatorCondition condition)
    {
        UnityEditor.Animations.AnimatorConditionMode mode = condition.mode;
        switch (mode)
        {
            case UnityEditor.Animations.AnimatorConditionMode.If:
                return "Equal";
            case UnityEditor.Animations.AnimatorConditionMode.IfNot:
                return "NotEqual";
            case UnityEditor.Animations.AnimatorConditionMode.Equals:
                return "Equal";
            case UnityEditor.Animations.AnimatorConditionMode.NotEqual:
                return "NotEqual";
            case UnityEditor.Animations.AnimatorConditionMode.Less:
                return "Less";
            case UnityEditor.Animations.AnimatorConditionMode.Greater:
                return "Greater";
            default:
                return "Unknown";
        }
    }

    // 获取条件值
    private static object GetConditionValue(UnityEditor.Animations.AnimatorCondition condition)
    {
        UnityEditor.Animations.AnimatorConditionMode mode = condition.mode;
        
        // 根据mode直接判断类型，避免调用GetConditionType（因为需要controller参数）
        switch (mode)
        {
            case UnityEditor.Animations.AnimatorConditionMode.If:
            case UnityEditor.Animations.AnimatorConditionMode.IfNot:
                // Bool类型
                return mode == UnityEditor.Animations.AnimatorConditionMode.If;
            case UnityEditor.Animations.AnimatorConditionMode.Equals:
            case UnityEditor.Animations.AnimatorConditionMode.NotEqual:
                // Int类型
                return (int)condition.threshold;
            case UnityEditor.Animations.AnimatorConditionMode.Less:
            case UnityEditor.Animations.AnimatorConditionMode.Greater:
                // Float类型
                return condition.threshold;
            default:
                return null;
        }
    }

    // 自定义JSON序列化方法，确保Trigger类型不输出Compare和Value字段
    private static string SerializeToJson(StateMachineData data)
    {
        StringBuilder json = new StringBuilder();
        json.Append("{\n");
        json.Append("  \"version\": \"").Append(data.version).Append("\",\n");
        json.Append("  \"type\": \"").Append(data.type).Append("\",\n");
        json.Append("  \"Nodes\": [\n");
        
        // 序列化Nodes
        for (int i = 0; i < data.Nodes.Count; i++)
        {
            NodeData node = data.Nodes[i];
            json.Append("    {\n");
            json.Append("      \"Name\": \"").Append(EscapeJsonString(node.Name)).Append("\"");
            
            // 如果有color字段，添加color
            if (!string.IsNullOrEmpty(node.color))
            {
                json.Append(",\n");
                json.Append("      \"color\": \"").Append(EscapeJsonString(node.color)).Append("\"");
            }
            
            json.Append("\n    }");
            if (i < data.Nodes.Count - 1)
                json.Append(",");
            json.Append("\n");
        }
        json.Append("  ],\n");
        json.Append("  \"Transitions\": [\n");
        
        // 序列化Transitions
        for (int i = 0; i < data.Transitions.Count; i++)
        {
            json.Append("    {\n");
            json.Append("      \"Name\": \"").Append(EscapeJsonString(data.Transitions[i].Name)).Append("\",\n");
            json.Append("      \"From\": \"").Append(EscapeJsonString(data.Transitions[i].From)).Append("\",\n");
            json.Append("      \"To\": \"").Append(EscapeJsonString(data.Transitions[i].To)).Append("\",\n");
            json.Append("      \"Conditions\": [\n");
            
            // 序列化Conditions（即使为空数组也会输出）
            for (int j = 0; j < data.Transitions[i].Conditions.Count; j++)
            {
                ConditionData cond = data.Transitions[i].Conditions[j];
                json.Append("        {\n");
                json.Append("          \"Parameter\": \"").Append(EscapeJsonString(cond.Parameter)).Append("\",\n");
                json.Append("          \"Type\": \"").Append(EscapeJsonString(cond.Type)).Append("\"");
                
                // 如果不是Trigger类型，输出Compare和Value
                if (cond.Type != "Trigger")
                {
                    json.Append(",\n");
                    json.Append("          \"Compare\": \"").Append(EscapeJsonString(cond.Compare ?? "")).Append("\",\n");
                    
                    // 处理Value
                    if (cond.Value is bool)
                    {
                        json.Append("          \"Value\": ").Append(cond.Value.ToString().ToLower());
                    }
                    else if (cond.Value is int)
                    {
                        json.Append("          \"Value\": ").Append(cond.Value);
                    }
                    else if (cond.Value is float)
                    {
                        json.Append("          \"Value\": ").Append(((float)cond.Value).ToString("F6"));
                    }
                    else
                    {
                        json.Append("          \"Value\": null");
                    }
                }
                
                json.Append("\n        }");
                if (j < data.Transitions[i].Conditions.Count - 1)
                    json.Append(",");
                json.Append("\n");
            }
            
            json.Append("      ]\n");
            json.Append("    }");
            if (i < data.Transitions.Count - 1)
                json.Append(",");
            json.Append("\n");
        }
        
        json.Append("  ]\n");
        json.Append("}");
        
        return json.ToString();
    }
    
    // 转义JSON字符串
    private static string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str))
            return "";
        
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    // 数据结构类（用于JSON序列化）
    [Serializable]
    public class StateMachineData
    {
        public string version = "1.0";
        public string type = "UnityAnimatorControllerExporter";
        public List<NodeData> Nodes = new List<NodeData>();
        public List<TransitionData> Transitions = new List<TransitionData>();
    }
    
    [Serializable]
    public class NodeData
    {
        public string Name;
        public string color;  // 仅对默认节点（Entry、Exit、AnyState）有值
    }

    [Serializable]
    public class TransitionData
    {
        public string Name;
        public string From;
        public string To;
        public List<ConditionData> Conditions = new List<ConditionData>();
    }

    [Serializable]
    public class ConditionData
    {
        public string Parameter;
        public string Type;
        // 注意：JsonUtility在序列化时会包含所有字段，即使为null
        // 对于Trigger类型，我们需要在序列化后手动处理，或者使用自定义序列化
        // 但为了简化，我们让Compare和Value可以为null，在JSON中会显示为null
        // 如果需要在Trigger时不输出这些字段，需要使用自定义JSON序列化
        public string Compare;
        public object Value;
        
        // 自定义序列化方法（如果需要完全移除Trigger类型的Compare和Value字段，需要自定义JSON序列化）
        // 由于Unity的JsonUtility限制，我们暂时保留字段，但Trigger类型时Compare和Value为null
    }
}

