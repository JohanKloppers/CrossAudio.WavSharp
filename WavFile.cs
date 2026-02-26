namespace CrossAudio.WavSharp
{
    public class WavFile
    {
        // RIFF chunk descriptor
        public string ChunkId { get; set; }
        public uint ChunkSize { get; set; }
        public string Format { get; set; }

        // "fmt " sub-chunk
        public string SubChunk1Id { get; set; }
        public uint SubChunk1Size { get; set; }
        public ushort AudioFormat { get; set; }
        public ushort NumChannels { get; set; }
        public uint SampleRate { get; set; }
        public uint ByteRate { get; set; }
        public ushort BlockAlign { get; set; }
        public ushort BitsPerSample { get; set; }

        // "data" sub-chunk
        public string SubChunk2Id { get; set; }
        public uint SubChunk2Size { get; set; }
        public byte[] Data { get; set; }
    }
}
