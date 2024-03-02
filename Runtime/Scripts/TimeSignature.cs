using System;

namespace ABCUnity
{
    class TimeSignature
    {
        public int beatCount { get; }
        public float noteValue { get; }

        public TimeSignature(int beatCount, float noteValue)
        {
            this.beatCount = beatCount;
            this.noteValue = noteValue;
        }

        public static TimeSignature Parse(string timeSignature)
        {
            if (timeSignature == "C")
                return new TimeSignature(4, 1.0f / 4.0f);
            else if (timeSignature == "C|")
                return new TimeSignature(2, 1.0f / 2.0f);

            var parts = timeSignature.Split('/');

            if (parts.Length < 2)
                throw new LayoutException($"Unsupported Time Signature: {timeSignature}");

            try
            {
                var beatCount = int.Parse(parts[0]);
                var noteValue = 1 / float.Parse(parts[1]);

                return new TimeSignature(beatCount, noteValue);
            }
            catch (Exception)
            {
                throw new LayoutException($"Unsupported Time Signature: {timeSignature}");
            }
        }

        public override bool Equals(object obj)
        {
            return obj is TimeSignature signature &&
                   beatCount == signature.beatCount &&
                   noteValue == signature.noteValue;
        }

        public override int GetHashCode()
        {
            int hashCode = 1857893462;
            hashCode = hashCode * -1521134295 + beatCount.GetHashCode();
            hashCode = hashCode * -1521134295 + noteValue.GetHashCode();
            return hashCode;
        }
    }
}