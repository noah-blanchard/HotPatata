namespace HotPatata
{
    /// <summary>
    /// Implemented by stateful level objects (falling platforms, ...). The RunManager calls
    /// <see cref="ResetState"/> on every one of them during a section reset. Objects whose state is a pure
    /// function of <see cref="SectionClock"/> (moving platforms, rotating bars) need no explicit reset.
    /// </summary>
    public interface IResettable
    {
        void ResetState();
    }
}
