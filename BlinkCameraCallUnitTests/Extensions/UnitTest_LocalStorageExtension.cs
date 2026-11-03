using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces.System;
using FluentAssertions;
using Shadow.Quack;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_LocalStorageExtension
{
    [Fact]
    public void Test_ToLocalClipsListString_NonEmptyList_PrintsNumberedTable()
    {
        var clips = new List<ILocalStorageClip>
        {
            Duck.Implement<ILocalStorageClip>(new
            {
                Id = 1L,
                Camera_Name = "Pergoda",
                Created_At = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                Size = 12345L,
            }),
            Duck.Implement<ILocalStorageClip>(new
            {
                Id = 2L,
                Camera_Name = "Garage",
                Created_At = new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc),
                Size = 54321L,
            }),
        };

        var expected =
            "[1] Id 1  Camera: Pergoda  Recorded: 2026-11-01 12:00:00 UTC  Size: 12345 bytes" + Environment.NewLine +
            "[2] Id 2  Camera: Garage  Recorded: 2026-11-01 13:00:00 UTC  Size: 54321 bytes";

        clips.ToLocalClipsListString("My Blink Sync Module").Should().Be(expected);
    }

    [Fact]
    public void Test_ToLocalClipsListString_EmptyList_PrintsNoClipsMessage()
    {
        var clips = new List<ILocalStorageClip>();

        clips.ToLocalClipsListString("My Blink Sync Module").Should().Be("No clips on 'My Blink Sync Module''s SD card.");
    }
}
