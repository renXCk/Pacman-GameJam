namespace Game.Ghosts
{
    // Lets the flashlight beam tell something that it is lit (spec section 11).
    public interface ILightReceiver
    {
        void SetLit(bool lit);
    }
}
