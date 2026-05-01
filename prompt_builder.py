"""
Prompt 构建器
根据用户选择的花朵类型和风格，拼接高质量 prompt。
好的 prompt 是生成质量的关键。
"""


class PromptBuilder:
    """
    拼接策略:
    base_prompt + flower_type_detail + style_modifier + quality_suffix

    默认生成写实风格花朵，参考真实植物标本的质感。
    """

    # 花朵类型 → 写实描述（强调真实植物特征）
    FLOWER_DETAILS = {
        "auto": "a realistic single flower with natural petals, organic curved stem, visible veins on leaves, true-to-life proportions",
        "rose": "a realistic rose with layered velvety petals, natural color gradients from center to edge, thorny woody stem, serrated green leaves",
        "tulip": "a realistic tulip with smooth waxy cup-shaped petals, single straight thick stem, broad pointed leaves wrapping the base",
        "daisy": "a realistic gerbera daisy with dense thin petals radiating outward, fuzzy golden center disc, long slightly curved green stem",
        "lotus": "a realistic lotus blossom with wide overlapping petals, subtle pink-to-white gradient, round seed pod center, tall smooth stem",
        "sunflower": "a realistic sunflower with large bright yellow petals, detailed dark brown spiral seed pattern center, thick hairy stem, large rough leaves",
        "cherry_blossom": "a realistic cherry blossom branch with clusters of delicate five-petal soft pink flowers, thin brown woody branch, small buds",
        "lily": "a realistic lily with six recurved petals showing spotted pattern, long prominent stamens with orange pollen, smooth green stem",
        "orchid": "a realistic orchid with exotic waxy petals, intricate lip petal with color patterns, elegant arching stem with aerial roots",
        "lavender": "a realistic lavender stalk with tiny dense purple flower buds in a spike formation, narrow grey-green leaves, thin woody stem",
        "carnation": "a realistic carnation with ruffled layered petals, serrated petal edges, sturdy straight stem with nodes, narrow leaves",
    }

    # 风格 → 修饰词（写实为默认，大幅强化）
    STYLE_MODIFIERS = {
        "realistic": (
            "photorealistic rendering, natural organic colors, "
            "physically based materials with subsurface scattering on petals, "
            "botanical illustration accuracy, real flower reference, "
            "natural imperfections, slight wilting and asymmetry for realism"
        ),
        "fantasy": "fantasy art style, soft magical glow, vibrant saturated colors, enchanted look",
        "cartoon": "cartoon style, cel-shaded look, bold outlines, bright cheerful colors, stylized proportions",
        "lowpoly": "low poly geometric style, flat shading, minimal polygon count, clean faceted surfaces",
        "watercolor": "watercolor painting style, soft edges, translucent petals, artistic brush texture feel",
        "anime": "anime style, soft pastel colors, slightly exaggerated proportions, clean aesthetic",
    }

    # 通用质量后缀
    QUALITY_SUFFIX = (
        "single isolated flower, "
        "no pot, no vase, no planter, "
        "no background, dark neutral background only, "
        "clean silhouette, "
        "studio lighting, soft shadows, "
        "8k textures, high polygon detail, "
        "PBR materials, highest quality"
    )

    # negative prompt（强化写实排除项）
    NEGATIVE_PROMPT = (
        "low quality, low resolution, ugly, blurry, noisy, "
        "multiple objects, cluttered, text, watermark, "
        "deformed, mutated, worst quality, "
        "cartoon, stylized, flat shading, cel-shaded, "
        "plastic looking, artificial, oversaturated"
    )

    def build(
        self,
        flower_type: str = "auto",
        style: str = "fantasy",
        custom_description: str = "",
    ) -> str:
        """
        构建完整的 prompt。

        Args:
            flower_type: 花朵类型 key
            style: 风格 key
            custom_description: 用户自定义补充描述 (可选)

        Returns:
            完整的 prompt 字符串
        """
        # 花朵描述
        flower_desc = self.FLOWER_DETAILS.get(
            flower_type,
            self.FLOWER_DETAILS["auto"]
        )

        # 风格修饰
        style_mod = self.STYLE_MODIFIERS.get(
            style,
            self.STYLE_MODIFIERS["fantasy"]
        )

        # 组装
        parts = [
            f"a single {flower_desc} 3D model based on the user's sketch",
            style_mod,
        ]

        if custom_description:
            parts.append(custom_description.strip())

        parts.append(self.QUALITY_SUFFIX)

        prompt = ", ".join(parts)

        # Meshy API 限制 prompt 最长 600 字符
        if len(prompt) > 580:
            prompt = prompt[:577] + "..."

        return prompt

    def get_negative_prompt(self) -> str:
        return self.NEGATIVE_PROMPT

    def get_available_styles(self) -> list[str]:
        return list(self.STYLE_MODIFIERS.keys())

    def get_available_flower_types(self) -> list[str]:
        return list(self.FLOWER_DETAILS.keys())
