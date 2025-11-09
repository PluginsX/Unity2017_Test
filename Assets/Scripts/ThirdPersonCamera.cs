using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("目标设置")]
    public Transform target; // 要跟随的目标（角色）
    public Vector3 offset = new Vector3(0f, 1.5f, 0f); // 相对于目标的偏移

    [Header("摄像机设置")]
    public float distance = 5f; // 摄像机距离目标的距离
    public float height = 2f; // 摄像机高度
    public float mouseSensitivity = 2f; // 鼠标灵敏度
    public float rotationDamping = 10f; // 旋转阻尼
    public float positionDamping = 10f; // 位置阻尼

    [Header("限制设置")]
    public float minVerticalAngle = -30f; // 最小垂直角度
    public float maxVerticalAngle = 60f; // 最大垂直角度
    public float minDistance = 2f; // 最小距离
    public float maxDistance = 10f; // 最大距离

    [Header("碰撞检测")]
    public bool useCollision = true; // 是否使用碰撞检测
    public LayerMask obstacleLayer = 1; // 障碍物层
    public float collisionRadius = 0.3f; // 碰撞检测半径

    private float currentRotationX; // 当前水平旋转角度
    private float currentRotationY; // 当前垂直旋转角度
    private float currentDistance; // 当前距离
    private Vector3 currentVelocity; // 用于平滑移动

    private void Start()
    {
        // 如果没有指定目标，尝试查找玩家
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                // 查找ThirdPersonController组件
                ThirdPersonController controller = FindObjectOfType<ThirdPersonController>();
                if (controller != null)
                {
                    target = controller.transform;
                }
            }
        }

        // 初始化角度
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            currentRotationX = angles.y;
            currentRotationY = angles.x;
            currentDistance = distance;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleInput();
        UpdateCameraPosition();
    }

    private void HandleInput()
    {
        // 鼠标输入（X控制Yaw，Y控制Pitch）
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 滚轮缩放（向上推远，向下拉近）
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * 2f; // 向上滚动（正值）减小距离（推远），向下滚动（负值）增加距离（拉近）
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // 更新旋转角度
        currentRotationX += mouseX; // 鼠标X控制水平旋转（Yaw）
        currentRotationY -= mouseY; // 鼠标Y控制垂直旋转（Pitch），取反因为鼠标向上应该是向上看
        currentRotationY = Mathf.Clamp(currentRotationY, minVerticalAngle, maxVerticalAngle);
    }

    private void UpdateCameraPosition()
    {
        // 计算目标位置
        Quaternion rotation = Quaternion.Euler(currentRotationY, currentRotationX, 0f);
        Vector3 targetPosition = target.position + offset;
        Vector3 desiredPosition = targetPosition - rotation * Vector3.forward * distance;

        // 碰撞检测
        if (useCollision)
        {
            desiredPosition = CheckCollision(targetPosition, desiredPosition);
        }

        // 平滑移动摄像机
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / positionDamping);

        // 平滑旋转摄像机
        Quaternion desiredRotation = Quaternion.LookRotation((targetPosition - transform.position).normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * rotationDamping);
    }

    private Vector3 CheckCollision(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        RaycastHit hit;
        if (Physics.SphereCast(from, collisionRadius, direction.normalized, out hit, distance, obstacleLayer))
        {
            // 如果检测到碰撞，将摄像机位置调整到碰撞点前方
            return hit.point - direction.normalized * collisionRadius;
        }

        return to;
    }

    // 公共方法：设置目标
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // 公共方法：重置摄像机角度
    public void ResetCamera()
    {
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            currentRotationX = angles.y;
            currentRotationY = angles.x;
        }
    }
}

