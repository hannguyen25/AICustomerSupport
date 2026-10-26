from fastapi import APIRouter, HTTPException, status, UploadFile, File, Form
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from services.rag_service import RAGService
from services.vector_store import VectorStoreService 
from tasks import process_document_background
import shutil
import os

router = APIRouter(prefix="/api/ai", tags=["AI RAG"])
vector_store = VectorStoreService()
rag_service = RAGService(vector_store)

class ChatRequest(BaseModel):
    query: str
    top_k: int = 3
    similarity_threshold: float = 0.5

@router.post("/chat")
async def chat_endpoint(request: ChatRequest):
    try:
        result = await rag_service.answer_query(request.query, request.top_k, request.similarity_threshold)
        return result
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e)) 

@router.post("/index-document", status_code=status.HTTP_202_ACCEPTED)
async def index_document_endpoint(
    file: UploadFile = File(...),
    document_id: str = Form(...),
    version_id: str = Form(...)
):
    try:
        # 1. Đọc nội dung file trực tiếp thành bytes vào RAM
        file_bytes = await file.read()
        filename = file.filename

        # 2. Truyền bytes trực tiếp sang Celery Task (.delay)
        task = process_document_background.delay(
            file_bytes=file_bytes, 
            filename=filename,
            document_id=document_id,
            version_id=version_id
        )

        return JSONResponse(
            status_code=status.HTTP_202_ACCEPTED,
            content={
                "status": "Accepted",
                "message": "File đã được chuyển vào hàng đợi xử lý ngầm dưới dạng bytes.",
                "taskId": task.id
            }
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))