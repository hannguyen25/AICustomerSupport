import os
import logging
from typing import List, Dict, Any 
import chromadb
from chromadb.config import Settings
from sentence_transformers import SentenceTransformer, util
from rank_bm25 import BM25Okapi

logger = logging.getLogger(__name__)

def reciprocal_rank_fusion(vector_results: list, bm25_results: list, k: int = 60) -> list:
    """Kết hợp kết quả từ Vector Search và BM25 bằng thuật toán RRF"""
    rrf_scores = {}
    
    # Gộp điểm từ Vector Search
    for rank, item in enumerate(vector_results):
        doc_id = item['id']
        if doc_id not in rrf_scores:
            rrf_scores[doc_id] = {'item': item, 'score': 0.0}
        rrf_scores[doc_id]['score'] += 1.0 / (k + rank + 1)
        
    # Gộp điểm từ BM25 Search
    for rank, item in enumerate(bm25_results):
        doc_id = item['id']
        if doc_id not in rrf_scores:
            rrf_scores[doc_id] = {'item': item, 'score': 0.0}
        rrf_scores[doc_id]['score'] += 1.0 / (k + rank + 1)
        
    # Sắp xếp lại theo tổng điểm RRF giảm dần
    sorted_docs = sorted(rrf_scores.values(), key=lambda x: x['score'], reverse=True)
    return [d['item'] for d in sorted_docs]

class SemanticIntentRouter:
    """Bộ định tuyến ý định dựa trên ngữ nghĩa embedding và độ tương đồng Cosine"""
    def __init__(self, model: SentenceTransformer):
        self.model = model
        
        # Các câu mẫu đại diện cho nhóm Giáo trình (Học tập, giải thuật, thuật toán, môn học)
        self.academic_samples = [
            "giải thuật là gì", "thuật toán thế nào", "cách tính toán bài toán",
            "lý thuyết môn học trí tuệ nhân tạo", "cách tính khoảng cách minimax",
            "chương trình nội dung môn học", "giải thuật minimax là gì",
            "cấu trúc dữ liệu và giải thuật", "phương pháp tìm kiếm lời giải",
            "logic mệnh đề logic vị từ", "quy tắc suy diễn tam đoạn luận",
            "hàm đánh giá heuristic", "đồ thị không gian trạng thái"
        ]
        
        # Các câu mẫu đại diện cho nhóm Thông báo (Hành chính, quy chế, sinh viên 5 tốt, học phí)
        self.admin_samples = [
            "thông báo nhà trường sinh viên", "điểm rèn luyện quy chế",
            "thủ tục học phí hồ sơ", "tiêu chí xét duyệt sinh viên năm tốt",
            "đóng học phí ở đâu", "chính sách đổi trả áp dụng trong mấy ngày",
            "lịch học lịch thi biểu phí", "đăng ký học phần tín chỉ",
            "quy định xử lý học vụ", "cấp thẻ sinh viên giấy xác nhận"
        ]
        
        # Encode sẵn các câu mẫu lên RAM để tối ưu tốc độ xử lý
        self.academic_emb = self.model.encode(self.academic_samples, convert_to_tensor=True)
        self.admin_emb = self.model.encode(self.admin_samples, convert_to_tensor=True)

    def route(self, query: str) -> str:
        # Encode câu hỏi thực tế của người dùng
        query_emb = self.model.encode(query, convert_to_tensor=True)
        
        # Tính độ tương đồng (Cosine Similarity) với các câu mẫu
        score_academic = util.cos_sim(query_emb, self.academic_emb).max().item()
        score_admin = util.cos_sim(query_emb, self.admin_emb).max().item()
        
        logger.info(f"Intent Routing Scores -> Giáo trình: {score_academic:.2f} | Thông báo: {score_admin:.2f}")
        
        # Ngưỡng tin cậy (Threshold) để lọc nhiễu
        threshold = 0.35
        
        if score_academic > score_admin and score_academic > threshold:
            return "Giáo trình"
        elif score_admin > score_academic and score_admin > threshold:
            return "Thông báo"
            
        return None  # Nếu không đạt ngưỡng, trả về None để tìm kiếm trên toàn bộ DB (Fallback an toàn)


class VectorStoreService:
    _shared_model = None

    def __init__(self, persist_directory: str = "./chroma_db", model_name: str = 'all-MiniLM-L6-v2'):
        try:
            self.client = chromadb.PersistentClient(
                path=persist_directory,
                settings=Settings(anonymized_telemetry=False)
            )
            
            if VectorStoreService._shared_model is None:
                logger.info(f"Đang tải mô hình Embedding: {model_name}...")
                VectorStoreService._shared_model = SentenceTransformer(model_name)
            
            self.embedding_model = VectorStoreService._shared_model
            self.collection = self.client.get_or_create_collection(name="enterprise_knowledge")
            
            # Khởi tạo bộ định tuyến ngữ nghĩa thông minh
            self.intent_router = SemanticIntentRouter(self.embedding_model)
            
        except Exception as e:
            logger.error(f"Lỗi khởi tạo VectorStoreService: {str(e)}")
            raise

    def route_category(self, query: str) -> str:
        """Thay thế bộ lọc từ khóa cũ bằng Semantic Router dựa trên ngữ nghĩa"""
        return self.intent_router.route(query)

    def add_document_chunks(self, document_id: str, version_id: str, chunks: List[Dict[str, Any]], category: str = "Thông báo Hutech", batch_size: int = 64) -> int:
        """Nhận danh sách chunk kèm theo category từ C#, lưu vào ChromaDB an toàn"""
        if not chunks:
            return 0

        ids = []
        documents = []
        metadatas = []

        for chunk in chunks:
            chunk_id = f"{document_id}_v{version_id}_chunk_{chunk['chunk_index']}"
            ids.append(chunk_id)
            documents.append(chunk["content"])
            
            metadatas.append({
                "document_id": str(document_id),
                "version_id": str(version_id),
                "chunk_index": int(chunk["chunk_index"]),
                "page_number": int(chunk["page_number"]),
                "token_count": int(chunk["token_count"]),
                "section_title": str(chunk.get("section_title", "Phần mở đầu")),
                "category": str(category),  
                "status": "COMPLETED"  
            })

        try:
            total_added = 0
            for i in range(0, len(documents), batch_size):
                batch_docs = documents[i:i + batch_size]
                batch_ids = ids[i:i + batch_size]
                batch_metas = metadatas[i:i + batch_size]

                batch_embeddings = self.embedding_model.encode(batch_docs, show_progress_bar=False).tolist()

                self.collection.add(
                    ids=batch_ids,
                    embeddings=batch_embeddings,
                    documents=batch_docs,
                    metadatas=batch_metas
                )
                total_added += len(batch_ids)

            return total_added
        except Exception as e:
            logger.error(f"Lỗi khi thêm chunk vào Vector DB cho document {document_id}: {str(e)}")
            raise RuntimeError(f"Không thể lưu vector vào database: {str(e)}")

    def search_similar(self, query: str, top_k: int = 8) -> List[Dict[str, Any]]:
        if not query.strip():
            return []

        try:
            query_embedding = self.embedding_model.encode([query]).tolist()
            
            target_category = self.route_category(query)
            logger.info(f"Query: '{query}' -> Tự động định tuyến vào Category: {target_category}")
            
            # THỬ TÌM VỚI BỘ LỌC CATEGORY TRƯỚC
            where_condition = {"$and": [{"status": "COMPLETED"}, {"category": target_category}]} if target_category else {"status": "COMPLETED"}
            
            results = self.collection.query(
                query_embeddings=query_embedding,
                n_results=top_k,
                where=where_condition
            )

            # NẾU BỘ LỌC CỤ THỂ KHÔNG TÌM THẤY GÌ (Do dữ liệu cũ chưa gán nhãn đúng), FALLBACK TÌM RỘNG TOÀN BỘ DB
            if not results or not results.get("documents") or not results["documents"][0]:
                logger.warning(f"Không tìm thấy kết quả với category '{target_category}', tiến hành fallback tìm trên toàn bộ DB...")
                results = self.collection.query(
                    query_embeddings=query_embedding,
                    n_results=top_k,
                    where={"status": "COMPLETED"}
                )

            formatted_results = []
            if results and results.get("documents") and results["documents"]:
                for i in range(len(results["documents"][0])):
                    formatted_results.append({
                        "id": results["ids"][0][i],
                        "content": results["documents"][0][i],
                        "metadata": results["metadatas"][0][i],
                        "distance": results["distances"][0][i] if "distances" in results and results["distances"] else None
                    })
            return formatted_results
        except Exception as e:
            logger.error(f"Lỗi khi tìm kiếm vector với query '{query}': {str(e)}")
            return []

    def search_bm25(self, query: str, top_k: int = 8) -> list:
        """Tìm kiếm từ khóa truyền thống bằng BM25"""
        try:
            all_data = self.collection.get(include=["documents", "metadatas"])
            documents = all_data.get("documents", [])
            ids = all_data.get("ids", [])
            metadatas = all_data.get("metadatas", [])
            
            if not documents:
                return []

            tokenized_corpus = [doc.lower().split() for doc in documents]
            bm25 = BM25Okapi(tokenized_corpus)
            
            tokenized_query = query.lower().split()
            scores = bm25.get_scores(tokenized_query)
            
            top_indices = sorted(range(len(scores)), key=lambda i: scores[i], reverse=True)[:top_k]
            
            bm25_results = []
            for idx in top_indices:
                if scores[idx] > 0:
                    bm25_results.append({
                        "id": ids[idx],
                        "content": documents[idx],
                        "metadata": metadatas[idx],
                        "distance": float(scores[idx])
                    })
            return bm25_results
        except Exception as e:
            logger.error(f"Lỗi khi thực hiện BM25 search: {str(e)}")
            return []

    def search_hybrid(self, query: str, top_k: int = 8) -> list:
        """Thực hiện Hybrid Search kết hợp Vector Search và BM25 thông qua RRF"""
        if not query.strip():
            return []

        vector_results = self.search_similar(query, top_k=top_k)
        bm25_results = self.search_bm25(query, top_k=top_k)
        
        hybrid_results = reciprocal_rank_fusion(vector_results, bm25_results, k=60)
        return hybrid_results[:top_k]

    def delete_document_vectors(self, document_id: str):
        """Xóa toàn bộ các vector chunk cũ của document_id khỏi ChromaDB"""
        try:
            self.collection.delete(
                where={"document_id": str(document_id)}
            )
            logger.info(f">>> Đã xóa toàn bộ vector cũ của document_id: {document_id}")
        except Exception as e:
            logger.error(f"Lỗi khi xóa vector cũ: {str(e)}")