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
