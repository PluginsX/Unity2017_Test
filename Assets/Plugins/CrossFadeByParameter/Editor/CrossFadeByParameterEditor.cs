using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(CrossFadeByParameter))]
public class CrossFadeByParameterEditor : Editor
{
    private SerializedProperty nextStateNameProp;
    private SerializedProperty hasExitTimeProp;
    private SerializedProperty exitTimeProp;
    private SerializedProperty useFixedDurationProp;
    private SerializedProperty transitionOffsetProp;
    private SerializedProperty transitionDurationProp;
    private SerializedProperty canBeInterruptedProp;
    private SerializedProperty conditionsProp;
    private SerializedProperty availableParametersProp;
    private SerializedProperty parameterTypesProp;

    private void OnEnable()
    {
        // 检查target是否有效
        if (target == null)
        {
            return;
        }
        
        try
        {
            nextStateNameProp = serializedObject.FindProperty("nextStateName");
            hasExitTimeProp = serializedObject.FindProperty("hasExitTime");
            exitTimeProp = serializedObject.FindProperty("exitTime");
            useFixedDurationProp = serializedObject.FindProperty("useFixedDuration");
            transitionOffsetProp = serializedObject.FindProperty("transitionOffset");
            transitionDurationProp = serializedObject.FindProperty("transitionDuration");
            canBeInterruptedProp = serializedObject.FindProperty("canBeInterrupted");
            conditionsProp = serializedObject.FindProperty("conditions");
            availableParametersProp = serializedObject.FindProperty("availableParameters");
            parameterTypesProp = serializedObject.FindProperty("parameterTypes");
            
            // 在OnEnable时尝试初始化参数列表
            serializedObject.Update();
            CrossFadeByParameter script = target as CrossFadeByParameter;
            if (script != null)
            {
                AnimatorController controller = GetAnimatorControllerFromBehaviour(script);
                if (controller != null)
                {
                    UpdateParameterList(controller);
                }
            }
        }
        catch (System.Exception e)
        {
            // 静默处理错误，避免在Inspector中频繁显示
            UnityEngine.Debug.LogWarning("CrossFadeByParameterEditor OnEnable error: " + e.Message);
        }
    }

    public override void OnInspectorGUI()
    {
        // 检查target是否有效
        if (target == null)
        {
            EditorGUILayout.HelpBox("Target object is null.", MessageType.Warning);
            return;
        }
        
        // 检查序列化属性是否已初始化
        if (serializedObject == null || nextStateNameProp == null)
        {
            EditorGUILayout.HelpBox("Serialized properties not initialized. Please select the object again.", MessageType.Warning);
            return;
        }
        
        serializedObject.Update();

        CrossFadeByParameter script = target as CrossFadeByParameter;
        if (script == null)
        {
            EditorGUILayout.HelpBox("Target is not a CrossFadeByParameter component.", MessageType.Error);
            return;
        }

        // 更新参数列表 - 在Editor中直接获取AnimatorController
        AnimatorController controller = GetAnimatorControllerFromBehaviour(script);
        if (controller != null)
        {
            // 首先尝试使用标准方法
            UpdateParameterList(controller);
            
            // 如果参数列表为空，尝试从资产文件读取
            if (availableParametersProp.arraySize <= 1)
            {
                ReadParametersFromAssetFile(controller);
            }
        }
        else if (availableParametersProp.arraySize == 0)
        {
            // 如果没有控制器，至少初始化一个空选项
            availableParametersProp.arraySize = 1;
            parameterTypesProp.arraySize = 1;
            availableParametersProp.GetArrayElementAtIndex(0).stringValue = "";
            SerializedProperty firstTypeProp = parameterTypesProp.GetArrayElementAtIndex(0);
            firstTypeProp.enumValueIndex = GetEnumIndexForParameterType(AnimatorControllerParameterType.Float);
        }

        // 过渡目标设置
        EditorGUILayout.LabelField("过渡目标设置", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(nextStateNameProp, new GUIContent("Next State Name"));
        EditorGUILayout.Space();

        // 过渡参数
        EditorGUILayout.LabelField("过渡参数", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hasExitTimeProp, new GUIContent("Has Exit Time"));

        if (hasExitTimeProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(exitTimeProp, new GUIContent("Exit Time"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(useFixedDurationProp, new GUIContent("Fixed Duration"));
        EditorGUILayout.PropertyField(transitionDurationProp, new GUIContent("Transition Duration"));
        EditorGUILayout.PropertyField(transitionOffsetProp, new GUIContent("Transition Offset"));
        EditorGUILayout.PropertyField(canBeInterruptedProp, new GUIContent("Can Be Interrupted"));
        EditorGUILayout.Space();

        // 过渡条件 - 模仿Unity原生Transition样式
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
        
        // 绘制条件列表
        if (conditionsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No conditions added.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < conditionsProp.arraySize; i++)
            {
                DrawCondition(i);
            }
        }

        // 添加/删除按钮（模仿Unity原生样式，按钮在右侧）
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        
        // 添加按钮
        if (GUILayout.Button("+", GUILayout.Width(25), GUILayout.Height(18)))
        {
            AddCondition();
            GUI.FocusControl(null); // 清除焦点
        }
        
        // 删除按钮（只有存在条件时才显示）
        if (conditionsProp.arraySize > 0)
        {
            if (GUILayout.Button("-", GUILayout.Width(25), GUILayout.Height(18)))
            {
                RemoveCondition(conditionsProp.arraySize - 1);
                GUI.FocusControl(null); // 清除焦点
            }
        }
        
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCondition(int index)
    {
        SerializedProperty conditionProp = conditionsProp.GetArrayElementAtIndex(index);
        SerializedProperty paramNameProp = conditionProp.FindPropertyRelative("parameterName");
        SerializedProperty modeProp = conditionProp.FindPropertyRelative("mode");
        SerializedProperty thresholdProp = conditionProp.FindPropertyRelative("threshold");

        EditorGUILayout.BeginHorizontal();

        // 参数名称下拉菜单
        string[] paramNames = GetParameterDisplayNames();
        if (paramNames == null || paramNames.Length == 0)
        {
            // 如果没有参数，显示提示
            EditorGUILayout.HelpBox("无法获取Animator参数列表", MessageType.Warning);
            EditorGUILayout.EndHorizontal();
            return;
        }
        
        int currentParamIndex = GetParameterIndex(paramNameProp.stringValue);
        // 确保索引有效
        if (currentParamIndex < 0 || currentParamIndex >= paramNames.Length)
        {
            currentParamIndex = 0;
        }
        
        int newParamIndex = EditorGUILayout.Popup(currentParamIndex, paramNames, GUILayout.MinWidth(100));
        
        if (newParamIndex != currentParamIndex && newParamIndex >= 0 && newParamIndex < paramNames.Length)
        {
            if (newParamIndex == 0)
            {
                paramNameProp.stringValue = "";
            }
            else if (newParamIndex < availableParametersProp.arraySize)
            {
                paramNameProp.stringValue = availableParametersProp.GetArrayElementAtIndex(newParamIndex).stringValue;
            }
        }

        // 根据参数类型显示不同的UI
        AnimatorControllerParameterType paramType = GetParameterType(paramNameProp.stringValue);
        
        if (paramType == AnimatorControllerParameterType.Bool)
        {
            // Bool类型：显示If/IfNot下拉菜单
            string[] boolModes = { "If", "IfNot" };
            int currentMode = modeProp.enumValueIndex;
            // 确保只显示If或IfNot
            if (currentMode != (int)AnimatorConditionMode.If && currentMode != (int)AnimatorConditionMode.IfNot)
            {
                currentMode = (int)AnimatorConditionMode.If;
            }
            int arrayIndex = (currentMode == (int)AnimatorConditionMode.If) ? 0 : 1;
            int newArrayIndex = EditorGUILayout.Popup(arrayIndex, boolModes);
            if (newArrayIndex != arrayIndex)
            {
                modeProp.enumValueIndex = (newArrayIndex == 0) ? (int)AnimatorConditionMode.If : (int)AnimatorConditionMode.IfNot;
            }
        }
        else if (paramType == AnimatorControllerParameterType.Trigger)
        {
            // Trigger类型：只显示Trigger模式
            EditorGUILayout.LabelField("Trigger", EditorStyles.miniLabel);
            modeProp.enumValueIndex = (int)AnimatorConditionMode.Trigger;
        }
        else
        {
            // Int/Float类型：显示比较方式下拉菜单和阈值输入框
            string[] numericModes = { "Greater", "Less", "Equals", "NotEqual" };
            int currentMode = modeProp.enumValueIndex;
            // 将枚举值转换为数组索引（Greater=2, Less=3, Equals=4, NotEqual=5）
            int arrayIndex = -1;
            switch (currentMode)
            {
                case (int)AnimatorConditionMode.Greater: arrayIndex = 0; break;
                case (int)AnimatorConditionMode.Less: arrayIndex = 1; break;
                case (int)AnimatorConditionMode.Equals: arrayIndex = 2; break;
                case (int)AnimatorConditionMode.NotEqual: arrayIndex = 3; break;
                default: arrayIndex = 0; break;
            }
            
            int newArrayIndex = EditorGUILayout.Popup(arrayIndex, numericModes);
            if (newArrayIndex != arrayIndex)
            {
                switch (newArrayIndex)
                {
                    case 0: modeProp.enumValueIndex = (int)AnimatorConditionMode.Greater; break;
                    case 1: modeProp.enumValueIndex = (int)AnimatorConditionMode.Less; break;
                    case 2: modeProp.enumValueIndex = (int)AnimatorConditionMode.Equals; break;
                    case 3: modeProp.enumValueIndex = (int)AnimatorConditionMode.NotEqual; break;
                }
            }
            
            EditorGUILayout.PropertyField(thresholdProp, GUIContent.none);
        }

        EditorGUILayout.EndHorizontal();
    }

    private int GetParameterIndex(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            return 0;

        for (int i = 0; i < availableParametersProp.arraySize; i++)
        {
            if (availableParametersProp.GetArrayElementAtIndex(i).stringValue == parameterName)
            {
                return i;
            }
        }
        return 0;
    }

    private string[] GetParameterDisplayNames()
    {
        if (availableParametersProp == null || availableParametersProp.arraySize == 0)
        {
            return new string[] { "<None>" };
        }
        
        string[] names = new string[availableParametersProp.arraySize];
        for (int i = 0; i < availableParametersProp.arraySize; i++)
        {
            string paramName = availableParametersProp.GetArrayElementAtIndex(i).stringValue;
            if (string.IsNullOrEmpty(paramName))
            {
                names[i] = "<None>";
            }
            else
            {
                names[i] = paramName;
            }
        }
        return names;
    }

    private AnimatorControllerParameterType GetParameterType(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
            return AnimatorControllerParameterType.Float;

        for (int i = 0; i < availableParametersProp.arraySize; i++)
        {
            if (availableParametersProp.GetArrayElementAtIndex(i).stringValue == parameterName)
            {
                // 从enumValueIndex转换为AnimatorControllerParameterType
                int enumIndex = parameterTypesProp.GetArrayElementAtIndex(i).enumValueIndex;
                try
                {
                    string[] enumNames = System.Enum.GetNames(typeof(AnimatorControllerParameterType));
                    if (enumIndex >= 0 && enumIndex < enumNames.Length)
                    {
                        return (AnimatorControllerParameterType)System.Enum.Parse(typeof(AnimatorControllerParameterType), enumNames[enumIndex]);
                    }
                }
                catch
                {
                    // 如果解析失败，使用备用映射
                }
                
                // 备用映射
                switch (enumIndex)
                {
                    case 0: return AnimatorControllerParameterType.Float;
                    case 1: return AnimatorControllerParameterType.Int;
                    case 2: return AnimatorControllerParameterType.Bool;
                    case 3: return AnimatorControllerParameterType.Trigger;
                    default: return AnimatorControllerParameterType.Float;
                }
            }
        }
        return AnimatorControllerParameterType.Float;
    }

    private void AddCondition()
    {
        conditionsProp.arraySize++;
        SerializedProperty newCondition = conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1);
        newCondition.FindPropertyRelative("parameterName").stringValue = "";
        newCondition.FindPropertyRelative("mode").enumValueIndex = 0;
        newCondition.FindPropertyRelative("threshold").floatValue = 0f;
    }

    private void RemoveCondition(int index)
    {
        if (index >= 0 && index < conditionsProp.arraySize)
        {
            conditionsProp.DeleteArrayElementAtIndex(index);
        }
    }

    private void UpdateParameterList(AnimatorController controller)
    {
        if (controller == null) return;

        AnimatorControllerParameter[] parameters = controller.parameters;

        availableParametersProp.arraySize = parameters.Length + 1;
        parameterTypesProp.arraySize = parameters.Length + 1;

        availableParametersProp.GetArrayElementAtIndex(0).stringValue = "";
        // 使用SetEnumValueIndex而不是直接赋值，避免索引越界
        SerializedProperty firstTypeProp = parameterTypesProp.GetArrayElementAtIndex(0);
        firstTypeProp.enumValueIndex = GetEnumIndexForParameterType(AnimatorControllerParameterType.Float);

        for (int i = 0; i < parameters.Length; i++)
        {
            availableParametersProp.GetArrayElementAtIndex(i + 1).stringValue = parameters[i].name;
            SerializedProperty typeProp = parameterTypesProp.GetArrayElementAtIndex(i + 1);
            typeProp.enumValueIndex = GetEnumIndexForParameterType(parameters[i].type);
        }
        
        // 应用修改以确保参数列表被保存
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        serializedObject.Update();
    }
    
    // 获取AnimatorControllerParameterType的枚举索引
    private int GetEnumIndexForParameterType(AnimatorControllerParameterType type)
    {
        // Unity的AnimatorControllerParameterType枚举值：
        // Float = 1, Int = 3, Bool = 4, Trigger = 9
        // 但在SerializedProperty中，枚举索引是从0开始的，需要映射到正确的索引
        // 通过获取枚举名称数组来确定正确的索引
        try
        {
            string[] enumNames = System.Enum.GetNames(typeof(AnimatorControllerParameterType));
            string typeName = type.ToString();
            
            for (int i = 0; i < enumNames.Length; i++)
            {
                if (enumNames[i] == typeName)
                {
                    return i;
                }
            }
        }
        catch
        {
            // 如果获取失败，使用备用映射
        }
        
        // 备用映射（基于常见的枚举顺序）
        switch (type)
        {
            case AnimatorControllerParameterType.Float:
                return 0;
            case AnimatorControllerParameterType.Int:
                return 1;
            case AnimatorControllerParameterType.Bool:
                return 2;
            case AnimatorControllerParameterType.Trigger:
                return 3;
            default:
                return 0;
        }
    }
    
    // 在Editor中获取AnimatorController的方法
    private AnimatorController GetAnimatorControllerFromBehaviour(CrossFadeByParameter behaviour)
    {
        if (behaviour == null) return null;

        try
        {
            // 方法1: 通过反射获取（Unity内部方法）
            // StateMachineBehaviour有一个内部字段存储控制器引用
            System.Reflection.FieldInfo controllerField = typeof(StateMachineBehaviour).GetField("m_Controller", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (controllerField != null)
            {
                UnityEngine.Object controllerObj = controllerField.GetValue(behaviour) as UnityEngine.Object;
                if (controllerObj != null)
                {
                    AnimatorController controller = controllerObj as AnimatorController;
                    if (controller != null)
                    {
                        return controller;
                    }
                }
            }
            
            // 方法2: 通过SerializedObject获取StateMachineBehaviour关联的控制器
            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
            SerializedProperty controllerProp = serializedBehaviour.FindProperty("m_Controller");
            
            if (controllerProp != null && controllerProp.objectReferenceValue != null)
            {
                AnimatorController controller = controllerProp.objectReferenceValue as AnimatorController;
                if (controller != null)
                {
                    return controller;
                }
            }
            
            // 方法3: 通过当前选中的AnimatorController获取
            UnityEngine.Object[] selectedObjects = Selection.objects;
            foreach (UnityEngine.Object obj in selectedObjects)
            {
                AnimatorController controller = obj as AnimatorController;
                if (controller != null)
                {
                    return controller;
                }
            }
            
            // 方法4: 通过当前选中的GameObject上的Animator组件获取
            if (Selection.activeGameObject != null)
            {
                Animator animator = Selection.activeGameObject.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
                    if (controller != null)
                    {
                        return controller;
                    }
                }
            }
            
            // 方法5: 从所有加载的AnimatorController资产中查找（通过AssetDatabase）
            string[] guids = AssetDatabase.FindAssets("t:AnimatorController");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller != null)
                {
                    // 检查这个控制器是否包含当前behaviour所在的状态
                    // 这是一个简化的方法，实际应该检查状态机
                    return controller;
                }
            }
        }
        catch (Exception e)
        {
            // 静默失败，不显示警告（避免在Inspector中频繁显示）
        }
        
        return null;
    }
    
    // 从AnimatorController资产文件直接读取参数（通过解析YAML）
    private void ReadParametersFromAssetFile(AnimatorController controller)
    {
        if (controller == null) return;
        
        try
        {
            string assetPath = AssetDatabase.GetAssetPath(controller);
            if (string.IsNullOrEmpty(assetPath)) return;
            
            // 读取资产文件的文本内容
            string[] lines = System.IO.File.ReadAllLines(assetPath);
            bool inParametersSection = false;
            System.Collections.Generic.List<string> paramNames = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<AnimatorControllerParameterType> paramTypes = new System.Collections.Generic.List<AnimatorControllerParameterType>();
            
            string currentParamName = "";
            int currentParamType = 0;
            bool readingParam = false;
            
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                // 检测参数区域开始
                if (line.Contains("m_AnimatorParameters:"))
                {
                    inParametersSection = true;
                    continue;
                }
                
                // 如果遇到新的顶级对象（以"---"开头），结束参数区域
                if (inParametersSection && line.StartsWith("---"))
                {
                    // 保存最后一个参数
                    if (readingParam && !string.IsNullOrEmpty(currentParamName))
                    {
                        paramNames.Add(currentParamName);
                        AnimatorControllerParameterType paramType = AnimatorControllerParameterType.Float;
                        switch (currentParamType)
                        {
                            case 1: paramType = AnimatorControllerParameterType.Float; break;
                            case 3: paramType = AnimatorControllerParameterType.Int; break;
                            case 4: paramType = AnimatorControllerParameterType.Bool; break;
                            case 9: paramType = AnimatorControllerParameterType.Trigger; break;
                        }
                        paramTypes.Add(paramType);
                    }
                    break;
                }
                
                if (inParametersSection)
                {
                    // 检测新参数开始（以"-"开头，表示列表项）
                    if (trimmedLine.StartsWith("-"))
                    {
                        // 保存上一个参数
                        if (readingParam && !string.IsNullOrEmpty(currentParamName))
                        {
                            paramNames.Add(currentParamName);
                            AnimatorControllerParameterType paramType = AnimatorControllerParameterType.Float;
                            switch (currentParamType)
                            {
                                case 1: paramType = AnimatorControllerParameterType.Float; break;
                                case 3: paramType = AnimatorControllerParameterType.Int; break;
                                case 4: paramType = AnimatorControllerParameterType.Bool; break;
                                case 9: paramType = AnimatorControllerParameterType.Trigger; break;
                            }
                            paramTypes.Add(paramType);
                        }
                        
                        // 开始读取新参数
                        readingParam = true;
                        currentParamName = "";
                        currentParamType = 0;
                    }
                    // 解析参数名称
                    else if (readingParam && trimmedLine.StartsWith("m_Name:"))
                    {
                        int colonIndex = trimmedLine.IndexOf(":");
                        if (colonIndex >= 0 && colonIndex < trimmedLine.Length - 1)
                        {
                            currentParamName = trimmedLine.Substring(colonIndex + 1).Trim();
                        }
                    }
                    // 解析参数类型
                    else if (readingParam && trimmedLine.StartsWith("m_Type:"))
                    {
                        int colonIndex = trimmedLine.IndexOf(":");
                        if (colonIndex >= 0 && colonIndex < trimmedLine.Length - 1)
                        {
                            string typeStr = trimmedLine.Substring(colonIndex + 1).Trim();
                            if (!int.TryParse(typeStr, out currentParamType))
                            {
                                currentParamType = 0;
                            }
                        }
                    }
                }
            }
            
            // 保存最后一个参数
            if (readingParam && !string.IsNullOrEmpty(currentParamName))
            {
                paramNames.Add(currentParamName);
                AnimatorControllerParameterType paramType = AnimatorControllerParameterType.Float;
                switch (currentParamType)
                {
                    case 1: paramType = AnimatorControllerParameterType.Float; break;
                    case 3: paramType = AnimatorControllerParameterType.Int; break;
                    case 4: paramType = AnimatorControllerParameterType.Bool; break;
                    case 9: paramType = AnimatorControllerParameterType.Trigger; break;
                }
                paramTypes.Add(paramType);
            }
            
            // 如果成功解析到参数，更新参数列表
            if (paramNames.Count > 0 && paramNames.Count == paramTypes.Count)
            {
                availableParametersProp.arraySize = paramNames.Count + 1;
                parameterTypesProp.arraySize = paramNames.Count + 1;
                
                availableParametersProp.GetArrayElementAtIndex(0).stringValue = "";
                SerializedProperty firstTypeProp = parameterTypesProp.GetArrayElementAtIndex(0);
                firstTypeProp.enumValueIndex = GetEnumIndexForParameterType(AnimatorControllerParameterType.Float);
                
                for (int i = 0; i < paramNames.Count; i++)
                {
                    availableParametersProp.GetArrayElementAtIndex(i + 1).stringValue = paramNames[i];
                    SerializedProperty typeProp = parameterTypesProp.GetArrayElementAtIndex(i + 1);
                    typeProp.enumValueIndex = GetEnumIndexForParameterType(paramTypes[i]);
                }
                
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                serializedObject.Update();
            }
        }
        catch (Exception e)
        {
            // 解析失败时静默处理
        }
    }
}

