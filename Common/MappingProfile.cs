using AutoMapper;
using FinalProjectApi.Dtos.Appointment;
using FinalProjectApi.Entities;

namespace FinalProjectApi.Common;

public class MappingProfile: Profile
{
    public MappingProfile() {
        CreateMap<Appointment, ViewAppointment>();
        CreateMap<ViewAppointment, Appointment>();
    }   
}
