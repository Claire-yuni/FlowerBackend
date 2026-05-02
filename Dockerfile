FROM python:3.10-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# 防止 Print 包含 emoji 导致报错
ENV PYTHONIOENCODING=utf-8

# 暴露端口
EXPOSE 8000

# 启动命令 (适配 Render 的动态端口)
CMD sh -c "uvicorn server:app --host 0.0.0.0 --port ${PORT:-8000}"
