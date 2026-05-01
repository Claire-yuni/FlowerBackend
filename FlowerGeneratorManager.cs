using UnityEngine;
using MeshyFlowerVR.Drawing;

namespace MeshyFlowerVR.Core
{
    /// <summary>
    /// 花朵生成器 - 主流程管理器
    /// 
    /// 串联整个流程:
    /// 画画 → 导出 PNG → 发送后端 → 轮询 → 加载模型 → 展示
    /// 
    /// 场景搭建:
    /// 1. 创建空物体 "FlowerGeneratorManager"
    /// 2. 挂载此脚本 + BackendClient + GLBModelLoader
    /// 3. 在 Inspector 中拖入:
    ///    - drawingCanvas: VR 画布
    ///    - styleSelector: 风格选择 UI (可选)
    ///    - backendClient: 后端通信
    ///    - modelLoader: 模型加载器
    ///    - 各个状态显示对象
    /// </summary>
    public class FlowerGeneratorManager : MonoBehaviour
    {
        [Header("核心组件")]
        [SerializeField] private VRDrawingCanvas drawingCanvas;
        [SerializeField] private BackendClient backendClient;
        [SerializeField] private GLBModelLoader modelLoader;

        [Header("VR UI 面板 (3D 空间中的面板)")]
        [Tooltip("绘画阶段显示的面板 (画布+工具栏)")]
        [SerializeField] private GameObject drawingPanel;

        [Tooltip("生成等待阶段的面板")]
        [SerializeField] private GameObject loadingPanel;

        [Tooltip("结果展示阶段的面板")]
        [SerializeField] private GameObject resultPanel;

        [Header("状态显示 (可选, 用 TextMesh 或 TMP_Text)")]
        [SerializeField] private TextMesh statusText;
        [SerializeField] private TextMesh progressText;

        [Header("风格设置")]
        [SerializeField] private string currentFlowerStyle = "fantasy";
        [SerializeField] private string currentFlowerType = "auto";

        [Header("音效 (可选)")]
        [SerializeField] private AudioClip submitSound;
        [SerializeField] private AudioClip successSound;
        [SerializeField] private AudioClip errorSound;
        private AudioSource audioSource;

        public enum GeneratorState
        {
            Drawing,    // 用户正在画
            Submitting, // 上传中
            Generating, // Meshy 生成中
            Loading,    // 下载模型中
            Displaying, // 展示结果
            Error       // 出错了
        }

        public GeneratorState CurrentState { get; private set; } = GeneratorState.Drawing;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            // 绑定后端事件
            if (backendClient != null)
            {
                backendClient.OnTaskCreated += OnTaskCreated;
                backendClient.OnProgressUpdated += OnProgressUpdated;
                backendClient.OnStatusChanged += OnStatusChanged;
                backendClient.OnModelDownloaded += OnModelDownloaded;
                backendClient.OnError += OnError;
            }

            // 绑定模型加载完成事件
            if (modelLoader != null)
            {
                modelLoader.OnModelLoaded += OnModelLoaded;
                modelLoader.OnLoadError += (err) => OnError(err);
            }

            SetState(GeneratorState.Drawing);
        }

        // ============================================================
        // 公开方法 - 由 VR 按钮调用
        // ============================================================

        /// <summary>
        /// 提交当前画布内容进行生成。
        /// 绑定到 VR 场景中的 "Generate" 按钮。
        /// </summary>
        public void SubmitDrawing()
        {
            if (CurrentState != GeneratorState.Drawing)
            {
                Debug.LogWarning("[Manager] 当前状态不允许提交");
                return;
            }

            if (drawingCanvas == null || backendClient == null)
            {
                Debug.LogError("[Manager] 缺少必要组件引用");
                return;
            }

            if (!drawingCanvas.HasContent)
            {
                SetStatusText("请先画一朵花再提交哦！");
                return;
            }

            SetState(GeneratorState.Submitting);
            PlaySound(submitSound);

            // 导出 PNG 并发送到后端
            byte[] pngData = drawingCanvas.ExportPNG();
            backendClient.SubmitSketch(pngData, currentFlowerStyle, currentFlowerType);
        }

        /// <summary>
        /// 重新开始绘画。
        /// 绑定到 VR 场景中的 "New Drawing" 按钮。
        /// </summary>
        public void StartNewDrawing()
        {
            modelLoader?.ClearCurrentModel();
            drawingCanvas?.ClearCanvas();
            SetState(GeneratorState.Drawing);
        }

        /// <summary>
        /// 设置花朵风格。
        /// 由 FlowerStyleSelector 调用。
        /// </summary>
        public void SetFlowerStyle(string style)
        {
            currentFlowerStyle = style;
            Debug.Log($"[Manager] 风格切换: {style}");
        }

        /// <summary>
        /// 设置花朵类型。
        /// 由 FlowerStyleSelector 调用。
        /// </summary>
        public void SetFlowerType(string type)
        {
            currentFlowerType = type;
            Debug.Log($"[Manager] 类型切换: {type}");
        }

        // ============================================================
        // 后端回调
        // ============================================================

        private void OnTaskCreated(string taskId)
        {
            SetState(GeneratorState.Generating);
            Debug.Log($"[Manager] 任务已创建: {taskId}");
        }

        private void OnProgressUpdated(int progress)
        {
            if (progressText != null)
                progressText.text = $"{progress}%";
        }

        private void OnStatusChanged(string status)
        {
            SetStatusText(status);
        }

        private void OnModelDownloaded(byte[] glbData)
        {
            SetState(GeneratorState.Loading);
            SetStatusText("正在加载 3D 模型...");
            modelLoader.LoadFromBytes(glbData);
        }

        private void OnModelLoaded(GameObject model)
        {
            SetState(GeneratorState.Displaying);
            SetStatusText("花朵绽放了！");
            PlaySound(successSound);
        }

        private void OnError(string error)
        {
            SetState(GeneratorState.Error);
            SetStatusText($"出了点问题: {error}");
            PlaySound(errorSound);

            // 5 秒后自动回到绘画状态
            Invoke(nameof(BackToDrawing), 5f);
        }

        private void BackToDrawing()
        {
            if (CurrentState == GeneratorState.Error)
                SetState(GeneratorState.Drawing);
        }

        // ============================================================
        // 状态管理
        // ============================================================

        private void SetState(GeneratorState state)
        {
            CurrentState = state;

            // 切换面板显示
            bool showDrawing = state == GeneratorState.Drawing;
            bool showLoading = state == GeneratorState.Submitting
                            || state == GeneratorState.Generating
                            || state == GeneratorState.Loading;
            bool showResult = state == GeneratorState.Displaying;

            if (drawingPanel != null) drawingPanel.SetActive(showDrawing);
            if (loadingPanel != null) loadingPanel.SetActive(showLoading);
            if (resultPanel != null) resultPanel.SetActive(showResult || state == GeneratorState.Error);

            Debug.Log($"[Manager] 状态切换: {state}");
        }

        private void SetStatusText(string text)
        {
            if (statusText != null) statusText.text = text;
            Debug.Log($"[Manager] {text}");
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
                audioSource.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            if (backendClient != null)
            {
                backendClient.OnTaskCreated -= OnTaskCreated;
                backendClient.OnProgressUpdated -= OnProgressUpdated;
                backendClient.OnStatusChanged -= OnStatusChanged;
                backendClient.OnModelDownloaded -= OnModelDownloaded;
                backendClient.OnError -= OnError;
            }
        }
    }
}
