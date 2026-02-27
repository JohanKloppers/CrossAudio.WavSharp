namespace CrossAudio.WavSharp
{
    public class Metadata
    {
        public SamplerInfo? SamplerInfo { get; set; }
        public string? Artist { get; set; }
        public string? Comments { get; set; }
        public string? Copyright { get; set; }
        public string? CreationDate { get; set; }
        public string? Engineer { get; set; }
        public string? Technician { get; set; }
        public string? Genre { get; set; }
        public string? Keywords { get; set; }
        public string? Medium { get; set; }
        public string? Title { get; set; }
        public string? Product { get; set; }
        public string? Subject { get; set; }
        public string? Software { get; set; }
        public string? Source { get; set; }
        public string? Location { get; set; }
        public string? TrackNbr { get; set; }
        public CuePoint[]? CuePoints { get; set; }
    }

    public class SamplerInfo
    {
        public string? Manufacturer { get; set; }
        public string? Product { get; set; }
        public uint SamplePeriod { get; set; }
        public uint MIDIUnityNote { get; set; }
        public uint MIDIPitchFraction { get; set; }
        public uint SMPTEFormat { get; set; }
        public uint SMPTEOffset { get; set; }
        public uint NumSampleLoops { get; set; }
        public SampleLoop[]? Loops { get; set; }
    }

    public class SampleLoop
    {
        public string? CuePointID { get; set; }
        public uint Type { get; set; }
        public uint Start { get; set; }
        public uint End { get; set; }
        public uint Fraction { get; set; }
        public uint PlayCount { get; set; }
    }

    public class CuePoint
    {
        public string? ID { get; set; }
        public uint Position { get; set; }
        public string? DataChunkID { get; set; }
        public uint ChunkStart { get; set; }
        public uint BlockStart { get; set; }
        public uint SampleOffset { get; set; }
    }
}
