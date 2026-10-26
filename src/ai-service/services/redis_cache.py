import redis
import json
import hashlib

class RedisCacheManager:
    def __init__(self, host="localhost", port=6379, db=0, ttl=3600):
        # Kết nối tới Redis Server
        self.client = redis.Redis(host=host, port=port, db=db, decode_responses=True)
        self.ttl = ttl  # Thời gian sống của cache (giây), mặc định 1 giờ

    def _generate_key(self, query: str) -> str:
        # Chuẩn hóa query và tạo mã hash MD5 làm key trên Redis
        normalized_query = query.strip().lower()
        query_hash = hashlib.md5(normalized_query.encode("utf-8")).hexdigest()
        return f"rag_cache:{query_hash}"

    def get(self, query: str):
        key = self._generate_key(query)
        cached_data = self.client.get(key)
        if cached_data:
            return json.loads(cached_data)
        return None

    def set(self, query: str, response_data: dict):
        key = self._generate_key(query)
        # Lưu trữ kết quả dưới dạng JSON string với thời gian TTL
        self.client.setex(key, self.ttl, json.dumps(response_data, ensure_ascii=False))