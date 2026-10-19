using System;

namespace AICustomerSupport.Domain.Entities;

public class MessageSource
{
    public Guid MessageSourceId { get; set; }
    public Guid MessageId { get; set; }
    public Guid ChunkId { get; set; }
    public double SimilarityScore { get; set; }
    public int Rank { get; set; }

    public Message Message { get; set; } = null!;
}