namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Every world sprite draws on the default sorting layer, so depth is purely order-driven.
    /// </summary>
    public static class SortingOrders
    {
        public const int Backdrop = -100;
        public const int BackdropGlow = -90;
        public const int BackdropDecor = -80;
        public const int Structure = 0;
        public const int Shadow = 4;
        public const int Block = 6;
        public const int Cannon = 8;
        public const int Trail = 9;
        public const int Projectile = 10;
        public const int Effects = 14;
        public const int AimGuide = 20;
    }
}
