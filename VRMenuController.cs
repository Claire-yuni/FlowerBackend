using UnityEngine;

namespace MeshyFlowerVR.UI
{
    /// <summary>
    /// VR 主菜单控制器
    /// 
    /// 在 VR 空间中放置 "生成花朵" / "清除画布" / "重新来" 等功能按钮。
    /// 用户用手指点按触发。
    /// 
    /// 场景搭建:
    /// 1. 创建空物体放在画布下方或旁边
    /// 2. 挂载此脚本
    /// 3. 拖入 FlowerGeneratorManager 和 VRDrawingCanvas 引用
    /// </summary>
    public class VRMenuController : MonoBehaviour
    {
        [SerializeField] private Core.FlowerGeneratorManager manager;
        [SerializeField] private Drawing.VRDrawingCanvas drawingCanvas;

        [Header("按钮布局")]
        [SerializeField] private float buttonSpacing = 0.12f;

        private void Start()
        {
            if (manager == null || drawingCanvas == null)
            {
                Debug.LogError("[VRMenu] 缺少组件引用");
                return;
            }

            CreateMainButtons();
        }

        private void CreateMainButtons()
        {
            // 生成按钮 (最重要，放中间且最大)
            var generateBtn = CreateButton(
                "生成花朵",
                new Vector3(0, 0, 0),
                new Vector3(0.14f, 0.05f, 0.02f),
                new Color(0.2f, 0.75f, 0.3f)
            );
            var genTrigger = generateBtn.AddComponent<Drawing.VRButtonTrigger>();
            genTrigger.OnPressed += () => manager.SubmitDrawing();

            // 清除按钮
            var clearBtn = CreateButton(
                "清除画布",
                new Vector3(-buttonSpacing, 0, 0),
                new Vector3(0.1f, 0.04f, 0.02f),
                new Color(0.8f, 0.6f, 0.2f)
            );
            var clearTrigger = clearBtn.AddComponent<Drawing.VRButtonTrigger>();
            clearTrigger.OnPressed += () => drawingCanvas.ClearCanvas();

            // 撤销按钮
            var undoBtn = CreateButton(
                "撤销",
                new Vector3(-buttonSpacing * 2, 0, 0),
                new Vector3(0.08f, 0.04f, 0.02f),
                new Color(0.6f, 0.6f, 0.6f)
            );
            var undoTrigger = undoBtn.AddComponent<Drawing.VRButtonTrigger>();
            undoTrigger.OnPressed += () => drawingCanvas.Undo();

            // 重新开始按钮
            var newBtn = CreateButton(
                "重新来",
                new Vector3(buttonSpacing, 0, 0),
                new Vector3(0.1f, 0.04f, 0.02f),
                new Color(0.3f, 0.5f, 0.9f)
            );
            var newTrigger = newBtn.AddComponent<Drawing.VRButtonTrigger>();
            newTrigger.OnPressed += () => manager.StartNewDrawing();
        }

        private GameObject CreateButton(string label, Vector3 localPos, Vector3 scale, Color color)
        {
            var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
            btn.name = $"Btn_{label}";
            btn.transform.SetParent(transform, false);
            btn.transform.localPosition = localPos;
            btn.transform.localScale = scale;

            btn.GetComponent<Renderer>().material = new Material(Shader.Find("Unlit/Color")) { color = color };
            btn.GetComponent<BoxCollider>().isTrigger = true;

            var rb = btn.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // 文字标签
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btn.transform, false);
            labelObj.transform.localPosition = new Vector3(0, 0, -0.6f);
            labelObj.transform.localScale = Vector3.one * 0.2f;

            var tm = labelObj.AddComponent<TextMesh>();
            tm.text = label;
            tm.fontSize = 24;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            return btn;
        }
    }
}
