import os
import base64
from pathlib import Path

from openai import OpenAI


class SketchEnhancer:
    def __init__(self, api_key: str = None):
        self.api_key = api_key or os.getenv("OPENAI_API_KEY", "").strip()
        self.base_url = os.getenv("OPENAI_BASE_URL", "").strip()

        if not self.api_key:
            raise ValueError("缺少 OPENAI_API_KEY")

        if self.base_url:
            self.client = OpenAI(api_key=self.api_key, base_url=self.base_url)
        else:
            self.client = OpenAI(api_key=self.api_key)

    async def enhance_sketch(
        self,
        sketch_path: str,
        flower_type: str = "auto",
        style: str = "realistic",
    ) -> str:
        """
        将用户简笔画转化为带有三维体积感的写实花朵图片。
        """
        prompt = self._build_enhance_prompt(flower_type, style)

        try:
            sketch_file = Path(sketch_path)
            if not sketch_file.exists():
                print(f"[SketchEnhancer] 草图不存在: {sketch_path}")
                return sketch_path

            with open(sketch_path, "rb") as f:
                result = self.client.images.edit(
                    model="gpt-image-1",
                    image=f,
                    prompt=prompt,
                    size="1024x1024",
                    quality="high",
                )

            print("[SketchEnhancer] result type:", type(result))
            print("[SketchEnhancer] result repr:", repr(result)[:500])

            output_path = str(sketch_file.with_name(f"{sketch_file.stem}_enhanced.png"))

            # 情况 1：标准 OpenAI SDK 返回对象
            if hasattr(result, "data") and result.data:
                item = result.data[0]

                # GPT image 常见返回：b64_json
                if getattr(item, "b64_json", None):
                    image_bytes = base64.b64decode(item.b64_json)
                    with open(output_path, "wb") as out:
                        out.write(image_bytes)

                    print(f"[SketchEnhancer] 写实图片已保存: {output_path}")
                    return output_path

                # 某些老接口/其他模型可能返回 url
                if getattr(item, "url", None):
                    import httpx
                    resp = httpx.get(item.url, timeout=30.0)
                    resp.raise_for_status()

                    with open(output_path, "wb") as out:
                        out.write(resp.content)

                    print(f"[SketchEnhancer] 写实图片已保存(URL): {output_path}")
                    return output_path

            # 情况 2：代理直接返回 base64 字符串
            if isinstance(result, str):
                possible_base64 = result.strip()

                try:
                    image_bytes = base64.b64decode(possible_base64, validate=True)
                    with open(output_path, "wb") as out:
                        out.write(image_bytes)

                    print(f"[SketchEnhancer] 写实图片已保存(base64 str): {output_path}")
                    return output_path
                except Exception:
                    # 如果它不是 base64，而是已有文件路径
                    if possible_base64.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
                        print(f"[SketchEnhancer] 返回的是路径字符串: {possible_base64}")
                        return possible_base64

            # 情况 3：代理返回 dict
            if isinstance(result, dict):
                data = result.get("data")
                if data and isinstance(data, list):
                    item = data[0]

                    if item.get("b64_json"):
                        image_bytes = base64.b64decode(item["b64_json"])
                        with open(output_path, "wb") as out:
                            out.write(image_bytes)

                        print(f"[SketchEnhancer] 写实图片已保存(dict): {output_path}")
                        return output_path

                    if item.get("url"):
                        import httpx
                        resp = httpx.get(item["url"], timeout=30.0)
                        resp.raise_for_status()

                        with open(output_path, "wb") as out:
                            out.write(resp.content)

                        print(f"[SketchEnhancer] 写实图片已保存(dict URL): {output_path}")
                        return output_path

            print("[SketchEnhancer] OpenAI 未返回可解析图片，使用原草图")
            return sketch_path

        except Exception as e:
            print(f"[SketchEnhancer] OpenAI 调用失败: {e}，使用原草图")
            return sketch_path

    def _build_enhance_prompt(self, flower_type: str, style: str) -> str:
        prompt = (
            "You are a professional botanical artist and 3D texture artist. "
            f"The input is a simple line art sketch of a {flower_type} flower in {style} style. "
            "Your critical task is to transform this simple blueprint into a photorealistic, "
            "fully volumetric, macro photography image. "

            "1. Treat sketch as a blueprint. "
            "2. Preserve the user's original composition and creative design exactly. "
            "3. Add realistic petal thickness, curvature, veins, stem fibers, and subtle dew. "
            "4. Use neutral studio lighting, single centered object, clean dark grey background. "
            "5. Optimize for 3D generation reference."
        )
        return prompt