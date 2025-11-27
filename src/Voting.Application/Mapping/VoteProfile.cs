using AutoMapper;
using Voting.Application.DTOs;
using Voting.Domain.Entities;

namespace Voting.Application.Mapping;

public class VoteProfile : Profile
{
    public VoteProfile()
    {
        CreateMap<VoteRequest, VoteRecord>()
            .ForMember(dest => dest.VoteId, opt => opt.MapFrom(src => Guid.NewGuid())) 
            .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => DateTime.UtcNow)); 

        CreateMap<VoteRecord, VoteResponse>();
        
        CreateMap<Poll, PollDto>();

// Mapowanie Opcji
        CreateMap<PollOption, PollOptionDto>();
    }
}