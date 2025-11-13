using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;

public class AnimatorExporterWindow : EditorWindow
{
    private AnimatorController targetAnimator;
    private string exportPath = "";
    private string fileName = "";
    
    // 状态机筛选相关
    private List<string> stateMachineNames = new List<string>();
    private int selectedStateMachineIndex = 0;
    private bool includeSubStateMachines = false;
    
    private const string PREFS_KEY_PATH = "AnimatorExporter_LastPath";
    private const string PREFS_KEY_FILENAME = "AnimatorExporter_LastFileName";
    private const string PREFS_KEY_SELECTED_STATE_MACHINE = "AnimatorExporter_SelectedStateMachine";
    private const string PREFS_KEY_INCLUDE_SUB_STATE_MACHINES = "AnimatorExporter_IncludeSubStateMachines";
    
    [MenuItem("Window/Custom/AnimatorExporter", priority = 1)]
    [MenuItem("Assets/AnimatorExporter", false, 0)]
    public static void ShowWindow()
    {
        AnimatorExporterWindow window = GetWindow<AnimatorExporterWindow>("Animator Exporter");
        window.minSize = new Vector2(400, 350);
        window.LoadPreferences();
        window.CheckSelectedAsset();
    }
    
    private void OnEnable()
    {
        LoadPreferences();
        CheckSelectedAsset();
        UpdateStateMachineList();
    }
    
    private void OnGUI()
    {
        // 0. 使用方法简述文本
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("使用说明：\n1. 选择或拖拽动画控制器到下方引用框\n2. 设置导出路径和文件名\n3. 点击导出按钮完成导出", MessageType.Info);
        EditorGUILayout.Space();
        
        // 1. 动画控制器资产引用
        EditorGUILayout.LabelField("动画控制器", EditorStyles.boldLabel);
        AnimatorController previousAnimator = targetAnimator;
        targetAnimator = (AnimatorController)EditorGUILayout.ObjectField(
            "控制器", 
            targetAnimator, 
            typeof(AnimatorController), 
            false
        );
        
        // 如果控制器改变，更新状态机列表
        if (previousAnimator != targetAnimator)
        {
            UpdateStateMachineList();
        }
        
        EditorGUILayout.Space();
        
        // 2. 导出路径，路径拾取按钮
        EditorGUILayout.LabelField("导出设置", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("导出路径:", GUILayout.Width(80));
        exportPath = EditorGUILayout.TextField(exportPath);
        if (GUILayout.Button("选择路径", GUILayout.Width(80)))
        {
            // 获取默认路径
            string defaultPath = Application.dataPath;
            if (!string.IsNullOrEmpty(exportPath))
            {
                if (Path.IsPathRooted(exportPath))
                {
                    defaultPath = exportPath;
                }
                else
                {
                    string relativePath = exportPath.Replace("Assets/", "").Replace("Assets", "");
                    if (!string.IsNullOrEmpty(relativePath))
                    {
                        defaultPath = Path.Combine(Application.dataPath, relativePath);
                    }
                }
            }
            
            string selectedPath = EditorUtility.OpenFolderPanel("选择导出路径", defaultPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // 转换为相对于项目Assets的路径
                if (selectedPath.StartsWith(Application.dataPath))
                {
                    exportPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
                else
                {
                    exportPath = selectedPath;
                }
                SavePreferences();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 3. 导出文件名输入框(默认为控制器名称.json)
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("文件名:", GUILayout.Width(80));
        fileName = EditorGUILayout.TextField(fileName);
        if (GUILayout.Button("重置", GUILayout.Width(60)))
        {
            if (targetAnimator != null)
            {
                fileName = targetAnimator.name + ".json";
            }
            else
            {
                fileName = "";
            }
            SavePreferences();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 4. 状态机筛选
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("导出状态机筛选", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("状态机:", GUILayout.Width(80));
        if (stateMachineNames.Count > 0)
        {
            int previousIndex = selectedStateMachineIndex;
            selectedStateMachineIndex = EditorGUILayout.Popup(selectedStateMachineIndex, stateMachineNames.ToArray());
            if (previousIndex != selectedStateMachineIndex)
            {
                SavePreferences();
            }
        }
        else
        {
            EditorGUILayout.LabelField("无可用状态机");
        }
        bool previousIncludeSub = includeSubStateMachines;
        includeSubStateMachines = EditorGUILayout.Toggle("包含子状态机", includeSubStateMachines);
        if (previousIncludeSub != includeSubStateMachines)
        {
            SavePreferences();
        }
        EditorGUILayout.EndHorizontal();
        
        // 5. 选项设置
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("导出选项", EditorStyles.boldLabel);
        AnimatorExporterCore.AddPrefix = EditorGUILayout.Toggle("为子状态机节点添加前缀", AnimatorExporterCore.AddPrefix);
        
        // 6. 导出按钮
        EditorGUILayout.Space();
        GUI.enabled = targetAnimator != null;
        if (GUILayout.Button("导出", GUILayout.Height(30)))
        {
            ExportToJson();
        }
        GUI.enabled = true;
        
        // 显示当前设置状态
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("状态信息", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("控制器:", targetAnimator != null ? targetAnimator.name : "未选择");
        EditorGUILayout.LabelField("路径:", string.IsNullOrEmpty(exportPath) ? "未设置" : exportPath);
        EditorGUILayout.LabelField("文件名:", string.IsNullOrEmpty(fileName) ? "未设置" : fileName);
    }
    
    private void CheckSelectedAsset()
    {
        // 检查当前选中的资产
        Object[] selectedObjects = Selection.objects;
        foreach (Object obj in selectedObjects)
        {
            if (obj is AnimatorController)
            {
                targetAnimator = (AnimatorController)obj;
                // 如果文件名为空，自动设置为控制器名称
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = targetAnimator.name + ".json";
                    SavePreferences();
                }
                UpdateStateMachineList();
                Repaint();
                break;
            }
        }
    }
    
    /// <summary>
    /// 获取当前引用控制器内的所有子状态机名列表（包括最顶级的状态机，顶级状态机命名为ROOT）
    /// </summary>
    private void UpdateStateMachineList()
    {
        stateMachineNames.Clear();
        
        if (targetAnimator == null)
        {
            return;
        }
        
        if (targetAnimator.layers == null || targetAnimator.layers.Length == 0)
        {
            return;
        }
        
        // 添加根状态机（命名为ROOT）
        stateMachineNames.Add("ROOT");
        
        // 递归获取所有子状态机
        CollectSubStateMachines(targetAnimator.layers[0].stateMachine);
        
        // 确保选中的索引有效
        if (selectedStateMachineIndex >= stateMachineNames.Count)
        {
            selectedStateMachineIndex = 0;
        }
    }
    
    /// <summary>
    /// 递归收集所有子状态机名称
    /// </summary>
    private void CollectSubStateMachines(AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null)
        {
            return;
        }
        
        // 遍历当前状态机的所有子状态机
        foreach (var childMachine in stateMachine.stateMachines)
        {
            if (childMachine.stateMachine != null)
            {
                string stateMachineName = childMachine.stateMachine.name;
                if (!string.IsNullOrEmpty(stateMachineName) && !stateMachineNames.Contains(stateMachineName))
                {
                    stateMachineNames.Add(stateMachineName);
                }
                
                // 递归处理子状态机的子状态机
                CollectSubStateMachines(childMachine.stateMachine);
            }
        }
    }
    
    private void ExportToJson()
    {
        if (targetAnimator == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择动画控制器", "确定");
            return;
        }
        
        string finalPath = exportPath;
        string finalFileName = fileName;
        
        // 如果路径未设置，弹出路径选择对话框
        if (string.IsNullOrEmpty(finalPath))
        {
            string selectedPath = EditorUtility.SaveFolderPanel("选择导出路径", Application.dataPath, "");
            if (string.IsNullOrEmpty(selectedPath))
            {
                return; // 用户取消
            }
            
            // 转换为相对于项目Assets的路径
            if (selectedPath.StartsWith(Application.dataPath))
            {
                finalPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
            else
            {
                finalPath = selectedPath;
            }
        }
        
        // 如果文件名为空，使用控制器名称
        if (string.IsNullOrEmpty(finalFileName))
        {
            finalFileName = targetAnimator.name + ".json";
        }
        
        // 确保文件名有.json扩展名
        if (!finalFileName.EndsWith(".json"))
        {
            finalFileName += ".json";
        }
        
        // 检查路径是否存在
        string fullPath = finalPath;
        bool pathExists = false;
        
        if (Path.IsPathRooted(finalPath))
        {
            // 绝对路径
            fullPath = finalPath;
            pathExists = Directory.Exists(fullPath);
        }
        else
        {
            // 相对路径，转换为绝对路径
            string relativePath = finalPath.Replace("Assets/", "").Replace("Assets", "");
            if (string.IsNullOrEmpty(relativePath))
            {
                fullPath = Application.dataPath;
            }
            else
            {
                fullPath = Path.Combine(Application.dataPath, relativePath);
            }
            pathExists = Directory.Exists(fullPath);
        }
        
        // 如果路径和文件名都已设置好，且路径存在，直接导出
        if (!string.IsNullOrEmpty(exportPath) && !string.IsNullOrEmpty(fileName) && pathExists)
        {
            // 直接导出，不弹出对话框
            DoExport(targetAnimator, fullPath, finalFileName);
        }
        else
        {
            // 路径不存在或未完全设置，弹出保存对话框
            string defaultPath = pathExists ? fullPath : Application.dataPath;
            string savePath = EditorUtility.SaveFilePanel(
                "保存JSON文件",
                defaultPath,
                finalFileName,
                "json"
            );
            
            if (!string.IsNullOrEmpty(savePath))
            {
                // 更新路径和文件名
                string directory = Path.GetDirectoryName(savePath);
                string name = Path.GetFileName(savePath);
                
                // 转换为相对于项目Assets的路径
                if (directory.StartsWith(Application.dataPath))
                {
                    exportPath = "Assets" + directory.Substring(Application.dataPath.Length);
                }
                else
                {
                    exportPath = directory;
                }
                
                fileName = name;
                SavePreferences();
                
                DoExport(targetAnimator, directory, name);
            }
        }
    }
    
    private void DoExport(AnimatorController controller, string directory, string fileName)
    {
        try
        {
            // 确保目录存在
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 获取选中的状态机名称
            string selectedStateMachineName = "ROOT";
            if (stateMachineNames.Count > 0 && selectedStateMachineIndex >= 0 && selectedStateMachineIndex < stateMachineNames.Count)
            {
                selectedStateMachineName = stateMachineNames[selectedStateMachineIndex];
            }
            
            // 执行导出（使用筛选功能）
            string json = AnimatorExporterCore.ExportToJson(controller, selectedStateMachineName, includeSubStateMachines);
            string filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, json);
            
            // 刷新资源数据库
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("成功", "状态机已导出至:\n" + filePath, "确定");
            Debug.Log("状态机已导出至: " + filePath);
            
            // 同步路径和文件名（始终同步最近一次导出的路径和文件名）
            if (directory.StartsWith(Application.dataPath))
            {
                exportPath = "Assets" + directory.Substring(Application.dataPath.Length);
            }
            else
            {
                exportPath = directory;
            }
            this.fileName = fileName;
            SavePreferences();
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("错误", "导出失败:\n" + e.Message, "确定");
            Debug.LogError("导出失败: " + e.Message);
        }
    }
    
    private void LoadPreferences()
    {
        exportPath = EditorPrefs.GetString(PREFS_KEY_PATH, "");
        fileName = EditorPrefs.GetString(PREFS_KEY_FILENAME, "");
        selectedStateMachineIndex = EditorPrefs.GetInt(PREFS_KEY_SELECTED_STATE_MACHINE, 0);
        includeSubStateMachines = EditorPrefs.GetBool(PREFS_KEY_INCLUDE_SUB_STATE_MACHINES, false);
    }
    
    private void SavePreferences()
    {
        EditorPrefs.SetString(PREFS_KEY_PATH, exportPath);
        EditorPrefs.SetString(PREFS_KEY_FILENAME, fileName);
        EditorPrefs.SetInt(PREFS_KEY_SELECTED_STATE_MACHINE, selectedStateMachineIndex);
        EditorPrefs.SetBool(PREFS_KEY_INCLUDE_SUB_STATE_MACHINES, includeSubStateMachines);
    }
    
    private void OnSelectionChange()
    {
        CheckSelectedAsset();
    }
}

