import io
import os
import requests
from celery_app import celery_app
from services.vector_store import VectorStoreService
from services.document_processor import DocumentProcessor

vector_store = VectorStoreService()

@celery_app.task(bind=True, name="tasks.process_document_background")
def process_document_background(self, file_bytes: bytes, filename: str, document_id: str, version_id: str):
    try:
        print(f">>> [Celery Worker] Đang xử lý file từ bytes: {filename}")
        
        file_stream = io.BytesIO(file_bytes)
        _, file_extension = os.path.splitext(filename)
        
        processor = DocumentProcessor()
        raw_chunks = processor.process_stream(file_stream, file_extension)
        
        if not raw_chunks:
            return {"status": "Failed", "message": "File không có nội dung."}

        chunks_data = []
        
        # Danh sách chuẩn bị add vào ChromaDB của Python
        embeddings_list = []
        documents_list = []
        metadatas_list = []
        ids_list = []

        for index, chunk in enumerate(raw_chunks):
            content = chunk["content"]
            vector_embedding = vector_store.embedding_model.encode(content).tolist()
            
            chunk_id = f"{document_id}_{chunk['chunk_index']}"
            
            # 1. Dữ liệu gửi về C# SQL
            chunks_data.append({
                "chunkIndex": chunk["chunk_index"],
                "content": content,
                "pageNumber": chunk["page_number"],
                "tokenCount": chunk["token_count"],
                "sectionTitle": chunk["section_title"],
                "embedding": vector_embedding
            })

            # 2. Dữ liệu chuẩn bị lưu vào ChromaDB cục bộ của Python
            embeddings_list.append(vector_embedding)
            documents_list.append(content)
            metadatas_list.append({
                "document_id": document_id,
                "version_id": version_id,
                "page_number": chunk["page_number"],
                "section_title": chunk["section_title"]
            })
            ids_list.append(chunk_id)

        # --- QUAN TRỌNG: Lưu vào ChromaDB cục bộ để Python service có thể tìm kiếm khi chat ---
        try:
            vector_store.collection.add(
                embeddings=embeddings_list,
                documents=documents_list,
                metadatas=metadatas_list,
                ids=ids_list
            )
            print(f">>> [Celery Worker] Đã lưu thành công {len(ids_list)} chunks vào ChromaDB cục bộ của Python.")
        except Exception as chroma_err:
            print(f">>> [Celery Worker] Cảnh báo khi lưu ChromaDB (có thể trùng ID): {str(chroma_err)}")

        # Đồng bộ kết quả về C# Backend để lưu vào SQL Server
        csharp_backend_url = os.environ.get("CSHARP_BACKEND_URL", "http://csharp-backend:5145")
        sync_url = f"{csharp_backend_url}/api/documents/sync-chunks" 
        
        payload = {
            "documentId": document_id,
            "versionId": version_id,
            "chunks": chunks_data
        }
        
        response = requests.post(sync_url, json=payload, timeout=120)
        
        if response.status_code == 200:
            print(">>> [Celery Worker] Đồng bộ chunks thành công về C#!")
            return {"status": "Success"}
        else:
            raise RuntimeError(f"C# Backend trả về lỗi: {response.status_code}")
            
    except Exception as e:
        print(f">>> [Celery Worker] Lỗi: {str(e)}")
        import traceback
        traceback.print_exc()
        raise self.retry(exc=e, countdown=15, max_retries=3)