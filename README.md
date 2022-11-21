# RG.AutoException

[![NuGet](https://img.shields.io/nuget/v/RG.AutoException.svg)](https://www.nuget.org/packages/RG.AutoException/)

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
    public sealed class StupidUserException : Exception { }
}
```