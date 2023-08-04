# Contributing

Everyone is welcome to contribute to GoDaddy's Open Source Software.
Contributing doesn’t just mean submitting pull requests. To get involved, you
can report or triage bugs, and participate in discussions on the evolution of
any project.

No matter how you want to get involved, we ask that you first learn what’s
expected of anyone who participates in the project by reading this Contribution
Guidelines document.

## Answering Questions

One of the most important and immediate ways you can support this project is to
answer questions on [Github][issues]. Whether you’re helping a
newcomer understand a feature or troubleshooting an edge case with a seasoned
developer, your knowledge and experience with a programming language can go a
long way to help others.

## Reporting Bugs

**Do not report potential security vulnerabilities here. Refer to
[SECURITY.md](./SECURITY.md) for more details about the process of reporting
security vulnerabilities.**

Before submitting a ticket, please search our [Issue Tracker][issues] to make
sure it does not already exist and have a simple replication of the behavior. If
the issue is isolated to one of the dependencies of this project, please create
a Github issue in that project. All dependencies should be open source software
and can be found on Github.

Submit a ticket for your issue, assuming one does not already exist:
  - Create it on our [Issue Tracker][issues].
  - Clearly describe the issue by following the template layout.
    - Make sure to include steps to reproduce the bug.
    - A reproducible (unit) test could be helpful in solving the bug.
    - Describe the environment that (re)produced the problem.

## Triaging bugs or contributing code

If you're triaging a bug, try to reduce it. Once a bug can be reproduced, reduce
it to the smallest amount of code possible. Reasoning about a sample or unit
test that reproduces a bug in just a few lines of code is easier than reasoning
about a longer sample.

From a practical perspective, contributions are as simple as:
  - [Forking](https://help.github.com/en/articles/fork-a-repo) the repository on GitHub.
  - Making changes to your forked repository.
  - When committing, reference your issue (if present) and include a note about the fix.
  - If possible, and if applicable, please also add/update unit tests for your changes.
  - Push the changes to your fork and submit a pull request to the 'master' branch of the project's repository.

If you are new to this process, consider taking a look at the whole flow overview
[here](https://guides.github.com/activities/forking/).

If you are interested in making a large change and feel unsure about its overall
effect, start with opening an Issue in the project's [Issue Tracker][issues]
with a high-level proposal and discuss it with the core contributors through
Github comments. After reaching a consensus with core
contributors about the change, discuss the best way to go about implementing it.

## Code Review

Any open source project relies heavily on code review to improve software
quality. All significant changes, by all developers, must be reviewed before
they are committed to the repository. Code reviews are conducted on GitHub
through comments on pull requests or commits. The developer responsible for a
code change is also responsible for making all necessary review-related changes.

Sometimes code reviews will take longer than you would hope for, especially for
larger features. Here are some accepted ways to speed up review times for your
patches:

- Review other people’s changes. If you help out, others will more likely be
willing to do the same for you.
- Split your change into multiple smaller changes. The smaller your change,
the higher the probability that somebody will take a quick look at it.

**Note that anyone is welcome to review and give feedback on a change, but only
people with commit access to the repository can approve it.**

## Attribution of Changes

When contributors submit a change to this project, after that change is
approved, other developers with commit access may commit it for the author. When
doing so, it is important to retain correct attribution of the contribution.
Generally speaking, Git handles attribution automatically.

## Code Style and Documentation

Ensure that your contribution follows the standards set by the project's style
guide with respect to patterns, naming, documentation and testing.

# Additional Resources

- [General GitHub Documentation](https://help.github.com/)
- [GitHub Pull Request documentation](https://help.github.com/send-pull-requests/)

[issues]: https://github.com/godaddy/asherah/issues
