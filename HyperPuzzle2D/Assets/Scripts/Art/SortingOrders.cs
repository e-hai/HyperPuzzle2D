namespace HyperPuzzle2D.Art
{
    /// <summary>
    /// Every world sprite draws on the default sorting layer, so depth is purely order-driven.
    /// </summary>
    public static class SortingOrders
    {
        public const int Backdrop = -100;

        /// <summary>Washi grain over the sky gradient, under everything that sits in the scene.</summary>
        public const int BackdropGrain = -99;
        public const int SkylineFar = -98;
        public const int SkylineNear = -96;
        public const int Rigging = -94;
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
