using System.Collections;
using UnityEngine;

namespace MeshyFlowerVR.Display
{
    /// <summary>
    /// 花朵展示台 + 生长动画
    /// 
    /// 花朵模型不是突然出现的，而是从地面 "长出来"，有缩放和旋转动画。
    /// 
    /// 场景搭建:
    /// 1. 创建空物体 "FlowerPedestal" 放在用户前方偏下位置
    /// 2. 挂载此脚本
    /// 3. 将 GLBModelLoader 的 spawnPoint 指向此物体
    /// 4. 将 GLBModelLoader 的 OnModelLoaded 绑定到 PresentFlower()
    /// </summary>
    public class FlowerPedestal : MonoBehaviour
    {
        [Header("展示台")]
        [SerializeField] private bool createPedestal = true;
        [SerializeField] private float pedestalRadius = 0.15f;
        [SerializeField] private float pedestalHeight = 0.02f;
        [SerializeField] private Color pedestalColor = new Color(0.35f, 0.25f, 0.15f);

        [Header("生长动画")]
        [SerializeField] private float growDuration = 1.5f;
        [SerializeField] private AnimationCurve growCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float initialScale = 0.01f;
        [SerializeField] private float spinRevolutions = 0.5f;

        [Header("粒子效果 (可选)")]
        [SerializeField] private ParticleSystem bloomParticles;

        [Header("悬浮动画")]
        [SerializeField] private bool enableFloat = true;
        [SerializeField] private float floatAmplitude = 0.01f;
        [SerializeField] private float floatSpeed = 1.5f;

        private GameObject pedestalObject;
        private GameObject currentFlower;
        private Vector3 flowerBasePosition;
        private bool isFloating = false;

        private void Start()
        {
            if (createPedestal)
                CreatePedestalMesh();
        }

        /// <summary>
        /// 展示一朵新花 (带生长动画)。
        /// 由 FlowerGeneratorManager 在模型加载完成后调用。
        /// </summary>
        public void PresentFlower(GameObject flower)
        {
            // 清除旧的
            if (currentFlower != null && currentFlower != flower)
                Destroy(currentFlower);

            currentFlower = flower;

            // 定位到展示台上方
            flowerBasePosition = transform.position + Vector3.up * (pedestalHeight + 0.01f);
            flower.transform.position = flowerBasePosition;

            // 播放生长动画
            StartCoroutine(GrowAnimation(flower));
        }

        private IEnumerator GrowAnimation(GameObject flower)
        {
            isFloating = false;

            Vector3 targetScale = flower.transform.localScale;
            flower.transform.localScale = Vector3.one * initialScale;

            float startY = flowerBasePosition.y - 0.05f;
            Quaternion startRot = flower.transform.rotation;
            Quaternion endRot = startRot * Quaternion.Euler(0, 360f * spinRevolutions, 0);

            // 播放粒子
            if (bloomParticles != null)
            {
                bloomParticles.transform.position = flowerBasePosition;
                bloomParticles.Play();
            }

            float elapsed = 0f;
            while (elapsed < growDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / growDuration);
                float curvedT = growCurve.Evaluate(t);

                // 缩放: 从极小到目标大小
                flower.transform.localScale = Vector3.Lerp(
                    Vector3.one * initialScale,
                    targetScale,
                    curvedT
                );

                // 位置: 略微上升
                flower.transform.position = new Vector3(
                    flowerBasePosition.x,
                    Mathf.Lerp(startY, flowerBasePosition.y, curvedT),
                    flowerBasePosition.z
                );

                // 旋转: 慢慢转
                flower.transform.rotation = Quaternion.Slerp(startRot, endRot, curvedT);

                yield return null;
            }

            // 确保最终状态精确
            flower.transform.localScale = targetScale;
            flower.transform.position = flowerBasePosition;
            flower.transform.rotation = endRot;

            isFloating = enableFloat;
        }

        private void Update()
        {
            // 悬浮动画
            if (isFloating && currentFlower != null)
            {
                float offset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
                currentFlower.transform.position = flowerBasePosition + Vector3.up * offset;
            }
        }

        /// <summary>清除展示台上的花朵</summary>
        public void ClearFlower()
        {
            if (currentFlower != null)
            {
                StartCoroutine(ShrinkAndDestroy(currentFlower));
                currentFlower = null;
                isFloating = false;
            }
        }

        private IEnumerator ShrinkAndDestroy(GameObject flower)
        {
            Vector3 originalScale = flower.transform.localScale;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                flower.transform.localScale = Vector3.Lerp(originalScale, Vector3.one * 0.001f, t);
                yield return null;
            }

            Destroy(flower);
        }

        // ============================================================
        // 展示台网格
        // ============================================================

        private void CreatePedestalMesh()
        {
            pedestalObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestalObject.name = "PedestalMesh";
            pedestalObject.transform.SetParent(transform, false);
            pedestalObject.transform.localPosition = Vector3.zero;
            pedestalObject.transform.localScale = new Vector3(
                pedestalRadius * 2f,
                pedestalHeight,
                pedestalRadius * 2f
            );

            var mat = new Material(Shader.Find("Standard"));
            mat.color = pedestalColor;
            mat.SetFloat("_Glossiness", 0.3f);
            pedestalObject.GetComponent<Renderer>().material = mat;

            // 禁用碰撞 (不需要和手交互)
            Destroy(pedestalObject.GetComponent<Collider>());
        }
    }
}
