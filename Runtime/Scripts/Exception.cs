using System;

namespace ABCUnity
{
    public class BeatAlignmentException : Exception
    {
        public BeatAlignmentException(string message) : base(message) {}
    }

    public class LayoutException : Exception
    {
        public LayoutException(string message) : base(message) { }
    }

    public class Exception : System.Exception
    {
        public Exception(string message) : base(message) { }
    }
}
