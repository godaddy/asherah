# FAQ

#### Why not use something off-the-shelf?

Asherah effectively *is* a composition of off the shelf technologies. All of the envelope encryption keys are rooted in a KMS.
For example, if we are deploying to AWS it would involve Amazon KMS. Encryption techniques comes from language specific
cryptographic libraries. SecureMemory provides secure memory heap, additional memory protection primitives and the
ability to control core dumps and swap, all of which use kernel functionality.

#### Why not use Transparent Disk Encryption or native database encryption functionality?

Application layer encryption is vastly more secure than transparent disk encryption or database encryption. Encryption has
value when unauthorized parties steal the data, but see the data encrypted and thus, it is useless. With transparent disk
encryption, the unauthorized parties would have to steal the disks themselves or image the disks at the block layer to
see the encrypted data. In the public cloud with the type of security employed this is an unlikely scenario. Native
database encryption provides better protection by working one layer higher, such that if the database files were accessed,
they would be encrypted. However, it is far more likely and common that the application will be exploited to exfiltrate the
information at the application layer. Moreover, with security at an application layer, the attacker needs to compromise both
the application server and the database server. Using properly scoped application layer encryption, we make it difficult for an
exploited application to exfiltrate more than one account's information since the session would generally have the key scoped
to that account cached and logically accessible.


#### Why not build a sidecar rather than N libraries for N supported languages?

A sidecar would in best case run on localhost / 127.0.0.1. Thus, unless the localhost connections were encrypted, one could
simply wireshark (packet capture) the localhost / loopback interface, and observe all of the sensitive information as it is
accessed. If we introduce encryption at that layer, that would prevent the application traffic from being packet captured but
it wouldn't help with the fact that anyone on localhost could potentially connect to the port and issue their own commands to
fetch encrypted fields. Then we would have to solve for authentication of clients as well as authentication of servers. Each
time we introduce a TCP connection, we have to deal with encryption, client validation, server validation and the associated
certificates that make those things possible.


#### Why not build a majority of the code in C and reuse it?

This is a viable solution and it was considered, however we determined that if it were possible to make platform native
libraries that use the unmanaged interop facilities of the language to make native calls, it would be easier for application
developers to debug rather than providing a "black box". This also prevents other potential build and compatibility issues
depending on whether we shipped C code that needs to be built for your particular platform, or binary plugs for supported
platforms like Linux/x64, Linux/ARM, Windows/x64, etc. Also, by using the language/platform we are targeting, we are able
to use the Amazon SDK for the language in question. We also improve the ability for the majority of our users to contribute
to the libraries by using their language of choice rather than C.

#### Is it safe to cache crypto keys?

The methodology used to create and protect an off-heap memory area for caching crypto keys is similar to implementations in
the Chrome/Chromium browser, libsecret/Gnome, and the OpenSSL secure memory API. This is a common pattern for maintaining
keys in memory in a way that *attempts* to avoid any key exfiltration including swapping, core dumps, debugger memory scans,
Specter like CPU vulnerabilities, etc. As hardware memory protection facilities are being developed and tested, we will
monitor those options and consider using them after they've been shown secure (eg. Intel SGX, AMD SEV, TPM, etc).

The keys are scoped and the design also allows us to rotate keys fairly often which helps mitigate the potential exposure of
keys.

#### Why can't I just encrypt the data myself with a key stored securely in Kubernetes Secrets?

This simple model doesn't have any of the advantages of the application layer envelope encryption model we have chosen:
- No ability to scope keys and limit the scope of access of sessions
- A great deal of data is encrypted with the same key (high risk of single key leak)
- Rotation of the key requires all of the encrypted data to be rewritten (slow, massive I/O costs, possible outage during
rotation depending on design)
- Does not have the "right to be forgotten" insurance of a per customer key
- Additional tooling required to root keys in a KMS/Cloud HSM

#### Why can't I just use a Key Management Service (KMS) directly?

The added latency of doing a KMS operation for every database read is not insignificant, and it also poses an additional
cost. By maintaining our own key hierarchy that roots in a KMS, we can choose to cache System keys at service startup,
and Intermediate keys at login. This allows us to minimize the calls to a KMS to generally one call on service startup.
The performance gains and cost savings are significant.
