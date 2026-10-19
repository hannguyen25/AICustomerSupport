import logging
from typing import List, Dict, Any
from transformers import AutoModelForSequenceClassification, AutoTokenizer
import torch

logger = logging.getLogger(__name__)

class RerankService:
    _shared_model = None
    _shared_tokenizer = None

    def __init__(self, model_name: str = "BAAI/bge-reranker-large"):
        try:
            if RerankService._shared_model is None:
                logger.info(f"Đang tải mô hình Reranker: {model_name}...")
                RerankService._shared_tokenizer = AutoTokenizer.from_pretrained(model_name)
                RerankService._shared_model = AutoModelForSequenceClassification.from_pretrained(model_name)
                RerankService._shared_model.eval() # Đặt ở chế độ inference
                
            self.tokenizer = RerankService._shared_tokenizer
            self.model = RerankService._shared_model
        except Exception as e:
            logger.error(f"Lỗi khởi tạo RerankService: {str(e)}")
            raise

    def rerank(self, query: str, documents: List[Dict[str, Any]], top_k: int = 5) -> List[Dict[str, Any]]:
        """
        Nhận danh sách documents từ bước Hybrid Search, chấm điểm lại và trả về top_k chuẩn xác nhất.
        """
        if not documents:
            return []

        try:
            # Chuẩn bị cặp [query, doc_content] cho mô hình Reranker
            pairs = [[query, doc["content"]] for doc in documents]
            
            with torch.no_grad():
                inputs = self.tokenizer(
                    pairs, 
                    padding=True, 
                    truncation=True, 
                    max_length=512, 
                    return_tensors="pt"
                )
                scores = self.model(**inputs).logits.squeeze(dim=-1).tolist()
                
                # Trường hợp chỉ trả về 1 điểm số đơn lẻ (1 cặp)
                if isinstance(scores, float):
                    scores = [scores]

            # Gắn điểm số rerank vào từng document
            for i, doc in enumerate(documents):
                doc["rerank_score"] = float(scores[i])

            # Sắp xếp lại theo điểm số rerank giảm dần
            sorted_docs = sorted(documents, key=lambda x: x["rerank_score"], reverse=True)
            
            # Trả về top_k kết quả tốt nhất sau khi lọc nhiễu
            return sorted_docs[:top_k]

        except Exception as e:
            logger.error(f"Lỗi trong quá trình Rerank: {str(e)}")
            # Fallback: Trả về danh sách ban đầu nếu xảy ra lỗi rerank
            return documents[:top_k]