using CrossAudio.WavSharp;
using System.IO;
using System.Linq;
using Xunit;

namespace CrossAudio.WavSharp.Tests;

public class WavPortTests
{
    [Fact]
    public void EncoderRoundTrip_KickWav_ShouldPreserveData()
    {
        RoundTripTest("TestAudio/kick.wav");
    }

    [Fact]
    public void EncoderRoundTrip_Kick16b441kWav_ShouldPreserveData()
    {
        RoundTripTest("TestAudio/kick-16b441k.wav");
    }

    [Fact]
    public void EncoderRoundTrip_BassWav_ShouldPreserveData()
    {
        RoundTripTest("TestAudio/bass.wav");
    }

    [Fact]
    public void EncoderRoundTrip_8bitWav_ShouldPreserveData()
    {
        RoundTripTest("TestAudio/8bit.wav");
    }

    [Fact]
    public void EncoderRoundTrip_32bitWav_ShouldPreserveData()
    {
        RoundTripTest("TestAudio/32bit.wav");
    }

    [Fact]
    public void EncoderRoundTrip_WithMetadata_ShouldPreserveMetadata()
    {
        var metadata = new Metadata
        {
            Artist = "Matt",
            Copyright = "copyleft",
            Comments = "A comment",
            CreationDate = "2017-12-12",
            Engineer = "Matt A",
            Technician = "Matt Aimonetti",
            Genre = "test",
            Keywords = "go code",
            Medium = "Virtual",
            Title = "Titre",
            Product = "go-audio",
            Subject = "wav codec",
            Software = "go-audio codec",
            Source = "Audacity generator",
            Location = "Los Angeles",
            TrackNbr = "42"
        };

        RoundTripTest("TestAudio/8bit.wav", metadata);
    }

    private void RoundTripTest(string inputFile, Metadata? metadata = null)
    {
        var reader = new WavReader();
        var writer = new WavWriter();

        // Read original file
        var originalWav = reader.Read(inputFile);

        // Create temp output file
        var outputFile = Path.GetTempFileName();

        try
        {
            // Write using new writer
            if (metadata != null)
            {
                originalWav.Metadata = metadata;
            }

            writer.Write(outputFile, originalWav);

            // Read back
            var roundTripWav = reader.Read(outputFile);

            // Compare basic properties
            Assert.Equal(originalWav.AudioFormat, roundTripWav.AudioFormat);
            Assert.Equal(originalWav.NumChannels, roundTripWav.NumChannels);
            Assert.Equal(originalWav.SampleRate, roundTripWav.SampleRate);
            Assert.Equal(originalWav.ByteRate, roundTripWav.ByteRate);
            Assert.Equal(originalWav.BlockAlign, roundTripWav.BlockAlign);
            Assert.Equal(originalWav.BitsPerSample, roundTripWav.BitsPerSample);

            // Compare data
            Assert.Equal(originalWav.Data, roundTripWav.Data);

            // Compare metadata if present
            if (metadata != null)
            {
                Assert.NotNull(roundTripWav.Metadata);
                Assert.Equal(metadata.Artist, roundTripWav.Metadata.Artist);
                Assert.Equal(metadata.Comments, roundTripWav.Metadata.Comments);
                Assert.Equal(metadata.Copyright, roundTripWav.Metadata.Copyright);
                Assert.Equal(metadata.CreationDate, roundTripWav.Metadata.CreationDate);
                Assert.Equal(metadata.Engineer, roundTripWav.Metadata.Engineer);
                Assert.Equal(metadata.Technician, roundTripWav.Metadata.Technician);
                Assert.Equal(metadata.Genre, roundTripWav.Metadata.Genre);
                Assert.Equal(metadata.Keywords, roundTripWav.Metadata.Keywords);
                Assert.Equal(metadata.Medium, roundTripWav.Metadata.Medium);
                Assert.Equal(metadata.Title, roundTripWav.Metadata.Title);
                Assert.Equal(metadata.Product, roundTripWav.Metadata.Product);
                Assert.Equal(metadata.Subject, roundTripWav.Metadata.Subject);
                Assert.Equal(metadata.Software, roundTripWav.Metadata.Software);
                Assert.Equal(metadata.Source, roundTripWav.Metadata.Source);
                Assert.Equal(metadata.Location, roundTripWav.Metadata.Location);
                Assert.Equal(metadata.TrackNbr, roundTripWav.Metadata.TrackNbr);
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

    [Fact]
    public void Reader_IsValidFile_KickWav_ShouldReturnTrue()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/kick.wav");
        try
        {
            Assert.True(reader.IsValidFile());
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_IsValidFile_BassWav_ShouldReturnTrue()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/bass.wav");
        try
        {
            Assert.True(reader.IsValidFile());
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_IsValidFile_BwfWav_ShouldReturnTrue()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/bwf.wav");
        try
        {
            Assert.True(reader.IsValidFile());
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_IsValidFile_AviFile_ShouldReturnFalse()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/sample.avi");
        try
        {
            Assert.False(reader.IsValidFile());
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_IsValidFile_AiffFile_ShouldReturnFalse()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/bloop.aif");
        try
        {
            Assert.False(reader.IsValidFile());
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_Duration_KickWav_ShouldMatchExpected()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/kick.wav");
        try
        {
            var duration = reader.Duration();
            // kick.wav: 4484 samples at 22050 Hz = 4484/22050 ≈ 0.2034 seconds = 203.4ms
            var expectedMs = 203.4;
            Assert.Equal(expectedMs, duration.TotalMilliseconds, 1);
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_Seek_BassWav_ShouldWork()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/bass.wav");
        try
        {
            var pcmLength = reader.WavFile!.SubChunk2Size;
            var seekPos = pcmLength / 2;
            var actualPos = reader.Seek(seekPos, SeekOrigin.Begin);
            Assert.Equal(seekPos, actualPos);
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_Rewind_BassWav_ShouldWork()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/bass.wav");
        try
        {
            // Read some data first
            var buffer = new byte[512];
            var bytesRead = reader.PCMBuffer(buffer, 0, 512);
            Assert.True(bytesRead > 0);

            // Rewind
            reader.Rewind();

            // Read again and compare
            var buffer2 = new byte[512];
            var bytesRead2 = reader.PCMBuffer(buffer2, 0, 512);
            Assert.Equal(bytesRead, bytesRead2);
            Assert.Equal(buffer, buffer2);
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_FullPCMBuffer_BassWav_ShouldReturnAllData()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/bass.wav");
        try
        {
            var data = reader.FullPCMBuffer();
            Assert.NotNull(data);
            Assert.Equal(reader.WavFile!.SubChunk2Size, (uint)data!.Length);
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_FullPCMBuffer_KickWav_ShouldReturnCorrectData()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/kick.wav");
        try
        {
            var data = reader.FullPCMBuffer();
            Assert.NotNull(data);

            // Convert first few samples to 16-bit signed integers (little endian)
            var expectedFirstSamples = new short[] { 76, 75, 77, 73, 74, 69, 73, 68, 72, 66, 67, 71, 529, 1427, 2243, 2943 };
            for (int i = 0; i < expectedFirstSamples.Length; i++)
            {
                var sample = (short)(data![i * 2] | (data[i * 2 + 1] << 8));
                Assert.Equal(expectedFirstSamples[i], sample);
            }
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Reader_ReadMetadata_ListInfoWav_ShouldParseMetadata()
    {
        var reader = new WavReader();
        var wavFile = reader.Read("TestAudio/listinfo.wav");

        Assert.NotNull(wavFile.Metadata);
        Assert.Equal("artist", wavFile.Metadata.Artist);
        Assert.Equal("track title", wavFile.Metadata.Title);
        Assert.Equal("album title", wavFile.Metadata.Product);
        Assert.Equal("42", wavFile.Metadata.TrackNbr);
        Assert.Equal("2017", wavFile.Metadata.CreationDate);
        Assert.Equal("genre", wavFile.Metadata.Genre);
        Assert.Equal("my comment", wavFile.Metadata.Comments);
    }

    [Fact]
    public void Reader_ReadMetadata_FlloopWav_ShouldParseSamplerInfoAndCuePoints()
    {
        var reader = new WavReader();
        var wavFile = reader.Read("TestAudio/flloop.wav");

        Assert.NotNull(wavFile.Metadata);
        Assert.NotNull(wavFile.Metadata.SamplerInfo);
        Assert.Equal(22676u, wavFile.Metadata.SamplerInfo.SamplePeriod);
        Assert.Equal(60u, wavFile.Metadata.SamplerInfo.MIDIUnityNote);
        Assert.Equal(1u, wavFile.Metadata.SamplerInfo.NumSampleLoops);

        Assert.NotNull(wavFile.Metadata.SamplerInfo.Loops);
        Assert.Single(wavFile.Metadata.SamplerInfo.Loops);
        var loop = wavFile.Metadata.SamplerInfo.Loops[0];
        Assert.Equal(1024u, loop.Type);
        Assert.Equal(0u, loop.Start);
        Assert.Equal(107999u, loop.End);
        Assert.Equal(0u, loop.Fraction);
        Assert.Equal(0u, loop.PlayCount);

        Assert.NotNull(wavFile.Metadata.CuePoints);
        Assert.Equal(16, wavFile.Metadata.CuePoints.Length);

        // Check first cue point
        var cuePoint = wavFile.Metadata.CuePoints[0];
        Assert.Equal("0001", cuePoint.ID);
        Assert.Equal(0u, cuePoint.Position);
        Assert.Equal("data", cuePoint.DataChunkID);
    }

    [Fact]
    public void Reader_Attributes_KickWav_ShouldMatchExpected()
    {
        var reader = new WavReader();
        var wavFile = reader.Read("TestAudio/kick.wav");

        Assert.Equal(1, wavFile.NumChannels);
        Assert.Equal(22050u, wavFile.SampleRate);
        Assert.Equal(44100u, wavFile.ByteRate);
        Assert.Equal(16, wavFile.BitsPerSample);
    }

    [Fact]
    public void Reader_PCMBuffer_BassWav_ShouldReadCorrectly()
    {
        var reader = new WavReader();
        reader.Open("TestAudio/bass.wav");
        try
        {
            var buffer = new byte[4096];
            var bytesRead = reader.PCMBuffer(buffer, 0, buffer.Length);

            // Should read some data
            Assert.True(bytesRead > 0);

            // Convert first few samples (24-bit little endian signed)
            var expectedFirstSamples = new int[] { 0, 0, 110, 103, 63, 58, -2915, -2756, 2330, 2209 };
            var sampleIndex = 0;
            for (int i = 0; i < expectedFirstSamples.Length * 3 && i < bytesRead; i += 3)
            {
                var sample = buffer[i] | (buffer[i + 1] << 8) | ((sbyte)buffer[i + 2] << 16);
                Assert.Equal(expectedFirstSamples[sampleIndex], sample);
                sampleIndex++;
            }
        }
        finally
        {
            reader.Close();
        }
    }

    [Fact]
    public void Writer_ChunkedWrite_ShouldWork()
    {
        var outputFile = Path.GetTempFileName();
        var writer = new WavWriter();

        try
        {
            writer.Open(outputFile, 44100, 16, 1);

            // Write some sample data in chunks
            var chunk1 = new byte[] { 1, 2, 3, 4 };
            var chunk2 = new byte[] { 5, 6, 7, 8 };

            writer.Write(chunk1);
            writer.Write(chunk2);
            writer.Close();

            // Read back and verify
            var reader = new WavReader();
            var wavFile = reader.Read(outputFile);

            Assert.Equal(1, wavFile.NumChannels);
            Assert.Equal(44100u, wavFile.SampleRate);
            Assert.Equal(16, wavFile.BitsPerSample);
            Assert.Equal(chunk1.Concat(chunk2).ToArray(), wavFile.Data);
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