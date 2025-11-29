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
    }
}