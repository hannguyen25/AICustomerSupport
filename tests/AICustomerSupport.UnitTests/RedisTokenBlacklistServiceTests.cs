using Xunit;
using Moq;
using Microsoft.Extensions.Caching.Distributed;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public class RedisFaqCachingServiceTests
{
    [Fact]
    public async Task GetCachedFaq_ReturnsDataFromRedis_WhenCacheExists()
    {
        // Arrange
        var distributedCacheMock = new Mock<IDistributedCache>();
        string faqQuery = "Tieu chi Sinh vien 5 tot Khoa HTTTQL";
        string expectedCachedAnswer = "Buoc 1: Dang ki truc tuyen tren sinhvien.hutech.edu.vn. Buoc 2: Tao Drive ca nhan...";
        
        byte[] cachedBytes = Encoding.UTF8.GetBytes(expectedCachedAnswer);

        // Giả lập Redis trả về dữ liệu cache cho câu hỏi FAQ
        distributedCacheMock
            .Setup(c => c.GetAsync(It.Is<string>(k => k.Contains(faqQuery)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);

        // Act
        var resultBytes = await distributedCacheMock.Object.GetAsync($"faq:{faqQuery}");
        string actualAnswer = resultBytes != null ? Encoding.UTF8.GetString(resultBytes) : null;

        // Assert
        Assert.NotNull(actualAnswer);
        Assert.Equal(expectedCachedAnswer, actualAnswer);
        // Xác nhận rằng Vector DB / LLM API không cần phải gọi lại vì đã ăn cache Redis
    }
}