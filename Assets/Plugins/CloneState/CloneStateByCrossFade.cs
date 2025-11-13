using UnityEngine;

/// <summary>
/// 挂载到动画状态的跳转组件，支持挂载在空状态上，兼容状态的Motion为空的情况，通过CrossFade实现0延迟瞬间跳转（适配 Unity 2017.4.30f1）
/// </summary>
public class CloneStateByCrossFade : StateMachineBehaviour
{
    [Header("跳转配置")]
    [Tooltip("输入状态机中目标源状态的名称（必须与Animator窗口中的状态名完全一致，包含层级路径，如'Base Layer.Idle'）")]
    public string SourceState; // 暴露给Inspector的字符串参数，用于输入源状态名称
    
    [Tooltip("是否在初始化时就尝试跳转（适用于空状态）")]
    public bool JumpOnInitialize = true; // 在状态机初始化时就尝试跳转，增强空状态兼容性

    [Header("调试选项")]
    [Tooltip("是否启用详细日志输出")]
    public bool EnableDebugLog = false; // 控制是否输出调试日志

    private Animator _animator;
    private int _sourceStateHash; // 源状态的哈希值（优化查找效率，适配2017版本）
    private bool _isInitialized = false; // 标记是否已初始化

    /// <summary>
    /// 状态机初始化时调用（仅一次）
    /// </summary>
    /// <summary>
    /// 状态机初始化时调用（仅一次），兼容Motion为空的状态
    /// </summary>
    override public void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
    {
        _animator = animator;
        InitializeState();
        
        // 支持在初始化时直接跳转（对于空状态或Motion为空的状态尤为重要）
        if (JumpOnInitialize && _isInitialized && _sourceStateHash != 0)
        {
            if (EnableDebugLog)
                Debug.Log("【CloneStateByCrossFade】状态机初始化时直接尝试跳转", animator);
            
            // 使用CrossFade实现0延迟瞬间跳转 - 兼容Motion为空的状态
            SafeCrossFade(_sourceStateHash, -1);
        }
    }
    
    /// <summary>
    /// 初始化状态配置 
    /// </summary>
    private void InitializeState()
    {
        // 校验SourceState是否为空，为空则提示警告
        if (string.IsNullOrEmpty(SourceState))
        {
            if (EnableDebugLog)
            Debug.LogWarning("【CloneStateByCrossFade】未设置SourceState！请在Inspector中输入目标状态名称", _animator);
            _sourceStateHash = 0;
            _isInitialized = false;
            return;
        }

        // 在Unity 2017中，建议使用完整路径（包含层名）创建哈希值
        // 尝试使用完整路径（如果用户未提供）
        string fullStatePath = SourceState;
        if (!fullStatePath.Contains("."))
        {
            // 如果用户只提供了状态名，自动添加基础层前缀
            fullStatePath = "Base Layer." + SourceState;
            if (EnableDebugLog)
                Debug.Log("【CloneStateByCrossFade】自动添加基础层路径：" + fullStatePath, _animator);
        }

        // 将完整状态路径转换为哈希值
        _sourceStateHash = Animator.StringToHash(fullStatePath);
        _isInitialized = true;
        
        if (EnableDebugLog)
            Debug.Log("【CloneStateByCrossFade】已初始化，准备跳转到状态：" + fullStatePath + " (Hash: " + _sourceStateHash + ")", _animator);
    }

    /// <summary>
    /// 进入当前挂载的状态时调用（每次进入都会触发）
    /// </summary>
    /// <summary>
    /// 进入当前挂载的状态时调用（每次进入都会触发），兼容Motion为空的状态
    /// </summary>
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 确保初始化
        if (!_isInitialized)
        {
            _animator = animator;
            InitializeState();
        }

        // 跳过无效状态，避免报错
        if (!_isInitialized || _sourceStateHash == 0)
        {
            if (EnableDebugLog)
            Debug.LogWarning("【CloneStateByCrossFade】源状态无效，跳过跳转", animator);
            return;
        }

        // 在Unity 2017中，使用CrossFade方法实现0延迟瞬间跳转
        // 参数: stateNameHash, transitionDuration(0), layer, normalizedTime
        if (EnableDebugLog)
            Debug.Log("【CloneStateByCrossFade】尝试从状态「" + stateInfo.shortNameHash + "」瞬间跳转到「" + SourceState + "」", animator);

        // 使用CrossFade实现0延迟瞬间跳转 - 兼容Motion为空的状态
        SafeCrossFade(_sourceStateHash, layerIndex);
        
        // 额外的保障措施：连续多次尝试跳转以确保成功
        for (int i = 0; i < 2; i++)
        {
            SafeCrossFade(_sourceStateHash, layerIndex);
        }
        
        if (EnableDebugLog)
            Debug.Log("【CloneStateByCrossFade】瞬间跳转开始", animator);
    }
    
    /// <summary>
    /// 状态更新时调用，确保跳转确实发生
    /// </summary>
    /// <summary>
    /// 安全地执行CrossFade操作，增加对Motion为空状态的兼容性
    /// </summary>
    private void SafeCrossFade(int stateHash, int layerIndex)
    {
        if (_animator != null)
        {
            try
            {
                _animator.CrossFade(stateHash, 0.0f, layerIndex, 0.0f);
                _animator.Update(0.0f);
            }
            catch (System.Exception e)
            {
                if (EnableDebugLog)
                    Debug.LogWarning("【CloneStateByCrossFade】CrossFade执行异常：" + e.Message, _animator);
                // 失败时尝试直接Play
                SafePlay(stateHash, layerIndex);
            }
        }
    }
    
    /// <summary>
    /// 安全地执行Play操作，作为备选方案
    /// </summary>
    private void SafePlay(int stateHash, int layerIndex)
    {
        if (_animator != null)
        {
            try
            {
                _animator.Play(stateHash, layerIndex, 0.0f);
                _animator.Update(0.0f);
            }
            catch (System.Exception e)
            {
                if (EnableDebugLog)
                    Debug.LogWarning("【CloneStateByCrossFade】Play执行异常：" + e.Message, _animator);
            }
        }
    }
    
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 确保_animator引用有效 - 兼容Motion为空的状态
        if (_animator == null)
            _animator = animator;
            
        // 如果初始化成功，再次尝试确保跳转到目标状态（防止某些情况下跳转被忽略）
        // 对于空状态或Motion为空的状态，需要更激进的跳转策略
        if (_isInitialized && _sourceStateHash != 0)
        {
            // 检查当前状态是否已经是目标状态，如果不是则再次尝试跳转
            if (stateInfo.shortNameHash != _sourceStateHash && stateInfo.normalizedTime < 0.2f) // 状态开始时多次尝试
            {
                // 同时使用CrossFade和Play方法，增加对Motion为空状态的兼容性
                SafeCrossFade(_sourceStateHash, layerIndex);
                SafePlay(_sourceStateHash, layerIndex);
                
                if (EnableDebugLog && stateInfo.normalizedTime < 0.1f) // 只在状态刚开始时输出日志，避免刷屏
                    Debug.Log("【CloneStateByCrossFade】OnStateUpdate中确认过渡", animator);
            }
        }
    }
}