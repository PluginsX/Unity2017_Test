using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("移动设置")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;
    public float rotationSpeed = 10f;
    public float acceleration = 10f;
    public float deceleration = 10f;

    [Header("移动模式")]
    public bool isWalkMode = false; // false=奔跑模式, true=行走模式
    public KeyCode toggleWalkKey = KeyCode.LeftControl; // Ctrl切换行走/奔跑

    [Header("跳跃设置")]
    public float jumpHeight = 2f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer = 1; // Default layer

    [Header("动画参数名称")]
    public string moveSpeedParam = "MoveSpeed";
    public string isGroundedParam = "InAir"; // 注意：使用InAir的反值表示是否在地面
    public string jumpTriggerParam = "Jump";
    public string attackTriggerParam = "Attack";
    public string sprintBoolParam = "Sprint";

    private CharacterController characterController;
    private Animator animator;
    private Transform cameraTransform;
    private Vector3 velocity;
    private bool isGrounded;
    private float currentSpeed;
    private float targetSpeed;
    private bool isSprinting;
    private bool wasGrounded;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        
        // 查找摄像机（可能在子对象中）
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = FindObjectOfType<Camera>();
        }
        if (cam != null)
        {
            cameraTransform = cam.transform;
        }
    }

    private void Update()
    {
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        HandleAttack();
        UpdateAnimator();
    }

    private void HandleGroundCheck()
    {
        // 使用CharacterController的isGrounded，但也可以使用射线检测
        isGrounded = characterController.isGrounded;
        
        // 如果使用射线检测（更精确）
        // RaycastHit hit;
        // isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, groundCheckDistance + 0.1f, groundLayer);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // 轻微向下的力，确保贴地
        }
    }

    private void HandleMovement()
    {
        // 处理行走/奔跑模式切换（Ctrl键）
        if (Input.GetKeyDown(toggleWalkKey))
        {
            isWalkMode = !isWalkMode;
        }
        
        // 获取输入（WASD键）
        float horizontal = 0f;
        float vertical = 0f;
        
        if (Input.GetKey(KeyCode.W)) vertical = 1f;
        if (Input.GetKey(KeyCode.S)) vertical = -1f;
        if (Input.GetKey(KeyCode.A)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D)) horizontal = 1f;
        
        // 检查是否按下冲刺键（Left Shift）- 只在奔跑时有效
        isSprinting = Input.GetKey(KeyCode.LeftShift) && !isWalkMode && (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f);
        
        // 计算移动方向（相对于摄像机）
        Vector3 moveDirection = Vector3.zero;
        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            
            forward.y = 0f;
            right.y = 0f;
            
            forward.Normalize();
            right.Normalize();
            
            moveDirection = forward * vertical + right * horizontal;
        }
        else
        {
            // 如果没有摄像机，使用世界坐标
            moveDirection = new Vector3(horizontal, 0f, vertical);
        }
        
        // 计算目标速度
        if (moveDirection.magnitude > 0.1f)
        {
            if (isSprinting)
            {
                targetSpeed = sprintSpeed;
            }
            else if (isWalkMode)
            {
                targetSpeed = walkSpeed;
            }
            else
            {
                targetSpeed = runSpeed;
            }
        }
        else
        {
            targetSpeed = 0f;
        }
        
        // 平滑速度变化
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * (targetSpeed > currentSpeed ? acceleration : deceleration));
        
        // 移动角色
        if (moveDirection.magnitude > 0.1f)
        {
            // 旋转角色朝向移动方向
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
            
            // 移动
            characterController.Move(moveDirection.normalized * currentSpeed * Time.deltaTime);
        }
        
        // 应用重力
        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleJump()
    {
        // 空格键跳跃
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            // 触发跳跃动画
            if (!string.IsNullOrEmpty(jumpTriggerParam))
            {
                animator.SetTrigger(jumpTriggerParam);
            }
        }
    }

    private void HandleAttack()
    {
        // 检测攻击输入（鼠标左键或特定按键）
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.J))
        {
            if (!string.IsNullOrEmpty(attackTriggerParam))
            {
                animator.SetTrigger(attackTriggerParam);
            }
        }
    }

    private void UpdateAnimator()
    {
        // 更新移动速度参数
        if (!string.IsNullOrEmpty(moveSpeedParam))
        {
            animator.SetFloat(moveSpeedParam, currentSpeed);
        }
        
        // 更新是否在地面参数（如果参数名是InAir，则使用反值）
        if (!string.IsNullOrEmpty(isGroundedParam))
        {
            if (isGroundedParam == "InAir")
            {
                animator.SetBool(isGroundedParam, !isGrounded);
            }
            else
            {
                animator.SetBool(isGroundedParam, isGrounded);
            }
        }
        
        // 更新冲刺参数
        if (!string.IsNullOrEmpty(sprintBoolParam))
        {
            animator.SetBool(sprintBoolParam, isSprinting && currentSpeed > walkSpeed);
        }
    }
}

