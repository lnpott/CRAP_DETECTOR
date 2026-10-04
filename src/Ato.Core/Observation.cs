namespace Ato.Core;

public enum Origin
{
    Unknown,
    User,
    Application,
    System,
    Automation,
}

public enum Confidence
{
    Baixa,
    Media,
    Alta,
}

public enum ObservationKind
{
    Foreground,
    MinimizeStart,
    MinimizeEnd,
    MoveSizeStart,
    MoveSizeEnd,
    ObjectShow,
    ObjectHide,
    LocationChange,
    Focus,
}

public sealed record Observation(
    Guid Id,
    long TsQpc,
    long TsSrcTicks,
    Origin Source,
    ObservationKind Kind,
    ulong Hwnd,
    uint Pid,
    uint Tid,
    string? Exe,
    string? ClassName,
    string? Title,
    WindowState? StateBefore,
    WindowState? StateAfter,
    string Payload
);

public sealed record WindowState(
    bool Visible,
    bool Minimized,
    bool Maximized,
    bool Cloaked,
    int Left,
    int Top,
    int Width,
    int Height
);

public sealed record Inference(
    Guid Id,
    Guid ObservationId,
    Origin Origin,
    Confidence Confidence,
    string RuleId,
    int RuleVersion,
    IReadOnlyList<Guid> EvidenceIds
);
