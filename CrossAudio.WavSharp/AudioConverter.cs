using System.IO;

namespace CrossAudio.WavSharp
{
    public static class AudioConverter
    {
        /// <summary>
        /// Converts a WAV file to a different encoding and/or sample rate
        /// </summary>
        /// <param name="inputFile">Input WAV file path</param>
        /// <param name="outputFile">Output WAV file path</param>
        /// <param name="targetAudioFormat">Target audio format (1=PCM, 3=IEEE Float)</param>
        /// <param name="targetBitsPerSample">Target bits per sample</param>
        /// <param name="targetSampleRate">Target sample rate (0 to keep original)</param>
        public static void ConvertWavFile(string inputFile, string outputFile, ushort targetAudioFormat = 3, ushort targetBitsPerSample = 32, uint targetSampleRate = 0)
        {
            var reader = new WavReader();
            var writer = new WavWriter();

            // Read the input file
            var wavFile = reader.Read(inputFile);

            // Get samples as float (normalized -1.0 to 1.0)
            var floatSamples = GetSamplesAsFloat(wavFile);

            if (floatSamples == null)
            {
                throw new InvalidOperationException("Unable to extract samples from WAV file");
            }

            // Resample if needed
            var finalSampleRate = targetSampleRate == 0 ? wavFile.SampleRate : targetSampleRate;
            if (finalSampleRate != wavFile.SampleRate)
            {
                floatSamples = WavWriter.ResampleAudio(floatSamples, wavFile.SampleRate, finalSampleRate, wavFile.NumChannels);
            }

            // Convert to target format
            if (targetAudioFormat == 3 && targetBitsPerSample == 32)
            {
                // Write as IEEE float
                writer.Open(outputFile, finalSampleRate, 32, wavFile.NumChannels, 3);
                writer.WriteFloatSamples(floatSamples);
                writer.Close();
            }
            else if (targetAudioFormat == 1)
            {
                // Convert to PCM
                var pcmData = WavWriter.ConvertFloatToPcm(floatSamples, targetBitsPerSample);

                var convertedWavFile = new WavFile
                {
                    AudioFormat = 1,
                    NumChannels = wavFile.NumChannels,
                    SampleRate = finalSampleRate,
                    BitsPerSample = targetBitsPerSample,
                    BlockAlign = (ushort)(wavFile.NumChannels * targetBitsPerSample / 8),
                    ByteRate = finalSampleRate * wavFile.NumChannels * targetBitsPerSample / 8,
                    Data = pcmData,
                    Metadata = wavFile.Metadata // Preserve metadata
                };

                writer.Write(outputFile, convertedWavFile);
            }
            else
            {
                throw new NotSupportedException($"Conversion to format {targetAudioFormat} with {targetBitsPerSample} bits not supported");
            }
        }

        /// <summary>
        /// Converts WAV file encoding to IEEE Float 32-bit
        /// </summary>
        public static void ConvertToFloat32(string inputFile, string outputFile)
        {
            ConvertWavFile(inputFile, outputFile, 3, 32, 0);
        }

        /// <summary>
        /// Converts WAV file encoding to PCM 16-bit
        /// </summary>
        public static void ConvertToPcm16(string inputFile, string outputFile)
        {
            ConvertWavFile(inputFile, outputFile, 1, 16, 0);
        }

        /// <summary>
        /// Converts WAV file encoding to PCM 24-bit
        /// </summary>
        public static void ConvertToPcm24(string inputFile, string outputFile)
        {
            ConvertWavFile(inputFile, outputFile, 1, 24, 0);
        }

        /// <summary>
        /// Resamples WAV file to a different sample rate (maintains original encoding)
        /// </summary>
        public static void ResampleWavFile(string inputFile, string outputFile, uint targetSampleRate)
        {
            var reader = new WavReader();
            var writer = new WavWriter();

            // Read the input file
            var wavFile = reader.Read(inputFile);

            if (wavFile.SampleRate == targetSampleRate)
            {
                // No resampling needed, just copy
                File.Copy(inputFile, outputFile, true);
                return;
            }

            // Get samples as float
            var floatSamples = GetSamplesAsFloat(wavFile);

            if (floatSamples == null)
            {
                throw new InvalidOperationException("Unable to extract samples from WAV file");
            }

            // Resample
            floatSamples = WavWriter.ResampleAudio(floatSamples, wavFile.SampleRate, targetSampleRate, wavFile.NumChannels);

            // Write back in original format
            if (wavFile.AudioFormat == 3)
            {
                writer.Open(outputFile, targetSampleRate, wavFile.BitsPerSample, wavFile.NumChannels, 3);
                writer.WriteFloatSamples(floatSamples);
                writer.Close();
            }
            else
            {
                var pcmData = WavWriter.ConvertFloatToPcm(floatSamples, wavFile.BitsPerSample);

                var resampledWavFile = new WavFile
                {
                    AudioFormat = wavFile.AudioFormat,
                    NumChannels = wavFile.NumChannels,
                    SampleRate = targetSampleRate,
                    BitsPerSample = wavFile.BitsPerSample,
                    BlockAlign = (ushort)(wavFile.NumChannels * wavFile.BitsPerSample / 8),
                    ByteRate = targetSampleRate * wavFile.NumChannels * wavFile.BitsPerSample / 8,
                    Data = pcmData,
                    Metadata = wavFile.Metadata
                };

                writer.Write(outputFile, resampledWavFile);
            }
        }

        private static float[]? GetSamplesAsFloat(WavFile wavFile)
        {
            if (wavFile.AudioFormat == 3)
            {
                // Already float
                if (wavFile.Data == null) return null;
                var floatSamples = new float[wavFile.Data.Length / 4];
                for (int i = 0; i < floatSamples.Length; i++)
                {
                    floatSamples[i] = BitConverter.ToSingle(wavFile.Data, i * 4);
                }
                return floatSamples;
            }
            else if (wavFile.AudioFormat == 1)
            {
                // Convert PCM to float
                if (wavFile.Data == null) return null;
                return ConvertPcmToFloat(wavFile.Data, wavFile.BitsPerSample);
            }
            else
            {
                throw new NotSupportedException($"Audio format {wavFile.AudioFormat} not supported");
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
    }
}