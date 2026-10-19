import React, { useState } from 'react';
import { Send, MessageSquare, ChevronDown, ChevronUp, FileText, Plus, Database, LogOut } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function ChatPage() {
  const navigate = useNavigate();
  const [conversations, setConversations] = useState([
    { id: 1, title: 'Quy chế điểm rèn luyện 2026' },
    { id: 2, title: 'Thủ tục và học vụ sinh viên' }
  ]);
  const [activeChat, setActiveChat] = useState(1);
  const [messages, setMessages] = useState([
    { 
      sender: 'ai', 
      text: 'Chào bạn, tôi có thể tra cứu thông tin quy chế và hỗ trợ giải đáp thắc mắc gì cho bạn hôm nay?', 
      sources: [
        { title: 'ThongBaoKhungDiemRenLuyen.pdf', page: 3, snippet: 'Quy định về tiêu chí đánh giá rèn luyện sinh viên từ năm học 2026 - 2027...' }
      ] 
    }
  ]);
  const [input, setInput] = useState('');
  const [openSourceIndex, setOpenSourceIndex] = useState(null);

  const handleSendMessage = async (e) => {
    e.preventDefault();
    if (!input.trim()) return;

    const userMsg = { sender: 'user', text: input };
    setMessages(prev => [...prev, userMsg]);
    const query = input;
    setInput('');

    try {
      const response = await fetch('http://localhost:5145/api/chat', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({ message: query })
      });
      const data = await response.json();

      setMessages(prev => [...prev, {
        sender: 'ai',
        text: data.answer,
        sources: data.sources || []
      }]);
    } catch (err) {
      setMessages(prev => [...prev, { sender: 'ai', text: 'Lỗi kết nối đến máy chủ AI.' }]);
    }
  };

  return (
    <div className="flex h-screen bg-white text-gray-800">
      {/* Sidebar Trái */}
      <div className="w-80 bg-slate-900 text-slate-300 flex flex-col border-r border-slate-800">
        <div className="p-4 border-b border-slate-800 flex items-center justify-between">
          <span className="font-bold text-white text-lg tracking-wide">AI Support</span>
          <button 
            onClick={() => navigate('/knowledge-base')} 
            className="p-2 bg-slate-800 hover:bg-slate-700 text-blue-400 rounded-lg transition"
            title="Quản lý Knowledge Base"
          >
            <Database size={18} />
          </button>
        </div>

        <div className="p-3">
          <button className="w-full py-2.5 px-4 bg-blue-600 hover:bg-blue-700 text-white rounded-xl font-medium transition flex items-center justify-center gap-2 shadow-md shadow-blue-600/20">
            <Plus size={18} /> Cuộc hội thoại mới
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-3 space-y-1 mt-2">
          <div className="text-xs font-semibold text-slate-500 uppercase px-3 mb-2">Gần đây</div>
          {conversations.map(c => (
            <div 
              key={c.id} 
              onClick={() => setActiveChat(c.id)}
              className={`flex items-center gap-3 px-3 py-2.5 rounded-xl cursor-pointer transition text-sm ${activeChat === c.id ? 'bg-blue-600 text-white font-medium shadow-sm' : 'hover:bg-slate-800 text-slate-300'}`}
            >
              <MessageSquare size={16} />
              <span className="truncate">{c.title}</span>
            </div>
          ))}
        </div>

        <div className="p-4 border-t border-slate-800">
          <button 
            onClick={() => { localStorage.clear(); navigate('/login'); }}
            className="w-full py-2 px-3 hover:bg-slate-800 text-red-400 rounded-xl transition flex items-center gap-2 text-sm font-medium"
          >
            <LogOut size={16} /> Đăng xuất
          </button>
        </div>
      </div>

      {/* Khung Chat Phải */}
      <div className="flex-1 flex flex-col bg-white">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between shadow-xs">
          <h3 className="font-semibold text-gray-800">Phiên tư vấn học tập & quy chế</h3>
          <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-blue-50 text-blue-700 text-xs font-medium rounded-full border border-blue-200">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span> AI Online
          </span>
        </div>
        
        {/* Tin nhắn */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6 bg-gray-50/50">
          {messages.map((msg, idx) => (
            <div key={idx} className={`flex flex-col ${msg.sender === 'user' ? 'items-end' : 'items-start'}`}>
              <div className={`max-w-2xl p-4 rounded-2xl text-sm leading-relaxed ${msg.sender === 'user' ? 'bg-blue-600 text-white shadow-md shadow-blue-600/10' : 'bg-white border border-gray-200 text-gray-800 shadow-sm'}`}>
                <p>{msg.text}</p>

                {/* Box Sources Collapsible */}
                {msg.sources && msg.sources.length > 0 && (
                  <div className="mt-4 pt-3 border-t border-gray-100">
                    <button 
                      onClick={() => setOpenSourceIndex(openSourceIndex === idx ? null : idx)}
                      className="flex items-center gap-1.5 text-xs font-semibold text-blue-600 hover:text-blue-700 transition"
                    >
                      <FileText size={14} />
                      Tài liệu trích dẫn tham khảo ({msg.sources.length})
                      {openSourceIndex === idx ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                    </button>

                    {openSourceIndex === idx && (
                      <div className="mt-2.5 space-y-2 bg-blue-50/60 border border-blue-100 p-3 rounded-xl text-xs text-gray-700">
                        {msg.sources.map((src, sIdx) => (
                          <div key={sIdx} className="space-y-1">
                            <span className="font-bold text-blue-900">{src.title}</span> (Trang {src.page})
                            <p className="italic text-gray-600 bg-white/60 p-2 rounded-lg border border-blue-100/50">"{src.snippet}"</p>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>

        {/* Input gửi tin nhắn */}
        <div className="p-4 bg-white border-t border-gray-100">
          <form onSubmit={handleSendMessage} className="flex gap-3 max-w-4xl mx-auto">
            <input 
              type="text" 
              value={input} 
              onChange={(e) => setInput(e.target.value)}
              placeholder="Nhập câu hỏi của bạn về quy chế, điểm rèn luyện..." 
              className="flex-1 px-4 py-3 bg-gray-50 border border-gray-300 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-600 focus:bg-white transition text-sm"
            />
            <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white px-6 py-3 rounded-xl transition font-medium flex items-center gap-2 shadow-lg shadow-blue-600/20">
              <Send size={18} /> Gửi
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}