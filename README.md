# RG.AutoException

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