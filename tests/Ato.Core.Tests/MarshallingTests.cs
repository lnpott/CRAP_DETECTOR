using System.Text.Json;
using Ato.Core;
using Xunit;

public sealed class MarshallingTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void RoundTrip_Observation_IncludesNullableMetadataOnlyWhenPresent()
    {
        var before = new WindowState(
            Visible: true,
            Minimized: false,
            Maximized: false,
            Cloaked: false,
            Left: 100,
            Top: 200,
            Width: 1280,
            Height: 720);

        var observation = new Observation(
            Id: Guid.NewGuid(),
            TsQpc: 123456789L,
            TsSrcTicks: 0L,
            Source: Origin.Unknown,
            Kind: ObservationKind.LocationChange,
            Hwnd: 0x001A0000,
            Pid: 1111U,
            Tid: 2222U,
            Exe: "explorer.exe",
            ClassName: "CabinetWClass",
            Title: "Pasta",
            StateBefore: before,
            StateAfter: null,
            Payload: "delta");

        var json = JsonSerializer.Serialize(observation, Json);
        var round = JsonSerializer.Deserialize<Observation>(json, Json)!;

        Assert.Equal(observation.Id, round.Id);
        Assert.Equal(observation.TsQpc, round.TsQpc);
        Assert.Equal(observation.Source, round.Source);
        Assert.Equal(observation.Kind, round.Kind);
        Assert.Equal(observation.Hwnd, round.Hwnd);
        Assert.Equal(observation.Pid, round.Pid);
        Assert.Equal(observation.Tid, round.Tid);
        Assert.Equal(observation.Exe, round.Exe);
        Assert.Equal(observation.ClassName, round.ClassName);
        Assert.Equal(observation.Title, round.Title);
        Assert.Equal(observation.StateBefore, round.StateBefore);
        Assert.Null(round.StateAfter);
        Assert.Equal(observation.Payload, round.Payload);
    }

    [Fact]
    public void Inference_ExcludesInferenceIdFromEquality()
    {
        var eids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var a1 = new Inference(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Origin.User,
            Confidence.Alta,
            "R1",
            1,
            eids);
        var a2 = new Inference(
            Guid.NewGuid(),
            a1.ObservationId,
            a1.Origin,
            a1.Confidence,
            a1.RuleId,
            a1.RuleVersion,
            a1.EvidenceIds);

        Assert.Equal(a1.ObservationId, a2.ObservationId);
        Assert.Equal(a1.Origin, a2.Origin);
    }
}
