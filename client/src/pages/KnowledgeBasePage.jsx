import React, { useState } from 'react';
import { Upload, FileCheck, Clock, Trash2, ArrowLeft, FileText } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function KnowledgeBasePage() {
  const navigate = useNavigate();
  const [documents, setDocuments] = useState([
    { id: 1, name: 'ThongBaoKhungDiemRenLuyen.pdf', status: 'Processed', date: '2026-10-18' },
    { id: 2, name: 'QuyCheDaoTaoDaiHoc.pdf', status: 'Processing', date: '2026-10-19' }
  ]);
  const [selectedFile, setSelectedFile] = useState(null);

  const handleUpload = (e) => {
    e.preventDefault();
    if (!selectedFile) return;

    const newDoc = {
      id: Date.now(),
      name: selectedFile.name,
      status: 'Processing',
      date: new Date().toISOString().split('T')[0]
    };
    setDocuments([newDoc, ...documents]);
    setSelectedFile(null);
    alert('Tải lên thành công! AI Service đang tiến hành phân tách và embedding vào vector database.');
  };

  return (
    <div className="min-h-screen bg-gray-50 p-8">
      <div className="max-w-5xl mx-auto space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <button 
              onClick={() => navigate('/chat')}
              className="p-2 bg-white border border-gray-200 hover:bg-gray-100 rounded-xl transition text-gray-700 shadow-xs"
            >
              <ArrowLeft size={20} />
            </button>
            <div>
              <h1 className="text-2xl font-bold text-gray-900">Quản lý Cơ sở Tri thức</h1>
              <p className="text-sm text-gray-500">Tải lên và theo dõi trạng thái tài liệu RAG trong hệ thống</p>
            </div>
          </div>
        </div>

        {/* Upload Card */}
        <form onSubmit={handleUpload} className="bg-white p-6 rounded-2xl shadow-sm border border-gray-200 flex flex-col sm:flex-row items-center gap-4">
          <div className="w-full flex-1 border-2 border-dashed border-gray-300 rounded-xl p-4 flex items-center justify-center gap-3 bg-gray-50 hover:bg-blue-50/50 transition cursor-pointer relative">
            <FileText className="text-blue-600" size={24} />
            <input 
              type="file" 
              accept=".pdf,.txt"
              onChange={(e) => setSelectedFile(e.target.files[0])}
              className="absolute inset-0 opacity-0 cursor-pointer w-full h-full"
            />
            <span className="text-sm font-medium text-gray-600">
              {selectedFile ? selectedFile.name : 'Chọn file PDF hoặc kéo thả vào đây'}
            </span>
          </div>
          <button 
            type="submit" 
            className="w-full sm:w-auto bg-blue-600 hover:bg-blue-700 text-white px-6 py-3 rounded-xl transition font-medium flex items-center justify-center gap-2 shadow-md shadow-blue-600/20 whitespace-nowrap"
          >
            <Upload size={18} /> Tải lên & Xử lý RAG
          </button>
        </form>

        {/* Table danh sách tài liệu */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="p-4 border-b border-gray-100 font-semibold text-gray-800">Danh sách tài liệu đã đồng bộ</div>
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-100 text-xs font-semibold text-gray-500 uppercase tracking-wider">
                <th className="p-4">Tên tài liệu</th>
                <th className="p-4">Trạng thái Vector</th>
                <th className="p-4">Ngày tải lên</th>
                <th className="p-4 text-center">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 text-sm text-gray-700">
              {documents.map(doc => (
                <tr key={doc.id} className="hover:bg-blue-50/30 transition">
                  <td className="p-4 font-medium text-gray-900 flex items-center gap-2">
                    <FileText size={16} className="text-blue-600" /> {doc.name}
                  </td>
                  <td className="p-4">
                    {doc.status === 'Processed' ? (
                      <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-green-50 text-green-700 text-xs font-medium rounded-full border border-green-200">
                        <FileCheck size={14} /> Đã xử lý (Indexed)
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-amber-50 text-amber-700 text-xs font-medium rounded-full border border-amber-200">
                        <Clock size={14} className="animate-spin" /> Đang xử lý...
                      </span>
                    )}
                  </td>
                  <td className="p-4 text-gray-500">{doc.date}</td>
                  <td className="p-4 text-center">
                    <button 
                      onClick={() => setDocuments(documents.filter(d => d.id !== doc.id))}
                      className="p-2 hover:bg-red-50 text-red-500 hover:text-red-700 rounded-xl transition"
                      title="Xóa tài liệu"
                    >
                      <Trash2 size={16} />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}