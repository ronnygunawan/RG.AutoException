# RG.AutoException

[![NuGet](https://img.shields.io/nuget/v/RG.AutoException.svg)](https://www.nuget.org/packages/RG.AutoException/) [![.NET](https://github.com/ronnygunawan/RG.AutoException/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ronnygunawan/RG.AutoException/actions/workflows/dotnet.yml)

Generate exceptions as you type.

Just type:
```cs
throw new StupidUserException();
```

And the following code will be generated:
```cs
using System;

namespace GeneratedExceptions
{
    public sealed class StupidUserException : Exception
    {
        public StupidUserException() : base() { }
        public StupidUserException(string message) : base(message) { }
        public StupidUserException(string message, Exception innerException) : base(message, innerException) { }
    }
}
```

All three standard exception constructors are generated, allowing you to use any of these patterns:
```cs
throw new StupidUserException();
throw new StupidUserException("Something went wrong");
throw new StupidUserException("Something went wrong", innerException);
```

## Init-Only Properties

You can also use object initializer syntax to add custom properties to your exceptions:

```cs
throw new StupidUserException
{
    Name = "Bambang"
};
```

This will generate a nullable init-only property:

```cs
using System;

namespace GeneratedExceptions
{
    public sealed class StupidUserException : Exception
    {
        public StupidUserException() : base() { }
        public StupidUserException(string message) : base(message) { }
        public StupidUserException(string message, Exception innerException) : base(message, innerException) { }

        public string? Name { get; init; }
    }
}
```

### Supported Property Types

Only primitive types are supported for init-only properties:
- `string`
- `int`, `long`, `short`, `byte`, `sbyte`
- `uint`, `ulong`, `ushort`
- `float`, `double`, `decimal`
- `bool`
- `char`
- `Guid`
- `DateTime`, `DateTimeOffset`, `TimeSpan`

### Multiple Properties

Properties are merged from all usages of the same exception type:

```cs
// First usage
throw new UserException { Name = "Test" };

// Second usage (somewhere else in the code)
throw new UserException { Age = 25 };

// Generated exception will have both properties:
// public string? Name { get; init; }
// public int? Age { get; init; }
```

### Conflicting Property Types

If the same property is used with different types across usages, a `ConflictingType` placeholder type is used for the property:

```cs
// First usage
throw new UserException { Id = "bambang" };

// Second usage
throw new UserException { Id = 1024 };

// Generated exception will have:
// public ConflictingType? Id { get; init; }
```

Since `ConflictingType` doesn't exist, this will result in a compilation error, prompting you to use consistent types for the property.
