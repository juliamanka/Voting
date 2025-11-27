using System.Text.Json.Serialization;

namespace Voting.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VoteStatus
{
    Counted, Rejected, Pending, Duplicate, Failed
}