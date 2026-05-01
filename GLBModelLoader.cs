using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

// 取消注释以使用 GLTFast (需要先安装 com.atteneder.gltfast)
// using GLTFast;

namespace MeshyFlowerVR.Core
{
    /// <summary>
    /// GLB 模型运行时加载器
    /// 
    /// 将 Meshy 返回的 GLB 二进制数据加载为 Unity GameObject。
    /// 
    /// 必须安装 GLTFast 包: com.atteneder.gltfast
    /// 安装方法: Package Manager → Add by name → com.atteneder.gltfast
    /// </summary>
    public class GLBModelLoader : MonoBehaviour
    {
        [Header("模型放置")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float targetHeight = 0.3f; // 花朵目标高度 (米)
        [SerializeField] private bool autoScale = true;

        [Header("展示")]
        [SerializeField] private bool enableSlowRotation = true;
        [SerializeField] private float rotationSpeed = 15f;

        private GameObject currentModel;
        public GameObject CurrentModel => currentModel;

        public event Action<GameObject> OnModelLoaded;
        public event Action<string> OnLoadError;

        /// <summary>
        /// 从 GLB 字节数据加载模型到场景
        /// </summary>
        public async void LoadFromBytes(byte[] glbData)
        {
            if (glbData == null || glbData.Length == 0)
            {
                OnLoadError?.Invoke("GLB 数据为空");
                return;
            }

            ClearCurrentModel();

            // ============================================================
            // GLTFast 方案 (推荐)
            // 取消下面的注释，并注释掉 "备用方案" 部分
            // ============================================================

            /*
            try
            {
                var gltf = new GltfImport();
                bool success = await gltf.LoadGltfBinary(glbData);

                if (success)
                {
                    currentModel = new GameObject("GeneratedFlower");
                    currentModel.transform.position = GetSpawnPosition();

                    var instantiator = new GameObjectInstantiator(gltf, currentModel.transform);
                    await gltf.InstantiateMainSceneAsync(instantiator);

                    if (autoScale) AutoScaleModel(currentModel);
                    CenterModelAtBase(currentModel);

                    Debug.Log($"[GLBLoader] 模型加载成功，{currentModel.GetComponentsInChildren<MeshFilter>().Length} 个 mesh");
                    OnModelLoaded?.Invoke(currentModel);
                }
                else
                {
                    OnLoadError?.Invoke("GLTFast 解析 GLB 失败");
                }
            }
            catch (Exception e)
            {
                OnLoadError?.Invoke($"加载异常: {e.Message}");
                Debug.LogException(e);
            }
            */

            // ============================================================
            // 备用方案: 保存文件 + 占位模型
            // 在没有 GLTFast 时使用
            // ============================================================

            string dir = Path.Combine(Application.persistentDataPath, "GeneratedFlowers");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"flower_{DateTime.Now:yyyyMMdd_HHmmss}.glb");

            try
            {
                File.WriteAllBytes(path, glbData);
                Debug.Log($"[GLBLoader] GLB 已保存: {path}");
                Debug.Log("[GLBLoader] 请安装 GLTFast 以加载真实模型: com.atteneder.gltfast");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GLBLoader] 保存失败: {e.Message}");
            }

            // 创建占位花朵
            currentModel = CreatePlaceholderFlower();
            OnModelLoaded?.Invoke(currentModel);
        }

        /// <summary>清除当前模型</summary>
        public void ClearCurrentModel()
        {
            if (currentModel != null)
            {
                Destroy(currentModel);
                currentModel = null;
            }
        }

        private void Update()
        {
            if (enableSlowRotation && currentModel != null)
            {
                currentModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }

        // ============================================================
        // 模型调整
        // ============================================================

        private Vector3 GetSpawnPosition()
        {
            return spawnPoint != null ? spawnPoint.position : new Vector3(0, 0.8f, 1f);
        }

        private void AutoScaleModel(GameObject model)
        {
            Bounds bounds = CalculateBounds(model);
            if (bounds.size.magnitude < 0.001f) return;

            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = targetHeight / maxDim;
            model.transform.localScale = Vector3.one * scale;
        }

        private void CenterModelAtBase(GameObject model)
        {
            Bounds bounds = CalculateBounds(model);
            Vector3 offset = bounds.center - model.transform.position;
            offset.y = bounds.min.y - model.transform.position.y;
            model.transform.position -= offset;
            model.transform.position = GetSpawnPosition();
        }

        private Bounds CalculateBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.zero);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        // ============================================================
        // 占位花朵 (没有 GLTFast 时显示)
        // ============================================================

        private GameObject CreatePlaceholderFlower()
        {
            var root = new GameObject("PlaceholderFlower");
            root.transform.position = GetSpawnPosition();

            var mat_stem = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.2f, 0.55f, 0.2f) };
            var mat_center = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.95f, 0.8f, 0.15f) };
            var mat_petal = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.9f, 0.3f, 0.4f) };

            // 茎
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(root.transform, false);
            stem.transform.localScale = new Vector3(0.01f, 0.1f, 0.01f);
            stem.transform.localPosition = new Vector3(0, 0.1f, 0);
            stem.GetComponent<Renderer>().material = mat_stem;
            Destroy(stem.GetComponent<Collider>());

            // 花心
            var center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            center.name = "Center";
            center.transform.SetParent(root.transform, false);
            center.transform.localScale = Vector3.one * 0.04f;
            center.transform.localPosition = new Vector3(0, 0.21f, 0);
            center.GetComponent<Renderer>().material = mat_center;
            Destroy(center.GetComponent<Collider>());

            // 花瓣
            for (int i = 0; i < 6; i++)
            {
                float angle = i * (360f / 6f) * Mathf.Deg2Rad;
                var petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                petal.name = $"Petal_{i}";
                petal.transform.SetParent(root.transform, false);
                petal.transform.localScale = new Vector3(0.05f, 0.015f, 0.03f);
                petal.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 0.04f,
                    0.21f,
                    Mathf.Sin(angle) * 0.04f
                );
                petal.transform.LookAt(center.transform);
                petal.GetComponent<Renderer>().material = mat_petal;
                Destroy(petal.GetComponent<Collider>());
            }

            return root;
        }
    }
}
