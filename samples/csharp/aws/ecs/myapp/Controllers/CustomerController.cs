using System;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using myapp.Models;
using GoDaddy.Asherah.AppEncryption;
using GoDaddy.Asherah.Crypto;

namespace myapp.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomerController : ControllerBase
    {
        private static readonly ConcurrentDictionary<Guid, CustomerDTO> _customers =
            new ConcurrentDictionary<Guid, CustomerDTO>();

        private readonly SessionFactory _sessionFactory;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(ILogger<CustomerController> logger)
        {
            _logger = logger;

            long sessionCacheSize;
            if (!long.TryParse(Environment.GetEnvironmentVariable("ASHERAH_SESSION_CACHE_SIZE"), out sessionCacheSize))
            {
                sessionCacheSize = 100;
            }

            CryptoPolicy cryptoPolicy = BasicExpiringCryptoPolicy
                .NewBuilder()
                .WithKeyExpirationDays(30)
                .WithRevokeCheckMinutes(30)
                .WithCanCacheSessions(true)                 // Enable session cache
                .WithSessionCacheMaxSize(sessionCacheSize)  // Define the number of maximum sessions to cache
                .WithSessionCacheExpireMillis(5000)         // Evict the session from cache after some milliseconds
                .Build();

            _sessionFactory = SessionFactory
                .NewBuilder("myapp", "api")
                .WithInMemoryMetastore()
                .WithCryptoPolicy(cryptoPolicy)
                .WithStaticKeyManagementService("thisIsAStaticMasterKeyForTesting")
                .WithLogger(_logger)
                .Build();
        }

        // GET: api/customers
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(_customers.Values);
        }

        // GET: api/customers/00000000-0000-0000-0000-000000000000
        [HttpGet("{id}")]
        public ActionResult<CustomerDTO> GetCustomerDTO(Guid id)
        {
            CustomerDTO? retrieved = null;
            if (_customers.TryGetValue(id, out retrieved))
            {
                return retrieved;
            }

            return NotFound();
        }

        // GET: api/customers/00000000-0000-0000-0000-000000000000/full
        [HttpGet("{id}/full")]
        public ActionResult<Customer> GetCustomer(Guid id)
        {
            CustomerDTO? retrieved = null;
            if (_customers.TryGetValue(id, out retrieved))
            {
                return fromCustomerDTO(retrieved);
            }

            return NotFound();
        }

        // POST: api/customers
        [HttpPost]
        public ActionResult<CustomerDTO> PostCustomer(Customer customer)
        {
            customer.Id = Guid.NewGuid();
            customer.Created = DateTime.UtcNow;
            var custDTO = toCustomerDTO(customer);
            if (!_customers.TryAdd(customer.Id, custDTO))
            {
                return BadRequest();
            }

            _logger.LogInformation("customer added successfully (count: {})", _customers.Count);

            return custDTO;
        }

        private CustomerDTO toCustomerDTO(Customer customer)
        {
            // Get a session using the customer id as the partition id
            using var session = _sessionFactory.GetSessionJson(customer.Id.ToString());

            // Extract the PII from the Customer object
            var pii = (JObject)JToken.FromObject(customer.PII());

            // Use the session to encrypt the customer PII
            var encryptedBytes = session.Encrypt(pii);

            return new CustomerDTO
            {
                Id = customer.Id,
                Created = customer.Created,
                SecretInfo = Convert.ToBase64String(encryptedBytes)
            };
        }

        private Customer fromCustomerDTO(CustomerDTO dto)
        {
            // Get a session using the customer id as the partition id
            using var session = _sessionFactory.GetSessionJson(dto.Id.ToString());

            // Check if SecretInfo is null before processing
            if (dto.SecretInfo == null)
            {
                throw new ArgumentException("Customer SecretInfo cannot be null", nameof(dto));
            }

            // Use the session to decrypt the customer PII (SecretInfo)
            var jobject = session.Decrypt(Convert.FromBase64String(dto.SecretInfo));
            var pii = jobject.ToObject<CustomerPII>();

            // Add null check before dereferencing
            if (pii == null)
            {
                throw new InvalidOperationException("Failed to deserialize CustomerPII from decrypted data");
            }

            return new Customer
            {
                Id = dto.Id,
                Created = dto.Created,
                FirstName = pii.FirstName,
                LastName = pii.LastName,
                Address = pii.Address
            };
        }
    }
}
