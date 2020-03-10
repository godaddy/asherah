# Logging

The logging project provides static factory methods that are used to provide logging within the Asherah SDK.

**This project will be removed once we refactor the usage of ILogger in the SDK.**

## Usage

### Setup the Logger Factory

The logger factory is setup by calling the provided `SetLoggerFactory` method with a LoggerFactory instance
as a parameter. This can typically be done during service startup.

```c#
ILoggerFactory loggerFactory = new LoggerFactory();
loggerFactory.AddProvider(new ConsoleLoggerProvider((category, level) => level >= LogLevel.Information, true));
LogManager.SetLoggerFactory(loggerFactory);
```

### Use the Logger to log relevant details
Use the `CreateLogger` method to get an ILogger instance. This instance is used for logging.

```C#
ILogger logger = LogManager.CreateLogger<_ClassName_>();
logger.LogError("Logger Ready");
```
