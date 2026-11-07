using NexMed360Api.Entities;
namespace NexMed360Api.Interfaces.Services;
public interface IHospitalAdminRepository
{
    Task<HospitalAdmin?> Add(HospitalAdmin row);
}
