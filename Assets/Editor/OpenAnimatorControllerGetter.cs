using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using UnityEditor.Animations;
using UnityEngine.Animations;

// 必须放在 Assets/Editor 文件夹下
public class OpenAnimatorControllerTracker : EditorWindow
{
    // 菜单栏入口，打开监控窗口
    [MenuItem("Tools/实时监控Animator控制器")]
    public static void ShowTrackerWindow()
    {
        GetWindow<OpenAnimatorControllerTracker>("Animator控制器监控");
    }

    private void OnGUI()
    {
        GUILayout.Label("当前Animator编辑器中打开的控制器：", EditorStyles.boldLabel);
        
        // 实时获取当前打开的AnimatorController
        AnimatorController activeController = GetActiveAnimatorController();
        
        if (activeController != null)
        {
            GUILayout.Label("名称：" + activeController.name);
            GUILayout.Label("路径：" + AssetDatabase.GetAssetPath(activeController));
            // 可选：添加一个按钮快速定位到资源
            if (GUILayout.Button("在Project窗口中显示"))
            {
                EditorGUIUtility.PingObject(activeController);
            }
        }
        else
        {
            GUILayout.Label("未打开任何Animator控制器，或未找到控制器");
        }
    }

    // 每帧刷新窗口，确保实时性
    private void Update()
    {
        Repaint();
    }

    /// <summary>
    /// 核心方法：获取当前Animator编辑器（AnimationWindow）中打开的AnimatorController
    /// </summary>
    private AnimatorController GetActiveAnimatorController()
    {
        // 1. 获取UnityEditor程序集
        Assembly unityEditorAssembly = null;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name == "UnityEditor")
            {
                unityEditorAssembly = asm;
                break;
            }
        }
        if (unityEditorAssembly == null)
        {
            Debug.LogError("未找到UnityEditor程序集，请重启Unity");
            return null;
        }

        // 2. 定位到AnimationWindow类型（已确认的类型名）
        Type animationWindowType = null;
        foreach (Type type in unityEditorAssembly.GetTypes())
        {
            if (type.Name == "AnimationWindow" && type.IsSubclassOf(typeof(EditorWindow)))
            {
                animationWindowType = type;
                break;
            }
        }
        if (animationWindowType == null)
        {
            Debug.LogError("未找到AnimationWindow类型（确认Unity版本为2017.4.30f1）");
            return null;
        }

        // 3. 获取所有打开的AnimationWindow窗口
        EditorWindow[] openAnimationWindows = Resources.FindObjectsOfTypeAll(animationWindowType) as EditorWindow[];
        if (openAnimationWindows == null || openAnimationWindows.Length == 0)
        {
            return null; // 没有打开的Animator编辑器窗口
        }

        // 4. 优先选择当前激活的窗口（用户正在操作的）
        EditorWindow activeWindow = null;
        foreach (var window in openAnimationWindows)
        {
            if (window == EditorWindow.focusedWindow)
            {
                activeWindow = window;
                break;
            }
        }
        if (activeWindow == null)
        {
            activeWindow = openAnimationWindows[0]; // 若没有激活窗口，取第一个
        }

        // 5. 反射查找存储AnimatorController的字段（2017.4中通常是m_Controller或m_Target）
        FieldInfo controllerField = null;
        // 遍历所有私有字段，筛选可能存储控制器的字段
        foreach (FieldInfo field in animationWindowType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            // 字段类型为AnimatorController，或字段是Object且实际是AnimatorController
            if (field.FieldType == typeof(AnimatorController) ||
                (field.FieldType == typeof(UnityEngine.Object) && field.GetValue(activeWindow) is AnimatorController))
            {
                controllerField = field;
                break;
            }
        }

        if (controllerField == null)
        {
            Debug.LogError("未找到存储AnimatorController的字段，可尝试手动指定字段名（如m_Controller）");
            return null;
        }

        // 6. 返回获取到的AnimatorController
        return controllerField.GetValue(activeWindow) as AnimatorController;
    }
}