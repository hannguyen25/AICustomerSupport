import os
import re
import unicodedata
from typing import List, Dict, Any 
from pypdf import PdfReader
from docx import Document
import tiktoken


class DocumentProcessor:
    def __init__(self, encoding_name: str = "cl100k_base"):
        self.tokenizer = tiktoken.get_encoding(encoding_name)

    def count_tokens(self, text: str) -> int:
        return len(self.tokenizer.encode(text))

    def clean_page_text(self, text: str) -> str:
        """Làm sạch kỹ văn bản từng trang: loại bỏ khoảng trắng thừa, số trang chân trang, rác"""
        # Chuẩn hóa form Unicode (NFC)
        text = unicodedata.normalize('NFC', text)
        
        # Loại bỏ số trang ở đầu/chân trang
        text = re.sub(r'\b(Trang|Page)\s*\d+(\s*(?:của|of)\s*\d+)?\b', '', text, flags=re.IGNORECASE)
        text = re.sub(r'^\s*-\s*\d+\s*-\s*$', '', text, flags=re.MULTILINE)

        
        text = re.sub(r'(?<=[à-ỹa-z])\s+(?=[à-ỹa-z]\b)', '', text, flags=re.IGNORECASE)
        text = re.sub(r'\b([à-ỹa-z])\s+([à-ỹa-z])\b', r'\1\2', text, flags=re.IGNORECASE)
        
        # Chuẩn hóa các khoảng trắng thừa thành 1 khoảng trắng đơn
        text = re.sub(r'[ \t]+', ' ', text)
        return text.strip()

    def extract_text_from_pdf(self, file_path: str) -> List[Dict[str, Any]]:
        """Trích xuất text theo từng trang từ file PDF và làm sạch"""
        pages_content = []
        try:
            reader = PdfReader(file_path)
            for index, page in enumerate(reader.pages):
                text = page.extract_text() or ""
                cleaned_text = self.clean_page_text(text)
                if cleaned_text:
                    pages_content.append({
                        "page_number": index + 1,
                        "content": cleaned_text
                    })
        except Exception as e:
            raise ValueError(f"Lỗi khi đọc file PDF: {str(e)}")
        return pages_content

    def extract_text_from_docx(self, file_path: str) -> List[Dict[str, Any]]:
        """Trích xuất text từ file DOCX (bao gồm cả bảng) và làm sạch"""
        try:
            doc = Document(file_path)
            full_text = []
            
            for para in doc.paragraphs:
                if para.text.strip():
                    full_text.append(para.text.strip())
            
            for table in doc.tables:
                for row in table.rows:
                    row_text = " | ".join([cell.text.strip() for cell in row.cells if cell.text.strip()])
                    if row_text:
                        full_text.append(row_text)
            
            combined_text = " ".join(full_text)
            cleaned_text = self.clean_page_text(combined_text)
            
            return [{
                "page_number": 1,
                "content": cleaned_text
            }]
        except Exception as e:
            raise ValueError(f"Lỗi khi đọc file DOCX: {str(e)}")

    def chunk_text(self, pages: List[Dict[str, Any]], min_chars: int = 500, max_chars: int = 1000) -> List[Dict[str, Any]]:
        """
        Cắt chunk:
        - Duy trì tiêu đề chương/mục xuyên suốt qua các trang (không bị reset ở đầu trang mới).
        - Gắn nhãn tiêu đề chuẩn xác vào metadata (`section_title`).
        """
        chunks = []
        chunk_index = 0
        current_section_title = "Phần mở đầu / Tài liệu chung"

        # Regex nhận diện tiêu đề chương/mục chuẩn xác hơn
        heading_regex = re.compile(r'^(CHƯƠNG\s+\d+|PHẦN\s+\d+|\d+[\.\)]\s+[A-ZÀ-Ỹ].*|\d+\.\d+\s+[A-ZÀ-Ỹ].*)', re.IGNORECASE)
        separators = ["\n\n", "\n", ". ", ", ", " "]

        # Gộp toàn bộ nội dung các trang lại để xử lý dòng mạch lạc tránh đứt gãy ngắt trang
        all_lines = []
        for page in pages:
            page_num = page["page_number"]
            text = page["content"]
            lines = text.split('\n')
            for line in lines:
                line_str = line.strip()
                if line_str:
                    all_lines.append((page_num, line_str))

        # Phân tách văn bản thành các section dựa trên tiêu đề xuyên suốt toàn tài liệu
        structured_sections = []
        current_block = []
        current_page_num = 1

        for page_num, line_str in all_lines:
            current_page_num = page_num
            if heading_regex.match(line_str):
                if current_block:
                    structured_sections.append((current_section_title, "\n".join(current_block), current_page_num))
                    current_block = []
                current_section_title = line_str  # Cập nhật tiêu đề mới và giữ nó làm mốc cho các trang sau
                current_block.append(line_str)
            else:
                current_block.append(line_str)

        if current_block:
            structured_sections.append((current_section_title, "\n".join(current_block), current_page_num))

        # Đệ quy cắt các đoạn nội dung theo kích thước từ 500 - 1000 ký tự
        for sec_title, sec_content, page_num in structured_sections:
            sub_chunks = self._recursive_split(sec_content, separators, max_chars, min_chars)
            for sub_text in sub_chunks:
                final_content = sub_text.strip()
                if len(final_content) > 20:
                    chunks.append({
                        "chunk_index": chunk_index,
                        "content": final_content,
                        "page_number": page_num,
                        "token_count": self.count_tokens(final_content),
                        "section_title": sec_title  # Đảm bảo tiêu đề được kế thừa chính xác
                    })
                    chunk_index += 1

        return chunks

    def _recursive_split(self, text: str, separators: List[str], max_chars: int, min_chars: int) -> List[str]:
        """Hàm đệ quy chia nhỏ văn bản dựa theo danh sách phân tách ưu tiên"""
        if len(text) <= max_chars:
            return [text]

        current_separator = separators[0]
        next_separators = separators[1:] if len(separators) > 1 else [" "]

        splits = text.split(current_separator)
        good_splits = []
        current_chunk = []
        current_length = 0

        for part in splits:
            part_len = len(part) + len(current_separator)
            if current_length + part_len <= max_chars:
                current_chunk.append(part)
                current_length += part_len
            else:
                if current_chunk:
                    joined = current_separator.join(current_chunk)
                    if len(joined) >= min_chars or not next_separators:
                        good_splits.append(joined)
                    else:
                        sub = self._recursive_split(joined, next_separators, max_chars, min_chars)
                        good_splits.extend(sub)
                    current_chunk = [part]
                    current_length = len(part)
                else:
                    sub = self._recursive_split(part, next_separators, max_chars, min_chars)
                    good_splits.extend(sub)

        if current_chunk:
            joined = current_separator.join(current_chunk)
            if len(joined) >= min_chars or not next_separators:
                good_splits.append(joined)
            else:
                sub = self._recursive_split(joined, next_separators, max_chars, min_chars)
                good_splits.extend(sub)

        return good_splits

    def process_file(self, file_path: str, file_extension: str) -> List[Dict[str, Any]]:
        ext = file_extension.lower()
        if ext == ".pdf":
            pages = self.extract_text_from_pdf(file_path)
        elif ext in [".docx", ".doc"]:
            pages = self.extract_text_from_docx(file_path)
        elif ext == ".txt":
            try:
                with open(file_path, "r", encoding="utf-8") as f:
                    content = f.read()
            except UnicodeDecodeError:
                with open(file_path, "r", encoding="latin-1") as f:
                    content = f.read()
            cleaned_content = self.clean_page_text(content)
            pages = [{"page_number": 1, "content": cleaned_content}]
        else:
            raise ValueError(f"Định dạng tệp không được hỗ trợ: {ext}")

        return self.chunk_text(pages)