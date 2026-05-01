using UnityEngine;

namespace MeshyFlowerVR.UI
{
    /// <summary>
    /// 花朵风格选择器
    /// 
    /// 在 VR 空间中以 3D 面板形式展示花朵类型和风格选项。
    /// 用户用手指点按选择，选择结果传给 FlowerGeneratorManager。
    /// 
    /// 场景搭建:
    /// 1. 创建一个空物体放在画布旁边
    /// 2. 挂载此脚本
    /// 3. 将 FlowerGeneratorManager 拖入 manager 字段
    /// 4. Start() 会自动生成 3D 选择按钮
    /// </summary>
    public class FlowerStyleSelector : MonoBehaviour
    {
        [SerializeField] private Core.FlowerGeneratorManager manager;

        [Header("位置参数")]
        [SerializeField] private float buttonSpacing = 0.07f;

        private readonly StyleOption[] flowerTypes = new StyleOption[]
        {
            new("auto",           "自动识别", new Color(0.5f, 0.5f, 0.5f)),
            new("rose",           "玫瑰",     new Color(0.9f, 0.2f, 0.2f)),
            new("tulip",          "郁金香",   new Color(0.9f, 0.4f, 0.6f)),
            new("daisy",          "雏菊",     new Color(1.0f, 1.0f, 0.8f)),
            new("lotus",          "荷花",     new Color(0.95f, 0.7f, 0.8f)),
            new("sunflower",      "向日葵",   new Color(1.0f, 0.85f, 0.0f)),
            new("cherry_blossom", "樱花",     new Color(1.0f, 0.75f, 0.8f)),
        };

        private readonly StyleOption[] artStyles = new StyleOption[]
        {
            new("fantasy",    "奇幻风",   new Color(0.5f, 0.3f, 0.8f)),
            new("realistic",  "写实风",   new Color(0.3f, 0.6f, 0.3f)),
            new("cartoon",    "卡通风",   new Color(1.0f, 0.6f, 0.2f)),
            new("lowpoly",    "低面风",   new Color(0.2f, 0.6f, 0.8f)),
            new("watercolor", "水彩风",   new Color(0.7f, 0.8f, 0.9f)),
            new("anime",      "动漫风",   new Color(0.9f, 0.5f, 0.7f)),
        };

        private GameObject currentTypeIndicator;
        private GameObject currentStyleIndicator;

        private void Start()
        {
            if (manager == null)
            {
                Debug.LogError("[StyleSelector] 缺少 FlowerGeneratorManager 引用");
                return;
            }

            CreateSection("花朵类型", flowerTypes, 0f, true);
            CreateSection("画风", artStyles, -0.15f, false);
        }

        private void CreateSection(string title, StyleOption[] options, float yOffset, bool isFlowerType)
        {
            // 标题
            var titleObj = new GameObject($"Title_{title}");
            titleObj.transform.SetParent(transform, false);
            titleObj.transform.localPosition = new Vector3(0, yOffset + 0.04f, 0);
            var tm = titleObj.AddComponent<TextMesh>();
            tm.text = title;
            tm.fontSize = 20;
            tm.characterSize = 0.01f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;

            // 按钮
            float startX = -(options.Length - 1) * buttonSpacing * 0.5f;

            for (int i = 0; i < options.Length; i++)
            {
                var opt = options[i];

                var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                btn.name = $"Btn_{opt.key}";
                btn.transform.SetParent(transform, false);
                btn.transform.localPosition = new Vector3(startX + i * buttonSpacing, yOffset, 0);
                btn.transform.localScale = new Vector3(0.06f, 0.035f, 0.012f);

                btn.GetComponent<Renderer>().material = CreateMat(opt.color);
                btn.GetComponent<BoxCollider>().isTrigger = true;

                var rb = btn.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                // 标签
                var label = new GameObject("Label");
                label.transform.SetParent(btn.transform, false);
                label.transform.localPosition = new Vector3(0, 0, -0.6f);
                label.transform.localScale = Vector3.one * 0.25f;
                var labelTM = label.AddComponent<TextMesh>();
                labelTM.text = opt.displayName;
                labelTM.fontSize = 20;
                labelTM.anchor = TextAnchor.MiddleCenter;
                labelTM.color = Color.white;

                // 点击事件
                var trigger = btn.AddComponent<Drawing.VRButtonTrigger>();
                string capturedKey = opt.key;
                bool capturedIsType = isFlowerType;

                trigger.OnPressed += () =>
                {
                    if (capturedIsType)
                    {
                        manager.SetFlowerType(capturedKey);
                        MoveIndicator(ref currentTypeIndicator, btn.transform.position + Vector3.up * 0.03f);
                    }
                    else
                    {
                        manager.SetFlowerStyle(capturedKey);
                        MoveIndicator(ref currentStyleIndicator, btn.transform.position + Vector3.up * 0.03f);
                    }
                };
            }
        }

        private void MoveIndicator(ref GameObject indicator, Vector3 pos)
        {
            if (indicator == null)
            {
                indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicator.name = "SelectionIndicator";
                indicator.transform.localScale = Vector3.one * 0.01f;
                indicator.GetComponent<Renderer>().material = CreateMat(Color.white);
                Destroy(indicator.GetComponent<Collider>());
            }
            indicator.transform.position = pos;
        }

        private Material CreateMat(Color c)
        {
            var m = new Material(Shader.Find("Unlit/Color"));
            m.color = c;
            return m;
        }

        private struct StyleOption
        {
            public string key;
            public string displayName;
            public Color color;

            public StyleOption(string key, string displayName, Color color)
            {
                this.key = key;
                this.displayName = displayName;
                this.color = color;
            }
        }
    }
}
