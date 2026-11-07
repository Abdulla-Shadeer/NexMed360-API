namespace NexMed360Api.Entities
{
    public class Hospital: Entity
    {

        public string Name { get; set; }

        public string Street { get; set; }
        public string City { get; set; }
        public string District { get; set; }

        public string Phone { get; set; }
        public string Grade { get; set; }

        public void Update(string? name, string? street, string? city, string? district, string? phone, string? grade)
        {
            Name = name ?? Name; Street = street ?? Street; City = city ?? City;
            District = district ?? District; Phone = phone ?? Phone; Grade = grade ?? Grade;
        }
}
}
