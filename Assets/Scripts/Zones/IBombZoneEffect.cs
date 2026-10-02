namespace HotPatata
{
    /// <summary>
    /// An effect of a <see cref="Zone"/> on the flying bomb. Called on the authority by <see cref="BombZoneSweep"/>
    /// on every physics step in which the Thrown bomb's path touches the zone, so it must be idempotent (a bomb
    /// crossing a deep volume is reported on several steps).
    /// </summary>
    public interface IBombZoneEffect
    {
        void OnBombPassed(BombController bomb);
    }
}
