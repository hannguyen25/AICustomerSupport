from fastapi import FastAPI, UploadFile, File, Form, HTTPException
import shutil
import os
import httpx
from services.document_processor import DocumentProcessor
from services.vector_store import VectorStoreService
import traceback
from pydantic import BaseModel
from routers.ai_router import router as ai_router

class SearchRequest(BaseModel):
    query: str
    topK: int = 3

app = FastAPI(title="AI Customer Support RAG Pipeline", version="1.0")
processor = DocumentProcessor()
vector_store = VectorStoreService()

UPLOAD_DIR = "./temp_storage"
os.makedirs(UPLOAD_DIR, exist_ok=True)

app.include_router(ai_router)

@app.get("/")
def root():
    return {"message": "AI Service is running smoothly!"}

# Hàm đồng bộ chunks ngược lại về C# Backend (SQL Server)
async def sync_chunks_to_sql(document_id: str, version_id: str, chunks: list):
    csharp_backend_url = "http://csharp-backend:5145/api/documents/sync-chunks"
    
    payload = {
        "documentId": str(document_id),
        "versionId": str(version_id),
        "chunks": [
            {
                "chunkIndex": int(chunk["chunk_index"]),
                "content": str(chunk["content"]),
                "pageNumber": int(chunk["page_number"]),
                "tokenCount": int(chunk["token_count"]),
                "sectionTitle": str(chunk.get("section_title", ""))
            }
            for chunk in chunks
        ]
    }

    async with httpx.AsyncClient() as client:
        try:
            print(f">>> Đang đồng bộ {len(chunks)} chunks về C# Backend...")
            response = await client.post(csharp_backend_url, json=payload, timeout=15.0)
            print(f">>> Phản hồi từ C# Backend: Status {response.status_code} - Body: {response.text}")
            
            if response.status_code != 200:
                print(f">>> LỖI TỪ C# BACKEND: {response.text}")
                raise HTTPException(status_code=500, detail=f"C# Sync Failed: {response.text}")
        except Exception as e:
            print(f">>> CHI TIẾT LỖI KHI GỌI API ĐỒNG BỘ: {str(e)}")
            raise e

@app.post("/api/ai/index-document")
async def index_document(
    document_id: str = Form(...),
    version_id: str = Form(...),
    file: UploadFile = File(...)
):
    file_path = os.path.join(UPLOAD_DIR, file.filename)
    try:
        # 1. Lưu file tạm thời
        with open(file_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)

        _, file_extension = os.path.splitext(file.filename)
        
        # 2. Xử lý trích xuất và chunking
        chunks = processor.process_file(file_path, file_extension)

        # 3. Tạo Embedding và Index vào Vector Database
        indexed_count = vector_store.add_document_chunks(document_id, version_id, chunks)

        # 4. Đồng bộ chunks ngược sang SQL Server của C# Backend
        try:
            await sync_chunks_to_sql(document_id, version_id, chunks)
        except Exception as sync_err:
            print(f"--- LỖI THỰC TẾ KHI ĐỒNG BỘ SQL: {str(sync_err)} ---")
            raise sync_err

        return {
            "success": True,
            "message": f"Successfully indexed {indexed_count} chunks for document {document_id}",
            "document_id": document_id,
            "total_chunks": indexed_count
        }
    except Exception as e:
        print(f"--- LỖI TẠI INDEX-DOCUMENT: {str(e)} ---")
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if os.path.exists(file_path):
            os.remove(file_path)

@app.post("/api/ai/search")
async def search_knowledge(request: SearchRequest):
    """API kiểm thử tìm kiếm ngữ nghĩa (Semantic Search)"""
    try:
        results = vector_store.search_similar(request.query, top_k=request.topK)
        return {
            "query": request.query,
            "results": results
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/api/ai/reprocess-document")
async def reprocess_document(
    document_id: str = Form(...),
    version_id: str = Form(...),
    file: UploadFile = File(...)
):
    file_path = os.path.join(UPLOAD_DIR, file.filename)
    try:
        # 1. Lưu file mới tạm thời
        with open(file_path, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)

        _, file_extension = os.path.splitext(file.filename)
        
        # 2. Xóa bỏ toàn bộ vector cũ trong Vector DB liên quan đến document_id này
        vector_store.delete_document_vectors(document_id)

        # 3. Trích xuất text và chia chunk phiên bản mới
        chunks = processor.process_file(file_path, file_extension)

        # 4. Sinh embedding và index lại vào Vector DB cho version mới
        indexed_count = vector_store.add_document_chunks(document_id, version_id, chunks)

        # 5. Đồng bộ chunks mới sang SQL Server
        await sync_chunks_to_sql(document_id, version_id, chunks)

        return {
            "success": True,
            "message": f"Successfully reprocessed and re-indexed {indexed_count} chunks for document {document_id} (Version {version_id})",
            "document_id": document_id,
            "version_id": version_id,
            "total_chunks": indexed_count
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        if os.path.exists(file_path):
            os.remove(file_path)

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=8001, reload=True)