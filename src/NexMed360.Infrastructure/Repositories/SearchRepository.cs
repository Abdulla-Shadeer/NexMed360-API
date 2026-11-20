using Microsoft.EntityFrameworkCore;
using NexMed360.Domain.Scheduling;
using NexMed360.Application.Search;
using NexMed360Api.Data;
using NexMed360Api.Dtos.Search;
using NexMed360Api.Dtos.Patient;
using NexMed360Api.Dtos.PractitionerRole;
using NexMed360Api.Dtos.Organization;
using NexMed360Api.Interfaces.Services;

namespace NexMed360.Infrastructure.Repositories;

public class SearchRepository(ApplicationDbContext context, IPractitionerRoleRepository practitioners, IOrganizationRepository organizations) : ISearchRepository
{
    public Task<List<ViewPatientDto>> Patients(InputTypes type, string input)
    {
        var search = input.Trim().ToLowerInvariant();
        var patients =
            from patient in context.Patients
            join person in context.People on patient.PersonId equals person.Id
            where type == InputTypes.Email
                ? person.Email != null && person.Email.ToLower().Contains(search)
                : (person.FirstName + " " + person.LastName).ToLower().Contains(search)
            select new ViewPatientDto
            {
                Id = patient.Id.ToString(), PersonId = "", UserId = "",
                FirstName = person.FirstName, LastName = person.LastName,
                Email = person.Email!, PhoneNumber = "",
                Gender = patient.Gender, BirthDate = patient.BirthDate
            };

        return patients.OrderBy(row => row.FirstName).ThenBy(row => row.LastName).ThenBy(row => row.Id).Take(30).ToListAsync();
    }

    public async Task<List<ViewOrganizationDto>> Organizations(string input) => await organizations.Views(
        await context.Organizations.Where(row => EF.Functions.ILike(row.Name, $"%{input}%")).ToListAsync());

    public async Task<List<ViewPractitionerRoleDto>> PractitionerRoles(string? name, string? email, string? specialty, bool forBooking = false, DateOnly? date = null)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var roles =
            from role in context.PractitionerRoles
            join practitioner in context.Practitioners on role.PractitionerId equals practitioner.Id
            join person in context.People on practitioner.PersonId equals person.Id
            where (!forBooking || role.AcceptsAppointments && role.IsActive
                && (!role.StartDate.HasValue || role.StartDate <= day) && (!role.EndDate.HasValue || role.EndDate >= day))
                && (string.IsNullOrEmpty(name) || EF.Functions.ILike(person.FirstName + " " + person.LastName, $"%{name}%"))
                && (string.IsNullOrEmpty(email) || EF.Functions.ILike(person.Email!, $"%{email}%"))
                && (string.IsNullOrEmpty(specialty) || EF.Functions.ILike(role.Specialty, $"%{specialty}%"))
            select role;

        var matches = await roles.ToListAsync();
        if (forBooking)
        {
            var ids = matches.Select(role => role.Id).ToArray();
            var sessions = await context.PractitionerSchedules.Where(session => session.IsActive && ids.Contains(session.PractitionerRoleId)).ToListAsync();
            var sessionIds = sessions.Select(session => session.Id).ToArray();
            var slots = date.HasValue ? await context.SessionSlots.Where(slot => sessionIds.Contains(slot.SessionId) && slot.Date == day).ToListAsync() : [];
            var available = sessions.Where(session => !date.HasValue || new SessionSchedule(session.StartTime, session.EndTime, session.Occurrences).IsDateAvailable(day))
                .Where(session => !date.HasValue || (slots.Any(slot => slot.SessionId == session.Id)
                    ? slots.Any(slot => slot.SessionId == session.Id && slot.IsAvailable && slot.BookingAllowed)
                    : session.GenerateSlots(day).Any(slot => slot.BookingAllowed)))
                .Select(session => session.PractitionerRoleId).ToHashSet();
            matches = matches.Where(role => available.Contains(role.Id)).ToList();
        }
        return await practitioners.Views(matches);
    }
}
