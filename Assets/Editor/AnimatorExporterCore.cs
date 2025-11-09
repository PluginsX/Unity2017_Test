using UnityEngine;
using UnityEditor.Animations;
using System.Collections.Generic;
using System;

/// <summary>
/// Animator导出核心逻辑类
/// </summary>
public static class AnimatorExporterCore
{
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
        ProcessStateMachine(controller.layers[0].stateMachine, data, "", controller);
        
        // 序列化并返回
        return JsonUtility.ToJson(data, true);
    }
    
    // 处理状态机（包括子状态机）
    private static void ProcessStateMachine(AnimatorStateMachine stateMachine, StateMachineData data, string parentPath, AnimatorController controller)
    {
        string currentPath = string.IsNullOrEmpty(parentPath) 
            ? stateMachine.name 
            : parentPath + "/" + stateMachine.name;

        // 添加特殊节点：Entry/AnyState
        string entryNodeName = currentPath + "_Entry";
        if (!data.Nodes.Contains(entryNodeName))
            data.Nodes.Add(entryNodeName);

        string anyStateNodeName = currentPath + "_AnyState";
        if (!data.Nodes.Contains(anyStateNodeName))
            data.Nodes.Add(anyStateNodeName);

        // 处理普通状态节点
        foreach (var state in stateMachine.states)
        {
            string stateName = currentPath + "/" + state.state.name;
            if (!data.Nodes.Contains(stateName))
                data.Nodes.Add(stateName);
        }

        // 处理子状态机
        foreach (var childMachine in stateMachine.stateMachines)
        {
            string subMachineName = currentPath + "/" + childMachine.stateMachine.name;
            
            // 添加子状态机特殊节点
            string subEntryName = subMachineName + "_Entry";
            if (!data.Nodes.Contains(subEntryName))
                data.Nodes.Add(subEntryName);
                
            string subExitName = subMachineName + "_Exit";
            if (!data.Nodes.Contains(subExitName))
                data.Nodes.Add(subExitName);
                
            string subAnyStateName = subMachineName + "_AnyState";
            if (!data.Nodes.Contains(subAnyStateName))
                data.Nodes.Add(subAnyStateName);

            // 递归处理子状态机
            ProcessStateMachine(childMachine.stateMachine, data, currentPath, controller);
        }

        // 处理过渡
        ProcessTransitions(stateMachine, data, currentPath, controller);
    }

    // 处理所有过渡
    private static void ProcessTransitions(AnimatorStateMachine stateMachine, StateMachineData data, string currentPath, AnimatorController controller)
    {
        // 处理AnyState的过渡
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            TransitionData transitionData = new TransitionData();
            transitionData.From = currentPath + "_AnyState";

            // 处理目标节点
            if (transition.destinationStateMachine != null)
            {
                transitionData.To = currentPath + "/" + transition.destinationStateMachine.name + "_Entry";
            }
            else if (transition.destinationState != null)
            {
                transitionData.To = currentPath + "/" + transition.destinationState.name;
            }

            // 设置过渡名称
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
                transitionData.From = currentPath + "/" + state.state.name;

                // 处理目标节点
                if (transition.destinationStateMachine != null)
                {
                    transitionData.To = currentPath + "/" + transition.destinationStateMachine.name + "_Entry";
                }
                else if (transition.destinationState != null)
                {
                    transitionData.To = currentPath + "/" + transition.destinationState.name;
                }

                // 设置过渡名称
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

        // 处理子状态机的过渡
        foreach (var childMachine in stateMachine.stateMachines)
        {
            ProcessTransitions(childMachine.stateMachine, data, currentPath + "/" + childMachine.stateMachine.name, controller);
        }
    }

    // 处理过渡条件
    private static void ProcessTransitionConditions(AnimatorStateTransition transition, TransitionData transitionData, AnimatorController controller)
    {
        foreach (var condition in transition.conditions)
        {
            ConditionData conditionData = new ConditionData();
            conditionData.Parameter = condition.parameter;
            conditionData.Type = GetConditionType(condition, controller);
            
            if (conditionData.Type != "Trigger")
            {
                conditionData.Compare = GetComparisonOperator(condition);
                conditionData.Value = GetConditionValue(condition);
            }

            transitionData.Conditions.Add(conditionData);
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

    // 数据结构类（用于JSON序列化）
    [Serializable]
    public class StateMachineData
    {
        public string version = "1.0";
        public string type = "UnityAnimatorControllerExporter";
        public List<string> Nodes = new List<string>();
        public List<TransitionData> Transitions = new List<TransitionData>();
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
        public string Compare;
        public object Value;
    }
}

