using UnityEngine;

/// <summary>
/// 挂载到动画状态的跳转组件，进入该状态后立即跳转到指定源状态（适配 Unity 2017.4.30f1）
/// </summary>
public class CloneState : StateMachineBehaviour
{
    [Header("跳转配置")]
    [Tooltip("输入状态机中目标源状态的名称（必须与Animator窗口中的状态名完全一致，包含层级路径，如'Base Layer.Idle'）")]
    public string SourceState; // 暴露给Inspector的字符串参数，用于输入源状态名称

    [Header("调试选项")]
    [Tooltip("是否启用详细日志输出")]
    public bool EnableDebugLog = true; // 控制是否输出调试日志

    private Animator _animator;
    private int _sourceStateHash; // 源状态的哈希值（优化查找效率，适配2017版本）
    private bool _isInitialized = false; // 标记是否已初始化

    /// <summary>
    /// 状态机初始化时调用（仅一次）
    /// </summary>
    override public void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
    {
        _animator = animator;
        InitializeState();
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
                Debug.LogWarning("【CloneState】未设置SourceState！请在Inspector中输入目标状态名称", _animator);
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
                Debug.Log("【CloneState】自动添加基础层路径：" + fullStatePath, _animator);
        }

        // 将完整状态路径转换为哈希值
        _sourceStateHash = Animator.StringToHash(fullStatePath);
        _isInitialized = true;
        
        if (EnableDebugLog)
            Debug.Log("【CloneState】已初始化，准备跳转到状态：" + fullStatePath + " (Hash: " + _sourceStateHash + ")", _animator);
    }

    /// <summary>
    /// 进入当前挂载的状态时调用（每次进入都会触发）
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
                Debug.LogWarning("【CloneState】源状态无效，跳过跳转", animator);
            return;
        }

        // 在Unity 2017中，使用Play方法时需要注意参数顺序和含义
        // 这里使用正确的重载版本：Play(string stateName, int layer, float normalizedTime)
        // 但使用哈希值代替字符串名称
        if (EnableDebugLog)
            Debug.Log("【CloneState】尝试从状态「" + stateInfo.shortNameHash + "」跳转到「" + SourceState + "」", animator);

        // 关键修复：在Unity 2017中，使用Play方法时需要使用正确的参数格式
        // 使用stateNameHash、layer、normalizedTime的组合
        animator.Play(_sourceStateHash, layerIndex, 0.0f);
        
        // 强制更新动画器以确保状态立即切换
        animator.Update(0.0f);
        
        if (EnableDebugLog)
            Debug.Log("【CloneState】跳转完成", animator);
    }
    
    /// <summary>
    /// 状态更新时调用，确保跳转确实发生
    /// </summary>
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 如果初始化成功，再次尝试确保跳转到目标状态（防止某些情况下跳转被忽略）
        if (_isInitialized && _sourceStateHash != 0)
        {
            // 检查当前状态是否已经是目标状态，如果不是则再次尝试跳转
            if (stateInfo.shortNameHash != _sourceStateHash)
            {
                animator.Play(_sourceStateHash, layerIndex, 0.0f);
                animator.Update(0.0f);
                
                if (EnableDebugLog && stateInfo.normalizedTime < 0.1f) // 只在状态刚开始时输出日志，避免刷屏
                    Debug.Log("【CloneState】OnStateUpdate中确认跳转", animator);
            }
        }
    }
}