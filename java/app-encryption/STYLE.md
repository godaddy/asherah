# The Java Style Guide
The coding style for the project is inspired by [Google's style guide](https://google.github.io/styleguide/javaguide.html). 
We are using [Checkstyle](https://checkstyle.org) to enforce the rules which are defined [here](./checkstyle.xml). 
The design rules are validated when we build the project and any deviation from them causes the build to fail.

We also use [EditorConfig](https://editorconfig.org/) for cross-IDE style enforcement not handled by Checkstyle,
such as file encoding.

## IDE Integration

### JetBrains IntelliJ
To enable checkstyle, use the [Checkstyle plugin](https://plugins.jetbrains.com/plugin/1065-checkstyle-idea) and 
configure it to use the [checkstyle](./checkstyle.xml) file.

EditorConfig support is built in by default. If the plugin is disabled, you can enable it by following the steps
[here](https://www.jetbrains.com/help/idea/configuring-code-style.html#editorconfig)

### Eclipse
To enable checkstyle, use the plugin available [here](https://checkstyle.org/eclipse-cs/#!/).

Use [this](https://github.com/ncjones/editorconfig-eclipse#readme) plugin to enable support for EditorConfig.
