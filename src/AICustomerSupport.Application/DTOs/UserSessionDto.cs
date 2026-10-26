namespace AICustomerSupport.Application.DTOs
{
    public class UserSessionDto
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; }
    }
}