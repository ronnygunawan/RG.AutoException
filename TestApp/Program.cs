using GeneratedExceptions;

namespace TestApp
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                throw new HelloWorldException();
            }
            catch (HelloWorldException)
            {
                try
                {
                    throw new AnotherTestException("Error message");
                }
                catch (AnotherTestException ex2)
                {
                    throw new WrappedException("Wrapped error", ex2);
                }
            }
        }

        // Test method for init-only properties
        private static void TestInitProperties()
        {
            // Test with string property
            throw new StupidUserException
            {
                Name = "Bambang"
            };
        }

        // Test method for multiple properties
        private static void TestMultipleProperties()
        {
            throw new PropertyTestException
            {
                UserId = 42,
                UserName = "TestUser",
                IsActive = true
            };
        }

        // Test method for explicit cast to specify base exception class
        private static void TestExplicitCastBaseClass(string? name)
        {
            if (name is null)
            {
                // This will generate CastTestException with ArgumentNullException as base
                throw (ArgumentNullException)new CastTestException(nameof(name));
            }

            // This demonstrates ArgumentException as base with paramName
            throw (ArgumentException)new InvalidNameException("Invalid name format", nameof(name));
        }

        // Test method for explicit cast with properties
        private static void TestExplicitCastWithProperties()
        {
            throw (InvalidOperationException)new DetailedOperationException("Operation failed")
            {
                OperationName = "SaveData",
                RetryCount = 3
            };
        }
    }
}