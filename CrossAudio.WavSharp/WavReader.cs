using System;
using System.IO;
using System.Text;
using CrossAudio.WavSharp.Exceptions;

namespace CrossAudio.WavSharp
{
    public class WavReader
    {
        private Stream? _stream;
        private BinaryReader? _reader;
        private WavFile? _wavFile;
        private long _pcmDataStartPosition;
        private bool _isInitialized;

        public WavFile Read(string filePath)
        {
            var wavFile = new WavFile();

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                // Read RIFF chunk descriptor
                wavFile.ChunkId = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
                if (wavFile.ChunkId != "RIFF")
                {
                    throw new InvalidWavFileException("Not a valid RIFF file");
                }
                wavFile.ChunkSize = binaryReader.ReadUInt32();
                wavFile.Format = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
                if (wavFile.Format != "WAVE")
                {
                    throw new InvalidWavFileException("Not a valid WAVE file");
                }

                // Loop through chunks
                while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                {
                    var chunkId = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
                    var chunkSize = binaryReader.ReadUInt32();

                    switch (chunkId)
                    {
                        case "fmt ":
                            ReadFmtChunk(binaryReader, wavFile, chunkSize);
                            break;
                        case "data":
                            ReadDataChunk(binaryReader, wavFile, chunkSize);
                            break;
                        case "LIST":
                            ReadListChunk(binaryReader, wavFile, chunkSize);
                            break;
                        case "smpl":
                            ReadSmplChunk(binaryReader, wavFile, chunkSize);
                            break;
                        case "cue ":
                            ReadCueChunk(binaryReader, wavFile, chunkSize);
                            break;
                        default:
                            // Skip unknown chunks
                            binaryReader.ReadBytes((int)chunkSize);
                            break;
                    }
                }

                if (wavFile.SubChunk1Id == null)
                {
                    throw new InvalidWavFileException("'fmt ' chunk not found");
                }
                if (wavFile.SubChunk2Id == null)
                {
                    throw new InvalidWavFileException("'data' chunk not found");
                }
            }

            return wavFile;
        }

        public void Open(string filePath)
        {
            if (_stream != null)
            {
                _stream.Dispose();
            }

            _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            _reader = new BinaryReader(_stream);
            _wavFile = new WavFile();
            _isInitialized = false;
        }

        public void Close()
        {
            _reader?.Dispose();
            _stream?.Dispose();
            _stream = null;
            _reader = null;
            _wavFile = null;
            _isInitialized = false;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized && _reader != null && _wavFile != null)
            {
                ReadHeaders();
                _isInitialized = true;
            }
        }

        private void ReadHeaders()
        {
            if (_reader == null || _wavFile == null)
            {
                throw new InvalidOperationException("Reader not opened");
            }

            // Read RIFF chunk descriptor
            _wavFile.ChunkId = Encoding.ASCII.GetString(_reader.ReadBytes(4));
            if (_wavFile.ChunkId != "RIFF")
            {
                throw new InvalidWavFileException("Not a valid RIFF file");
            }
            _wavFile.ChunkSize = _reader.ReadUInt32();
            _wavFile.Format = Encoding.ASCII.GetString(_reader.ReadBytes(4));
            if (_wavFile.Format != "WAVE")
            {
                throw new InvalidWavFileException("Not a valid WAVE file");
            }

            // Loop through chunks
            while (_reader.BaseStream.Position < _reader.BaseStream.Length)
            {
                var chunkId = Encoding.ASCII.GetString(_reader.ReadBytes(4));
                var chunkSize = _reader.ReadUInt32();

                switch (chunkId)
                {
                    case "fmt ":
                        ReadFmtChunk(_reader, _wavFile, chunkSize);
                        break;
                    case "data":
                        _pcmDataStartPosition = _reader.BaseStream.Position;
                        ReadDataChunk(_reader, _wavFile, chunkSize);
                        // Don't read the data yet, just remember where it starts
                        _reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                        break;
                    case "LIST":
                        ReadListChunk(_reader, _wavFile, chunkSize);
                        break;
                    case "smpl":
                        ReadSmplChunk(_reader, _wavFile, chunkSize);
                        break;
                    case "cue ":
                        ReadCueChunk(_reader, _wavFile, chunkSize);
                        break;
                    default:
                        // Skip unknown chunks
                        _reader.ReadBytes((int)chunkSize);
                        break;
                }
            }

            if (_wavFile.SubChunk1Id == null)
            {
                throw new InvalidWavFileException("'fmt ' chunk not found");
            }
            if (_wavFile.SubChunk2Id == null)
            {
                throw new InvalidWavFileException("'data' chunk not found");
            }
        }

        public bool IsValidFile()
        {
            try
            {
                EnsureInitialized();
                if (_wavFile == null) return false;

                if (_wavFile.NumChannels < 1) return false;

                // For PCM, BitsPerSample should be at least 8
                // For IEEE Float, BitsPerSample should be 32 or 64
                if (_wavFile.AudioFormat == 1 && _wavFile.BitsPerSample < 8) return false;
                if (_wavFile.AudioFormat == 3 && _wavFile.BitsPerSample != 32 && _wavFile.BitsPerSample != 64) return false;

                var duration = Duration();
                return duration > TimeSpan.Zero;
            }
            catch
            {
                return false;
            }
        }

        public TimeSpan Duration()
        {
            EnsureInitialized();
            if (_wavFile == null || _wavFile.SampleRate == 0)
            {
                throw new InvalidOperationException("Cannot calculate duration");
            }

            long bytesPerSample;
            if (_wavFile.AudioFormat == 3)
            {
                // IEEE Float: BitsPerSample indicates float size (32 or 64)
                bytesPerSample = _wavFile.BitsPerSample / 8;
            }
            else
            {
                // PCM: BitsPerSample indicates bit depth
                bytesPerSample = _wavFile.BitsPerSample / 8;
            }

            var totalSamples = _wavFile.SubChunk2Size / (bytesPerSample * _wavFile.NumChannels);
            var durationSeconds = (double)totalSamples / _wavFile.SampleRate;
            return TimeSpan.FromSeconds(durationSeconds);
        }

        public long Seek(long offset, SeekOrigin origin)
        {
            EnsureInitialized();
            if (_stream == null) throw new InvalidOperationException("Reader not opened");

            return _stream.Seek(offset, origin);
        }

        public void Rewind()
        {
            EnsureInitialized();
            if (_stream == null) throw new InvalidOperationException("Reader not opened");

            _stream.Seek(0, SeekOrigin.Begin);
            _isInitialized = false; // Force re-initialization
        }

        public int PCMBuffer(byte[] buffer, int offset, int count)
        {
            EnsureInitialized();
            if (_reader == null || _wavFile == null) throw new InvalidOperationException("Reader not opened");

            // Seek to PCM data if not already there
            if (_stream!.Position != _pcmDataStartPosition)
            {
                _stream.Seek(_pcmDataStartPosition, SeekOrigin.Begin);
            }

            return _reader.Read(buffer, offset, count);
        }

        public byte[]? FullPCMBuffer()
        {
            EnsureInitialized();
            if (_wavFile?.Data != null)
            {
                return _wavFile.Data;
            }

            if (_reader == null) return null;

            // Seek to PCM data
            _stream!.Seek(_pcmDataStartPosition, SeekOrigin.Begin);

            return _reader.ReadBytes((int)_wavFile.SubChunk2Size);
        }

        public float[]? GetFloatSamples()
        {
            EnsureInitialized();
            if (_wavFile == null || _wavFile.Data == null) return null;

            if (_wavFile.AudioFormat != 3)
            {
                throw new InvalidOperationException("Audio format is not IEEE Floating-Point");
            }

            var floatSamples = new float[_wavFile.Data.Length / 4];
            for (int i = 0; i < floatSamples.Length; i++)
            {
                floatSamples[i] = BitConverter.ToSingle(_wavFile.Data, i * 4);
            }
            return floatSamples;
        }

        public float[]? GetSamplesAsFloat()
        {
            EnsureInitialized();
            if (_wavFile == null || _wavFile.Data == null) return null;

            if (_wavFile.AudioFormat == 3)
            {
                // Already float
                return GetFloatSamples();
            }
            else if (_wavFile.AudioFormat == 1)
            {
                // Convert PCM to float
                return ConvertPcmToFloat(_wavFile.Data, _wavFile.BitsPerSample);
            }
            else
            {
                throw new NotSupportedException($"Audio format {_wavFile.AudioFormat} not supported");
            }
        }

        private static float[] ConvertPcmToFloat(byte[] pcmData, ushort bitsPerSample)
        {
            var sampleCount = pcmData.Length * 8 / bitsPerSample;
            var floatSamples = new float[sampleCount];

            if (bitsPerSample == 16)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    var sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
                    floatSamples[i] = sample / 32768.0f;
                }
            }
            else if (bitsPerSample == 24)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    var sample = pcmData[i * 3] | (pcmData[i * 3 + 1] << 8) | ((sbyte)pcmData[i * 3 + 2] << 16);
                    floatSamples[i] = sample / 8388608.0f;
                }
            }
            else if (bitsPerSample == 32)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    var sample = BitConverter.ToInt32(pcmData, i * 4);
                    floatSamples[i] = sample / 2147483648.0f;
                }
            }
            else
            {
                throw new NotSupportedException($"PCM bit depth {bitsPerSample} not supported");
            }

            return floatSamples;
        }

        public WavFile? WavFile => _wavFile;

        private void ReadFmtChunk(BinaryReader binaryReader, WavFile wavFile, uint chunkSize)
        {
            wavFile.SubChunk1Id = "fmt ";
            wavFile.SubChunk1Size = chunkSize;
            wavFile.AudioFormat = binaryReader.ReadUInt16();
            wavFile.NumChannels = binaryReader.ReadUInt16();
            wavFile.SampleRate = binaryReader.ReadUInt32();
            wavFile.ByteRate = binaryReader.ReadUInt32();
            wavFile.BlockAlign = binaryReader.ReadUInt16();
            wavFile.BitsPerSample = binaryReader.ReadUInt16();

            // Skip the rest of the chunk if it has extra data
            var remaining = (int)chunkSize - 16;
            if (remaining > 0)
            {
                binaryReader.ReadBytes(remaining);
            }
        }

        private void ReadDataChunk(BinaryReader binaryReader, WavFile wavFile, uint chunkSize)
        {
            wavFile.SubChunk2Id = "data";
            wavFile.SubChunk2Size = chunkSize;
            wavFile.Data = binaryReader.ReadBytes((int)chunkSize);
        }

        private void ReadListChunk(BinaryReader binaryReader, WavFile wavFile, uint chunkSize)
        {
            var listType = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
            if (listType != "INFO")
            {
                // Not an INFO list, skip it
                binaryReader.ReadBytes((int)chunkSize - 4);
                return;
            }

            if (wavFile.Metadata == null)
            {
                wavFile.Metadata = new Metadata();
            }

            var endPosition = binaryReader.BaseStream.Position + chunkSize - 4;

            while (binaryReader.BaseStream.Position < endPosition)
            {
                var subChunkId = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
                var subChunkSize = binaryReader.ReadUInt32();
                var value = Encoding.ASCII.GetString(binaryReader.ReadBytes((int)subChunkSize));
                // The strings are null-terminated, so we remove the null character
                value = value.TrimEnd('\0');

                switch (subChunkId)
                {
                    case "IARL":
                        wavFile.Metadata.Location = value;
                        break;
                    case "IART":
                        wavFile.Metadata.Artist = value;
                        break;
                    case "ISFT":
                        wavFile.Metadata.Software = value;
                        break;
                    case "ICRD":
                        wavFile.Metadata.CreationDate = value;
                        break;
                    case "ICOP":
                        wavFile.Metadata.Copyright = value;
                        break;
                    case "INAM":
                        wavFile.Metadata.Title = value;
                        break;
                    case "IENG":
                        wavFile.Metadata.Engineer = value;
                        break;
                    case "IGNR":
                        wavFile.Metadata.Genre = value;
                        break;
                    case "IPRD":
                        wavFile.Metadata.Product = value;
                        break;
                    case "ISRC":
                        wavFile.Metadata.Source = value;
                        break;
                    case "ISBJ":
                        wavFile.Metadata.Subject = value;
                        break;
                    case "ICMT":
                        wavFile.Metadata.Comments = value;
                        break;
                    case "ITRK":
                    case "itrk":
                        wavFile.Metadata.TrackNbr = value;
                        break;
                    case "ITCH":
                        wavFile.Metadata.Technician = value;
                        break;
                    case "IKEY":
                        wavFile.Metadata.Keywords = value;
                        break;
                    case "IMED":
                        wavFile.Metadata.Medium = value;
                        break;
                }
                // Word alignment
                if (subChunkSize % 2 != 0)
                {
                    binaryReader.ReadByte();
                }
            }
        }

        private void ReadCueChunk(BinaryReader binaryReader, WavFile wavFile, uint chunkSize)
        {
            var numCuePoints = binaryReader.ReadUInt32();
            if (numCuePoints == 0)
            {
                return;
            }

            if (wavFile.Metadata == null)
            {
                wavFile.Metadata = new Metadata();
            }
            wavFile.Metadata.CuePoints = new CuePoint[numCuePoints];

            for (var i = 0; i < numCuePoints; i++)
            {
                var id = binaryReader.ReadUInt32(); // Read as little-endian uint32
                wavFile.Metadata.CuePoints[i] = new CuePoint
                {
                    ID = id.ToString("D4"), // Format as zero-padded 4-digit string
                    Position = binaryReader.ReadUInt32(),
                    DataChunkID = Encoding.ASCII.GetString(binaryReader.ReadBytes(4)),
                    ChunkStart = binaryReader.ReadUInt32(),
                    BlockStart = binaryReader.ReadUInt32(),
                    SampleOffset = binaryReader.ReadUInt32()
                };
            }
        }

        private void ReadSmplChunk(BinaryReader binaryReader, WavFile wavFile, uint chunkSize)
        {
            if (wavFile.Metadata == null)
            {
                wavFile.Metadata = new Metadata();
            }

            var samplerInfo = new SamplerInfo
            {
                Manufacturer = Encoding.ASCII.GetString(binaryReader.ReadBytes(4)),
                Product = Encoding.ASCII.GetString(binaryReader.ReadBytes(4)),
                SamplePeriod = binaryReader.ReadUInt32(),
                MIDIUnityNote = binaryReader.ReadUInt32(),
                MIDIPitchFraction = binaryReader.ReadUInt32(),
                SMPTEFormat = binaryReader.ReadUInt32(),
                SMPTEOffset = binaryReader.ReadUInt32(),
                NumSampleLoops = binaryReader.ReadUInt32()
            };

            binaryReader.ReadBytes(4);

            if (samplerInfo.NumSampleLoops > 0)
            {
                samplerInfo.Loops = new SampleLoop[samplerInfo.NumSampleLoops];
                for (var i = 0; i < samplerInfo.NumSampleLoops; i++)
                {
                    samplerInfo.Loops[i] = new SampleLoop
                    {
                        CuePointID = Encoding.ASCII.GetString(binaryReader.ReadBytes(4)),
                        Type = binaryReader.ReadUInt32(),
                        Start = binaryReader.ReadUInt32(),
                        End = binaryReader.ReadUInt32(),
                        Fraction = binaryReader.ReadUInt32(),
                        PlayCount = binaryReader.ReadUInt32()
                    };
                }
            }

            wavFile.Metadata.SamplerInfo = samplerInfo;
        }
    }
}
