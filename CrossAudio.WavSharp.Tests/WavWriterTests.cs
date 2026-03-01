using System.IO;
using Xunit;

namespace CrossAudio.WavSharp.Tests
{
    public class WavWriterTests
    {
        [Fact]
        public void WriteAndRead_SimpleWavFile_ShouldBeEqual()
        {
            // Arrange
            var originalWavFile = new WavFile
            {
                AudioFormat = 1,
                NumChannels = 1,
                SampleRate = 44100,
                ByteRate = 88200,
                BlockAlign = 2,
                BitsPerSample = 16,
                Data = new byte[] { 1, 2, 3, 4 }
            };

            var writer = new WavWriter();
            var reader = new WavReader();
            var filePath = Path.GetTempFileName();

            // Act
            writer.Write(filePath, originalWavFile);
            var readWavFile = reader.Read(filePath);

            // Assert
            Assert.Equal(originalWavFile.AudioFormat, readWavFile.AudioFormat);
            Assert.Equal(originalWavFile.NumChannels, readWavFile.NumChannels);
            Assert.Equal(originalWavFile.SampleRate, readWavFile.SampleRate);
            Assert.Equal(originalWavFile.ByteRate, readWavFile.ByteRate);
            Assert.Equal(originalWavFile.BlockAlign, readWavFile.BlockAlign);
            Assert.Equal(originalWavFile.BitsPerSample, readWavFile.BitsPerSample);
            Assert.Equal(originalWavFile.Data, readWavFile.Data);

            // Clean up
            File.Delete(filePath);
        }

        [Fact]
        public void WriteAndRead_IEEEFloatWavFile_ShouldBeEqual()
        {
            // Arrange
            var testSamples = new float[] { 0.0f, 0.5f, 1.0f, -0.5f, -1.0f };
            var data = new byte[testSamples.Length * 4];
            for (int i = 0; i < testSamples.Length; i++)
            {
                BitConverter.GetBytes(testSamples[i]).CopyTo(data, i * 4);
            }

            var originalWavFile = new WavFile
            {
                AudioFormat = 3,
                NumChannels = 1,
                SampleRate = 44100,
                ByteRate = 176400, // 44100 * 1 * 4
                BlockAlign = 4,    // 1 * 4
                BitsPerSample = 32,
                Data = data
            };

            var writer = new WavWriter();
            var reader = new WavReader();
            var filePath = Path.GetTempFileName();

            // Act
            writer.Write(filePath, originalWavFile);
            var readWavFile = reader.Read(filePath);

            // Assert
            Assert.Equal(originalWavFile.AudioFormat, readWavFile.AudioFormat);
            Assert.Equal(originalWavFile.NumChannels, readWavFile.NumChannels);
            Assert.Equal(originalWavFile.SampleRate, readWavFile.SampleRate);
            Assert.Equal(originalWavFile.ByteRate, readWavFile.ByteRate);
            Assert.Equal(originalWavFile.BlockAlign, readWavFile.BlockAlign);
            Assert.Equal(originalWavFile.BitsPerSample, readWavFile.BitsPerSample);
            Assert.Equal(originalWavFile.Data, readWavFile.Data);

            // Clean up
            File.Delete(filePath);
        }
    }
}
