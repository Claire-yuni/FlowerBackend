using UnityEngine;

namespace MeshyFlowerVR.Drawing
{
    /// <summary>
    /// 绘画工具栏
    /// 
    /// 在 VR 场景中以 3D 物体形式呈现颜色选择和功能按钮。
    /// 用户用手指点按对应的 3D 按钮来切换颜色、调整粗细、撤销、清除。
    ///
    /// 场景搭建:
    /// 1. 创建一排小 Sphere 作为颜色按钮，每个挂 ColorButton 组件
    /// 2. 创建 Cube 作为功能按钮 (撤销/清除)
    /// 3. 所有按钮需要 Collider (trigger) + Rigidbody (isKinematic)
    /// 4. 将此脚本挂在工具栏父物体上
    /// </summary>
    public class DrawingToolkit : MonoBehaviour
    {
        [SerializeField] private VRDrawingCanvas canvas;

        [Header("预设颜色")]
        [SerializeField] private ColorButtonConfig[] colorButtons = new ColorButtonConfig[]
        {
            new() { name = "Red",     color = new Color(0.9f, 0.2f, 0.3f) },
            new() { name = "Pink",    color = new Color(1.0f, 0.5f, 0.7f) },
            new() { name = "Yellow",  color = new Color(1.0f, 0.85f, 0.0f) },
            new() { name = "Purple",  color = new Color(0.6f, 0.2f, 0.8f) },
            new() { name = "Orange",  color = new Color(1.0f, 0.6f, 0.0f) },
            new() { name = "Green",   color = new Color(0.2f, 0.6f, 0.2f) },
            new() { name = "Black",   color = new Color(0.1f, 0.1f, 0.1f) },
            new() { name = "Eraser",  color = Color.white },
        };

        [Header("画笔大小预设")]
        [SerializeField] private int[] brushSizes = { 2, 4, 6, 10, 16 };

        [Header("自动生成按钮的参数")]
        [SerializeField] private float buttonRadius = 0.02f;
        [SerializeField] private float buttonSpacing = 0.05f;
        [SerializeField] private float sizeButtonSpacing = 0.06f;

        private GameObject selectedIndicator;

        private void Start()
        {
            if (canvas == null)
            {
                Debug.LogError("[DrawingToolkit] 缺少 VRDrawingCanvas 引用");
                return;
            }

            GenerateColorButtons();
            GenerateSizeButtons();
            GenerateFunctionButtons();
        }

        // ============================================================
        // 自动生成 3D 按钮
        // ============================================================

        private void GenerateColorButtons()
        {
            float startX = -(colorButtons.Length - 1) * buttonSpacing * 0.5f;

            for (int i = 0; i < colorButtons.Length; i++)
            {
                var config = colorButtons[i];
                var btn = CreateSphereButton(
                    config.name,
                    new Vector3(startX + i * buttonSpacing, 0, 0),
                    config.color,
                    buttonRadius
                );

                // 点击事件
                Color capturedColor = config.color;
                var trigger = btn.AddComponent<VRButtonTrigger>();
                trigger.OnPressed += () =>
                {
                    canvas.SetBrushColor(capturedColor);
                    MoveIndicator(btn.transform.position);
                };
            }
        }

        private void GenerateSizeButtons()
        {
            float startX = -(brushSizes.Length - 1) * sizeButtonSpacing * 0.5f;
            float yOffset = -0.06f;

            for (int i = 0; i < brushSizes.Length; i++)
            {
                int size = brushSizes[i];
                float radius = Mathf.Lerp(0.008f, 0.025f, (float)i / (brushSizes.Length - 1));

                var btn = CreateSphereButton(
                    $"Size_{size}",
                    new Vector3(startX + i * sizeButtonSpacing, yOffset, 0),
                    new Color(0.6f, 0.6f, 0.6f),
                    radius
                );

                var trigger = btn.AddComponent<VRButtonTrigger>();
                int capturedSize = size;
                trigger.OnPressed += () => canvas.SetBrushSize(capturedSize);
            }
        }

        private void GenerateFunctionButtons()
        {
            float yOffset = -0.12f;

            // 撤销按钮
            var undoBtn = CreateCubeButton("Undo", new Vector3(-0.04f, yOffset, 0), new Color(0.9f, 0.7f, 0.2f));
            var undoTrigger = undoBtn.AddComponent<VRButtonTrigger>();
            undoTrigger.OnPressed += () => canvas.Undo();

            // 清除按钮
            var clearBtn = CreateCubeButton("Clear", new Vector3(0.04f, yOffset, 0), new Color(0.8f, 0.3f, 0.3f));
            var clearTrigger = clearBtn.AddComponent<VRButtonTrigger>();
            clearTrigger.OnPressed += () => canvas.ClearCanvas();
        }

        // ============================================================
        // 按钮创建辅助
        // ============================================================

        private GameObject CreateSphereButton(string name, Vector3 localPos, Color color, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * radius * 2f;

            go.GetComponent<Renderer>().material = CreateUnlitMaterial(color);
            go.GetComponent<SphereCollider>().isTrigger = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            return go;
        }

        private GameObject CreateCubeButton(string name, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(0.06f, 0.03f, 0.015f);

            go.GetComponent<Renderer>().material = CreateUnlitMaterial(color);
            go.GetComponent<BoxCollider>().isTrigger = true;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // 标签文字 (用 TextMesh 简单显示)
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0, 0, -0.55f);
            textGo.transform.localScale = Vector3.one * 0.3f;
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = name;
            tm.fontSize = 24;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            return go;
        }

        private Material CreateUnlitMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = color;
            return mat;
        }

        private void MoveIndicator(Vector3 worldPos)
        {
            if (selectedIndicator == null)
            {
                selectedIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                selectedIndicator.name = "SelectedIndicator";
                selectedIndicator.transform.localScale = Vector3.one * 0.008f;
                selectedIndicator.GetComponent<Renderer>().material = CreateUnlitMaterial(Color.white);
                Destroy(selectedIndicator.GetComponent<Collider>());
            }
            selectedIndicator.transform.position = worldPos + Vector3.up * 0.03f;
        }

        [System.Serializable]
        public struct ColorButtonConfig
        {
            public string name;
            public Color color;
        }
    }

    /// <summary>
    /// VR 按钮触发器 - 检测手指触碰
    /// 需要 Collider (isTrigger) + Rigidbody (isKinematic)
    /// </summary>
    public class VRButtonTrigger : MonoBehaviour
    {
        public event System.Action OnPressed;

        [SerializeField] private float cooldown = 0.5f;
        private float lastPressTime = -1f;

        private void OnTriggerEnter(Collider other)
        {
            // 检查是否是手指/手部碰撞体
            // XR Hands 会自动生成带 Collider 的手指骨骼
            // 你也可以通过 tag 或 layer 过滤
            if (Time.time - lastPressTime < cooldown) return;

            if (IsFingerCollider(other))
            {
                lastPressTime = Time.time;
                OnPressed?.Invoke();

                // 视觉反馈: 按下缩小
                StartCoroutine(PressAnimation());
            }
        }

        private bool IsFingerCollider(Collider col)
        {
            // 方式 1: 通过名字判断 (XR Hands 默认命名含 "Index")
            string name = col.gameObject.name.ToLower();
            if (name.Contains("index") || name.Contains("finger") || name.Contains("tip"))
                return true;

            // 方式 2: 通过 Tag 判断
            if (col.CompareTag("FingerTip"))
                return true;

            // 方式 3: 通过 Layer 判断
            // if (col.gameObject.layer == LayerMask.NameToLayer("Hand"))
            //     return true;

            return false;
        }

        private System.Collections.IEnumerator PressAnimation()
        {
            Vector3 original = transform.localScale;
            transform.localScale = original * 0.8f;
            yield return new WaitForSeconds(0.15f);
            transform.localScale = original;
        }
    }
}
