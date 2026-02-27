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
}
