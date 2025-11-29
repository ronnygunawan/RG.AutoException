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
            catch (HelloWorldException ex)
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
    }
}