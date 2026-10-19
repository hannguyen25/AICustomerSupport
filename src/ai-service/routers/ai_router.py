from fastapi import APIRouter, HTTPException
from pydantic import BaseModel
from services.rag_service import RAGService
from services.vector_store import VectorStoreService 

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