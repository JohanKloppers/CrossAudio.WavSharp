using System;

namespace CrossAudio.WavSharp.Exceptions
{
    public class InvalidWavFileException : Exception
    {
        public InvalidWavFileException()
        {
        }

        public InvalidWavFileException(string message)
            : base(message)
        {
        }

        public InvalidWavFileException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
