using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

/// <summary>
/// AnimatorController配置辅助工具
/// 用于在Editor中快速创建和配置AnimatorController
/// </summary>
public class AnimatorControllerHelper : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/ThirdPerson/创建基础AnimatorController")]
    public static void CreateBasicAnimatorController()
    {
        // 创建新的AnimatorController
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/Art/Animation/Controller/AC_Player_Basic.controller");
        
        // 添加参数
        controller.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Sprint", AnimatorControllerParameterType.Bool);
        controller.AddParameter("InAir", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        
        Debug.Log("已创建基础AnimatorController: AC_Player_Basic.controller");
        Debug.Log("请手动添加动画状态和配置过渡条件");
    }
    
    [MenuItem("Tools/ThirdPerson/检查AnimatorController参数")]
    public static void CheckAnimatorControllerParameters()
    {
        AnimatorController controller = Selection.activeObject as AnimatorController;
        if (controller == null)
        {
            Debug.LogWarning("请先选择一个AnimatorController");
            return;
        }
        
        Debug.Log("AnimatorController参数列表：");
        foreach (var param in controller.parameters)
        {
            Debug.Log(string.Format("  {0} ({1})", param.name, param.type));
        }
    }
#endif
}

