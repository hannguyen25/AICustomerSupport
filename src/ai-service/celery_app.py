from celery import Celery
import os

redis_host = os.environ.get("REDIS_HOST", "redis")
broker_url = f"redis://{redis_host}:6379/0"
backend_url = f"redis://{redis_host}:6379/0"

celery_app = Celery(
    "ai_worker",
    broker=broker_url,
    backend=backend_url,
    include=["tasks"]
)

celery_app.conf.update(
    task_serializer="json",
    accept_content=["json"],
    result_serializer="json",
    timezone="UTC",
    enable_utc=True,
)