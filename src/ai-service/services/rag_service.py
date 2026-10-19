import os
import logging
from fastapi import HTTPException
from fastapi.concurrency import run_in_threadpool
from .RerankService import RerankService
from openai import AsyncOpenAI, RateLimitError, APITimeoutError

logger = logging.getLogger(__name__)

class RAGService:
    def __init__(self, vector_store_service):
        self.vector_store = vector_store_service
        self.rerank_service = RerankService() # Khởi tạo Reranker Service

        self.client = AsyncOpenAI(
            api_key=os.environ.get("OPENAI_API_KEY", "ollama"),
            base_url=os.environ.get("OPENAI_BASE_URL", "http://host.docker.internal:11434/v1")
        )

    async def answer_query(self, query: str, top_k: int = 5, similarity_threshold: float = 0.4):
        try:
            # Lấy số lượng ứng viên rộng từ Hybrid Search để bước Rerank lọc lại chính xác
            fetch_k = max(top_k * 3, 20)
            
            # Bước 1: Gọi Hybrid Search (Vector + BM25)
            search_results = await run_in_threadpool(
                self.vector_store.search_hybrid, query, fetch_k
            )
            
            if not search_results:
                return {
                    "answer": "Tôi chưa tìm thấy thông tin đủ phù hợp trong knowledge base để trả lời chính xác câu hỏi này. Bạn vui lòng liên hệ trực tiếp với quản trị viên hoặc bộ phận hỗ trợ.",
                    "sources": [],
                    "status": "insufficient_information"
                }

            # Bước 2: Đưa qua Reranker để chấm điểm chi tiết và lọc ra top_k tốt nhất
            reranked_results = await run_in_threadpool(
                self.rerank_service.rerank, query, search_results, top_k
            )
            
            # === DEBUG PRINT ===
            print(f"\n==================== RAG + RERANK DEBUG ====================")
            print(f"Query nhận được: {query}")
            print(f"Số lượng kết quả ban đầu từ Hybrid: {len(search_results)}")
            print(f"Số lượng kết quả sau khi Rerank lấy top_k: {len(reranked_results)}")
            for idx, doc in enumerate(reranked_results):
                print(f"Top {idx+1} | Rerank Score: {doc.get('rerank_score', 0):.4f} | ID: {doc.get('id')}")
            print(f"===========================================================\n")
            
            valid_chunks = []
            raw_sources = []
            
            for res in reranked_results:
                # Sử dụng trực tiếp điểm số từ Reranker làm độ tin cậy (hoặc chuẩn hóa tuỳ ý)
                rerank_score = res.get("rerank_score", 0.0)
                
                valid_chunks.append(res["content"])
                raw_sources.append({
                    "document": res["metadata"].get("document_id"),
                    "sectionTitle": res["metadata"].get("section_title", "Tài liệu chung"),
                    "page": res["metadata"].get("page_number"),
                    "score": round(float(rerank_score), 2)
                })

            unique_sources = []
            seen = set()
            for src in raw_sources:
                identifier = (src["document"], src["sectionTitle"], src["page"])
                if identifier not in seen:
                    seen.add(identifier)
                    unique_sources.append(src)
            
            final_sources = unique_sources[:top_k]
            final_chunks = valid_chunks[:top_k]

            if not final_chunks:
                return {
                    "answer": "Tôi chưa tìm thấy thông tin đủ phù hợp trong knowledge base để trả lời chính xác câu hỏi này. Bạn vui lòng liên hệ trực tiếp với quản trị viên hoặc bộ phận hỗ trợ.",
                    "sources": [],
                    "status": "insufficient_information"
                }

            context_text = "\n\n".join([
                f"[Source {i+1}\n{src['sectionTitle']} (Trang {src['page']})]:\n{chunk}" 
                for i, (chunk, src) in enumerate(zip(final_chunks, final_sources))
            ])
            
            system_instruction = (
                "Bạn là trợ lý AI thông minh chuyên hỗ trợ sinh viên và cán bộ nhân viên trường Đại học Công nghệ TP. Hồ Chí Minh (HUTECH). "
                "Nhiệm vụ của bạn là trả lời các câu hỏi dựa CHÍNH XÁC VÀO CONTEXT được cung cấp dưới đây. "
                "TUYỆT ĐỐI KHÔNG tự bịa đặt, suy diễn hoặc đưa thông tin ngoài context vào câu trả lời."
            )

            user_prompt = f"Context:\n{context_text}\n\nCâu hỏi của người dùng: {query}"

            response = await self.client.chat.completions.create(
                model="qwen2.5:7b",
                messages=[
                    {"role": "system", "content": system_instruction},
                    {"role": "user", "content": user_prompt}
                ],
                temperature=0.1,
                timeout=90.0
            )

            answer = response.choices[0].message.content

            return {
                "answer": answer,
                "sources": final_sources,
                "status": "answered"
            }

        except RateLimitError:
            logger.warning("AI Service Rate Limit exceeded.")
            raise HTTPException(status_code=429, detail="Hệ thống AI đang quá tải.")
        except APITimeoutError:
            logger.error("AI Service request timed out.")
            raise HTTPException(status_code=504, detail="Yêu cầu gửi tới dịch vụ AI bị quá thời gian chờ.")
        except Exception as e:
            logger.exception(f"Lỗi không xác định trong RAG Service: {str(e)}")
            raise HTTPException(status_code=500, detail=f"Đã xảy ra lỗi hệ thống: {str(e)}")