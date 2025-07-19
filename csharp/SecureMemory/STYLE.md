# The C# Style Guide
This project follows the [standard design guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/index)
for C#, and generally uses the Rosyln analyzers to enforce these guidelines.
The design rules are validated when we build the project, and any deviation from them
causes the build to fail. There are, however, a few exceptions which are explained below.

## Manual Checks
There are a few checks which are required, but currently not enforced by StyleCopAnalyzers. Therefore, it is up to the
developer to ensure that these rules are adhered to.

### Unused Imports
Avoid leaving unused imports in files.
