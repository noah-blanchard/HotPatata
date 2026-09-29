namespace HotPatata
{
    /// <summary>
    /// Something that can open one <see cref="SignalActuator"/> (PROJECT_SPEC §13.15): a <see cref="BombGate"/> for a
    /// while after a pass, a <see cref="PressurePlate"/> while someone stands on it. One source drives one actuator;
    /// there is no AND/OR wiring (§17.2).
    /// </summary>
    public interface ISignalSource
    {
        bool Active { get; }
    }
}
