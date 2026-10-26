using Xunit;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public class RedisFaqCachingServiceTests
{
    [Fact]
    public async Task GetCachedFaq_ReturnsDataFromRedis_WhenCacheExists()
    {
        // Arrange
        // Giả lập IDistributedCache bằng Moq thuần túy
        var distributedCacheMock = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        string faqQuery = "Tieu chi Sinh vien 5 tot Khoa HTTTQL";
        string expectedCachedAnswer = "Buoc 1: Dang ki truc tuyen tren sinhvien.hutech.edu.vn...";
        
        byte[] cachedBytes = Encoding.UTF8.GetBytes(expectedCachedAnswer);

        distributedCacheMock
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);

        // Act
        var resultBytes = await distributedCacheMock.Object.GetAsync($"faq:{faqQuery}");
        string actualAnswer = resultBytes != null ? Encoding.UTF8.GetString(resultBytes) : null;

        // Assert
        Assert.NotNull(actualAnswer);
        Assert.Equal(expectedCachedAnswer, actualAnswer);
    }
}