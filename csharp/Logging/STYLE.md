# The C# Style Guide
This project follows the [standard design guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/index)
for C#, and generally uses the default [StyleCop](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) ruleset to
enforce these guidelines. The design rules are validated when we build the project, and any deviation from them
causes the build to fail. There are, however, a few exceptions which are explained below.

We also use [EditorConfig](https://editorconfig.org/) for cross-IDE style enforcement not handled by StyleCop, such as
file encoding.

## StyleCopAnalyzers Rules
Some of the default rules have been overridden using a ruleset file, which is available [here](./StyleCopCustom.ruleset).

## Manual Checks
There are a few checks which are required, but currently not enforced by StyleCopAnalyzers. Therefore, it is up to the
developer to ensure that these rules are adhered to.

### Optional Arguments
Avoid the use of optional arguments. Use method overloading instead.

**GOOD:**
```c#
public void ExampleMethod(int required) :
    this(required, "default string")

public void ExampleMethod(int required, string optional)
```
**BAD:**
```c#
public void ExampleMethod(int required, string optional = "default string")
```

### Line Length
Lines should be no longer than **120** characters long.

### Unused Imports
Avoid leaving unused imports in files.

## IDE Integration

### JetBrains Rider
In order to get full integration with Rider, either reload the solution or restart Rider completely.

