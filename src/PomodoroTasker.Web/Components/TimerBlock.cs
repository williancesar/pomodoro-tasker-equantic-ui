using eQuantic.UI.Components;
using eQuantic.UI.Lucide;
using eQuantic.UI.Primitives;
using PomodoroTasker.Web.Server;
using PomodoroTasker.Web.Theme;

namespace PomodoroTasker.Web.Components;

/// <summary>
/// The timer: the full block on Hoje, a strip elsewhere. The clock is always derived from the
/// end timestamp the server stores; the tick only redraws it and asks the server when it reaches zero.
/// </summary>
public sealed class TimerBlock : StatefulComponent
{
    private TimerView _view = new();
    private IDisposable? _tick;
    private double _now;
    private int _ticks;
    private bool _busy;
    private bool _stopping;
    private string _reason = "";
    private string _flash = "";

    // A component with Server Actions is built by the server's container to run them, so it
    // takes no constructor arguments: what it is given comes through its properties.
    /// <summary>The full block on Hoje; otherwise the strip.</summary>
    public bool Big { get; set; }

    public override void AdoptConfig(UiComponent next)
    {
        if (next is TimerBlock fresh) Big = fresh.Big;
    }

    [ServerAction]
    public Task<TimerView> Load() => Task.FromResult(Api.Timer());

    [ServerAction]
    public Task<TimerView> Send(string type, string reason) => Task.FromResult(Api.Send(type, 0, reason));

    private static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    protected override void OnMount()
    {
        _now = Now();
        Bus.Timer = () => _ = Refresh(false);
        _tick = GetService<IClock>()?.Every(TimeSpan.FromMilliseconds(500), Tick);
        _ = Refresh(false);
    }

    protected override void OnUnmount()
    {
        _tick?.Dispose();
        Bus.Timer = null;
    }

    private void Tick()
    {
        SetState(() => _now = Now());
        _ticks++;
        // Every 30 seconds the server is asked anyway: that is how a new day is noticed.
        var ended = _view.EndsAt > 0 && _view.PausedLeft < 0 && _now >= _view.EndsAt;
        if ((ended || _ticks % 60 == 0) && !_busy) _ = Refresh(true);
    }

    private async Task Refresh(bool tell) => Apply(await Guard(Load()), tell);

    private async Task Dispatch(string type, string reason = "") => Apply(await Guard(Send(type, reason)), true);

    private async Task<TimerView> Guard(Task<TimerView> call)
    {
        _busy = true;
        try { return await call; }
        finally { _busy = false; }
    }

    /// <param name="tell">Whether the screen beside the block should reload.</param>
    private void Apply(TimerView view, bool tell)
    {
        var dayChanged = _view.Day != "" && _view.Day != view.Day;
        SetState(() =>
        {
            _view = view;
            _now = Now();
            if (view.Flash != "") _flash = view.Flash;
        });
        if (view.Missed) Navigator.Go("/dias-fechados");
        else if (tell || dayChanged || view.Recorded > 0) Bus.Data?.Invoke();
    }

    private bool IsBreak => _view.Phase is "short" or "long";
    private bool IsPaused => _view.PausedLeft >= 0;

    private double Left() => IsPaused ? _view.PausedLeft : _view.EndsAt > 0 ? Math.Max(0, _view.EndsAt - _now) : -1;

    private static string Clock(double ms)
    {
        var total = (int)Math.Ceiling(ms / 1000);
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void Secondary()
    {
        if (IsBreak) _ = Dispatch("skip");
        else if (_view.Phase == "focus") SetState(() => { _reason = ""; _stopping = true; });
        else _ = Dispatch("stop");
    }

    private void ConfirmStop()
    {
        var reason = _reason;
        SetState(() => _stopping = false);
        _ = Dispatch("stop", reason);
    }

    public override VisualNode Build(ComponentContext context)
    {
        var v = _view;
        var left = Left();
        var idle = v.Phase == "idle";
        var time = left >= 0 ? Clock(left) : Clock(v.FocusMinutes * 60_000.0);
        var label = v.Phase == "focus" ? $"{(IsPaused ? T.TimerPaused : T.TimerFocus)} · {T.TimerOf(v.Cycle + 1, v.Every)}"
            : IsBreak ? $"{(v.Phase == "short" ? T.TimerShort : T.TimerLong)}{(IsPaused ? $" · {T.TimerBreakPaused}" : "")}"
            : v.Phase == "wait" ? T.TimerWait : T.TimerIdle;
        var line = idle ? T.TimerIdleHint : v.Phase == "wait" ? T.TimerNext(v.TaskTitle)
            : IsBreak ? T.TimerAfter(v.TaskTitle) : v.TaskTitle;
        var main = idle ? "" : v.Phase == "wait" ? T.TimerStart : IsPaused ? T.TimerResume : T.TimerPause;
        var mainIcon = v.Phase == "wait" || IsPaused ? LucideIcons.Play : LucideIcons.Pause;
        var second = idle ? "" : IsBreak ? T.TimerSkip : T.TimerStop;
        // Focus runs in the accent, a break in green, anything paused or waiting is dimmed.
        var clockColor = IsPaused || idle || v.Phase == "wait" ? TaskerTheme.BlockMute
            : IsBreak ? TaskerTheme.LeafOnBlock : TaskerTheme.Accent;

        VisualNode block;
        if (Big)
        {
            block = Row(gap: Space.S6, cross: CrossAlign.End, children: [
                Flexible(Column(gap: Space.S2, children: [
                    Text(label, TypeRole.Label, TaskerTheme.BlockMute, maxLines: 1),
                    Text(time, TypeRole.Display, clockColor, maxLines: 1),
                    Text(line, TypeRole.BodyL, TaskerTheme.BlockInk, maxLines: 1),
                    Row(gap: Space.S2, children: [
                        .. Enumerable.Range(0, v.Every).Select(i => (VisualNode)new Drawing(
                            i < v.Cycle ? Vectors.Tomato : Vectors.TomatoEmpty, width: 24,
                            label: i == 0 ? T.TimerCycle(v.Cycle, v.Every) : null)),
                    ]),
                ])),
                Column(gap: Space.S2, children: [
                    new Button(idle ? T.TimerStart : main, onPressed: () => _ = Dispatch("primary")) { Disabled = idle },
                    .. second == "" ? [] : new VisualNode[] { Button(second, Variant.Outline, onPressed: Secondary) },
                ]),
            ]);
        }
        else
        {
            block = Row(gap: Space.S4, cross: CrossAlign.Center, children: [
                Text(idle ? "--:--" : time, TypeRole.Heading, clockColor, maxLines: 1),
                Flexible(Column(gap: 0, children: [
                    Text(label, TypeRole.Caption, TaskerTheme.BlockMute, maxLines: 1),
                    Text(line, TypeRole.BodyM, TaskerTheme.BlockInk, maxLines: 1),
                ])),
                .. idle ? [] : new VisualNode[]
                {
                    new IconButton(new Icon(mainIcon, 20), main, IconButtonKind.Filled, onPressed: () => _ = Dispatch("primary")),
                    new IconButton(new Icon(IsBreak ? LucideIcons.SkipForward : LucideIcons.Square, 20, TaskerTheme.BlockInk), second, onPressed: Secondary),
                },
            ]);
        }

        var children = new List<VisualNode>
        {
            Box(new BoxStyle
            {
                Width = SizeValue.Fill, Background = TaskerTheme.Block,
                Padding = Big ? EdgeInsets.All(Space.S6) : EdgeInsets.Symmetric(Space.S6, Space.S3),
            }, block),
        };
        if (_flash != "")
            children.Add(new Toast(_flash, Variant.Success, T.Close, () => SetState(() => _flash = "")));
        if (_stopping)
            children.Add(Parts.Modal(context.Theme, T.TimerStopTitle, Column(gap: Space.S4, children: [
                Text(T.TimerStopTitle, TypeRole.Title, context.Theme.TextPrimary),
                Text(T.TimerStopText((int)Math.Ceiling(Math.Max(0, left) / 60_000)), TypeRole.BodyM, context.Theme.TextSecondary),
                new TextInput(_reason, value => SetState(() => _reason = value), T.TimerReason) { Autofocus = true },
                Row(gap: Space.S2, wrap: true, children: [
                    .. T.TimerReasons.Select(r => (VisualNode)Chip(r, ChipKind.Filter, _reason == r, () => SetState(() => _reason = r))),
                ]),
                Row(gap: Space.S2, main: MainAlign.End, children: [
                    Button(T.TimerKeepFocus, Variant.Outline, onPressed: () => SetState(() => _stopping = false)),
                    Button(T.TimerStopFocus, onPressed: ConfirmStop),
                ]),
            ]), () => SetState(() => _stopping = false)));
        return Column(gap: 0, children: [.. children]);
    }
}
