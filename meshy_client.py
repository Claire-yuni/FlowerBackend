"""
Meshy API 客户端封装
负责: 创建 Image-to-3D 任务 / 查询状态 / 解析返回
"""

import base64
import httpx
from pathlib import Path


class MeshyClient:
    BASE_URL = "https://api.meshy.ai/openapi/v1"

    def __init__(self, api_key: str):
        self.api_key = api_key
        self.headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        }

    async def create_image_to_3d(
        self,
        image_path: str,
        prompt: str = "",
        enable_pbr: bool = True,
        should_remesh: bool = True,
        topology: str = "triangle",
        target_polycount: int = 50000,
    ) -> str:
        """
        创建 Image-to-3D 任务。

        Args:
            image_path: 本地 PNG/JPG 文件路径
            prompt: 辅助文字描述 (可选，但建议填)
            enable_pbr: 启用 PBR 材质
            should_remesh: 启用自动 remesh
            topology: 拓扑类型 triangle/quad
            target_polycount: 目标面数

        Returns:
            Meshy 的 task_id
        """
        # 读取图片并转 base64 data URI
        image_path = Path(image_path)
        suffix = image_path.suffix.lower().lstrip(".")
        mime = "image/png" if suffix == "png" else "image/jpeg"

        with open(image_path, "rb") as f:
            raw = f.read()
        b64 = base64.b64encode(raw).decode("utf-8")
        data_uri = f"data:{mime};base64,{b64}"

        # 构造请求体
        body = {
            "image_url": data_uri,
            "enable_pbr": enable_pbr,
            "should_remesh": should_remesh,
            "should_texture": True,
            "topology": topology,
            "target_polycount": target_polycount,
        }

        async with httpx.AsyncClient(timeout=60) as client:
            resp = await client.post(
                f"{self.BASE_URL}/image-to-3d",
                headers=self.headers,
                json=body,
            )

            if resp.status_code not in (200, 202):
                raise Exception(
                    f"Meshy API 错误 [{resp.status_code}]: {resp.text}"
                )

            data = resp.json()
            task_id = data.get("result")

            if not task_id:
                raise Exception(f"未获取到 task_id: {data}")

            return task_id

    async def get_task_status(self, task_id: str) -> dict:
        """
        查询任务状态。

        Returns:
            {
                "status": "PENDING" | "IN_PROGRESS" | "SUCCEEDED" | "FAILED" | "EXPIRED",
                "progress": 0-100,
                "model_url": "https://..." or None,
                "thumbnail_url": "https://..." or None,
                "error": "..." or None,
            }
        """
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.get(
                f"{self.BASE_URL}/image-to-3d/{task_id}",
                headers=self.headers,
            )

            if resp.status_code != 200:
                raise Exception(
                    f"Meshy API 查询失败 [{resp.status_code}]: {resp.text}"
                )

            data = resp.json()

            result = {
                "status": data.get("status", "UNKNOWN"),
                "progress": data.get("progress", 0),
                "model_url": None,
                "thumbnail_url": data.get("thumbnail_url"),
                "error": None,
            }

            # 提取 GLB 下载地址
            model_urls = data.get("model_urls")
            if model_urls and isinstance(model_urls, dict):
                result["model_url"] = model_urls.get("glb")

            # 提取错误信息
            task_error = data.get("task_error")
            if task_error:
                if isinstance(task_error, dict):
                    result["error"] = task_error.get("message", str(task_error))
                else:
                    result["error"] = str(task_error)

            return result

    async def create_text_to_3d(
        self,
        prompt: str,
        negative_prompt: str = "low quality, low resolution, ugly, blurry",
        should_remesh: bool = True,
        topology: str = "triangle",
        target_polycount: int = 30000,
    ) -> str:
        """
        备用方案: 纯文字生成 3D (当用户草图质量不够时降级使用)。

        Returns:
            Meshy 的 task_id (preview 阶段)
        """
        body = {
            "mode": "preview",
            "prompt": prompt,
            "negative_prompt": negative_prompt,
            "should_remesh": should_remesh,
            "topology": topology,
            "target_polycount": target_polycount,
        }

        async with httpx.AsyncClient(timeout=60) as client:
            resp = await client.post(
                "https://api.meshy.ai/openapi/v2/text-to-3d",
                headers=self.headers,
                json=body,
            )

            if resp.status_code != 200:
                raise Exception(
                    f"Meshy Text-to-3D 错误 [{resp.status_code}]: {resp.text}"
                )

            data = resp.json()
            return data.get("result")

    async def get_text_to_3d_status(self, task_id: str) -> dict:
        """查询 Text-to-3D 任务状态 (v2 端点)"""
        async with httpx.AsyncClient(timeout=30) as client:
            resp = await client.get(
                f"https://api.meshy.ai/openapi/v2/text-to-3d/{task_id}",
                headers=self.headers,
            )

            if resp.status_code != 200:
                raise Exception(
                    f"Meshy Text-to-3D 查询失败 [{resp.status_code}]: {resp.text}"
                )

            data = resp.json()
            result = {
                "status": data.get("status", "UNKNOWN"),
                "progress": data.get("progress", 0),
                "model_url": None,
                "thumbnail_url": data.get("thumbnail_url"),
                "error": None,
            }

            model_urls = data.get("model_urls")
            if model_urls and isinstance(model_urls, dict):
                result["model_url"] = model_urls.get("glb")

            task_error = data.get("task_error")
            if task_error:
                if isinstance(task_error, dict):
                    result["error"] = task_error.get("message", str(task_error))
                else:
                    result["error"] = str(task_error)

            return result
