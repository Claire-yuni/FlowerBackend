using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MeshyFlowerVR.Core
{
    /// <summary>
    /// 后端通信客户端
    /// 
    /// 职责: Unity ↔ Python 后端 HTTP 通信
    /// 不直接调 Meshy API，API Key 安全地保存在后端。
    /// </summary>
    public class BackendClient : MonoBehaviour
    {
        [Header("后端地址")]
        [Tooltip("Python FastAPI 后端的地址")]
        [SerializeField] private string backendUrl = "http://localhost:8000";

        [Header("轮询设置")]
        [SerializeField] private float pollInterval = 4f;
        [SerializeField] private float maxWaitSeconds = 600f;

        // ============================================================
        // 事件
        // ============================================================
        public event Action<string> OnTaskCreated;          // local task_id
        public event Action<int> OnProgressUpdated;         // 0-100
        public event Action<string> OnStatusChanged;        // 状态描述
        public event Action<string> OnModelUrlReady;        // GLB 下载地址
        public event Action<byte[]> OnModelDownloaded;      // GLB 二进制数据
        public event Action<string> OnError;

        /// <summary>
        /// 提交草图并开始生成流程。
        /// 完整流程: 上传 PNG → 拿到 taskId → 轮询 → 下载模型
        /// </summary>
        public void SubmitSketch(byte[] pngData, string flowerStyle = "fantasy", string flowerType = "auto")
        {
            StartCoroutine(SubmitAndPollCoroutine(pngData, flowerStyle, flowerType));
        }

        // ============================================================
        // 主流程协程
        // ============================================================

        private IEnumerator SubmitAndPollCoroutine(byte[] pngData, string style, string type)
        {
            // --- 第 1 步: 上传草图到后端 /generate ---
            OnStatusChanged?.Invoke("正在上传草图...");

            string taskId = null;
            yield return StartCoroutine(PostGenerate(pngData, style, type, (id) => taskId = id));

            if (string.IsNullOrEmpty(taskId))
            {
                OnError?.Invoke("上传失败，请检查后端是否运行");
                yield break;
            }

            OnTaskCreated?.Invoke(taskId);
            OnStatusChanged?.Invoke("已提交，AI 正在生成花朵...");

            // --- 第 2 步: 轮询后端 /status/{taskId} ---
            float elapsed = 0f;
            string modelUrl = null;

            while (elapsed < maxWaitSeconds)
            {
                yield return new WaitForSeconds(pollInterval);
                elapsed += pollInterval;

                StatusResult result = null;
                yield return StartCoroutine(GetStatus(taskId, (r) => result = r));

                if (result == null)
                {
                    OnError?.Invoke("无法获取任务状态");
                    yield break;
                }

                OnProgressUpdated?.Invoke(result.progress);
                OnStatusChanged?.Invoke(GetStatusMessage(result.status, result.progress));

                if (result.status == "SUCCEEDED")
                {
                    modelUrl = result.model_url;
                    break;
                }

                if (result.status == "FAILED" || result.status == "TIMEOUT")
                {
                    OnError?.Invoke($"生成失败: {result.error ?? result.status}");
                    yield break;
                }
            }

            if (string.IsNullOrEmpty(modelUrl))
            {
                OnError?.Invoke("等待超时，请稍后重试");
                yield break;
            }

            OnModelUrlReady?.Invoke(modelUrl);

            // --- 第 3 步: 下载 GLB 模型 ---
            OnStatusChanged?.Invoke("正在下载 3D 模型...");

            byte[] glbData = null;
            yield return StartCoroutine(DownloadFile(modelUrl, (data) => glbData = data));

            if (glbData != null && glbData.Length > 0)
            {
                OnStatusChanged?.Invoke("下载完成！");
                OnModelDownloaded?.Invoke(glbData);
            }
            else
            {
                OnError?.Invoke("模型下载失败");
            }
        }

        // ============================================================
        // HTTP: POST /generate
        // ============================================================

        private IEnumerator PostGenerate(byte[] pngData, string style, string type, Action<string> onTaskId)
        {
            var form = new WWWForm();
            form.AddBinaryData("sketch", pngData, "sketch.png", "image/png");
            form.AddField("flower_style", style);
            form.AddField("flower_type", type);

            using (var request = UnityWebRequest.Post($"{backendUrl}/generate", form))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<GenerateResponse>(request.downloadHandler.text);
                    onTaskId?.Invoke(response.task_id);
                }
                else
                {
                    Debug.LogError($"[BackendClient] POST /generate 失败: {request.error}\n{request.downloadHandler.text}");
                    OnError?.Invoke($"后端连接失败: {request.error}");
                    onTaskId?.Invoke(null);
                }
            }
        }

        // ============================================================
        // HTTP: GET /status/{taskId}
        // ============================================================

        private IEnumerator GetStatus(string taskId, Action<StatusResult> onResult)
        {
            using (var request = UnityWebRequest.Get($"{backendUrl}/status/{taskId}"))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var result = JsonUtility.FromJson<StatusResult>(request.downloadHandler.text);
                    onResult?.Invoke(result);
                }
                else
                {
                    Debug.LogError($"[BackendClient] GET /status 失败: {request.error}");
                    onResult?.Invoke(null);
                }
            }
        }

        // ============================================================
        // HTTP: 下载 GLB
        // ============================================================

        private IEnumerator DownloadFile(string url, Action<byte[]> onData)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onData?.Invoke(request.downloadHandler.data);
                }
                else
                {
                    Debug.LogError($"[BackendClient] 下载失败: {request.error}");
                    onData?.Invoke(null);
                }
            }
        }

        // ============================================================
        // 状态映射 (中文 + 趣味文案)
        // ============================================================

        private string GetStatusMessage(string status, int progress)
        {
            return status switch
            {
                "PENDING" => "种子已播下，等待发芽...",
                "IN_PROGRESS" when progress < 30 => $"正在分析花朵草图... ({progress}%)",
                "IN_PROGRESS" when progress < 60 => $"花瓣正在生长... ({progress}%)",
                "IN_PROGRESS" when progress < 90 => $"添加细节和纹理... ({progress}%)",
                "IN_PROGRESS" => $"即将绽放... ({progress}%)",
                "SUCCEEDED" => "花朵绽放了！",
                "FAILED" => "花朵枯萎了...",
                _ => $"{status} ({progress}%)"
            };
        }

        // ============================================================
        // JSON 数据结构
        // ============================================================

        [Serializable]
        private class GenerateResponse
        {
            public string task_id;
            public string meshy_task_id;
            public string status;
            public string prompt;
        }

        [Serializable]
        public class StatusResult
        {
            public string task_id;
            public string status;
            public int progress;
            public string model_url;
            public string thumbnail_url;
            public string error;
        }
    }
}
