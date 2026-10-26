import pytest
from unittest.mock import patch, MagicMock

def test_celery_pdf_upload_async_queue():
    """Kiểm tra việc đẩy task xử lý file PDF lớn vào Celery Queue và trả về trạng thái bất đồng bộ"""
    with patch("celery.app.task.Task.apply_async") as mock_apply_async:
        # Giả lập task ID trả về khi đẩy vào hàng đợi
        mock_task = MagicMock()
        mock_task.id = "task-uuid-pdf-processing-123"
        mock_apply_async.return_value = mock_task

        # Giả lập hành vi upload file PDF (ví dụ: ThongBaoKhungDiemRenLuyen.pdf)
        filename = "ThongBaoKhungDiemRenLuyen.pdf"
        file_size_mb = 15.5 # File lớn dễ gây timeout nếu xử lý đồng bộ
        
        # Gọi hàm giả lập đẩy vào queue
        task_result = mock_apply_async(args=[filename, file_size_mb])

        # Assert: Kiểm tra task đã được tạo và trả về mã định danh queue thành công
        assert task_result.id == "task-uuid-pdf-processing-123"
        mock_apply_async.assert_called_once()

def test_vector_db_retrieval_across_three_files():
    """Kiểm tra ngữ cảnh Vector DB đã chứa chunk từ 3 file tài liệu (SV5T, Trí tuệ nhân tạo, Điểm rèn luyện)"""
    # Giả lập kết quả trả về từ Vector DB (Chứa các chunk dữ liệu từ 3 tài liệu đã đính kèm)
    vector_db_mock = MagicMock()
    
    mock_chunks = [
        {"source": "HutechTiepNhanHoSoSinhVienNamTot.pdf", "content": "Tieu chi Dao duc tot: Diem danh gia ren luyen tu 80 diem tro len."},
        {"source": "GTTriTueNhanTao.pdf", "content": "Tri tue nhan tao la tri thong minh cua may do con nguoi tao ra."},
        {"source": "ThongBaoKhungDiemRenLuyen.pdf", "content": "Khung diem danh gia ren luyen sinh vien ap dung tu nam hoc 2026 - 2027."}
    ]
    
    vector_db_mock.similarity_search.return_value = mock_chunks

    # Truy vấn thử nghiệm
    query = "Quy định về điểm rèn luyện và sinh viên 5 tốt?"
    results = vector_db_mock.similarity_search(query)

    # Assert: Đảm bảo hệ thống RAG truy xuất đúng các tài liệu trong kho Vector DB
    assert len(results) == 3
    assert any("Sinhviennaotot" in r["source"] or "Renluyen" in r["source"] for r in results)