using System.IO;
using System.Text;

namespace CrossAudio.WavSharp
{
    public class WavWriter : IDisposable
    {
        private Stream? _stream;
        private BinaryWriter? _writer;
        private WavFile? _wavFile;
        private bool _headerWritten;
        private bool _dataChunkStarted;
        private long _dataChunkSizePos;
        private int _writtenBytes;
        private bool _disposed;

        public void Write(string filePath, WavFile wavFile)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var binaryWriter = new BinaryWriter(fileStream))
            {
                // RIFF header
                binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
                binaryWriter.Write(0); // Placeholder for file size
                binaryWriter.Write(Encoding.ASCII.GetBytes("WAVE"));

                // fmt chunk
                binaryWriter.Write(Encoding.ASCII.GetBytes("fmt "));
                binaryWriter.Write(16); // Sub-chunk size for PCM
                binaryWriter.Write(wavFile.AudioFormat);
                binaryWriter.Write(wavFile.NumChannels);
                binaryWriter.Write(wavFile.SampleRate);
                binaryWriter.Write(wavFile.ByteRate);
                binaryWriter.Write(wavFile.BlockAlign);
                binaryWriter.Write(wavFile.BitsPerSample);

                // data chunk
                binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
                if (wavFile.Data != null)
                {
                    binaryWriter.Write(wavFile.Data.Length);
                    binaryWriter.Write(wavFile.Data);
                }
                else if (wavFile.AudioFormat == 3)
                {
                    // For IEEE float files created programmatically, Data might be null
                    // In that case, we write an empty data chunk
                    binaryWriter.Write(0);
                }

                // Metadata chunks
                WriteListChunk(binaryWriter, wavFile);
                WriteCueChunk(binaryWriter, wavFile);
                WriteSmplChunk(binaryWriter, wavFile);

                // Update file size
                var fileSize = (int)binaryWriter.BaseStream.Length;
                binaryWriter.BaseStream.Seek(4, SeekOrigin.Begin);
                binaryWriter.Write(fileSize - 8);
            }
        }

        public void Open(string filePath, uint sampleRate, ushort bitDepth, ushort numChannels, ushort audioFormat = 1)
        {
            if (_stream != null)
            {
                Dispose();
            }

            _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            _writer = new BinaryWriter(_stream);

            ushort bytesPerSample;
            if (audioFormat == 3)
            {
                // IEEE Float: bitDepth should be 32 or 64
                bytesPerSample = (ushort)(bitDepth / 8);
            }
            else
            {
                // PCM: bitDepth indicates bits per sample
                bytesPerSample = (ushort)(bitDepth / 8);
            }

            _wavFile = new WavFile
            {
                AudioFormat = audioFormat,
                NumChannels = numChannels,
                SampleRate = sampleRate,
                BitsPerSample = bitDepth,
                BlockAlign = (ushort)(numChannels * bytesPerSample),
                ByteRate = sampleRate * numChannels * bytesPerSample
            };
            _headerWritten = false;
            _dataChunkStarted = false;
            _writtenBytes = 0;
        }

        public void Write(byte[] data)
        {
            if (_writer == null || _wavFile == null) throw new InvalidOperationException("Writer not opened");

            if (!_headerWritten)
            {
                WriteHeader();
            }

            if (!_dataChunkStarted)
            {
                WriteDataChunkHeader();
            }

            _writer.Write(data);
            _writtenBytes += data.Length;
        }

        public void WriteFrame(byte[] frameData)
        {
            Write(frameData);
        }

        public void WriteFloatSamples(float[] samples)
        {
            if (_writer == null || _wavFile == null) throw new InvalidOperationException("Writer not opened");
            if (_wavFile.AudioFormat != 3) throw new InvalidOperationException("Audio format is not IEEE Floating-Point");

            if (!_headerWritten)
            {
                WriteHeader();
            }

            if (!_dataChunkStarted)
            {
                WriteDataChunkHeader();
            }

            foreach (var sample in samples)
            {
                _writer.Write(sample);
                _writtenBytes += 4;
            }
        }

        public static byte[] ConvertFloatToPcm(float[] floatSamples, ushort bitsPerSample)
        {
            var pcmData = new byte[floatSamples.Length * bitsPerSample / 8];

            if (bitsPerSample == 16)
            {
                for (int i = 0; i < floatSamples.Length; i++)
                {
                    var clamped = Math.Clamp(floatSamples[i], -1.0f, 1.0f);
                    var sample = (short)(clamped * 32767.0f);
                    var bytes = BitConverter.GetBytes(sample);
                    Array.Copy(bytes, 0, pcmData, i * 2, 2);
                }
            }
            else if (bitsPerSample == 24)
            {
                for (int i = 0; i < floatSamples.Length; i++)
                {
                    var clamped = Math.Clamp(floatSamples[i], -1.0f, 1.0f);
                    var sample = (int)(clamped * 8388607.0f);
                    pcmData[i * 3] = (byte)(sample & 0xFF);
                    pcmData[i * 3 + 1] = (byte)((sample >> 8) & 0xFF);
                    pcmData[i * 3 + 2] = (byte)((sample >> 16) & 0xFF);
                }
            }
            else if (bitsPerSample == 32)
            {
                for (int i = 0; i < floatSamples.Length; i++)
                {
                    var clamped = Math.Clamp(floatSamples[i], -1.0f, 1.0f);
                    var sample = (int)(clamped * 2147483647.0f);
                    var bytes = BitConverter.GetBytes(sample);
                    Array.Copy(bytes, 0, pcmData, i * 4, 4);
                }
            }
            else
            {
                throw new NotSupportedException($"PCM bit depth {bitsPerSample} not supported");
            }

            return pcmData;
        }

        public static float[] ResampleAudio(float[] inputSamples, uint inputSampleRate, uint outputSampleRate, ushort numChannels)
        {
            if (inputSampleRate == outputSampleRate)
            {
                return (float[])inputSamples.Clone();
            }

            var ratio = (double)outputSampleRate / inputSampleRate;
            var outputLength = (int)(inputSamples.Length * ratio / numChannels) * numChannels;
            var outputSamples = new float[outputLength];

            // Simple linear interpolation resampling
            for (int ch = 0; ch < numChannels; ch++)
            {
                for (int i = 0; i < outputLength / numChannels; i++)
                {
                    var inputIndex = (double)i / ratio * numChannels + ch;
                    var index = (int)inputIndex;
                    var fraction = inputIndex - index;

                    float sample1 = (index < inputSamples.Length) ? inputSamples[index] : 0.0f;
                    float sample2 = (index + numChannels < inputSamples.Length) ? inputSamples[index + numChannels] : sample1;

                    var interpolated = sample1 + (sample2 - sample1) * (float)fraction;
                    outputSamples[i * numChannels + ch] = interpolated;
                }
            }

            return outputSamples;
        }

        public void SetMetadata(Metadata metadata)
        {
            if (_wavFile != null)
            {
                _wavFile.Metadata = metadata;
            }
        }

        public void Close()
        {
            if (_writer == null || _wavFile == null) return;

            // Write metadata if present
            if (_wavFile.Metadata != null)
            {
                WriteListChunk(_writer, _wavFile);
                WriteCueChunk(_writer, _wavFile);
                WriteSmplChunk(_writer, _wavFile);
            }

            // Update data chunk size
            if (_dataChunkSizePos > 0)
            {
                var currentPos = _stream!.Position;
                _stream.Seek(_dataChunkSizePos, SeekOrigin.Begin);
                var dataSize = _writtenBytes;
                _writer.Write(dataSize);
                _stream.Seek(currentPos, SeekOrigin.Begin);
            }

            // Update file size
            _stream!.Seek(4, SeekOrigin.Begin);
            _writer.Write(_writtenBytes + (_wavFile.Metadata != null ? GetMetadataSize(_wavFile.Metadata) : 0) - 8);

            Dispose();
        }

        private void WriteHeader()
        {
            if (_writer == null || _wavFile == null || _headerWritten) return;

            // RIFF header
            _writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write(0); // Placeholder for file size
            _writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            _writer.Write(Encoding.ASCII.GetBytes("fmt "));
            _writer.Write(16); // Sub-chunk size for PCM
            _writer.Write(_wavFile.AudioFormat);
            _writer.Write(_wavFile.NumChannels);
            _writer.Write(_wavFile.SampleRate);
            _writer.Write(_wavFile.ByteRate);
            _writer.Write(_wavFile.BlockAlign);
            _writer.Write(_wavFile.BitsPerSample);

            _headerWritten = true;
        }

        private void WriteDataChunkHeader()
        {
            if (_writer == null || _dataChunkStarted) return;

            _writer.Write(Encoding.ASCII.GetBytes("data"));
            _dataChunkSizePos = _stream!.Position;
            _writer.Write(0); // Placeholder for data size
            _dataChunkStarted = true;
        }

        private int GetMetadataSize(Metadata metadata)
        {
            var size = 0;
            if (!string.IsNullOrEmpty(metadata.Artist)) size += 8 + Encoding.ASCII.GetBytes(metadata.Artist + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Comments)) size += 8 + Encoding.ASCII.GetBytes(metadata.Comments + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Copyright)) size += 8 + Encoding.ASCII.GetBytes(metadata.Copyright + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.CreationDate)) size += 8 + Encoding.ASCII.GetBytes(metadata.CreationDate + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Engineer)) size += 8 + Encoding.ASCII.GetBytes(metadata.Engineer + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Genre)) size += 8 + Encoding.ASCII.GetBytes(metadata.Genre + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Keywords)) size += 8 + Encoding.ASCII.GetBytes(metadata.Keywords + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Medium)) size += 8 + Encoding.ASCII.GetBytes(metadata.Medium + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Title)) size += 8 + Encoding.ASCII.GetBytes(metadata.Title + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Product)) size += 8 + Encoding.ASCII.GetBytes(metadata.Product + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Subject)) size += 8 + Encoding.ASCII.GetBytes(metadata.Subject + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Software)) size += 8 + Encoding.ASCII.GetBytes(metadata.Software + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Source)) size += 8 + Encoding.ASCII.GetBytes(metadata.Source + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Location)) size += 8 + Encoding.ASCII.GetBytes(metadata.Location + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.TrackNbr)) size += 8 + Encoding.ASCII.GetBytes(metadata.TrackNbr + "\0").Length;
            if (!string.IsNullOrEmpty(metadata.Technician)) size += 8 + Encoding.ASCII.GetBytes(metadata.Technician + "\0").Length;

            if (metadata.CuePoints != null)
            {
                size += 8 + 4 + metadata.CuePoints.Length * 24; // cue header + num cues + cue points
            }

            if (metadata.SamplerInfo != null)
            {
                size += 8 + 36; // smpl header + fixed size
                if (metadata.SamplerInfo.Loops != null)
                {
                    size += metadata.SamplerInfo.Loops.Length * 24; // loop entries
                }
            }

            return size > 0 ? 8 + size : 0; // LIST header + content
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _writer?.Dispose();
                _stream?.Dispose();
                _stream = null;
                _writer = null;
                _wavFile = null;
                _disposed = true;
            }
        }

        private void WriteListChunk(BinaryWriter binaryWriter, WavFile wavFile)
        {
            if (wavFile.Metadata != null)
            {
                using (var chunkStream = new MemoryStream())
                using (var chunkWriter = new BinaryWriter(chunkStream))
                {
                    chunkWriter.Write(Encoding.ASCII.GetBytes("INFO"));

                    WriteInfoSubChunk(chunkWriter, "IARL", wavFile.Metadata.Location!);
                    WriteInfoSubChunk(chunkWriter, "IART", wavFile.Metadata.Artist!);
                    WriteInfoSubChunk(chunkWriter, "ISFT", wavFile.Metadata.Software!);
                    WriteInfoSubChunk(chunkWriter, "ICRD", wavFile.Metadata.CreationDate!);
                    WriteInfoSubChunk(chunkWriter, "ICOP", wavFile.Metadata.Copyright!);
                    WriteInfoSubChunk(chunkWriter, "INAM", wavFile.Metadata.Title!);
                    WriteInfoSubChunk(chunkWriter, "IENG", wavFile.Metadata.Engineer!);
                    WriteInfoSubChunk(chunkWriter, "IGNR", wavFile.Metadata.Genre!);
                    WriteInfoSubChunk(chunkWriter, "IPRD", wavFile.Metadata.Product!);
                    WriteInfoSubChunk(chunkWriter, "ISRC", wavFile.Metadata.Source!);
                    WriteInfoSubChunk(chunkWriter, "ISBJ", wavFile.Metadata.Subject!);
                    WriteInfoSubChunk(chunkWriter, "ICMT", wavFile.Metadata.Comments!);
                    WriteInfoSubChunk(chunkWriter, "ITRK", wavFile.Metadata.TrackNbr!);
                    WriteInfoSubChunk(chunkWriter, "ITCH", wavFile.Metadata.Technician!);
                    WriteInfoSubChunk(chunkWriter, "IKEY", wavFile.Metadata.Keywords!);
                    WriteInfoSubChunk(chunkWriter, "IMED", wavFile.Metadata.Medium!);

                    binaryWriter.Write(Encoding.ASCII.GetBytes("LIST"));
                    binaryWriter.Write((int)chunkStream.Length);
                    binaryWriter.Write(chunkStream.ToArray());
                }
            }
        }

        private void WriteInfoSubChunk(BinaryWriter writer, string id, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            writer.Write(Encoding.ASCII.GetBytes(id));
            var bytes = Encoding.ASCII.GetBytes(value + '\0');
            writer.Write(bytes.Length);
            writer.Write(bytes);
            if (bytes.Length % 2 != 0)
            {
                writer.Write((byte)0);
            }
        }

        private void WriteCueChunk(BinaryWriter binaryWriter, WavFile wavFile)
        {
            if (wavFile.Metadata?.CuePoints == null || wavFile.Metadata.CuePoints.Length == 0)
            {
                return;
            }

            binaryWriter.Write(Encoding.ASCII.GetBytes("cue "));
            var numCuePoints = wavFile.Metadata.CuePoints.Length;
            var chunkSize = 4 + numCuePoints * 24;
            binaryWriter.Write(chunkSize);
            binaryWriter.Write(numCuePoints);

            foreach (var cuePoint in wavFile.Metadata.CuePoints)
            {
                if (cuePoint.ID != null)
                {
                    binaryWriter.Write(Encoding.ASCII.GetBytes(cuePoint.ID));
                }
                binaryWriter.Write(cuePoint.Position);
                if (cuePoint.DataChunkID != null)
                {
                    binaryWriter.Write(Encoding.ASCII.GetBytes(cuePoint.DataChunkID));
                }
                binaryWriter.Write(cuePoint.ChunkStart);
                binaryWriter.Write(cuePoint.BlockStart);
                binaryWriter.Write(cuePoint.SampleOffset);
            }
        }

        private void WriteSmplChunk(BinaryWriter binaryWriter, WavFile wavFile)
        {
            if (wavFile.Metadata?.SamplerInfo == null)
            {
                return;
            }

            binaryWriter.Write(Encoding.ASCII.GetBytes("smpl"));

            using (var chunkStream = new MemoryStream())
            using (var chunkWriter = new BinaryWriter(chunkStream))
            {
                var samplerInfo = wavFile.Metadata.SamplerInfo;
                if (samplerInfo.Manufacturer != null)
                {
                    chunkWriter.Write(Encoding.ASCII.GetBytes(samplerInfo.Manufacturer));
                }
                if (samplerInfo.Product != null)
                {
                    chunkWriter.Write(Encoding.ASCII.GetBytes(samplerInfo.Product));
                }
                chunkWriter.Write(samplerInfo.SamplePeriod);
                chunkWriter.Write(samplerInfo.MIDIUnityNote);
                chunkWriter.Write(samplerInfo.MIDIPitchFraction);
                chunkWriter.Write(samplerInfo.SMPTEFormat);
                chunkWriter.Write(samplerInfo.SMPTEOffset);
                chunkWriter.Write(samplerInfo.NumSampleLoops);
                chunkWriter.Write(0); // Sampler data

                if (samplerInfo.Loops != null)
                {
                    foreach (var loop in samplerInfo.Loops)
                    {
                        if (loop.CuePointID != null)
                        {
                            chunkWriter.Write(Encoding.ASCII.GetBytes(loop.CuePointID));
                        }
                        chunkWriter.Write(loop.Type);
                        chunkWriter.Write(loop.Start);
                        chunkWriter.Write(loop.End);
                        chunkWriter.Write(loop.Fraction);
                        chunkWriter.Write(loop.PlayCount);
                    }
                }

                binaryWriter.Write((int)chunkStream.Length);
                binaryWriter.Write(chunkStream.ToArray());
            }
        }
    }
}
