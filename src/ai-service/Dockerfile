# Sử dụng base image Python chính thức
FROM python:3.11-slim

# Thiết lập thư mục làm việc trong container
WORKDIR /app

# Cài đặt các gói hệ thống cần thiết (nếu có)
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Sao chép file danh sách thư viện và cài đặt
COPY requirements.txt .
RUN pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

# Sao chép toàn bộ mã nguồn vào container
COPY . .

# Mở cổng 8001 cho service
EXPOSE 8001

# Lệnh khởi động ứng dụng bằng uvicorn
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8001"]