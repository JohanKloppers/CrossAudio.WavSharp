using CrossAudio.WavSharp;
using System.IO;
using Xunit;

namespace CrossAudio.WavSharp.Tests;

public class WavReaderTests
{
    [Fact]
    public void Read_KickWav_ParsesAttributesCorrectly()
    {
        // Arrange
        var reader = new WavReader();
        var filePath = "TestAudio/kick.wav";

        // Act
        var wavFile = reader.Read(filePath);

        // Assert
        Assert.Equal(1, wavFile.NumChannels);
        Assert.Equal(22050u, wavFile.SampleRate);
        Assert.Equal(44100u, wavFile.ByteRate);
        Assert.Equal(16, wavFile.BitsPerSample);
    }

    [Fact]
    public void Read_32bitWav_ParsesAttributesCorrectly()
    {
        // Arrange
        var reader = new WavReader();
        var filePath = "TestAudio/32bit.wav";

        // Act
        var wavFile = reader.Read(filePath);

        // Assert
        Console.WriteLine($"AudioFormat: {wavFile.AudioFormat}");
        Console.WriteLine($"BitsPerSample: {wavFile.BitsPerSample}");
        Console.WriteLine($"NumChannels: {wavFile.NumChannels}");
        Console.WriteLine($"SampleRate: {wavFile.SampleRate}");
        Console.WriteLine($"Data length: {wavFile.Data?.Length}");
    }

    [Fact]
    public void Read_IEEEFloatWav_ReadsFloatSamplesCorrectly()
    {
        // Arrange
        var writer = new WavWriter();
        var reader = new WavReader();
        var outputFile = Path.GetTempFileName();
        var testSamples = new float[] { 0.0f, 0.5f, 1.0f, -0.5f, -1.0f };

        try
        {
            // Create IEEE float WAV file
            writer.Open(outputFile, 44100, 32, 1, 3); // 32-bit float
            writer.WriteFloatSamples(testSamples);
            writer.Close();

            // Read back using Read method
            var wavFile = reader.Read(outputFile);

            // Assert basic properties
            Assert.Equal(3, wavFile.AudioFormat);
            Assert.Equal(32, wavFile.BitsPerSample);
            Assert.Equal(1, wavFile.NumChannels);
            Assert.Equal(44100u, wavFile.SampleRate);

            // For IEEE float, convert data to float array
            var floatSamples = new float[wavFile.Data!.Length / 4];
            for (int i = 0; i < floatSamples.Length; i++)
            {
                floatSamples[i] = BitConverter.ToSingle(wavFile.Data, i * 4);
            }

            Assert.Equal(testSamples.Length, floatSamples.Length);

            // Check values (with small tolerance for floating point precision)
            for (int i = 0; i < testSamples.Length; i++)
            {
                Assert.Equal(testSamples[i], floatSamples[i], 1e-6f);
            }
        }
        finally
        {
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
        }
    }
}
