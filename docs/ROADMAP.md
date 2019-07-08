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
Right now, our Java and C# implementations have reached our 1.0.0 feature set and we plan to 
perform a security audit for these in Q3 2019. For Golang, we plan to reach 1.0.0 in Q3
2019 and peform an audit Q4 2019.

Generally, we will target a same- or next-quarter audit as languages hit major version milestones.


## Languages

Beyond our Java, C# and Golang releases we have already planned out releases for Python and 
Javascript/ECMAScript. These are tentatively targeted for Q3 2019. We hope to add other 
languages as we get interest in them.


## Features

Our 1.0.0 release comprises a feature set including a first version of our memory protections library, AES256-GCM 
encryption/decryption, AWS KMS key management store, and two backing storage engines for Metastore persistence: RDS
and DynamoDB. Each implementation of the SDK at this version includes a reference app, unit tests and some kind of 
testing app or integration test suite.

1.1.0 is currently tentatively targeted for Q3/Q4 2019 and our plans are to enhance our memory 
protections and give guidance on how ptrace_scope should be managed on machines running Asherah. In addition, 
we will formalize a cross-language testing method and implementation so that we have a guarantee that all 
languages and underlying storage schemas and data are compatible.

## Issues and Projects

Visit our [GitHub issue tracker](https://github.com/godaddy/asherah/issues) to view and create new
issues and our [GitHub project page](https://github.com/godaddy/asherah/project) to see our story tracking.
We are investigating using [ZenHub](https://www.zenhub.com/). Please note our projects page is very much a 
work in progress as we begin taking internal planning tickets and moving them to Asherah's new home.
