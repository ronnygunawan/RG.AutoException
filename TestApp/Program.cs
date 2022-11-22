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
                throw new KeyNotFoundException();
            }
        }
    }
}