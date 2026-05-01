"""
花朵生成器后端 - FastAPI
职责: 接收 Unity 草图 → OpenAI 美化成写实照片 → Meshy 建模 → 返回模型地址

启动: python server.py
"""

import os
import uuid
import asyncio
from datetime import datetime
from pathlib import Path

from fastapi import FastAPI, UploadFile, File, Form, HTTPException
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
from dotenv import load_dotenv

from meshy_client import MeshyClient
from prompt_builder import PromptBuilder
from sketch_enhancer import SketchEnhancer

load_dotenv()

app = FastAPI(title="Flower Generator Backend", version="2.0.0")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# ============================================================
# 全局实例
# ============================================================
meshy = MeshyClient(api_key=os.getenv("MESHY_API_KEY", ""))
prompt_builder = PromptBuilder()

# OpenAI 草图美化器（可选，没有 Key 就跳过）
enhancer = None
openai_key = os.getenv("OPENAI_API_KEY", "")
if openai_key:
    try:
        enhancer = SketchEnhancer(api_key=openai_key)
        print("✅ OpenAI 草图美化器已启用")
    except Exception as e:
        print(f"⚠️ OpenAI 初始化失败: {e}，将跳过草图美化步骤")

tasks: dict[str, dict] = {}

UPLOAD_DIR = Path("uploads")
UPLOAD_DIR.mkdir(exist_ok=True)


# ============================================================
# API 端点
# ============================================================

@app.post("/generate")
async def generate_flower(
    sketch: UploadFile = File(..., description="用户绘制的花朵草图 PNG"),
    flower_style: str = Form(default="realistic", description="风格: realistic/cartoon/fantasy/lowpoly"),
    flower_type: str = Form(default="auto", description="花朵类型: auto/rose/tulip/daisy/lotus/sunflower"),
):
    # 1. 保存草图
    file_ext = sketch.filename.split(".")[-1] if sketch.filename else "png"
    local_id = str(uuid.uuid4())[:8]
    save_path = UPLOAD_DIR / f"{local_id}.{file_ext}"

    content = await sketch.read()
    with open(save_path, "wb") as f:
        f.write(content)

    print(f"[{local_id}] 草图已保存: {save_path}")

    # 2. OpenAI 草图美化（如果可用）
    image_for_meshy = str(save_path)

    if enhancer is not None:
        try:
            print(f"[{local_id}] 正在用 OpenAI 美化草图...")
            enhanced_path = await enhancer.enhance_sketch(
                sketch_path=str(save_path),
                flower_type=flower_type,
                style=flower_style,
            )
            image_for_meshy = enhanced_path
            print(f"[{local_id}] ✅ 草图美化完成: {enhanced_path}")
        except Exception as e:
            print(f"[{local_id}] ⚠️ OpenAI 美化失败: {e}，使用原始草图")
            image_for_meshy = str(save_path)
    else:
        print(f"[{local_id}] OpenAI 未配置，使用原始草图")

    # 3. 构建 prompt
    prompt = prompt_builder.build(
        flower_type=flower_type,
        style=flower_style,
    )

    # 4. 调用 Meshy API
    try:
        meshy_task_id = await meshy.create_image_to_3d(
            image_path=image_for_meshy,
            prompt=prompt,
        )
    except Exception as e:
        raise HTTPException(status_code=502, detail=f"Meshy API 调用失败: {str(e)}")

    # 5. 记录任务
    tasks[local_id] = {
        "local_id": local_id,
        "meshy_task_id": meshy_task_id,
        "status": "PENDING",
        "progress": 0,
        "model_url": None,
        "thumbnail_url": None,
        "flower_style": flower_style,
        "flower_type": flower_type,
        "prompt": prompt,
        "enhanced": enhancer is not None,
        "created_at": datetime.now().isoformat(),
        "error": None,
    }

    # 6. 启动后台轮询
    asyncio.create_task(_poll_task(local_id))

    return JSONResponse({
        "task_id": local_id,
        "meshy_task_id": meshy_task_id,
        "status": "PENDING",
        "prompt": prompt,
        "enhanced": enhancer is not None,
    })


@app.get("/status/{task_id}")
async def get_status(task_id: str):
    task = tasks.get(task_id)
    if not task:
        raise HTTPException(status_code=404, detail="任务不存在")

    return JSONResponse({
        "task_id": task["local_id"],
        "status": task["status"],
        "progress": task["progress"],
        "model_url": task["model_url"],
        "thumbnail_url": task["thumbnail_url"],
        "error": task["error"],
    })


@app.get("/health")
async def health():
    return {
        "status": "ok",
        "meshy_api_key_set": bool(os.getenv("MESHY_API_KEY")),
        "openai_api_key_set": bool(os.getenv("OPENAI_API_KEY")),
        "sketch_enhancer_enabled": enhancer is not None,
    }


# ============================================================
# 后台轮询
# ============================================================

async def _poll_task(local_id: str):
    task = tasks[local_id]
    meshy_task_id = task["meshy_task_id"]

    for _ in range(120):
        await asyncio.sleep(5)

        try:
            result = await meshy.get_task_status(meshy_task_id)
        except Exception as e:
            task["error"] = str(e)
            task["status"] = "FAILED"
            return

        task["status"] = result["status"]
        task["progress"] = result.get("progress", 0)

        if result["status"] == "SUCCEEDED":
            task["model_url"] = result.get("model_url")
            task["thumbnail_url"] = result.get("thumbnail_url")
            return

        if result["status"] in ("FAILED", "EXPIRED"):
            task["error"] = result.get("error", "Unknown error")
            return

    task["status"] = "TIMEOUT"
    task["error"] = "生成超时，请重试"


if __name__ == "__main__":
    import uvicorn
    print("🌸 花朵生成器后端 v2 启动中...")
    print(f"   Meshy API Key:  {'✅' if os.getenv('MESHY_API_KEY') else '❌'}")
    print(f"   OpenAI API Key: {'✅' if os.getenv('OPENAI_API_KEY') else '❌ (草图美化将跳过)'}")
    print(f"   访问: http://localhost:8000/docs")
    uvicorn.run(app, host="0.0.0.0", port=8000)