# System Requirements

Asherah makes use of native system calls to provide its Secure Memory implementation. As a result, there are a few
system requirements to note.

Unless otherwise stated in language-specific Secure Memory documentation, the below information should apply to all
language implementations of Asherah.

## Supported Platforms and Required Libraries

The following platforms are supported:

* Linux x86-64
* MacOS x86-64 (primarily intended for local development of Asherah)

For both Linux and MacOS, Asherah depends on a `libc` implementation being available.

Windows is currently supported only for C# SDK, and is primarily intended for local development.

If the library is unable to make the native system calls (e.g. missing method or entire underlying library), the native
facilities of the language will raise an error.

## Memory Usage

One of the system calls Asherah uses in Secure Memory is `mlock`, which locks address space in memory and prevents it
from being paged to disk. Calling `mlock` on an address locks an entire page of memory. Page size is usually dependent
on processor architecture and is typically 4KiB. As a result, you should expect Asherah will use 4KiB per key of
memory and account for it in your memory requirements. Note that in languages calling `mlock` via an unmanaged native
interface, the memory usage will be off-heap.

## System Limits

The amount of memory a user can lock is limited by the system's memlock resource limits. If that limit is reached, the
SDK will throw an exception the next time it tries to lock memory. Below we provide some examples of setting memlock
rlimits.

### Current User Session

In Linux, non-persistent limits can be set using `ulimit`. This usually need to be done as the root user. To set the
current user's session to unlimited memlock, you could run the following:

```console
ulimit -l unlimited
```

### Permanent Setting

On Linux servers the `/etc/security/limits.conf` file allows system-wide and user-specific configuration of memory
locking limits. A new session is needed for user-specific settings, and a system reboot is needed for system-wide
settings. In the below example, we set it to unlimited for all users:

```console
# <User>     <soft/hard/both>     <item>        <value>
*                 -               memlock      unlimited
```

### Systemd

We have observed in internal testing that systemd appears to have its own override for rlimits for services it manages.
One solution we have used is to use systemd's configuration override mechanism:

In `/etc/systemd/system/<service_name>.service.d/override.conf` you could add the following for unlimited memlock:

```ini
[Service]
LimitMEMLOCK=infinity
```

You typically need to run `/bin/systemctl daemon-reload && /bin/systemctl restart <service_name>.service` for the
change to take effect.


### Running Docker Containers

When running Docker containers the `--ulimit` option can be used to set memlock limits. Note that if your Docker daemon
is running through systemd, you may need to do this in conjunction with the [systemd override mechanism](#Systemd).

Below is an example setting unlimited memlock:

```console
docker run -it --ulimit memlock=-1:-1  [...]
```
