using BlinkCommon.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_CommandStatusJsonExtension
{
    // Trimmed from the real logged response that exposed this bug — same field
    // count/shape (many extra fields, two always-present value-type fields
    // *absent*), just with placeholder values.
    private const string RealShapedPayload =
        "{\"id\":3796763377,\"created_at\":\"2026-11-10T10:52:36+00:00\",\"updated_at\":\"2026-11-10T10:52:36+00:00\"," +
        "\"deleted_at\":null,\"execute_time\":\"2026-11-10T10:52:36+00:00\",\"command\":\"arm\",\"state_stage\":\"rest\"," +
        "\"stage_rest\":\"2026-11-10T10:52:36+00:00\",\"stage_cs_db\":null,\"stage_cs_sent\":null,\"stage_sm\":null," +
        "\"stage_dev\":null,\"stage_is\":null,\"stage_lv\":null,\"stage_vs\":null,\"state_condition\":\"new\"," +
        "\"sm_ack\":null,\"lfr_ack\":null,\"sequence\":null,\"attempts\":0,\"transaction\":\"kkfBuDvzrR1QfKEN\"," +
        "\"player_transaction\":\"C7OBxH7JOPUCdT2l\",\"server\":null,\"duration\":null,\"by_whom\":\"iPhone 15 Pro - 50.1\"," +
        "\"diagnostic\":false,\"debug\":\"\",\"opts_1\":0,\"target\":null,\"target_id\":null," +
        "\"trace_parent\":\"00-6af2f77448f7d09132dac3131b0d7665-274f7ef1d25543b6-00\",\"sync_module_id\":1447602," +
        "\"ring_setup_id\":null,\"parent_command_id\":null,\"camera_id\":null,\"siren_id\":null,\"firmware_id\":null," +
        "\"network_id\":856592,\"account_id\":239361}";

    [Fact]
    public void Test_ParseCommandStatus_RealShapedPayload_ReadsIdCorrectly()
    {
        var result = RealShapedPayload.ParseCommandStatus();

        result.Id.Should().Be(3796763377L);
        result.Network_Id.Should().Be(856592L);
    }

    [Fact]
    public void Test_ParseCommandStatus_RealShapedPayload_MissingCompleteAndStatusCode_DefaultsRatherThanThrows()
    {
        var result = RealShapedPayload.ParseCommandStatus();

        result.Complete.Should().BeFalse();
        result.Status_Code.Should().Be(0);
    }

    [Fact]
    public void Test_ParseCommandStatus_SimpleExistingShape_StillParsesIdentically()
    {
        var result = "{\"id\":500,\"network_id\":263178,\"complete\":true,\"status_code\":908}".ParseCommandStatus();

        result.Id.Should().Be(500L);
        result.Network_Id.Should().Be(263178L);
        result.Complete.Should().BeTrue();
        result.Status_Code.Should().Be(908);
    }

    [Fact]
    public void Test_ParseCommandStatus_MalformedJson_ReturnsAllDefaultWithoutThrowing()
    {
        var result = "{not json".ParseCommandStatus();

        result.Id.Should().Be(0L);
        result.Complete.Should().BeFalse();
    }

    [Fact]
    public void Test_ParseCommandStatus_NullOrEmpty_ReturnsAllDefault()
    {
        ((string?)null).ParseCommandStatus().Id.Should().Be(0L);
        "".ParseCommandStatus().Id.Should().Be(0L);
    }
}
