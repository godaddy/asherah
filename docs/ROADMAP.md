# Asherah roadmap

![Roadmap](images/roadmap.png)

## Current Status

Asherah is an incubator project and we are currently testing internally. In addition, we have a
roadmap that includes plans to have third-party security audits of the code for every supported
language. Our goal in open sourcing it is to invite the security community and the developer
community at large to help us evaluate, test and iterate on this solution so that we can help
developers manage private data more securely.

## Security

We plan for each language implementation of Asherah to undergo periodic security audits. As we
have progressed from testing internally to releasing the codebase as open source to gather more
feedback about the primary use cases we wish to tackle, these plans are currently tentative.
Right now, our Java, Go and C# implementations are nearing the planned 1.0.0 feature set and we plan to
perform a security audit for these in Q4 2020.

Generally, we will target a same- or next-quarter audit as languages hit major version milestones.


## Languages

Beyond our native Java, C# and Golang implementations we have released [Asherah Server](/server) - a light-weight gRPC service layer built atop the Asherah SDK - which provides access to application-layer encryption from any [gRPC supported language](https://grpc.io/docs/languages/).

Given sufficient demand, additional native implementations may be developed in the future though none are planned at this time.  That said, contributions are always welcome :)


## Features

Our 1.0.0 release comprises a feature set including a first version of our memory protections library, AES256-GCM
encryption/decryption, AWS KMS key management store, and two backing storage engines for Metastore persistence: RDBMS
and DynamoDB. Each implementation of the SDK at this version includes a reference app, unit tests and some kind of
testing app or integration test suite.

1.0.0 is currently tentatively targeted for Q3/Q4 2020 and our plans are to enhance our memory
protections and give guidance on how ptrace_scope should be managed on machines running Asherah. In addition,
we will formalize a cross-language testing method and implementation so that we have a guarantee that all
languages and underlying storage schemas and data are compatible.
