using System;

namespace myapp.Models
{
    public class Customer
    {
        public Guid Id { get; set; }
        public DateTime Created { get; set; }

        public string? FirstName
        {
            get { return pii.FirstName; }
            set { pii.FirstName = value; }
        }

        public string? LastName
        {
            get { return pii.LastName; }
            set { pii.LastName = value; }
        }

        public string? Address
        {
            get { return pii.Address; }
            set { pii.Address = value; }
        }

        private readonly CustomerPII pii = new CustomerPII();

        public CustomerPII PII()
        {
            return pii;
        }
    }

    public class CustomerPII
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Address { get; set; }
    }

    /// <summary>
    /// A data transfer object for <see cref="Customer"/>. All sensitive data has been encrypted
    /// and stored in <see cref="SecretInfo"/>.
    /// </summary>
    public class CustomerDTO
    {
        public Guid Id { get; set; }
        public DateTime Created { get; set; }
        public string? SecretInfo { get; set; }
    }
}
