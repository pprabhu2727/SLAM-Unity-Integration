public interface IMotionLimiter
{
    /// Scale applied to movement input (0 = stop, 1 = full speed).
    void SetSpeedScale(float scale);
}
