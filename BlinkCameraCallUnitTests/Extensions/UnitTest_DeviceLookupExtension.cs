using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces;
using BlinkCommon.Interfaces.System;
using FluentAssertions;
using Shadow.Quack;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_DeviceLookupExtension
{
    private static ISessionDetails SessionWithCameras(object[]? cameras) =>
        Duck.Implement<ISessionDetails>(new
        {
            Cameras = cameras is null ? null : Duck.Implement<List<IHomeScreenCamera>>(cameras),
        });

    private static ISessionDetails SessionWithDoorbells(object[]? doorbells) =>
        Duck.Implement<ISessionDetails>(new
        {
            Doorbells = doorbells is null ? null : Duck.Implement<List<IHomeScreenDoorbells>>(doorbells),
        });

    private static ISessionDetails SessionWithSyncModules(object[]? syncModules) =>
        Duck.Implement<ISessionDetails>(new
        {
            SyncModules = syncModules is null ? null : Duck.Implement<List<IHomeScreenSyncModule>>(syncModules),
        });

    [Fact]
    public void Test_FindCameraByName_ExactMatch_ReturnsCamera()
    {
        var session = SessionWithCameras(new object[] { new { Id = 1L, Name = "Front Door", Network_Id = 10L } });

        session.FindCameraByName("Front Door")!.Name.Should().Be("Front Door");
    }

    [Fact]
    public void Test_FindCameraByName_CaseInsensitiveMatch_ReturnsCamera()
    {
        var session = SessionWithCameras(new object[] { new { Id = 1L, Name = "Front Door", Network_Id = 10L } });

        session.FindCameraByName("front door")!.Name.Should().Be("Front Door");
    }

    [Fact]
    public void Test_FindCameraByName_NoMatch_ReturnsNull()
    {
        var session = SessionWithCameras(new object[] { new { Id = 1L, Name = "Front Door", Network_Id = 10L } });

        session.FindCameraByName("Garage").Should().BeNull();
    }

    [Fact]
    public void Test_FindCameraByName_AmbiguousMatch_ReturnsNull()
    {
        var session = SessionWithCameras(new object[]
        {
            new { Id = 1L, Name = "Front Door", Network_Id = 10L },
            new { Id = 2L, Name = "Front Door", Network_Id = 11L },
        });

        session.FindCameraByName("Front Door").Should().BeNull();
    }

    [Fact]
    public void Test_FindCameraByName_NullList_ReturnsNull()
    {
        var session = SessionWithCameras(null);

        session.FindCameraByName("Front Door").Should().BeNull();
    }

    [Fact]
    public void Test_FindDoorbellByName_ExactMatch_ReturnsDoorbell()
    {
        var session = SessionWithDoorbells(new object[] { new { Id = 1L, Name = "Front Bell", Network_Id = 10L } });

        session.FindDoorbellByName("Front Bell")!.Name.Should().Be("Front Bell");
    }

    [Fact]
    public void Test_FindDoorbellByName_CaseInsensitiveMatch_ReturnsDoorbell()
    {
        var session = SessionWithDoorbells(new object[] { new { Id = 1L, Name = "Front Bell", Network_Id = 10L } });

        session.FindDoorbellByName("front bell")!.Name.Should().Be("Front Bell");
    }

    [Fact]
    public void Test_FindDoorbellByName_NoMatch_ReturnsNull()
    {
        var session = SessionWithDoorbells(new object[] { new { Id = 1L, Name = "Front Bell", Network_Id = 10L } });

        session.FindDoorbellByName("Back Bell").Should().BeNull();
    }

    [Fact]
    public void Test_FindDoorbellByName_AmbiguousMatch_ReturnsNull()
    {
        var session = SessionWithDoorbells(new object[]
        {
            new { Id = 1L, Name = "Front Bell", Network_Id = 10L },
            new { Id = 2L, Name = "Front Bell", Network_Id = 11L },
        });

        session.FindDoorbellByName("Front Bell").Should().BeNull();
    }

    [Fact]
    public void Test_FindDoorbellByName_NullList_ReturnsNull()
    {
        var session = SessionWithDoorbells(null);

        session.FindDoorbellByName("Front Bell").Should().BeNull();
    }

    [Fact]
    public void Test_FindSyncModuleByName_ExactMatch_ReturnsSyncModule()
    {
        var session = SessionWithSyncModules(new object[] { new { Id = 1L, Name = "Sync A", Network_Id = "10" } });

        session.FindSyncModuleByName("Sync A")!.Name.Should().Be("Sync A");
    }

    [Fact]
    public void Test_FindSyncModuleByName_CaseInsensitiveMatch_ReturnsSyncModule()
    {
        var session = SessionWithSyncModules(new object[] { new { Id = 1L, Name = "Sync A", Network_Id = "10" } });

        session.FindSyncModuleByName("sync a")!.Name.Should().Be("Sync A");
    }

    [Fact]
    public void Test_FindSyncModuleByName_NoMatch_ReturnsNull()
    {
        var session = SessionWithSyncModules(new object[] { new { Id = 1L, Name = "Sync A", Network_Id = "10" } });

        session.FindSyncModuleByName("Sync B").Should().BeNull();
    }

    [Fact]
    public void Test_FindSyncModuleByName_AmbiguousMatch_ReturnsNull()
    {
        var session = SessionWithSyncModules(new object[]
        {
            new { Id = 1L, Name = "Sync A", Network_Id = "10" },
            new { Id = 2L, Name = "Sync A", Network_Id = "11" },
        });

        session.FindSyncModuleByName("Sync A").Should().BeNull();
    }

    [Fact]
    public void Test_FindSyncModuleByName_NullList_ReturnsNull()
    {
        var session = SessionWithSyncModules(null);

        session.FindSyncModuleByName("Sync A").Should().BeNull();
    }
}
