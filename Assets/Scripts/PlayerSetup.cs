using UnityEngine;

/// <summary>
/// 玩家设置脚本 - 用于自动配置角色和摄像机
/// 将此脚本添加到角色Prefab的根对象上，它会自动设置所有必要的组件
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerSetup : MonoBehaviour
{
    [Header("自动设置")]
    public bool autoSetupCamera = true; // 自动设置摄像机
    public bool createCameraRig = true; // 创建摄像机rig

    [Header("摄像机设置")]
    public float cameraDistance = 5f;
    public float cameraHeight = 2f;
    public Vector3 cameraOffset = new Vector3(0f, 1.5f, 0f);

    private void Start()
    {
        SetupComponents();
        
        if (autoSetupCamera)
        {
            SetupCamera();
        }
    }

    private void SetupComponents()
    {
        // 确保有CharacterController
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
        }
        
        // 设置CharacterController的默认值
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0f, 1f, 0f);
        
        // 确保有Animator
        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }
        
        // 确保有ThirdPersonController
        ThirdPersonController controller = GetComponent<ThirdPersonController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<ThirdPersonController>();
        }
    }

    private void SetupCamera()
    {
        // 查找现有的摄像机
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera != null)
        {
            // 如果摄像机已经有ThirdPersonCamera组件，直接设置目标
            ThirdPersonCamera tpc = mainCamera.GetComponent<ThirdPersonCamera>();
            if (tpc != null)
            {
                tpc.SetTarget(transform);
                return;
            }

            // 如果没有，添加组件
            tpc = mainCamera.gameObject.AddComponent<ThirdPersonCamera>();
            tpc.target = transform;
            tpc.distance = cameraDistance;
            tpc.height = cameraHeight;
            tpc.offset = cameraOffset;
        }
        else if (createCameraRig)
        {
            // 创建新的摄像机rig
            CreateCameraRig();
        }
    }

    private void CreateCameraRig()
    {
        // 创建摄像机rig对象
        GameObject cameraRig = new GameObject("CameraRig");
        cameraRig.transform.position = transform.position + Vector3.back * cameraDistance + Vector3.up * cameraHeight;

        // 添加摄像机
        Camera cam = cameraRig.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;

        // 添加音频监听器
        cameraRig.AddComponent<AudioListener>();

        // 添加ThirdPersonCamera组件
        ThirdPersonCamera tpc = cameraRig.AddComponent<ThirdPersonCamera>();
        tpc.target = transform;
        tpc.distance = cameraDistance;
        tpc.height = cameraHeight;
        tpc.offset = cameraOffset;
    }
}

