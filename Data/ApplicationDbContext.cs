using FinalProjectApi.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinalProjectApi.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<User>(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // generate id on add
            builder.Entity<Hospital>().Property(h => h.Id).ValueGeneratedOnAdd();
            builder.Entity<Doctor>().Property(d => d.Id).ValueGeneratedOnAdd();
            builder.Entity<HospitalAdmin>().Property(hA => hA.Id).ValueGeneratedOnAdd();
            builder.Entity<Receptionist>().Property(r => r.Id).ValueGeneratedOnAdd();
            builder.Entity<Patient>().Property(p => p.Id).ValueGeneratedOnAdd();
            builder.Entity<VisitHistory>().Property(vh => vh.Id).ValueGeneratedOnAdd();
            builder.Entity<ClinicRecord>().Property(cr => cr.Id).ValueGeneratedOnAdd();
            builder.Entity<Prescription>().Property(p => p.Id).ValueGeneratedOnAdd();
            builder.Entity<Appointment>().Property(a => a.Id).ValueGeneratedOnAdd();

            builder
                .Entity<VisitHistory>()
                .Property(vh => vh.VisitDate)
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            ;

            // generate created date on add
            builder
                .Entity<Hospital>()
                .Property(h => h.CreatedDate)
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            builder
                .Entity<Doctor>()
                .Property(d => d.CreatedDate)
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            builder
                .Entity<HospitalAdmin>()
                .Property(hA => hA.CreatedDate)
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            builder
                .Entity<Receptionist>()
                .Property(r => r.CreatedDate)
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            builder
                .Entity<Patient>()
                .Property(p => p.CreatedDate)
                .ValueGeneratedOnAdd()
                .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Hospital> Hospitals { get; set; }
        public DbSet<HospitalAdmin> HospitalAdmins { get; set; }
        public DbSet<Receptionist> Receptionists { get; set; }
        public DbSet<VisitHistory> VisitHistorys { get; set; }
        public DbSet<ClinicRecord> ClinicRecords { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
    }
}
